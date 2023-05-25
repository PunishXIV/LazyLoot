﻿using Dalamud.Configuration;
using ECommons.DalamudServices;

namespace LazyLoot
{
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

        public int Version { get; set; }

        public void Save() => Svc.PluginInterface.SavePluginConfig(this);
    }
}