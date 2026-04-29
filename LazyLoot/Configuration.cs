using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace LazyLoot
{
    
    public class RestrictionGroup
    {
        public List<CustomRestriction> Items { get; set; } = [];
        public List<CustomRestriction> Duties { get; set; } = [];
    }
    
    public class CustomRestriction
    {
        public uint Id { get; set; }
        public bool Enabled { get; set; }
        public RollResult RollRule { get; set; }
    }
    
    public class Configuration : IPluginConfiguration
    {
        public bool FulfEnabled = false;
        public bool ShowDtrEntry = true;

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
        // AllItems - Only Untradeables
        public bool RestrictionAllUnlockablesOnlyUntradeables = false;
        // Mounts        
        public bool RestrictionIgnoreMounts = false;
        // Mounts - Only Untradeables
        public bool RestrictionMountsOnlyUntradeables = false;
        // Minions
        public bool RestrictionIgnoreMinions = false;
        // Minions - Only Untradeables
        public bool RestrictionMinionsOnlyUntradeables = false;
        // Bardings
        public bool RestrictionIgnoreBardings = false;
        // Bardings - Only Untradeables
        public bool RestrictionBardingsOnlyUntradeables = false;
        // TripleTriadCards
        public bool RestrictionIgnoreTripleTriadCards = false;
        // TripleTriadCards - Only Untradeables
        public bool RestrictionTripleTriadCardsOnlyUntradeables = false;
        // Emote/Hairstyle
        public bool RestrictionIgnoreEmoteHairstyle = false;
        // Emote/Hairstyle - Only Untradeables
        public bool RestrictionEmoteHairstyleOnlyUntradeables = false;
        // OrchestrionRolls
        public bool RestrictionIgnoreOrchestrionRolls = false;
        // OrchestrionRolls - Only Untradeables
        public bool RestrictionOrchestrionRollsOnlyUntradeables = false;
        // FadedCopy
        public bool RestrictionIgnoreFadedCopy = false;
        // FadedCopy - Only Untradeables
        public bool RestrictionFadedCopyOnlyUntradeables = false;
        // Items that can't use with actual class
        public bool RestrictionOtherJobItems = false;
        // Weekly lockout items
        public bool RestrictionWeeklyLockoutItems = false;
        public bool WeeklyLockoutDutyActive = false;
        public ushort WeeklyLockoutDutyTerritoryId = 0;
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
        // Never pass on glamour items (Items that has a level and ilevel of 1)
        public bool NeverPassGlam = true;

        //Diagnostics
        public bool DiagnosticsMode = false;
        public bool NoPassEmergency = false;

        public RestrictionGroup Restrictions { get; set; } = new();
        
        public int Version { get; set; }

        public void Save() => Svc.PluginInterface.SavePluginConfig(this);
    }
}