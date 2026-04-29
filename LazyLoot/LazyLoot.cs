using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using PunishLib;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.Logging;

namespace LazyLoot;

public class LazyLoot : IDalamudPlugin, IDisposable
{
    private static readonly RollResult[] RollArray =
    [
        RollResult.Needed,
        RollResult.Greeded,
        RollResult.Passed
    ];

    public string Name => "LazyLoot";

    internal static Configuration Config;
    private static ConfigUi _configUi;
    private static IDtrBarEntry _dtrEntry;

    static DateTime _nextRollTime = DateTime.Now;
    static RollResult _rollOption = RollResult.UnAwarded;
    private static int _need, _greed, _pass;
    
    private const uint CastYourLotMessage = 5194;
    private const uint WeeklyLockoutMessage = 4234;

    public LazyLoot(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);
        PunishLibMain.Init(pluginInterface, "LazyLoot", new AboutPlugin() { Developer = "53m1k0l0n/Gidedin" });

        Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _configUi = new ConfigUi();
        _dtrEntry = Svc.DtrBar.Get("LazyLoot");
        _dtrEntry.OnClick = OnDtrClick;


        Svc.PluginInterface.UiBuilder.OpenMainUi += OnOpenConfigUi;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        Svc.Chat.CheckMessageHandled += NoticeLoot;
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
        SyncWeeklyLockoutDutyState(Svc.ClientState.TerritoryType);

        Svc.Commands.AddHandler("/lazyloot", new CommandInfo(LazyCommand)
        {
            HelpMessage = "Open Lazy Loot config.",
            ShowInHelp = true,
        });

        Svc.Commands.AddHandler("/lazy", new CommandInfo(LazyCommand)
        {
            HelpMessage = "Open Lazy Loot config by default. Add need | greed | pass to roll on current items.",
            ShowInHelp = true,
        });

        Svc.Commands.AddHandler("/fulf", new CommandInfo(FulfCommand)
        {
            HelpMessage =
                "Enable/Disable FULF with /fulf [on|off] or change the loot rule with /fulf need | greed | pass.",
            ShowInHelp = true,
        });

        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private static void OnDtrClick(DtrInteractionEvent ev)
    {
        if (ev.ModifierKeys.HasFlag(ClickModifierKeys.Ctrl))
        {
            _configUi.IsOpen = true;
            return;
        }

        switch (ev.ClickType)
        {
            case MouseClickType.Left:
                CycleFulf(true);
                break;

            case MouseClickType.Right:
                CycleFulf(false);
                break;
        }
    }

