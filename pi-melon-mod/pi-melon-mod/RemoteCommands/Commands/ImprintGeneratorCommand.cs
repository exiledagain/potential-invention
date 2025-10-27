using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppLE.Factions;
using MelonLoader;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using UnityEngine;

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
                Amount = Math.Min(100_000, doc.RootElement.GetProperty("amount").GetInt32());
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

        public override byte[] Receive(Core core, byte[] data)
        {
            if (!Ready(core))
            {
                return [];
            }
            // get the work onto the game thread
            // probably a better way?
            BlockingCollection<byte[]> queue = [];
            core.EnqueueWork(() =>
            {
                try
                {
                    var args = new GenerationRequest(data);
                    var actor = PlayerFinder.getPlayerActor();
                    if (!ItemList.isEquipment(args.Item.itemType) || args.Item.isIdol() || args.Item.isCocooned())
                    {
                        queue.Add([]);
                        return;
                    }
                    if (args.DropImprint)
                    {
                        GroundItemManager.instance.dropItemForPlayer(actor, args.Item, actor.position(), false);
                    }

                    if (args.Faction == 0)
                    {
                        var faction = actor.localTreeData.getFactionInfoProvider().CoF();
                        faction.Join();
                        faction.GainReputation(100_000_000);
                    }
                    else
                    {
                        var faction = actor.localTreeData.getFactionInfoProvider().MG();
                        faction.Join();
                        faction.GainReputation(100_000_000);
                    }

                    // we must be a member of the weavers (LE 1.3)
                    var weaver = actor.localTreeData.getFactionInfoProvider().TW();
                    weaver.Join();
                    weaver.GainReputation(1000000000);
                    GUIUtility.systemCopyBuffer = args.Query;
                    // a possible alternative filter: ItemSearchExpression::ItemMatches
                    if (!ItemFilterManager.Instance.CreateLootFilterFromClipboard(out var filter) || filter == null)
                    {
                        queue.Add([]);
                        return;
                    }
                    int matches = 0;
                    for (int i = 0; i < args.Amount; ++i)
                    {
                        var sim = WeaverTreeNodesExtensions.GetSimilarItem(args.Item, args.ItemLevel, args.Corruption, actor);
                        if (filter.Match(sim, out var color, out var emphasize, out var number, out var sid, out var bid) == Rule.RuleOutcome.SHOW)
                        {
                            if (args.DropMatches)
                            {
                                GroundItemManager.instance.dropItemForPlayer(actor, sim, actor.position(), false);
                            }
                            matches += 1;
                        }
                    }
                    using var stream = new BinaryWriter(new MemoryStream());
                    stream.Write(matches);
                    stream.Write(args.Amount);
                    queue.Add(((MemoryStream)stream.BaseStream).ToArray());
                    queue.CompleteAdding();
                }
                catch (Exception)
                {
                    queue.Add([]);
                    return;
                }
            });
            return queue.Take();
        }
    }
}
