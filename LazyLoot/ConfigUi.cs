using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
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
                PunishLib.ImGuiMethods.AboutTab.Draw(LazyLoot.P);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public override void OnClose()
    {
        LazyLoot.Config.Save();
        Notify.Success("Configuration saved");
        base.OnClose();
    }

    private static void DrawFeatures()
    {
        ImGui.Text("/rolling need");
        ImGui.SameLine();
        ImGui.Text("Roll need for everything. If impossible, roll greed (or pass if greed is impossible).");
        ImGui.Separator();
        ImGui.Text("/rolling needonly");
        ImGui.SameLine();
        ImGui.Text("Roll need for everything. If impossible, roll pass.");
        ImGui.Separator();
        ImGui.Text("/rolling greed");
        ImGui.SameLine();
        ImGui.Text("Roll greed for everything. If impossible, roll pass.");
        ImGui.Separator();
        ImGui.Text("/rolling pass");
        ImGui.SameLine();
        ImGui.Text("Pass on things you haven't rolled for yet.");
        ImGui.Separator();
        ImGui.Text("/rolling passall");
        ImGui.SameLine();
        ImGui.Text("Pass on all, even if you rolled on them previously.");
        ImGui.Separator();
    }

    private static void DrawRollingDelay()
    {
        ImGui.SetNextItemWidth(100);

        if(ImGui.DragFloatRange2("Rolling delay between items", ref LazyLoot.Config.MinRollDelayInSeconds, ref LazyLoot.Config.MaxRollDelayInSeconds, 0.1f))
        {
            LazyLoot.Config.MinRollDelayInSeconds = Math.Max(LazyLoot.Config.MinRollDelayInSeconds, 0.5f);

            LazyLoot.Config.MaxRollDelayInSeconds = Math.Max(LazyLoot.Config.MaxRollDelayInSeconds, LazyLoot.Config.MinRollDelayInSeconds + 0.1f);
        }
    }

    private static void DrawUserRestriction()
    {
        ImGui.Separator();
        ImGui.Checkbox("Pass on items with an item level below:", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelow);
        if (LazyLoot.Config.RestrictionIgnoreItemLevelBelow)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.DragInt("###ILVL", ref LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue);

            if (LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue < 0)
            {
                LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue = 0;
            }
        }

        ImGui.TextColored(ImGuiColors.DalamudRed, "Passes on items even if they are tradeable.");
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
    }

    private void DrawChatAndToast()
    {
        ImGui.Checkbox("Display roll information in chat.", ref LazyLoot.Config.EnableChatLogMessage);

        ImGui.Checkbox("Quest", ref LazyLoot.Config.EnableQuestToast);
        ImGui.Checkbox("Normal", ref LazyLoot.Config.EnableNormalToast);
        ImGui.Checkbox("Error", ref LazyLoot.Config.EnableErrorToast);
    }

    private void DrawFulf()
    {
        ImGui.Text("Fancy Ultimate Lazy Feature. Enable or Disable with /fulf.");
        ImGui.Checkbox($"Enable FULF", ref LazyLoot.Config.FulfEnabled);
        ImGui.TextColored(LazyLoot.Config.FulfEnabled ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, "FULF");

        ImGui.Text("Options are persistent");

        ImGui.SetNextItemWidth(100);
        if (ImGui.Combo("Roll options", ref LazyLoot.Config.FulfRoll, new string[]
        {
        "Need",
        "Greed",
        "Pass",
        }, 3))
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
