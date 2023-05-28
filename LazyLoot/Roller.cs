using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

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

        if (_itemId == loot.ItemId && index == _index)
        {
            PluginLog.Warning($"Item [{loot.ItemId}] roll {option} failed, please contract to the author or lower your delay.");
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
        //Checks what the max possible roll type on the item is
        var stateMax = loot.RollState switch
        {
            RollState.UpToNeed => RollResult.Needed,
            RollState.UpToGreed => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        //Checks what the player set loot rules are
        var ruleMax = loot.LootMode switch
        {
            LootMode.Normal => RollResult.Needed,
            LootMode.GreedOnly => RollResult.Greeded,
            _ => RollResult.Passed,
        };
        return ResultMerge(stateMax, ruleMax);
    }
    private unsafe static RollResult GetPlayerRestrict(LootItem loot)
    {
        var item = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);
        if (item == null) return RollResult.Passed;

        //Unique.
        if (item.IsUnique && ItemCount(loot.ItemId) > 0) return RollResult.Passed;

        if (IsItemUnlocked(loot.ItemId))
        {
            if (LazyLoot.Config.RestrictionIgnoreItemUnlocked)
            {
                return RollResult.Passed;
            }

            if ((LazyLoot.Config.RestrictionIgnoreMounts || item.IsUnique)
                && item.ItemAction?.Value.Type == 1322)
            {
                return RollResult.Passed;
            }

            if ((LazyLoot.Config.RestrictionIgnoreMinions || item.IsUnique)
                && item.ItemAction?.Value.Type == 853)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreBardings
                && item.ItemAction?.Value.Type == 1013)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreEmoteHairstyle
                && item.ItemAction?.Value.Type == 2633)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreTripleTriadCards
                && item.ItemAction?.Value.Type == 3357)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreOrchestrionRolls
                && item.ItemAction?.Value.Type == 25183)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreFadedCopy
                && item.Icon == 25958)
            {
                return RollResult.Passed;
            }
        }

        if (item.EquipSlotCategory.Row != 0)
        {
            if (LazyLoot.Config.RestrictionIgnoreItemLevelBelow
                && item.LevelItem.Row <= LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue)
            {
                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionOtherJobItems
                && loot.RollState != RollState.UpToNeed)
            {
                return RollResult.Passed;
            }
        }

        //PLD set.
        if (LazyLoot.Config.RestrictionOtherJobItems
            && item.ItemAction?.Value.Type == 29153
            && !(Player.Object?.ClassJob?.Id is 1 or 19))
        {
            return RollResult.Passed;
        }

        return RollResult.Needed;
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
        var span = Loot.Instance()->ItemArraySpan;
        for (i = 0; i < span.Length; i++)
        {
            loot = span[(int)i];
            if (loot.ChestObjectId is 0 or GameObject.InvalidGameObjectId) continue;
            if ((RollResult)loot.RollResult != RollResult.UnAwarded) continue;
            if (loot.RollState is RollState.Rolled or RollState.Unavailable or RollState.Unknown) continue;
            if (loot.ItemId == 0) continue;
            if (loot.LootMode is LootMode.LootMasterGreedOnly or LootMode.Unavailable) continue;
            if (LazyLoot.Config.RestrictionWeeklyLockoutItems)
            {
                var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>().GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
                var instanceInfo = Svc.Data.GetExcelSheet<InstanceContent>().GetRow(contentFinderInfo.Content);

                if (instanceInfo.WeekRestriction == 1)
                {
                    var item = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);

                    if (item.ItemAction?.Value.Type != 853 && 
                        item.ItemAction?.Value.Type != 1013 && 
                        item.ItemAction?.Value.Type != 2633 && 
                        item.ItemAction?.Value.Type != 3357 &&
                        item.ItemAction?.Value.Type != 25183 &&
                        item.Icon != 25958)
                    {
                        continue;
                    }
                }
            }


            return true;
        }

        loot = default;
        return false;
    }

    private static unsafe void RollItem(RollResult option, uint index)
    {
        try
        {
            _rollItemRaw ??= Marshal.GetDelegateForFunctionPointer<RollItemRaw>(Svc.SigScanner.ScanText("41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08"));
            _rollItemRaw?.Invoke(Loot.Instance(), option, index);
        }
        catch (Exception ex)
        {
            PluginLog.Warning(ex, "Warning at roll");
        }
    }

    private static unsafe int ItemCount(uint itemId)
        => InventoryManager.Instance()->GetInventoryItemCount(itemId);

    private static unsafe bool IsItemUnlocked(uint itemId)
        => UIState.Instance()->IsItemActionUnlocked(ExdModule.GetItemRowById(itemId)) == 1;
}
