using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppLE.Factions;
using MelonLoader;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace pi_melon_mod.RemoteCommands.Commands
{
    internal class ImprintGeneratorCommand: RemoteCommand
    {
        private class GenerationRequest
        {
            public int Amount { get; private set; }
            public int Corruption { get; private set; }
            public bool DropImprint { get; private set; }
            public bool DropMatches { get; private set; }  
            public int ForgingPotential { get; private set; }
            public ItemDataUnpacked Item { get; private set; }
            public int ItemLevel { get; private set; }
            public string Query { get; private set; }
            public int Faction { get; private set; }

            public GenerationRequest(byte[] data)
            {
                var str = Encoding.UTF8.GetString(data, 0, data.Length);
                using var doc = JsonDocument.Parse(str);
                Amount = Math.Min(1_000_000, doc.RootElement.GetProperty("amount").GetInt32());
                DropImprint = doc.RootElement.GetProperty("dropImprint").GetBoolean();
                DropMatches = doc.RootElement.GetProperty("dropMatches").GetBoolean();
                Corruption = doc.RootElement.GetProperty("corruption").GetInt32();
                ForgingPotential = doc.RootElement.GetProperty("forgingPotential").GetInt32();
                Item = Common.CreateItemFromJson(doc.RootElement.GetProperty("item"));
                ItemLevel = doc.RootElement.GetProperty("ilvl").GetInt32();
                Query = doc.RootElement.GetProperty("query").GetString();
                Faction = doc.RootElement.GetProperty("faction").GetInt32();
                Item.forgingPotential = EpochExtensions.clampToByte(ForgingPotential);
                Item.RefreshIDAndValues();
            }
        }
        public ImprintGeneratorCommand():
            base((int)RemoteCommandId.ImprintGenerator, "Imprint Generator")
        {
        }

        public static bool Ready(Core core)
        {
            return core.Ready();
        }

        class GenerateContext
        {
            private Core core;
            private byte[] data;
            private GenerationRequest args;
            private BlockingCollection<byte[]> queue;
            private ItemFilter filter;
            private State state;
            private int matches;
            private int index;
            private Actor actor;

            private enum State
            {
                None,
                Ready,
                Done,
                Error
            }

            public GenerateContext(Core core, byte[] data, BlockingCollection<byte[]> queue)
            {
                this.core = new Core();
                this.data = data;
                this.queue = queue;
                index = 0;
                matches = 0;
                state = State.None;
            }

            private void Setup()
            {
                args = new GenerationRequest(data);
                actor = PlayerFinder.getPlayerActor();
                if (!ItemList.isEquipment(args.Item.itemType) || args.Item.isIdol() || args.Item.isCocooned())
                {
                    state = State.Error;
                    queue.Add([]);
                    return;
                }
                if (args.DropImprint)
                {
                    GroundItemManager.instance.dropItemForPlayer(actor, args.Item, actor.position(), false);
                }

                var cof = actor.localTreeData.getFactionInfoProvider().CoF();
                var mg = actor.localTreeData.getFactionInfoProvider().MG();
                if (args.Faction == 0)
                {
                    mg.Leave();
                    cof.Join();
                    cof.GainReputation(100_000_000);
                }
                else
                {
                    cof.Leave();
                    mg.Join();
                    mg.GainReputation(100_000_000);
                }

                // we must be a member of the weavers (LE 1.3)
                var weaver = actor.localTreeData.getFactionInfoProvider().TW();
                weaver.Join();
                weaver.GainReputation(100_000_000);
                GUIUtility.systemCopyBuffer = args.Query;
                // a possible alternative filter: ItemSearchExpression::ItemMatches
                if (!ItemFilterManager.Instance.CreateLootFilterFromClipboard(out filter) || filter == null)
                {
                    state = State.Error;
                    queue.Add([]);
                    return;
                }
                state = State.Ready;
            }

            public void Work()
            {
                int nextIndex = index + Math.Min(10_000, args.Amount - index);
                for (int j = index; j < nextIndex; j += 1)
                {
                    var sim = WeaverTreeNodesExtensions.GetSimilarItem(args.Item, args.ItemLevel, args.Corruption, actor);
                    if (filter.Match(sim, out _, out _, out _, out _, out _) == Rule.RuleOutcome.SHOW)
                    {
                        if (args.DropMatches)
                        {
                            GroundItemManager.instance.dropItemForPlayer(actor, sim, actor.position(), false);
                        }
                        matches += 1;
                    }
                }
                if (GarbageCollector.GetMode() == GarbageCollector.Mode.Manual)
                {
                    GarbageCollector.CollectIncremental(1_000_000_000);
                }
                index = nextIndex;
            }

            public bool Step()
            {
                try
                {
                    if (state == State.None)
                    {
                        Setup();
                    }
                    if (state != State.Ready)
                    {
                        queue.Add([]);
                        return false;
                    }
                    if (index >= args.Amount)
                    {
                        using var stream = new BinaryWriter(new MemoryStream());
                        stream.Write(matches);
                        stream.Write(args.Amount);
                        queue.Add(((MemoryStream)stream.BaseStream).ToArray());
                        queue.CompleteAdding();
                        state = State.Done;
                        return false;
                    }
                    Work();
                    return true;
                }
                catch (Exception e)
                {
                    state = State.Error;
                    queue.TryAdd([]);
                    core.LoggerInstance.Error(e);
                }
                return false;
            }

            public IEnumerable<bool> Generator()
            {
                while (Step())
                {
                    yield return true;
                }
            }
        }

        public override byte[] Receive(Core core, byte[] data)
        {
            if (!Ready(core))
            {
                return [];
            }
            var beginTime = DateTime.Now;
            // get the work onto the game thread
            // probably a better way?
            BlockingCollection<byte[]> queue = [];
            core.EnqueueWork(new GenerateContext(core, data, queue).Generator());
            var res = queue.Take();
            core.LoggerInstance.Msg("generate request completed in {0}s", (DateTime.Now - beginTime).TotalSeconds);
            return res;
        }
    }
}
