using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ECommons;

namespace LazyLoot;

internal static class Roller
{
    unsafe delegate bool RollItemRaw(Loot* lootIntPtr, RollResult option, uint lootItemIndex);

    static RollItemRaw _rollItemRaw;

    static uint _itemId = 0, _index = 0;

    public static void Clear()
    {
        _itemId = _index = 0;
    }

    public static bool RollOneItem(RollResult option, ref int need, ref int greed, ref int pass)
    {
        if (!GetNextLootItem(out var index, out var loot)) return false;

        //Make option valid.
        option = ResultMerge(option, GetRestrictResult(loot), GetPlayerRestrict(loot));

        if (LazyLoot.Config.DiagnosticsMode)
            Svc.Log.Debug($"{loot.ItemId} {option}");

        if (_itemId == loot.ItemId && index == _index)
        {
            if (LazyLoot.Config.DiagnosticsMode && !LazyLoot.Config.NoPassEmergency)
                DuoLog.Debug(
                    $"{Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId).Name.ToString()} has failed to roll for some reason. Passing for safety. [Emergency pass]");

            if (!LazyLoot.Config.NoPassEmergency)
            {
                switch (option)
                {
                    case RollResult.Needed:
                        need--;
                        break;
                    case RollResult.Greeded:
                        greed--;
                        break;
                    default:
                        pass--;
                        break;
                }

                option = RollResult.Passed;
            }
        }

        RollItem(option, index);
        _itemId = loot.ItemId;
        _index = index;

        switch (option)
        {
            case RollResult.Needed:
                need++;
                break;
            case RollResult.Greeded:
                greed++;
                break;
            default:
                pass++;
                break;
        }

        return true;
    }

