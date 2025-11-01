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
    internal class NemesisCommand : RemoteCommand
    {
        private class NemesisRequest
        {
            public int Amount { get; private set; }
            public int Empowers { get; private set; }
            public float Rarity { get; private set; }
            public bool DropEgg { get; private set; }
            public bool DropMatches { get; private set; }
            public ItemDataUnpacked Item { get; private set; }
            public int ItemLevel { get; private set; }
            public string Query { get; private set; }
            public int Faction { get; private set; }
            public bool Void { get; private set; }
            public bool UseActive { get; private set; }

            public NemesisRequest(byte[] data)
            {
                var str = Encoding.UTF8.GetString(data, 0, data.Length);
                using var doc = JsonDocument.Parse(str);
                Amount = Math.Min(100_000, doc.RootElement.GetProperty("amount").GetInt32());
                DropEgg = doc.RootElement.GetProperty("dropEgg").GetBoolean();
                DropMatches = doc.RootElement.GetProperty("dropMatches").GetBoolean();
                UseActive = doc.RootElement.GetProperty("useActive").GetBoolean();
                Empowers = doc.RootElement.GetProperty("empowers").GetInt32();
                Faction = doc.RootElement.GetProperty("faction").GetInt32();
                if (doc.RootElement.TryGetProperty("item", out var value))
                {
                    try
                    {
                        Item = Common.CreateItemFromJson(value);
                        Item.RefreshIDAndValues();
                        Item.legendaryPotential = 0;
                        Item.weaversWill = 0;
                    }
                    catch (Exception)
                    {
                        Item = null;
                    }
                }
                ItemLevel = doc.RootElement.GetProperty("ilvl").GetInt32();
                Query = doc.RootElement.GetProperty("query").GetString();
                Rarity = doc.RootElement.GetProperty("rarity").GetSingle();
                Void = doc.RootElement.GetProperty("void").GetBoolean();
                if (Item != null && !Item.isUnique())
                {
                    Item = null;
                }
            }
        }
        public NemesisCommand() :
            base((int)RemoteCommandId.Nemesis, "Imprint Generator")
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
                    var args = new NemesisRequest(data);
                    var actor = PlayerFinder.getPlayerActor();
                    if (args.Item != null && (!ItemList.isEquipment(args.Item.itemType) || args.Item.isIdol() || args.Item.isCocooned()))
                    {
                        queue.Add([]);
                        return;
                    }
                    if (args.Item != null && args.DropEgg)
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
                    weaver.GainReputation(1000000000);
                    if (!args.Void)
                    {
                        weaver.Leave();
                    }
                    if (!args.UseActive)
                    {
                        ZoneInfoManager.SetZoneLevel(args.ItemLevel, true);
                        var opts = new Il2CppSystem.Collections.Generic.List<MonolithEchoOption>();
                        opts.Add(new(new(), 0, 0, 0, false, 0));
                        opts.Add(new(new(), 0, 0, 0, false, 0));
                        MonolithGameplayManager.ActiveRun = new MonolithRun(TimelineID.Volcano, 1, new(), new(), 0, true, true, false, 0, EchoWebIsland.IslandType.Normal, 0, 0, 0, false, false, new(), opts);
                        MonolithGameplayManager.ActiveRun.increasedItemRarity = args.Rarity;
                    }
                    GUIUtility.systemCopyBuffer = args.Query;
                    // a possible alternative filter: ItemSearchExpression::ItemMatches
                    if (!ItemFilterManager.Instance.CreateLootFilterFromClipboard(out var filter) || filter == null)
                    {
                        queue.Add([]);
                        return;
                    }
                    int matches = 0;
                    var nemesisContainer = ItemContainersManager.Instance.nemesisItems;
                    for (int loop = 0; loop < args.Amount; ++loop)
                    {
                        nemesisContainer.PopulateItems(false);
                        if (args.Item != null)
                        {
                            foreach (var container in nemesisContainer.Containers)
                            {
                                if (container.HasSwappableUnique())
                                {
                                    nemesisContainer.swapContainer.Clear();
                                    if (nemesisContainer.swapContainer.TryAddItem(new ItemDataUnpacked(args.Item.getAsUnpacked()), 1, Context.SILENT))
                                    {
                                        nemesisContainer.SwapItemInSwapSlotForSwappableUnique();
                                    }
                                    else
                                    {
                                        queue.Add([]);
                                        return;
                                    }
                                }
                            }
                        }
                        for (int i = 0; i < args.Empowers; ++i)
                        {
                            nemesisContainer.EmpowerItems();
                        }

                        foreach (var container in nemesisContainer.Containers)
                        {
                            var item = container.content.data.getAsUnpacked();
                            if (filter.Match(item, out var color, out var emphasize, out var number, out var sid, out var bid) == Rule.RuleOutcome.SHOW)
                            {
                                if (args.DropMatches)
                                {
                                    GroundItemManager.instance.dropItemForPlayer(actor, item, actor.position(), false);
                                }
                                matches += 1;
                            }
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
