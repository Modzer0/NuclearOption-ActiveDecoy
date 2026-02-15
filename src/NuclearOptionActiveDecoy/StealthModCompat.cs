using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NuclearOptionActiveDecoy
{
    /// <summary>
    /// Detects and integrates with the ActualStealth (NuclearOptionRCSMod) mod.
    /// When ActualStealth is installed, stealth aircraft have their RCS divided by
    /// a configurable amount (default 100). We need to account for this so that:
    /// 1. The decoy's effective RCS is based on the ORIGINAL (pre-stealth) aircraft RCS
    /// 2. The decoy still presents a convincing false return to missiles
    /// </summary>
    public static class StealthModCompat
    {
        public const string STEALTH_MOD_GUID = "com.nuclearoption.rcsmod";

        public static bool IsStealthModInstalled { get; private set; }

        // Cached divisor values from the stealth mod's config
        private static Dictionary<string, float> _divisorLookup = new Dictionary<string, float>();

        // Map game object names to config field names in the stealth mod
        private static readonly Dictionary<string, string> _gameNameToConfigField =
            new Dictionary<string, string>
        {
            { "SmallFighter1", "VortexDivisor" },
            { "Multirole1",    "IfritDivisor" },
            { "Darkreach",     "DarkreachDivisor" }
        };

        // Also map display names for convenience
        private static readonly Dictionary<string, string> _displayNameToGameName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vortex",    "SmallFighter1" },
            { "fs-20",     "SmallFighter1" },
            { "ifrit",     "Multirole1" },
            { "kr-67",     "SmallFighter1" },
            { "darkreach", "Darkreach" },
            { "sfb-81",    "Darkreach" }
        };

        /// <summary>
        /// Call during plugin Awake to detect and read the stealth mod's configuration.
        /// </summary>
        public static void Initialize()
        {
            IsStealthModInstalled = false;
            _divisorLookup.Clear();

            // Check if the stealth mod plugin is loaded
            var stealthPlugin = BepInEx.Bootstrap.Chainloader.PluginInfos
                .Values
                .FirstOrDefault(p => p.Metadata.GUID == STEALTH_MOD_GUID);

            if (stealthPlugin == null)
            {
                Plugin.Log.LogInfo("ActualStealth mod not detected — using standard RCS values");
                return;
            }

            IsStealthModInstalled = true;
            Plugin.Log.LogInfo("ActualStealth mod detected — reading RCS divisors");

            // Read the config entries via reflection from the stealth mod's Plugin type
            var pluginInstance = stealthPlugin.Instance;
            if (pluginInstance == null)
            {
                Plugin.Log.LogWarning("ActualStealth plugin instance is null, using default divisor of 100");
                SetDefaultDivisors();
                return;
            }

            var pluginType = pluginInstance.GetType();

            foreach (var kvp in _gameNameToConfigField)
            {
                string gameName = kvp.Key;
                string fieldName = kvp.Value;

                try
                {
                    var field = pluginType.GetField(fieldName,
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    if (field != null)
                    {
                        var configEntry = field.GetValue(null) as ConfigEntry<float>;
                        if (configEntry != null)
                        {
                            _divisorLookup[gameName] = configEntry.Value;
                            Plugin.Log.LogInfo(
                                $"  {gameName}: RCS divisor = {configEntry.Value}");
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"Failed to read {fieldName} from ActualStealth: {ex.Message}");
                }

                // Fallback to default
                _divisorLookup[gameName] = 100f;
                Plugin.Log.LogInfo($"  {gameName}: using default divisor = 100");
            }
        }

        private static void SetDefaultDivisors()
        {
            foreach (var kvp in _gameNameToConfigField)
                _divisorLookup[kvp.Key] = 100f;
        }

        /// <summary>
        /// Gets the RCS divisor applied by ActualStealth for a given aircraft.
        /// Returns 1.0 if the stealth mod is not installed or the aircraft isn't affected.
        /// </summary>
        public static float GetRCSDivisor(Aircraft aircraft)
        {
            if (!IsStealthModInstalled || aircraft == null)
                return 1f;

            string gameName = aircraft.gameObject.name.Replace("(Clone)", "").Trim();

            if (_divisorLookup.TryGetValue(gameName, out float divisor))
                return divisor;

            return 1f;
        }

        /// <summary>
        /// Gets the original (pre-stealth-mod) RCS for an aircraft.
        /// If ActualStealth is installed, this reverses the division to get the true RCS.
        /// </summary>
        public static float GetOriginalRCS(Aircraft aircraft)
        {
            float currentRCS = aircraft.RCS;
            float divisor = GetRCSDivisor(aircraft);
            return currentRCS * divisor;
        }

        /// <summary>
        /// Checks if a given aircraft is affected by the stealth mod.
        /// </summary>
        public static bool IsStealthAircraft(Aircraft aircraft)
        {
            if (!IsStealthModInstalled || aircraft == null)
                return false;

            string gameName = aircraft.gameObject.name.Replace("(Clone)", "").Trim();
            return _divisorLookup.ContainsKey(gameName);
        }
    }
}
