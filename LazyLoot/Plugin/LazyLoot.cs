using Dalamud.Game;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using LazyLoot.Commands;
using LazyLoot.Config;
using LazyLoot.Ui;
using PunishLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LazyLoot.Plugin;

public class LazyLoot : IDalamudPlugin, IDisposable
{
    public string Name => "LazyLoot";

    internal static Configuration config;
    internal static ConfigUi ConfigUi;
    internal static DtrBarEntry DtrEntry;
    internal static bool FulfEnabled;
    internal List<BaseCommand> commands = new();
    internal static LazyLoot P;
    public LazyLoot(DalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, ECommons.Module.All);
        PunishLibMain.Init(pluginInterface, this);
        P = this;
        config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigUi = new ConfigUi();
        Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { ConfigUi.IsOpen = true; };
        DtrEntry ??= Svc.DtrBar.Get("LazyLoot");

        foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(BaseCommand)) && !t.IsAbstract))
        {
            try
            {
                PluginLog.Information($"Initializing command '{t.Name}'.");
                commands.Add((BaseCommand)Activator.CreateInstance(t)!);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed initializing command '{t.Name}'.");
            }
        }

        PluginLog.Information($"Loading all enabled commands.");

        foreach (var command in commands)
        {
            try
            {
                if (command.Enabled)
                {
                    command.Initialize();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed load up command '{command}'.");
            }
        }

        Svc.Framework.Update += OnFrameworkUpdate;
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

        ECommonsMain.Dispose();
        PunishLibMain.Dispose();
        PluginLog.Information(string.Format($">>Stop LazyLoot<<"));
        DtrEntry.Remove();

        foreach (var command in commands)
        {
            command.Dispose();
        }

        Svc.Framework.Update -= OnFrameworkUpdate;
        config.Save();
    }

    private void OnFrameworkUpdate(Framework framework)
    {
        if (FulfEnabled)
        {
            DtrEntry.Text = "LL-FULF";

            if (config.EnableNeedRoll)
            {
                DtrEntry.Text += " - Need";
            }
            else if (config.EnableNeedOnlyRoll)
            {
                DtrEntry.Text += " - Need Only";
            }
            else if (config.EnableGreedRoll)
            {
                DtrEntry.Text += " - Greed Only";
            }
            else if (config.EnablePassRoll)
            {
                DtrEntry.Text += " - Pass";
            }
            DtrEntry.Shown = true;
        }
        else
        {
            DtrEntry.Shown = false;
        }
    }
}