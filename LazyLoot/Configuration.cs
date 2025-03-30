using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using ECommons.DalamudServices;

namespace LazyLoot
{
    
    public class RestrictionGroup
    {
        public List<RestrictionItem> Items { get; set; } = new();
        public List<RestrictionDuty> Duties { get; set; } = new();
    }
    
    public class RestrictionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Enabled { get; set; } = false;
        public string Icon { get; set; } = "Icon";
        public string Name { get; set; } = "Item";
        public bool Need { get; set; } = false;
        public bool Greed { get; set; } = false;
        public bool Pass { get; set; } = false;
        public bool DoNothing { get; set; } = true;
    }
    
    public class RestrictionDuty
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Enabled { get; set; } = false;
        public string Icon { get; set; } = "Icon";
        public string Name { get; set; } = "Duty";
        public bool Need { get; set; } = false;
        public bool Greed { get; set; } = false;
        public bool Pass { get; set; } = false;
        public bool DoNothing { get; set; } = true;
    }
    
    public class Configuration : IPluginConfiguration
    {
        public bool FulfEnabled = false;

        // Output
        public bool EnableChatLogMessage = true;
        public bool EnableErrorToast = false;
        public bool EnableNormalToast = false;
        public bool EnableQuestToast = true;

        // FulfRollOption
        public int FulfRoll = 0;

        // RollDelay
        public float MinRollDelayInSeconds = 0.5f;
        public float MaxRollDelayInSeconds = 1f;
        public float FulfMinRollDelayInSeconds = 1.5f;
        public float FulfMaxRollDelayInSeconds = 3f;

        // Restrictions
        // ILvl
        public bool RestrictionIgnoreItemLevelBelow = false;
        public int RestrictionIgnoreItemLevelBelowValue = 0;
        // AllItems
        public bool RestrictionIgnoreItemUnlocked = false;
        // Mounts        
        public bool RestrictionIgnoreMounts = false;
        // Minnions
        public bool RestrictionIgnoreMinions = false;
        // Bardings
        public bool RestrictionIgnoreBardings = false;
        // TripleTriadCards
        public bool RestrictionIgnoreTripleTriadCards = false;
        // Emote/Hairstyle
        public bool RestrictionIgnoreEmoteHairstyle = false;
        // OrchestrionRolls
        public bool RestrictionIgnoreOrchestrionRolls = false;
        // FadedCopy
        public bool RestrictionIgnoreFadedCopy = false;
        // Items i can't use with actuall class
        public bool RestrictionOtherJobItems = false;
        // Weekly lockout items
        public bool RestrictionWeeklyLockoutItems = false;
        // Loot is below a certain treshhold for the current job ilvl
        public bool RestrictionLootLowerThanJobIlvl = false;
        public int RestrictionLootLowerThanJobIlvlTreshold = 30;
        public int RestrictionLootLowerThanJobIlvlRollState = 1;
        // Loot is an upgrade to the current job
        public bool RestrictionLootIsJobUpgrade = false;
        public int RestrictionLootIsJobUpgradeRollState = 1;
        // Loot by Seal Worth
        public bool RestrictionSeals = false;
        public int RestrictionSealsAmnt = 1;

        //Diagnostics
        public bool DiagnosticsMode = false;
        public bool NoPassEmergency = false;

        public RestrictionGroup Restrictions { get; set; } = new();
        
        public int Version { get; set; }

        public void Save() => Svc.PluginInterface.SavePluginConfig(this);
    }
}