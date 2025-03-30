using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using PunishLib.ImGuiMethods;

namespace LazyLoot;

public class ConfigUi : Window, IDisposable
{
    private static int debugValue;
    private static string searchResultsQuery;
    private static double lastSearchTime;
    private static Item[] itemSearchResults;
    private static ContentFinderCondition[] dutySearchResults;

    internal WindowSystem windowSystem = new();

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct DebugLootItem
    {
        [FieldOffset(0x00)] public uint ChestObjectId;
        [FieldOffset(0x04)] public uint ChestItemIndex; // This loot item's index in the chest it came from
        [FieldOffset(0x08)] public uint ItemId;
        [FieldOffset(0x0C)] public ushort ItemCount;

        [FieldOffset(0x1C)] public uint GlamourItemId;
        [FieldOffset(0x20)] public RollState RollState;
        [FieldOffset(0x24)] public RollResult RollResult;
        [FieldOffset(0x28)] public byte RollValue;
        [FieldOffset(0x34)] public byte Unk1;
        [FieldOffset(0x38)] public byte Unk2;
        [FieldOffset(0x2C)] public float Time;
        [FieldOffset(0x30)] public float MaxTime;

        [FieldOffset(0x38)] public LootMode LootMode;
    }

    public ConfigUi() : base("Lazy Loot Config")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(99999, 99999)
        };
        windowSystem.AddWindow(this);
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("config"))
        {
            if (ImGui.BeginTabItem("Features"))
            {
                DrawFeatures();
                ImGui.Separator();
                DrawRollingDelay();
                ImGui.Separator();
                DrawChatAndToast();
                ImGui.Separator();
                DrawFulf();
                ImGui.Separator();
                DrawDiagnostics();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("User Restriction"))
            {
                DrawUserRestriction();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("About"))
            {
                AboutTab.Draw("LazyLoot");
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private static IDalamudTextureWrap? GetItemIcon(uint id)
    {
        return Svc.Texture.GetFromGameIcon(new GameIconLookup
        {
            IconId = id
        }).GetWrapOrDefault();
    }

    private unsafe void DrawDebug()
    {
        if (ImGui.CollapsingHeader("Is Item Unlocked?"))
        {
            ImGui.InputInt("Debug Value Tester", ref debugValue);
            ImGui.Text($"Is Unlocked: {Roller.IsItemUnlocked((uint)debugValue)}");
        }

        if (ImGui.CollapsingHeader("Loot"))
        {
            var loot = Loot.Instance();
            if (loot != null)
            {
                foreach (var item in loot->Items)
                {
                    if (item.ItemId == 0) continue;
                    var casted = (DebugLootItem*)&item;
                    ImGui.PushID($"{casted->ItemId}");
                    Dalamud.Utility.Util.ShowStruct(casted);
                }
            }
        }

        //if (ImGui.Button("Faded Copy Converter Check?"))
        //{
        //    Roller.UpdateFadedCopy((uint)debugValue, out uint nonfaded);
        //    Svc.Log.Debug($"Non-Faded is {nonfaded}");\
        //}

        //if (ImGui.Button("Check all Faded Copies"))
        //{
        //    foreach (var i in Svc.Data.GetExcelSheet<Item>().Where(x => x.FilterGroup == 12 && x.ItemUICategory.Row == 94))
        //    {
        //        Roller.UpdateFadedCopy((uint)i.RowId, out uint nonfaded);
        //        Svc.Log.Debug($"{i.Name}");
        //    }
        //}
    }

    private void DrawDiagnostics()
    {
        ImGuiEx.LineCentered("DiagnosticsLabel", () => ImGuiEx.TextUnderlined("Diagnostics & Troubleshooting"));

        if (ImGui.Checkbox("Diagnostics Mode", ref LazyLoot.Config.DiagnosticsMode))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker(
            "Outputs additional messages to chat whenever an item is passed, with reasons. This is useful for helping to diagnose issues with the developers or for understanding why LazyLoot makes decisions to pass on items.\r\n\r\nThese messages will only be displayed to you, nobody else in-game can see them.");

        if (ImGui.Checkbox("Don't pass on items that fail to roll.", ref LazyLoot.Config.NoPassEmergency))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker(
            "Normally LazyLoot will pass on items that fail to roll. Enabling this option will prevent it from passing in those situations. Be warned there could be weird side effects doing this and should only be used if you're running into issues with emergency passing appearing.");
    }

    public override void OnClose()
    {
        LazyLoot.Config.Save();
        Notify.Success("Configuration saved");
        base.OnClose();
    }

    private static void DrawFeatures()
    {
        ImGuiEx.LineCentered("FeaturesLabel", () => ImGuiEx.TextUnderlined("LazyLoot Rolling Commands"));
        ImGui.Columns(2, null, false);
        ImGui.SetColumnWidth(0, 80);
        ImGui.Text("/lazy need");
        ImGui.NextColumn();
        ImGui.Text("Roll need for everything. If impossible, roll greed (or pass if greed is impossible).");
        ImGui.NextColumn();
        ImGui.Text("/lazy greed");
        ImGui.NextColumn();
        ImGui.Text("Roll greed for everything. If impossible, roll pass.");
        ImGui.NextColumn();
        ImGui.Text("/lazy pass");
        ImGui.NextColumn();
        ImGui.Text("Pass on things you haven't rolled for yet.");
        ImGui.NextColumn();
        ImGui.Columns(1);
    }

    private static void DrawRollingDelay()
    {
        ImGuiEx.LineCentered("RollingDelayLabel", () => ImGuiEx.TextUnderlined("Rolling Command Delay"));
        ImGui.SetNextItemWidth(100);

        if (ImGui.DragFloatRange2("Rolling delay between items", ref LazyLoot.Config.MinRollDelayInSeconds,
                ref LazyLoot.Config.MaxRollDelayInSeconds, 0.1f))
        {
            LazyLoot.Config.MinRollDelayInSeconds = Math.Max(LazyLoot.Config.MinRollDelayInSeconds, 0.5f);

            LazyLoot.Config.MaxRollDelayInSeconds = Math.Max(LazyLoot.Config.MaxRollDelayInSeconds,
                LazyLoot.Config.MinRollDelayInSeconds + 0.1f);
        }
    }

    private static void DrawUserRestrictionEverywhere()
    {
        ImGui.Text("Settings in this page will apply to every single item, even if they are tradeable or not.");
        ImGui.Separator();
        ImGui.Checkbox("Pass on items with an item level below",
            ref LazyLoot.Config.RestrictionIgnoreItemLevelBelow);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        ImGui.DragInt("###RestrictionIgnoreItemLevelBelowValue",
            ref LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue);
        if (LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue < 0)
            LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue = 0;

        ImGui.Checkbox(
            "Pass on all items already unlocked. (Triple Triad Cards, Orchestrions, Faded Copies, Minions, Mounts, Emotes, Hairstyles)",
            ref LazyLoot.Config.RestrictionIgnoreItemUnlocked);

        if (!LazyLoot.Config.RestrictionIgnoreItemUnlocked)
        {
            ImGui.Checkbox("Pass on unlocked Mounts.", ref LazyLoot.Config.RestrictionIgnoreMounts);
            ImGui.Checkbox("Pass on unlocked Minions.", ref LazyLoot.Config.RestrictionIgnoreMinions);
            ImGui.Checkbox("Pass on unlocked Bardings.", ref LazyLoot.Config.RestrictionIgnoreBardings);
            ImGui.Checkbox("Pass on unlocked Triple Triad cards.",
                ref LazyLoot.Config.RestrictionIgnoreTripleTriadCards);
            ImGui.Checkbox("Pass on unlocked Emotes and Hairstyle.",
                ref LazyLoot.Config.RestrictionIgnoreEmoteHairstyle);
            ImGui.Checkbox("Pass on unlocked Orchestrion Rolls.",
                ref LazyLoot.Config.RestrictionIgnoreOrchestrionRolls);
            ImGui.Checkbox("Pass on unlocked Faded Copies.", ref LazyLoot.Config.RestrictionIgnoreFadedCopy);
        }

        ImGui.Checkbox("Pass on items I can't use with current job.",
            ref LazyLoot.Config.RestrictionOtherJobItems);

        ImGui.Checkbox("Don't roll on items with a weekly lockout.",
            ref LazyLoot.Config.RestrictionWeeklyLockoutItems);

        ImGui.Checkbox("###RestrictionWeeklyLockoutItems", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvl);
        ImGui.SameLine();
        ImGui.Text("Roll");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootLowerThanJobIlvlRollState",
            ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState, new[] { "Greed", "Pass" }, 2);
        ImGui.SameLine();
        ImGui.Text("on items that are");
        ImGui.SetNextItemWidth(50);
        ImGui.SameLine();
        ImGui.DragInt("###RestrictionLootLowerThanJobIlvlTreshold",
            ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold);
        if (LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold < 0)
            LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold = 0;
        ImGui.SameLine();
        ImGui.Text($"item levels lower than your current job item level (\u2605 {Utils.GetPlayerIlevel()}).");
        ImGuiComponents.HelpMarker("This setting will only apply to gear you can need on.");

        ImGui.Checkbox("###RestrictionLootIsJobUpgrade", ref LazyLoot.Config.RestrictionLootIsJobUpgrade);
        ImGui.SameLine();
        ImGui.Text("Roll");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootIsJobUpgradeRollState",
            ref LazyLoot.Config.RestrictionLootIsJobUpgradeRollState,
            new[] { "Greed", "Pass" }, 2);
        ImGui.SameLine();
        ImGui.Text("on items if the current equipped item of the same type has a higher item level.");
        ImGuiComponents.HelpMarker("This setting will only apply to gear you can need on.");

        ImGui.Checkbox("###RestrictionSeals", ref LazyLoot.Config.RestrictionSeals);
        ImGui.SameLine();
        ImGui.Text("Pass on items with a expert delivery seal value of less than");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.DragInt("###RestrictionSealsAmnt", ref LazyLoot.Config.RestrictionSealsAmnt);
        ImGui.SameLine();
        ImGui.Text($"(item level {Roller.ConvertSealsToIlvl(LazyLoot.Config.RestrictionSealsAmnt)} and below)");
        ImGuiComponents.HelpMarker(
            "This setting will only apply to gear able to be turned in for expert delivery.");
    }

    private static void CenterText()
    {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - ImGui.GetFrameHeight()) * 0.5f);
    }

    private static void DrawUserRestrictionItems()
    {
        if (ImGui.BeginTable("UserRestrictionItemsTable", 8, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Need", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Greed", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Pass", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Nothing", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < LazyLoot.Config.Restrictions.Items.Count; i++)
            {
                var item = LazyLoot.Config.Restrictions.Items[i];

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var enabled = item.Enabled;
                CenterText();
                if (ImGui.Checkbox($"##{item.Id}", ref enabled))
                {
                    item.Enabled = enabled;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (uint.TryParse(item.Icon, out var iconId))
                {
                    var icon = GetItemIcon(iconId);
                    if (icon != null)
                    {
                        CenterText();
                        ImGui.Image(icon.ImGuiHandle, new Vector2(24, 24));
                    }
                }

                ImGui.TableNextColumn();
                ImGui.Text(item.Name);
                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##need{item.Id}", item.Need))
                {
                    item.Need = true;
                    item.Greed = false;
                    item.Pass = false;
                    item.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##greed{item.Id}", item.Greed))
                {
                    item.Need = false;
                    item.Greed = true;
                    item.Pass = false;
                    item.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##pass{item.Id}", item.Pass))
                {
                    item.Need = false;
                    item.Greed = false;
                    item.Pass = true;
                    item.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##doNothing{item.Id}", item.DoNothing))
                {
                    item.Need = false;
                    item.Greed = false;
                    item.Pass = false;
                    item.DoNothing = true;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Remove##{item.Id}"))
                {
                    LazyLoot.Config.Restrictions.Items.RemoveAt(i);
                    LazyLoot.Config.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }

        if (ImGui.Button("Add Item", new Vector2(-1, 0)))
        {
            searchResultsQuery = "";
            ImGui.OpenPopup("item_search_add");
        }

        var itemSheet = Svc.Data.GetExcelSheet<Item>();

        if (!ImGui.BeginPopup("item_search_add")) return;
        {
            ImGui.Text("Search for item:");
            var currentTime = ImGui.GetTime();
            if (ImGui.GetTime() > lastSearchTime + 0.1f)
            {
                lastSearchTime = currentTime;
                itemSearchResults = !string.IsNullOrEmpty(searchResultsQuery)
                    ? itemSheet.Where(x =>
                            x.Name.ToString().Contains(searchResultsQuery, StringComparison.OrdinalIgnoreCase) &&
                            LazyLoot.Config.Restrictions.Items.All(i => i.Id != x.RowId.ToString()))
                        .ToArray()
                    : [];
            }

            var maxWidth = Math.Max(300f, itemSearchResults.Select(item =>
                ImGui.CalcTextSize($"{item.Name} (ID: {item.RowId})").X).DefaultIfEmpty(200f).Max() + 30);
            ImGui.SetNextItemWidth(maxWidth);
            ImGui.InputText("##itemSearch", ref searchResultsQuery, 100);
            if (!string.IsNullOrEmpty(searchResultsQuery))
                if (ImGui.BeginChild("itemSearchResults", new Vector2(maxWidth, 200), true))
                {
                    foreach (var item in itemSearchResults)
                        if (ImGui.Selectable($"{item.Name} (ID: {item.RowId})"))
                        {
                            var selectedItemId = item.RowId;
                            var selectedItem = itemSheet.GetRow(selectedItemId);
                            var selectedItemName = selectedItem.Name.ToString();
                            var newItem = new RestrictionItem
                            {
                                Id = selectedItem.RowId.ToString(),
                                Enabled = true,
                                Name = selectedItemName,
                                Icon = selectedItem.Icon
                                    .ToString(),
                                Need = false,
                                Greed = false,
                                Pass = false,
                                DoNothing = true
                            };
                            LazyLoot.Config.Restrictions.Items.Add(newItem);
                            LazyLoot.Config.Save();
                            ImGui.CloseCurrentPopup();
                        }

                    ImGui.EndChild();
                }

            ImGui.EndPopup();
        }
    }

    private static void DrawUserRestrictionDuties()
    {
        if (ImGui.BeginTable("UserRestrictionDutiesTable", 8, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Need", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Greed", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Pass", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Nothing", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < LazyLoot.Config.Restrictions.Duties.Count; i++)
            {
                var duty = LazyLoot.Config.Restrictions.Duties[i];

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var enabled = duty.Enabled;
                CenterText();
                if (ImGui.Checkbox($"##{duty.Id}", ref enabled))
                {
                    duty.Enabled = enabled;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (uint.TryParse(duty.Icon, out var iconId))
                {
                    var icon = GetItemIcon(iconId);
                    if (icon != null)
                    {
                        CenterText();
                        ImGui.Image(icon.ImGuiHandle, new Vector2(24, 24));
                    }
                }

                ImGui.TableNextColumn();
                ImGui.Text(duty.Name);
                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##need{duty.Id}", duty.Need))
                {
                    duty.Need = true;
                    duty.Greed = false;
                    duty.Pass = false;
                    duty.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##greed{duty.Id}", duty.Greed))
                {
                    duty.Need = false;
                    duty.Greed = true;
                    duty.Pass = false;
                    duty.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##pass{duty.Id}", duty.Pass))
                {
                    duty.Need = false;
                    duty.Greed = false;
                    duty.Pass = true;
                    duty.DoNothing = false;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                CenterText();
                if (ImGui.RadioButton($"##doNothing{duty.Id}", duty.DoNothing))
                {
                    duty.Need = false;
                    duty.Greed = false;
                    duty.Pass = false;
                    duty.DoNothing = true;
                    LazyLoot.Config.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Remove##{duty.Id}"))
                {
                    LazyLoot.Config.Restrictions.Duties.RemoveAt(i);
                    LazyLoot.Config.Save();
                    break;
                }
            }

            ImGui.EndTable();
        }

        if (ImGui.Button("Add Duty", new Vector2(-1, 0)))
        {
            searchResultsQuery = "";
            ImGui.OpenPopup("duty_search_add");
        }

        var dutySheet = Svc.Data.GetExcelSheet<ContentFinderCondition>();

        if (!ImGui.BeginPopup("duty_search_add")) return;
        {
            ImGui.Text("Search for duty:");
            var currentTime = ImGui.GetTime();
            if (ImGui.GetTime() > lastSearchTime + 0.1f)
            {
                lastSearchTime = currentTime;
                dutySearchResults = !string.IsNullOrEmpty(searchResultsQuery)
                    ? dutySheet.Where(x =>
                            x.Name.ToString().Contains(searchResultsQuery,
                                StringComparison.OrdinalIgnoreCase) &&
                            LazyLoot.Config.Restrictions.Duties.All(i => i.Id != x.RowId.ToString()))
                        .ToArray()
                    : [];
            }

            var maxWidth = Math.Max(300f, dutySearchResults.Select(duty =>
                    ImGui.CalcTextSize($"{duty.Name} (ID: {duty.RowId})").X).DefaultIfEmpty(200f)
                .Max() + 30);
            ImGui.SetNextItemWidth(maxWidth);
            ImGui.InputText("##dutySearch", ref searchResultsQuery, 100);
            if (!string.IsNullOrEmpty(searchResultsQuery))
                if (ImGui.BeginChild("dutySearchResults", new Vector2(maxWidth, 200), true))
                {
                    foreach (var duty in dutySearchResults)
                        if (ImGui.Selectable($"{duty.Name} (ID: {duty.RowId})"))
                        {
                            var selectedDutyId = duty.RowId;
                            var selectedDuty = dutySheet.GetRow(selectedDutyId);
                            var selectedDutyName = selectedDuty.Name.ToString();
                            var newDuty = new RestrictionDuty
                            {
                                Id = selectedDuty.RowId.ToString(),
                                Enabled = true,
                                Name = selectedDutyName,
                                Icon = selectedDuty.ContentType.Value.Icon.ToString(),
                                Need = false,
                                Greed = false,
                                Pass = false,
                                DoNothing = true
                            };
                            LazyLoot.Config.Restrictions.Duties.Add(newDuty);
                            LazyLoot.Config.Save();
                            ImGui.CloseCurrentPopup();
                        }

                    ImGui.EndChild();
                }

            ImGui.EndPopup();
        }
    }

    private static void DrawUserRestriction()
    {
        if (ImGui.BeginTabBar("PerItemDutyConfigTabs"))
        {
            if (ImGui.BeginTabItem("Everywhere..."))
            {
                DrawUserRestrictionEverywhere();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("... but for these Items"))
            {
                DrawUserRestrictionItems();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("... but for these Duties"))
            {
                DrawUserRestrictionDuties();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawChatAndToast()
    {
        ImGuiEx.LineCentered("ChatInfoLabel", () => ImGuiEx.TextUnderlined("Roll Result Information"));
        ImGui.Checkbox("Display roll information in chat.", ref LazyLoot.Config.EnableChatLogMessage);
        ImGui.Spacing();
        ImGuiEx.LineCentered("ToastLabel", () => ImGuiEx.TextUnderlined("Display as Toasts"));
        ImGuiComponents.HelpMarker("Show your roll information as a pop-up toast, using the various styles below.");
        ImGui.Checkbox("Quest", ref LazyLoot.Config.EnableQuestToast);
        ImGui.SameLine();
        ImGui.Checkbox("Normal", ref LazyLoot.Config.EnableNormalToast);
        ImGui.SameLine();
        ImGui.Checkbox("Error", ref LazyLoot.Config.EnableErrorToast);
    }

    private void DrawFulf()
    {
        ImGuiEx.LineCentered("FULFLabel", () => ImGuiEx.TextUnderlined("Fancy Ultimate Lazy Feature"));

        ImGui.TextWrapped(
            "Fancy Ultimate Lazy Feature (FULF) is a set and forget feature that will automatically roll on items for you instead of having to use the commands above.");
        ImGui.Separator();
        ImGui.Columns(2, null, false);
        ImGui.SetColumnWidth(0, 80);
        ImGui.Text("/fulf need");
        ImGui.NextColumn();
        ImGui.Text("Set FULF to Needing mode, where it will follow the /lazy need rules.");
        ImGui.NextColumn();
        ImGui.Text("/fulf greed");
        ImGui.NextColumn();
        ImGui.Text("Set FULF to Greeding mode, where it will follow the /lazy greed rules.");
        ImGui.NextColumn();
        ImGui.Text("/fulf pass");
        ImGui.NextColumn();
        ImGui.Text("Set FULF to Passing mode, where it will follow the /lazy pass rules.");
        ImGui.NextColumn();
        ImGui.Columns(1);
        ImGui.Separator();
        ImGui.Checkbox("###FulfEnabled", ref LazyLoot.Config.FulfEnabled);
        ImGui.SameLine();
        ImGui.TextColored(LazyLoot.Config.FulfEnabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed,
            LazyLoot.Config.FulfEnabled ? "FULF Enabled" : "FULF Disabled");

        ImGui.SetNextItemWidth(100);

        if (ImGui.Combo("Roll options", ref LazyLoot.Config.FulfRoll, new[] { "Need", "Greed", "Pass" }, 3))
            LazyLoot.Config.Save();

        ImGui.Text("First Roll Delay Range (In seconds)");
        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("Minimum Delay in seconds. ", ref LazyLoot.Config.FulfMinRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMinRollDelayInSeconds >= LazyLoot.Config.FulfMaxRollDelayInSeconds)
            LazyLoot.Config.FulfMinRollDelayInSeconds = LazyLoot.Config.FulfMaxRollDelayInSeconds - 0.1f;

        if (LazyLoot.Config.FulfMinRollDelayInSeconds < 1.5f) LazyLoot.Config.FulfMinRollDelayInSeconds = 1.5f;

        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("Maximum Delay in seconds. ", ref LazyLoot.Config.FulfMaxRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMaxRollDelayInSeconds <= LazyLoot.Config.FulfMinRollDelayInSeconds)
            LazyLoot.Config.FulfMaxRollDelayInSeconds = LazyLoot.Config.FulfMinRollDelayInSeconds + 0.1f;
    }
}