    private static RollResult GetRestrictResult(LootItem loot)
    {
        var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(loot.ItemId);
        if (item == null)
            return RollResult.Passed;

        //Checks what the max possible roll type on the item is
        var stateMax = loot.RollState switch
        {
            RollState.UpToNeed => RollResult.Needed,
            RollState.UpToGreed => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        if (item.Value.IsUnique && IsItemUnlocked(loot.ItemId))
            stateMax = RollResult.Passed;

        if (LazyLoot.Config.DiagnosticsMode && stateMax == RollResult.Passed)
            DuoLog.Debug($"{item.Value.Name.ToString()} can only be passed on. [RollState UpToPass]");

        //Checks what the player set loot rules are
        var ruleMax = loot.LootMode switch
        {
            LootMode.Normal => RollResult.Needed,
            LootMode.GreedOnly => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        return ResultMerge(stateMax, ruleMax);
    }

    private static unsafe RollResult GetPlayerRestrict(LootItem loot)
    {
        var lootItem = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(loot.ItemId);

        UpdateFadedCopy(loot.ItemId, out var orchId);

        if (lootItem == null)
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug(
                    $"Passing due to unknown item? Please give this ID to the developers: {loot.ItemId} [Unknown ID]");
            return RollResult.Passed;
        }

        if (lootItem.Value.IsUnique && ItemCount(loot.ItemId) > 0)
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug(
                    $"{lootItem.Value.Name.ToString()} has been passed due to being unique and you already possess one. [Unique Item]");

            return RollResult.Passed;
        }

        // Here, we will check for the specific rules for items.
        var itemCustomRestriction =
            LazyLoot.Config.Restrictions.Items.FirstOrDefault(x => x.Id == loot.ItemId);
        if (itemCustomRestriction is { Enabled: true })
        {
            if (LazyLoot.Config.DiagnosticsMode)
            {
                var action = itemCustomRestriction.RollRule == RollResult.Passed ? "passing" :
                    itemCustomRestriction.RollRule == RollResult.Greeded ? "greeding" :
                    itemCustomRestriction.RollRule == RollResult.Needed ? "needing" : "passing";
                Svc.Log.Debug($"{lootItem.Value.Name.ToString()} is {action}. [Item Custom Restriction]");
            }

            return itemCustomRestriction.RollRule;
        }

        // Here, we will check for the specific rules for the Duty.
        var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>()
            .GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
        var dutyCustomRestriction =
            LazyLoot.Config.Restrictions.Duties.FirstOrDefault(x => x.Id == contentFinderInfo.RowId);
        if (dutyCustomRestriction is { Enabled: true })
        {
            if (LazyLoot.Config.DiagnosticsMode)
            {
                var action = dutyCustomRestriction.RollRule == RollResult.Passed ? "passing" :
                    dutyCustomRestriction.RollRule == RollResult.Greeded ? "greeding" :
                    dutyCustomRestriction.RollRule == RollResult.Needed ? "needing" : "passing";
                Svc.Log.Debug(
                    $"{lootItem.Value.Name.ToString()} is {action} due to being in {contentFinderInfo.Name}. [Duty Custom Restriction]");
            }

            return dutyCustomRestriction.RollRule;
        }

        if (orchId.Count > 0 && orchId.All(x => IsItemUnlocked(x)))
        {
            if (LazyLoot.Config.RestrictionIgnoreItemUnlocked)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on all items already unlocked"" enabled. [Pass All Unlocked]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreFadedCopy
                && lootItem.Value.FilterGroup == 12 && lootItem.Value.ItemUICategory.RowId == 94)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Faded Copies"" enabled. [Pass Faded Copies]");

                return RollResult.Passed;
            }
        }

        if (IsItemUnlocked(loot.ItemId))
        {
            if (LazyLoot.Config.RestrictionIgnoreItemUnlocked)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on all items already unlocked"" enabled. [Pass All Unlocked]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreMounts && lootItem.Value.ItemAction.Value.Type == 1322)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Mounts"" enabled. [Pass Mounts]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreMinions && lootItem.Value.ItemAction.Value.Type == 853)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Minions"" enabled. [Pass Minions]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreBardings
                && lootItem.Value.ItemAction.Value.Type == 1013)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Bardings"" enabled. [Pass Bardings]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreEmoteHairstyle
                && lootItem.Value.ItemAction.Value.Type == 2633)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Emotes and Hairstyles"" enabled. [Pass Emotes/Hairstyles]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreTripleTriadCards
                && lootItem.Value.ItemAction.Value.Type == 3357)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Triple Triad cards"" enabled. [Pass TTCards]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreOrchestrionRolls
                && lootItem.Value.ItemAction.Value.Type == 25183)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to being unlocked and you have ""Pass on unlocked Orchestrion Rolls"" enabled. [Pass Orchestrion]");

                return RollResult.Passed;
            }
        }

        if (LazyLoot.Config.RestrictionSeals)
        {
            if (lootItem.Value.Rarity > 1 && lootItem.Value.PriceLow > 0 && lootItem.Value.ClassJobCategory.RowId > 0)
            {
                var gcSealValue = Svc.Data.Excel.GetSheet<GCSupplyDutyReward>()?.GetRow(lootItem.Value.LevelItem.RowId)
                    .SealsExpertDelivery;
                if (gcSealValue < LazyLoot.Config.RestrictionSealsAmnt)
                {
                    if (LazyLoot.Config.DiagnosticsMode)
                        DuoLog.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to selling for less than {LazyLoot.Config.RestrictionSealsAmnt} seals. [Pass Seals]");

                    return RollResult.Passed;
                }
            }
        }

        if (lootItem.Value.EquipSlotCategory.RowId != 0)
        {
            // Check if the loot item level is below the average level of the user job
            if (LazyLoot.Config.RestrictionLootLowerThanJobIlvl && loot.RollState == RollState.UpToNeed)
            {
                if (lootItem.Value.LevelItem.RowId <
                    Utils.GetPlayerIlevel() - LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold)
                {
                    if (LazyLoot.Config.DiagnosticsMode &&
                        LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState != 0)
                        DuoLog.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to being below your average item level and you have set to pass items below your average job item level. [Pass Item Lower Than Average iLevel]");

                    return LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState == 0
                        ? RollResult.Greeded
                        : RollResult.Passed;
                }
            }

            if (LazyLoot.Config.RestrictionIgnoreItemLevelBelow
                && lootItem.Value.LevelItem.RowId < LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items with an item level below"" enabled and {lootItem.Value.LevelItem.RowId} is less than {LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue}. [Pass Item Level]");

                return RollResult.Passed;
            }

            // Check if the loot item is an actual upgrade to the user (has a higher ilvl)
            if (LazyLoot.Config.RestrictionLootIsJobUpgrade && loot.RollState == RollState.UpToNeed)
            {
                var lootItemSlot = lootItem.Value.EquipSlotCategory.RowId;
                var itemsToVerify = new List<uint>();
                InventoryContainer* equippedItems =
                    InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
                for (int i = 0; i < equippedItems->Size; i++)
                {
                    InventoryItem* equippedItem = equippedItems->GetInventorySlot(i);
                    var equippedItemData = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(equippedItem->ItemId);
                    if (equippedItemData == null) continue;
                    if (equippedItemData.Value.EquipSlotCategory.RowId != lootItemSlot) continue;
                    // We gather all the iLvls of the equipped items in the same slot (if any)
                    itemsToVerify.Add(equippedItemData.Value.LevelItem.RowId);
                }

                // And we check if from the items gathered, if the lowest is higher than the droped item, we follow the rules defined by the user
                if (itemsToVerify.Count > 0 && itemsToVerify.Min() > lootItem.Value.LevelItem.RowId)
                {
                    if (LazyLoot.Config.DiagnosticsMode && LazyLoot.Config.RestrictionLootIsJobUpgradeRollState != 0)
                        DuoLog.Debug(
                            $@"{lootItem.Value.Name.ToString()} has been passed due to being below the level of your current equipped item level and you have set to pass items below the level of your equipped item. [Pass Item if equipped is of higher level]");

                    return LazyLoot.Config.RestrictionLootIsJobUpgradeRollState == 0
                        ? RollResult.Greeded
                        : RollResult.Passed;
                }
            }

            if (LazyLoot.Config.RestrictionOtherJobItems
                && loot.RollState != RollState.UpToNeed)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug(
                        $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job]");

                return RollResult.Passed;
            }
        }

        //PLD set.
        if (LazyLoot.Config.RestrictionOtherJobItems
            && lootItem.Value.ItemAction.Value.Type == 29153
            && !(Player.Object?.ClassJob.RowId is 1 or 19))
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug(
                    $@"{lootItem.Value.Name.ToString()} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job (PLD Sets)]");

            return RollResult.Passed;
        }

        return RollResult.Needed;
    }

    public static void UpdateFadedCopy(uint itemId, out List<uint> orchId)
    {
        orchId = new();
        var lumina = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        if (lumina != null)
        {
            if (lumina.Value.FilterGroup == 12 && lumina.Value.ItemUICategory.RowId == 94)
            {
                var recipe = Svc.Data.GetExcelSheet<Recipe>()
                    ?.Where(x => x.Ingredient.Any(y => y.RowId == lumina.Value.RowId)).Select(x => x.ItemResult.Value)
                    .FirstOrDefault();
                if (recipe != null)
                {
                    if (LazyLoot.Config.DiagnosticsMode)
                        DuoLog.Debug(
                            $"Updating Faded Copy {lumina.Value.Name} ({itemId}) to Non-Faded {recipe.Value.Name} ({recipe.Value.RowId})");
                    orchId.Add(recipe.Value.RowId);
                    return;
                }
            }
        }
    }

    private static RollResult ResultMerge(params RollResult[] results)
        => results.Max() switch
        {
            RollResult.Needed => RollResult.Needed,
            RollResult.Greeded => RollResult.Greeded,
            _ => RollResult.Passed,
        };


    private static unsafe bool GetNextLootItem(out uint i, out LootItem loot)
    {
        var span = Loot.Instance()->Items;
        for (i = 0; i < span.Length; i++)
        {
            loot = span[(int)i];
            
            if (loot.ItemId >= 1000000) loot.ItemId -= 1000000;
            if (loot.ChestObjectId is 0 or 0xE0000000) continue;
            if (loot.RollResult != RollResult.UnAwarded) continue;
            if (loot.RollState is RollState.Rolled or RollState.Unavailable or RollState.Unknown) continue;
            if (loot.ItemId == 0) continue;
            if (loot.LootMode is LootMode.LootMasterGreedOnly or LootMode.Unavailable) continue;

            var checkWeekly = LazyLoot.Config.RestrictionWeeklyLockoutItems;
            
            var lootId = loot.ItemId;
            var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>()
                .GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
            
            // We load the users restrictions
            var itemCustomRestriction =
                LazyLoot.Config.Restrictions.Items.FirstOrDefault(x =>
                    x.Id == lootId && x is { Enabled: true });
            var dutyCustomRestriction =
                LazyLoot.Config.Restrictions.Duties.FirstOrDefault(x =>
                    x.Id == contentFinderInfo.RowId && x is { Enabled: true, RollRule: RollResult.UnAwarded });
            
            Item? item = null;

            if (LazyLoot.Config.DiagnosticsMode)
                // Only load the item if diagnostic mode is on
                item = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);

            if (itemCustomRestriction != null)
            {
                if (itemCustomRestriction.RollRule == RollResult.UnAwarded)
                {
                    if (LazyLoot.Config.DiagnosticsMode)
                        DuoLog.Debug(
                            $"{item?.Name.ToString()} is being ignored. [Item Custom Restriction]");
                    continue;
                }
                checkWeekly = false;
            }

            if (itemCustomRestriction == null && dutyCustomRestriction != null)
            {
                if (dutyCustomRestriction.RollRule == RollResult.UnAwarded)
                {
                    if (LazyLoot.Config.DiagnosticsMode)
                        DuoLog.Debug(
                            $"{item?.Name.ToString()} is being ignored due to being in {contentFinderInfo.Name}. [Duty Custom Restriction]");
                    continue;
                }
                checkWeekly = false;
            }
            
            // loot.RollValue == 20 means it cant be rolled because one was already obtained this week.
            // we ignore that so it will be passed automatically, as there is nothing the user can do other than
            // pass it
            if (loot.WeeklyLootItem && (byte)loot.RollState != 20 && checkWeekly) continue;

            return true;
        }

        loot = default;
        return false;
    }

    private static unsafe void RollItem(RollResult option, uint index)
    {
        try
        {
            _rollItemRaw ??=
                Marshal.GetDelegateForFunctionPointer<RollItemRaw>(
                    Svc.SigScanner.ScanText("41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08"));
            _rollItemRaw?.Invoke(Loot.Instance(), option, index);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Warning at roll");
        }
    }

    public static unsafe int ItemCount(uint itemId)
        => InventoryManager.Instance()->GetInventoryItemCount(itemId);

    public static unsafe bool IsItemUnlocked(uint itemId)
    {
        var exdItem = ExdModule.GetItemRowById(itemId);
        return exdItem is null || UIState.Instance()->IsItemActionUnlocked(exdItem) is 1;
    }

    public static uint ConvertSealsToIlvl(int sealAmnt)
    {
        var sealsSheet = Svc.Data.GetExcelSheet<GCSupplyDutyReward>();
        uint ilvl = 0;
        foreach (var row in sealsSheet)
        {
            if (row.SealsExpertDelivery < sealAmnt)
            {
                ilvl = row.RowId;
            }
        }

        return ilvl;
    }
}