    private void LazyCommand(string command, string arguments)
    {
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0)
        {
            OnOpenConfigUi();
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "test" when args.Length >= 2:
            {
                switch (args[1].ToLowerInvariant())
                {
                    case "item":
                        TestWhatWouldLlDo(string.Join(" ", args.Skip(2)));
                        return;
                }

                break;
            }
            default:
                RollingCommand(null!, arguments);
                return;
        }
    }

    private static void CycleFulf(bool forward)
    {
        if (!Config.FulfEnabled)
        {
            Config.FulfEnabled = true;
            Config.FulfRoll = forward ? 2 : 0;
            Config.Save();
            return;
        }

        if (forward)
        {
            switch (Config.FulfRoll)
            {
                case 2:
                    Config.FulfRoll = 1;
                    break;
                case 1:
                    Config.FulfRoll = 0;
                    break;
                default:
                    Config.FulfEnabled = false;
                    break;
            }
        }
        else
        {
            switch (Config.FulfRoll)
            {
                case 0:
                    Config.FulfRoll = 1;
                    break;
                case 1:
                    Config.FulfRoll = 2;
                    break;
                default:
                    Config.FulfEnabled = false;
                    break;
            }
        }

        Config.Save();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        Svc.PluginInterface.UiBuilder.OpenMainUi -= OnOpenConfigUi;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        Svc.Chat.CheckMessageHandled -= NoticeLoot;
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;

        Svc.Commands.RemoveHandler("/lazyloot");
        Svc.Commands.RemoveHandler("/lazy");
        Svc.Commands.RemoveHandler("/fulf");

        ECommonsMain.Dispose();
        PunishLibMain.Dispose();
        Svc.Log.Information(">>Stop LazyLoot<<");
        _dtrEntry.Remove();

        Svc.Framework.Update -= OnFrameworkUpdate;
        Config.Save();
    }

    private static void FulfCommand(string command, string arguments)
    {
        var res = GetResult(arguments);
        if (res.HasValue)
            Config.FulfRoll = res.Value;
        else if (arguments.Contains("off", StringComparison.CurrentCultureIgnoreCase))
            Config.FulfEnabled = false;
        else if (arguments.Contains("on", StringComparison.CurrentCultureIgnoreCase))
            Config.FulfEnabled = true;
        else
            Config.FulfEnabled = !Config.FulfEnabled;
    }

    private void RollingCommand(string command, string arguments)
    {
        var res = GetResult(arguments);
        if (res.HasValue)
        {
            _rollOption = RollArray[res.Value % 3];
        }
    }

    private static int? GetResult(string str)
    {
        if (str.Contains("need", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }
        else if (str.Contains("greed", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        else if (str.Contains("pass", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return null;
    }

    private static void OnOpenConfigUi()
    {
        _configUi.Toggle();
    }

    private static void OnFrameworkUpdate(IFramework framework)
    {
        string dtrText;
        if (Config.FulfEnabled)
        {
            dtrText = Config.FulfRoll switch
            {
                0 => "Needing",
                1 => "Greeding",
                2 => "Passing",
                _ => throw new ArgumentOutOfRangeException(nameof(Config.FulfRoll)),
            };
        }
        else
        {
            dtrText = "FULF Disabled";
        }

        var isWeeklyLockedDutyActive = Config is { RestrictionWeeklyLockoutItems: true, WeeklyLockoutDutyActive: true };

        if (isWeeklyLockedDutyActive) dtrText += " (Disabled | WLD)";

        _dtrEntry.Text = new SeString(
            new IconPayload(BitmapFontIcon.Dice),
            new TextPayload(dtrText));

        _dtrEntry.Shown = Config.ShowDtrEntry;

        if (isWeeklyLockedDutyActive) return;

        RollLoot();
    }

    private static void RollLoot()
    {
        if (_rollOption == RollResult.UnAwarded) return;
        if (DateTime.Now < _nextRollTime) return;

        //No rolling in cutscene.
        if (Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return;

        _nextRollTime = DateTime.Now.AddMilliseconds(Math.Max(1500, new Random()
            .Next((int)(Config.MinRollDelayInSeconds * 1000),
                (int)(Config.MaxRollDelayInSeconds * 1000))));

        try
        {
            if (Roller.RollOneItem(_rollOption, ref _need, ref _greed, ref _pass)) return; //Finish the loot
            ShowResult(_need, _greed, _pass);
            _need = _greed = _pass = 0;
            _rollOption = RollResult.UnAwarded;
            Roller.Clear();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Something Wrong with rolling!");
        }
    }

    private static void ShowResult(int need, int greed, int pass)
    {
        SeString seString = new(new List<Payload>()
        {
            new TextPayload("Need "),
            new UIForegroundPayload(575),
            new TextPayload(need.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" item" + (need == 1 ? "" : "s") + ", greed "),
            new UIForegroundPayload(575),
            new TextPayload(greed.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" item" + (greed == 1 ? "" : "s") + ", pass "),
            new UIForegroundPayload(575),
            new TextPayload(pass.ToString()),
            new UIForegroundPayload(0),
            new TextPayload(" item" + (pass == 1 ? "" : "s") + ".")
        });

        if (Config.EnableChatLogMessage)
        {
            Svc.Chat.Print(seString);
        }

        if (Config.EnableErrorToast)
        {
            Svc.Toasts.ShowError(seString);
        }

        if (Config.EnableNormalToast)
        {
            Svc.Toasts.ShowNormal(seString);
        }

        if (Config.EnableQuestToast)
        {
            Svc.Toasts.ShowQuest(seString);
        }
    }

    private void NoticeLoot(XivChatType type, int senderId, ref SeString sender, ref SeString message,
        ref bool isHandled)
    {
        if (!Config.FulfEnabled || type != (XivChatType)2105) return;
        // do a few checks to see if the message is the weekly lockout message the game sends
        if (CheckAndUpdateWeeklyLockoutDutyFlag(message)) return;
        // if not Cast your lot, then just ignore
        if (message.TextValue != Svc.Data.GetExcelSheet<LogMessage>().First(x => x.RowId == CastYourLotMessage).Text) return;
        _nextRollTime = DateTime.Now.AddMilliseconds(new Random()
            .Next((int)(Config.FulfMinRollDelayInSeconds * 1000),
                (int)(Config.FulfMaxRollDelayInSeconds * 1000)));
        _rollOption = RollArray[Config.FulfRoll];
    }

    private static bool CheckAndUpdateWeeklyLockoutDutyFlag(SeString message)
    {
        if (!Config.RestrictionWeeklyLockoutItems || Config.WeeklyLockoutDutyActive)
            return false;

        if (!IsHighEndDutyTerritory(Svc.ClientState.TerritoryType))
        {
            ClearWeeklyLockoutDutyState();
            return false;
        }

        var weeklyLockoutMessage = Svc.Data.GetExcelSheet<LogMessage>().GetRowOrDefault(WeeklyLockoutMessage);
        if (weeklyLockoutMessage == null)
            return false;

        if (message.TextValue != weeklyLockoutMessage.Value.Text) return false;
        
        Config.WeeklyLockoutDutyActive = true;
        Config.WeeklyLockoutDutyTerritoryId = Svc.ClientState.TerritoryType;
        Config.Save();
        DuoLog.Debug("Weekly lockout duty detected! Rolling is temporarily suspended.");

        return true;
    }

    private static void OnTerritoryChanged(ushort territoryId)
    {
        if (!IsHighEndDutyTerritory(territoryId))
        {
            ClearWeeklyLockoutDutyState();
            return;
        }

        if (!Config.WeeklyLockoutDutyActive)
            return;

        if (Config.WeeklyLockoutDutyTerritoryId == territoryId)
            return;

        ClearWeeklyLockoutDutyState();
    }

    private static void SyncWeeklyLockoutDutyState(ushort territoryId)
    {
        if (!Config.WeeklyLockoutDutyActive)
            return;

        if (!IsHighEndDutyTerritory(territoryId) || Config.WeeklyLockoutDutyTerritoryId != territoryId)
            ClearWeeklyLockoutDutyState();
    }

    private static void ClearWeeklyLockoutDutyState()
    {
        if (Config is { WeeklyLockoutDutyActive: false, WeeklyLockoutDutyTerritoryId: 0 })
            return;

        Config.WeeklyLockoutDutyActive = false;
        Config.WeeklyLockoutDutyTerritoryId = 0;
        Config.Save();
        DuoLog.Debug("Weekly lockout duty suspension cleared.");
    }

    private static bool IsHighEndDutyTerritory(ushort territoryId)
    {
        var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        var contentFinder = territory?.ContentFinderCondition.Value;
        return contentFinder is { HighEndDuty: true };
    }

    private static void TestWhatWouldLlDo(string idOrNameArg)
    {
        if (string.IsNullOrWhiteSpace(idOrNameArg))
        {
            DuoLog.Debug("Usage: /lazy test <Item ID or Item Name>");
            return;
        }

        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        if (!uint.TryParse(idOrNameArg, out var itemId))
        {
            var search = idOrNameArg.Trim();
            var matches = itemSheet
                .Where(x => x.Name.ToString()
                    .Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            switch (matches.Count)
            {
                case 0:
                    DuoLog.Debug($"No item found matching your search '{search}'.");
                    return;
                case > 1:
                {
                    Svc.Chat.Print(new SeString(new List<Payload>
                    {
                        new TextPayload(
                            $"Found {matches.Count} entries for search '{search}'. Showing the first 5:")
                    }));

                    foreach (var match in matches.Take(5))
                    {
                        Svc.Chat.Print(new SeString(new List<Payload>
                            {
                                new TextPayload($"[LazyLoot Item Test] :: ID {match.RowId} :: "),
                                new UIForegroundPayload((ushort)(0x223 + match.Rarity * 2)),
                                new UIGlowPayload((ushort)(0x224 + match.Rarity * 2)),
                                new ItemPayload(match.RowId, true),
                                new TextPayload(match.Name.ExtractText()),
                                RawPayload.LinkTerminator,
                                new UIForegroundPayload(0),
                                new UIGlowPayload(0),
                            })
                        );
                    }

                    return;
                }
                default:
                    itemId = matches[0].RowId;
                    break;
            }
        }

        if (itemId == 0)
        {
            DuoLog.Error($"Invalid item id or name: '{idOrNameArg}'.");
            return;
        }

        var item = itemSheet.GetRow(itemId);

        var tempDiagnosticsMode = Config.DiagnosticsMode;
        Config.DiagnosticsMode = true;
        Config.Save();
        var decision = Roller.WhatWouldLlDo(itemId);
        Config.DiagnosticsMode = tempDiagnosticsMode;
        Config.Save();

        var decisionText = decision switch
        {
            Roller.LlDecision.DoNothing => "DO NOTHING",
            Roller.LlDecision.Pass => "PASS",
            Roller.LlDecision.Greed => "GREED",
            Roller.LlDecision.Need => "NEED",
            _ => $"UNKNOWN ({decision})"
        };
        ushort decisionColor = decision switch
        {
            Roller.LlDecision.DoNothing => 8, // Grey
            Roller.LlDecision.Pass => 14, // Red
            Roller.LlDecision.Greed => 500, // Yellow
            Roller.LlDecision.Need => 45, // Green
            _ => 0
        };

        Svc.Chat.Print(new SeString(new List<Payload>
            {
                new TextPayload($"[LazyLoot Item Test] :: ID {itemId} :: "),
                new UIForegroundPayload((ushort)(0x223 + item.Rarity * 2)),
                new UIGlowPayload((ushort)(0x224 + item.Rarity * 2)),
                new ItemPayload(item.RowId, true),
                new TextPayload(item.Name.ExtractText()),
                RawPayload.LinkTerminator,
                new UIForegroundPayload(0),
                new UIGlowPayload(0),
                new TextPayload(" :: "),
                new UIForegroundPayload(decisionColor),
                new TextPayload($"{decisionText}"),
                new UIForegroundPayload(0),
            })
        );
    }
}