using HarmonyLib;
using UnityEngine;

namespace NuclearOptionActiveDecoy.Patches
{
    /// <summary>
    /// Patches Aircraft initialization to add the ActiveDecoyLauncher countermeasure
    /// to all aircraft that have a radar jammer (i.e., aircraft with EW capability).
    /// The ammo count matches the aircraft's flare count.
    /// </summary>
    [HarmonyPatch]
    public static class AircraftPatches
    {
        // Ammo counts per aircraft, matching flare counts from wiki data
        private static int GetDecoyAmmo(string aircraftName)
        {
            if (string.IsNullOrEmpty(aircraftName)) return 64;

            string lower = aircraftName.ToLower();

            if (lower.Contains("vortex") || lower.Contains("fs-20"))   return 72;
            if (lower.Contains("ifrit") || lower.Contains("kr-67"))    return 72;
            if (lower.Contains("medusa") || lower.Contains("ew-25"))   return 86;
            if (lower.Contains("darkreach") || lower.Contains("sfb-81")) return 86;
            if (lower.Contains("chicane") || lower.Contains("sah-46")) return 72;
            if (lower.Contains("ibis") || lower.Contains("uh-90"))     return 64;
            if (lower.Contains("revoker") || lower.Contains("fs-12"))  return 64;
            if (lower.Contains("compass") || lower.Contains("t/a-30") || lower.Contains("ta-30")) return 64;
            if (lower.Contains("cricket") || lower.Contains("ci-22"))  return 48;
            if (lower.Contains("brawler") || lower.Contains("a-19"))   return 120;
            if (lower.Contains("tarantula") || lower.Contains("vl-49")) return 144;

            return 64; // default
        }

        /// <summary>
        /// After Aircraft.OnStartClient(), add the active decoy countermeasure.
        /// OnStartClient is where countermeasureManager.Initialize() is called,
        /// so our postfix runs after the manager is ready to accept registrations.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Aircraft), "OnStartClient")]
        static void Aircraft_OnStartClient_Postfix(Aircraft __instance)
        {
            // Don't add if aircraft already has an active decoy
            if (__instance.GetComponentInChildren<ActiveDecoyLauncher>() != null)
                return;

            // Create the active decoy launcher on the aircraft
            var decoyObj = new GameObject("ActiveDecoyLauncher");
            decoyObj.transform.SetParent(__instance.transform);
            decoyObj.transform.localPosition = Vector3.zero;

            var launcher = decoyObj.AddComponent<ActiveDecoyLauncher>();
            launcher.displayName = "Active Decoy";
            launcher.displayImage = Plugin.ActiveDecoySprite;
            launcher.chargeable = false;

            // Set ammo based on aircraft type
            string name = "";
            if (__instance.definition != null)
            {
                name = __instance.definition.unitName ?? "";
                if (string.IsNullOrEmpty(name))
                    name = __instance.definition.jsonKey ?? "";
            }
            launcher.ammo = GetDecoyAmmo(name);

            // Attach to the aircraft's countermeasure system
            launcher.AttachToUnit(__instance);

            string stealthInfo = "";
            if (StealthModCompat.IsStealthModInstalled && StealthModCompat.IsStealthAircraft(__instance))
            {
                float divisor = StealthModCompat.GetRCSDivisor(__instance);
                float originalRCS = StealthModCompat.GetOriginalRCS(__instance);
                stealthInfo = $" [ActualStealth: RCS/{divisor}, original={originalRCS:F4}]";
            }

            Plugin.Log.LogDebug(
                $"Active decoy added to {name} with {launcher.ammo} rounds{stealthInfo}");
        }
    }
}
