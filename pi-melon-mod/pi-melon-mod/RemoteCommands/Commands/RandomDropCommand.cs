using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppLE.Data;
using Il2CppLE.Factions;
using Il2CppLE.Gameplay.Monolith;
using MelonLoader;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace pi_melon_mod.RemoteCommands.Commands
{
    internal class RandomDropCommand : RemoteCommand
    {
        private class RandomDropRequest
        {
            public int Amount { get; private set; }
            public float Rarity { get; private set; }
            public float Corruption { get; private set; }
            public bool DropMatches { get; private set; }
            public int ItemLevel { get; private set; }
            public string Query { get; private set; }
            public int Faction { get; private set; }
            public bool UseActive { get; private set; }

            public RandomDropRequest(byte[] data)
            {
                var str = Encoding.UTF8.GetString(data, 0, data.Length);
                using var doc = JsonDocument.Parse(str);
                Amount = Math.Min(10_000_000, doc.RootElement.GetProperty("amount").GetInt32());
                DropMatches = doc.RootElement.GetProperty("dropMatches").GetBoolean();
                UseActive = doc.RootElement.GetProperty("useActive").GetBoolean();
                Faction = doc.RootElement.GetProperty("faction").GetInt32();
                ItemLevel = doc.RootElement.GetProperty("ilvl").GetInt32();
                Query = doc.RootElement.GetProperty("query").GetString();
                Rarity = doc.RootElement.GetProperty("rarity").GetSingle();
                Corruption = doc.RootElement.GetProperty("corruption").GetSingle();
            }
        }
        public RandomDropCommand() :
            base((int)RemoteCommandId.RandomDrop, "Random Drop Generator")
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
                    var args = new RandomDropRequest(data);
                    var actor = PlayerFinder.getPlayerActor();

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

                    if (!args.UseActive)
                    {
                        ZoneInfoManager.SetZoneLevel(args.ItemLevel, true);
                        var opts = new Il2CppSystem.Collections.Generic.List<MonolithEchoOption>();
                        opts.Add(new(new(), 0, 0, 0, false, 0));
                        opts.Add(new(new(), 0, 0, 0, false, 0));
                        MonolithGameplayManager.ActiveRun = new MonolithRun(TimelineID.Volcano, 1, new(), new(), 0, true, true, false, 0, EchoWebIsland.IslandType.Normal, 0, 0, 0, false, false, new(), opts);
                        MonolithGameplayManager.ActiveRun.increasedItemRarity = args.Rarity;
                        MonolithGameplayManager.ActiveRun.web.corruption = (int)args.Corruption;
                        MonolithGameplayManager.ActiveRun.web.timeline = new();
                        MonolithGameplayManager.ActiveRun.web.timeline.difficulties.Add(new MonolithTimeline.Difficulty());
                        MonolithGameplayManager.ActiveRun.web.timeline.difficulties[0].level = args.ItemLevel;
                        MonolithGameplayManager.ActiveRun.web.timeline.difficulties[0].additionalCorruptionEffect = MonolithTimeline.AdditionalCorruptionEffect.RewardRarity;
                    }

                    GUIUtility.systemCopyBuffer = args.Query;
                    // a possible alternative filter: ItemSearchExpression::ItemMatches
                    if (!ItemFilterManager.Instance.CreateLootFilterFromClipboard(out var filter) || filter == null)
                    {
                        queue.Add([]);
                        return;
                    }

                    int matches = 0;
                    for (int loop = 0; loop < args.Amount; ++loop)
                    {
                        var fid = new Il2CppSystem.Nullable<FactionID>();
                        var corruptionBox = new Il2CppSystem.Nullable<int>();
                        corruptionBox.hasValue = true;
                        corruptionBox.value = (int)args.Corruption;
                        var item = actor.generateItems.initialiseRandomItemData(false, args.ItemLevel, false, ItemLocationTag.None, -1, -1, -1, -1, -1, false, 0, fid, 0, false, UniqueList.LegendaryType.LegendaryPotential, false, corruptionBox, corruptionBox);
                        if (filter.Match(item, out var color, out var emphasize, out var number, out var sid, out var bid) == Rule.RuleOutcome.SHOW)
                        {
                            if (args.DropMatches)
                            {
                                GroundItemManager.instance.dropItemForPlayer(actor, item, actor.position(), false);
                            }
                            matches += 1;
                        }
                        if (loop % 10000 == 0)
                        {
                            GarbageCollector.CollectIncremental(100_000_000);
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
