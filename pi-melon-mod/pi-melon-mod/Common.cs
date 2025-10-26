using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace pi_melon_mod
{
    internal class Common
    {
        public static ItemDataUnpacked CreateItemFromJson(JsonElement itemJson)
        {
            byte itemType = itemJson.GetProperty("itemType").GetByte();
            ushort subType = itemJson.GetProperty("subType").GetUInt16();

            var item = new ItemDataUnpacked
            {
                itemType = itemType,
                subType = subType
            };
            item.SetAllImplicitRolls(255);
            item.RefreshIDAndValues();

            // order matters here: unique, affixes, primordial, sealed
            var changes = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
            if (itemJson.TryGetProperty("uniqueID", out var uniqueIdJson))
            {
                ushort uniqueId = uniqueIdJson.GetUInt16();
                List<double> rolls = [];
                if (itemJson.TryGetProperty("uniqueRolls", out var uniqueRolls))
                {
                    foreach (var roll in uniqueRolls.EnumerateArray())
                    {
                        if (roll.ValueKind == JsonValueKind.Null)
                        {
                            rolls.Add(0.0);
                            continue;
                        }
                        if (roll.TryGetDouble(out var rollValue))
                        {
                            rolls.Add(rollValue);
                        }
                    }
                }
                item.uniqueID = uniqueId;
                item.rarity = (byte)(UniqueList.getUnique(uniqueId).isSetItem ? 8 : 7);
                for (int i = 0; i < rolls.Count && i < item.uniqueRolls.Count; ++i)
                {
                    item.uniqueRolls[i] = (byte)(255 * rolls[i]);
                }
                item.RefreshIDAndValues();
            }
            if (itemJson.TryGetProperty("affixes", out var affixesJson))
            {
                if (item.isUnique() && affixesJson.GetArrayLength() > 0)
                {
                    item.rarity = 9;
                }
                foreach (var affixJson in affixesJson.EnumerateArray())
                {
                    int affixId = affixJson.GetProperty("id").GetInt32();
                    byte tier = (byte)(affixJson.GetProperty("tier").GetByte() - 1);
                    double roll = affixJson.GetProperty("roll").GetDouble();
                    int count = item.GetNonSealedAffixes() != null ? item.GetNonSealedAffixes().Count : 0;
                    if (count < 4)
                    {
                        Il2CppSystem.Nullable<byte> v = new((byte)(255 * roll));
                        item.AddAffixNoCostOrChecks(affixId, false, tier, ref changes, v);
                    }
                }
                if (!item.isUniqueSetOrLegendary())
                {
                    item.setRarityFromAffixesForNormalMagicOrRareItem();
                    item.forgingPotential = 40;
                }
            }
            if (itemJson.TryGetProperty("primordialAffix", out var primordialJson))
            {
                int affixId = primordialJson.GetProperty("id").GetInt32();
                byte tier = (byte)(primordialJson.GetProperty("tier").GetByte() - 1);
                double roll = primordialJson.GetProperty("roll").GetDouble();
                Il2CppSystem.Nullable<byte> v = new((byte)(255 * roll));
                item.AddAffixNoCostOrChecks(affixId, true, tier, ref changes, v);
                foreach (var affix in item.affixes)
                {
                    if (affix.affixId == affixId)
                    {
                        // the function that makes an affix primordial doesn't seem to work but this does
                        affix.affixTier = 7;
                        affix.sealedAffixType = SealedAffixType.Primordial;
                        item.hasSealedPrimordialAffix = true;
                        break;
                    }
                }
            }
            if (itemJson.TryGetProperty("sealedAffix", out var sealedJson))
            {
                int affixId = sealedJson.GetProperty("id").GetInt32();
                byte tier = (byte)(sealedJson.GetProperty("tier").GetByte() - 1);
                double roll = sealedJson.GetProperty("roll").GetDouble();
                Il2CppSystem.Nullable<byte> v = new((byte)(255 * roll));
                item.AddAffixNoCostOrChecks(affixId, true, tier, ref changes, v);
                ItemAffix foundAffix = null;
                foreach (var affix in item.affixes)
                {
                    if (affix.affixId == affixId)
                    {
                        foundAffix = affix;
                        break;
                    }
                }
                if (foundAffix != null)
                {
                    item.SealAffix(foundAffix);
                    item.hasSealedRegularAffix = true;
                }
            }

            item.RefreshIDAndValues();
            return item;
        }
    }
}
