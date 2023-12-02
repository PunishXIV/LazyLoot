using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Numerics;

namespace LazyLoot;

public class ConfigUi : Window, IDisposable
{
    internal WindowSystem windowSystem = new();

    public ConfigUi() : base("Lazy Loot Config")
    {
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(99999, 99999),
        };
        windowSystem.AddWindow(this);
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }

    private int debugValue = 0;
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
                ImGui.BeginChild("generalFeatures");

                DrawFeatures();
                ImGui.Separator();
                DrawRollingDelay();
                ImGui.Separator();
                DrawChatAndToast();
                ImGui.Separator();
                DrawFulf();
                ImGui.Separator();
                DrawDiagnostics();

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("User Restriction"))
            {
                ImGui.BeginChild("generalFeatures");

                DrawUserRestriction();

                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("About"))
            {
                PunishLib.ImGuiMethods.AboutTab.Draw("LazyLoot");
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

    private void DrawDebug()
    {
        ImGui.InputInt("Debug Value Tester", ref debugValue);

        ImGui.Text($"Is Unlocked: {Roller.IsItemUnlocked((uint)debugValue)}");

        //if (ImGui.Button("Faded Copy Converter Check?"))
        //{
        //    Roller.UpdateFadedCopy((uint)debugValue, out uint nonfaded);
        //    Svc.Log.Debug($"Non-Faded is {nonfaded}");
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
        ImGuiEx.ImGuiLineCentered("DiagnosticsLabel", () => ImGuiEx.TextUnderlined("Diagnostics & Troubleshooting"));

        if (ImGui.Checkbox($"Diagnostics Mode", ref LazyLoot.Config.DiagnosticsMode))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker($"Outputs additional messages to chat whenever an item is passed, with reasons. This is useful for helping to diagnose issues with the developers or for understanding why LazyLoot makes decisions to pass on items.\r\n\r\nThese messages will only be displayed to you, nobody else in-game can see them.");

        if (ImGui.Checkbox("Don't pass on items that fail to roll.", ref LazyLoot.Config.NoPassEmergency))
            LazyLoot.Config.Save();

        ImGuiComponents.HelpMarker($"Normally LazyLoot will pass on items that fail to roll. Enabling this option will prevent it from passing in those situations. Be warned there could be weird side effects doing this and should only be used if you're running into issues with emergency passing appearing.");
    }

    public override void OnClose()
    {
        LazyLoot.Config.Save();
        Notify.Success("Configuration saved");
        base.OnClose();
    }

    private static void DrawFeatures()
    {
        ImGuiEx.ImGuiLineCentered("FeaturesLabel", () => ImGuiEx.TextUnderlined("LazyLoot Rolling Commands"));
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
        ImGuiEx.ImGuiLineCentered("RollingDelayLabel", () => ImGuiEx.TextUnderlined("Rolling Command Delay"));
        ImGui.SetNextItemWidth(100);

        if (ImGui.DragFloatRange2("Rolling delay between items", ref LazyLoot.Config.MinRollDelayInSeconds, ref LazyLoot.Config.MaxRollDelayInSeconds, 0.1f))
        {
            LazyLoot.Config.MinRollDelayInSeconds = Math.Max(LazyLoot.Config.MinRollDelayInSeconds, 0.5f);

            LazyLoot.Config.MaxRollDelayInSeconds = Math.Max(LazyLoot.Config.MaxRollDelayInSeconds, LazyLoot.Config.MinRollDelayInSeconds + 0.1f);
        }
    }

    private static void DrawUserRestriction()
    {
        ImGui.Text("Settings in this page will apply to every single item, even if they are tradeable or not.");
        ImGui.Separator();
        ImGui.Checkbox("Pass on items with an item level below", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelow);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        ImGui.DragInt("###RestrictionIgnoreItemLevelBelowValue", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue);
        if (LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue < 0) LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue = 0;

        ImGui.Checkbox("Pass on all items already unlocked. (Triple Triad Cards, Orchestrions, Faded Copies, Minions, Mounts, Emotes, Hairstyles)", ref LazyLoot.Config.RestrictionIgnoreItemUnlocked);

        if (!LazyLoot.Config.RestrictionIgnoreItemUnlocked)
        {
            ImGui.Checkbox("Pass on unlocked Mounts.", ref LazyLoot.Config.RestrictionIgnoreMounts);
            ImGui.Checkbox("Pass on unlocked Minions.", ref LazyLoot.Config.RestrictionIgnoreMinions);
            ImGui.Checkbox("Pass on unlocked Bardings.", ref LazyLoot.Config.RestrictionIgnoreBardings);
            ImGui.Checkbox("Pass on unlocked Triple Triad cards.", ref LazyLoot.Config.RestrictionIgnoreTripleTriadCards);
            ImGui.Checkbox("Pass on unlocked Emotes and Hairstyle.", ref LazyLoot.Config.RestrictionIgnoreEmoteHairstyle);
            ImGui.Checkbox("Pass on unlocked Orchestrion Rolls.", ref LazyLoot.Config.RestrictionIgnoreOrchestrionRolls);
            ImGui.Checkbox("Pass on unlocked Faded Copies.", ref LazyLoot.Config.RestrictionIgnoreFadedCopy);
        }

        ImGui.Checkbox("Pass on items I can't use with current job.", ref LazyLoot.Config.RestrictionOtherJobItems);

        ImGui.Checkbox("Don't roll on items with a weekly lockout.", ref LazyLoot.Config.RestrictionWeeklyLockoutItems);

        ImGui.Checkbox("###RestrictionWeeklyLockoutItems", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvl);
        ImGui.SameLine();
        ImGui.Text("Roll");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootLowerThanJobIlvlRollState", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState, new string[] { "Greed", "Pass" }, 2);
        ImGui.SameLine();
        ImGui.Text("on items that are");
        ImGui.SetNextItemWidth(50);
        ImGui.SameLine();
        ImGui.DragInt("###RestrictionLootLowerThanJobIlvlTreshold", ref LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold);
        if (LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold < 0) LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold = 0;
        ImGui.SameLine();
        ImGui.Text($"item levels lower than your current job item level (\u2605 {Utils.GetPlayerIlevel()}).");
        ImGuiComponents.HelpMarker("This setting will only apply to gear you can need on.");

        ImGui.Checkbox("###RestrictionLootIsJobUpgrade", ref LazyLoot.Config.RestrictionLootIsJobUpgrade);
        ImGui.SameLine();
        ImGui.Text("Roll");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.Combo("###RestrictionLootIsJobUpgradeRollState", ref LazyLoot.Config.RestrictionLootIsJobUpgradeRollState, new string[] { "Greed", "Pass" }, 2);
        ImGui.SameLine();
        ImGui.Text($"on items if the current equipped item of the same type has a higher item level.");
        ImGuiComponents.HelpMarker("This setting will only apply to gear you can need on.");

        ImGui.Checkbox($"###RestrictionSeals", ref LazyLoot.Config.RestrictionSeals);
        ImGui.SameLine();
        ImGui.Text("Pass on items with a expert delivery seal value of less than");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.DragInt("###RestrictionSealsAmnt", ref LazyLoot.Config.RestrictionSealsAmnt);
        ImGui.SameLine();
        ImGui.Text($"(item level {Roller.ConvertSealsToIlvl(LazyLoot.Config.RestrictionSealsAmnt)} and below)");
        ImGuiComponents.HelpMarker("This setting will only apply to gear able to be turned in for expert delivery.");

    }

    private void DrawChatAndToast()
    {
        ImGuiEx.ImGuiLineCentered("ChatInfoLabel", () => ImGuiEx.TextUnderlined("Roll Result Information"));
        ImGui.Checkbox("Display roll information in chat.", ref LazyLoot.Config.EnableChatLogMessage);
        ImGui.Spacing();
        ImGuiEx.ImGuiLineCentered("ToastLabel", () => ImGuiEx.TextUnderlined("Display as Toasts"));
        ImGuiComponents.HelpMarker("Show your roll information as a pop-up toast, using the various styles below.");
        ImGui.Checkbox("Quest", ref LazyLoot.Config.EnableQuestToast);
        ImGui.SameLine();
        ImGui.Checkbox("Normal", ref LazyLoot.Config.EnableNormalToast);
        ImGui.SameLine();
        ImGui.Checkbox("Error", ref LazyLoot.Config.EnableErrorToast);
    }

    private void DrawFulf()
    {
        ImGuiEx.ImGuiLineCentered("FULFLabel", () => ImGuiEx.TextUnderlined("Fancy Ultimate Lazy Feature"));

        ImGui.TextWrapped($"Fancy Ultimate Lazy Feature (FULF) is a set and forget feature that will automatically roll on items for you instead of having to use the commands above.");
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
        ImGui.TextColored(LazyLoot.Config.FulfEnabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, LazyLoot.Config.FulfEnabled ? "FULF Enabled" : "FULF Disabled");

        ImGui.SetNextItemWidth(100);

        if (ImGui.Combo("Roll options", ref LazyLoot.Config.FulfRoll, new string[] { "Need", "Greed", "Pass" }, 3))
        {
            LazyLoot.Config.Save();
        }

        ImGui.Text("First Roll Delay Range (In seconds)");
        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("Minimum Delay in seconds. ", ref LazyLoot.Config.FulfMinRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMinRollDelayInSeconds >= LazyLoot.Config.FulfMaxRollDelayInSeconds)
        {
            LazyLoot.Config.FulfMinRollDelayInSeconds = LazyLoot.Config.FulfMaxRollDelayInSeconds - 0.1f;
        }

        if (LazyLoot.Config.FulfMinRollDelayInSeconds < 1.5f)
        {
            LazyLoot.Config.FulfMinRollDelayInSeconds = 1.5f;
        }

        ImGui.SetNextItemWidth(100);
        ImGui.DragFloat("Maximum Delay in seconds. ", ref LazyLoot.Config.FulfMaxRollDelayInSeconds, 0.1F);

        if (LazyLoot.Config.FulfMaxRollDelayInSeconds <= LazyLoot.Config.FulfMinRollDelayInSeconds)
        {
            LazyLoot.Config.FulfMaxRollDelayInSeconds = LazyLoot.Config.FulfMinRollDelayInSeconds + 0.1f;
        }
    }
}
