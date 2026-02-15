using HarmonyLib;
using UnityEngine;

namespace NuclearOptionActiveDecoy.Patches
{
    /// <summary>
    /// Patches ARH and SARH missile seekers to check for active decoys.
    /// When a decoy presents a stronger radar return than the real target,
    /// the missile loses lock on the aircraft and tracks the decoy instead.
    /// </summary>
    [HarmonyPatch]
    public static class SeekerPatches
    {
        /// <summary>
        /// After each ARH seeker Seek() call, check if any active decoy should pull the missile away.
        /// We patch the terminal tracking to evaluate decoys as competing radar returns.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ARHSeeker), "Seek")]
        static void ARHSeeker_Seek_Postfix(ARHSeeker __instance)
        {
            TryRedirectToDecoy(__instance);
        }

        /// <summary>
        /// After each SARH seeker Seek() call, same check.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SARHSeeker), "Seek")]
        static void SARHSeeker_Seek_Postfix(SARHSeeker __instance)
        {
            TryRedirectToDecoy(__instance);
        }

        private static void TryRedirectToDecoy(MissileSeeker seeker)
        {
            // Check if the mod is enabled
            if (Plugin.EnableActiveDecoy != null && !Plugin.EnableActiveDecoy.Value)
                return;

            if (ActiveDecoyBehavior.ActiveDecoys.Count == 0)
                return;

            // Access the missile and target via Harmony traverse
            var missile = Traverse.Create(seeker).Field("missile").GetValue<Missile>();
            var targetUnit = Traverse.Create(seeker).Field("targetUnit").GetValue<Unit>();

            if (missile == null || targetUnit == null)
                return;

            // Get the seeker's radar parameters
            RadarParams radarParams;
            if (seeker is ARHSeeker)
            {
                radarParams = Traverse.Create(seeker)
                    .Field("radarParameters").GetValue<RadarParams>();
            }
            else if (seeker is SARHSeeker)
            {
                radarParams = Traverse.Create(seeker)
                    .Field("radarParams").GetValue<RadarParams>();
            }
            else return;

            Vector3 seekerPos = missile.transform.position;

            // Find the best decoy that should attract this missile
            ActiveDecoyBehavior bestDecoy = null;
            float bestReturn = 0f;

            for (int i = ActiveDecoyBehavior.ActiveDecoys.Count - 1; i >= 0; i--)
            {
                var decoy = ActiveDecoyBehavior.ActiveDecoys[i];
                if (decoy == null || !decoy.isActive)
                {
                    ActiveDecoyBehavior.ActiveDecoys.RemoveAt(i);
                    continue;
                }

                if (!decoy.ShouldAttractMissile(seekerPos, targetUnit, radarParams))
                    continue;

                float ret = decoy.GetDecoyRadarReturn(seekerPos, radarParams);
                if (ret > bestReturn)
                {
                    bestReturn = ret;
                    bestDecoy = decoy;
                }
            }

            if (bestDecoy == null)
                return;

            // Redirect the missile's known position to the decoy
            // This makes the missile fly toward the decoy instead of the aircraft
            var knownPosField = Traverse.Create(seeker).Field("knownPos");
            if (knownPosField.FieldExists())
            {
                knownPosField.SetValue(
                    bestDecoy.transform.position.ToGlobalPosition());
            }

            // Clear the velocity tracking so the missile doesn't lead the old target
            var knownVelField = Traverse.Create(seeker).Field("knownVel");
            if (knownVelField.FieldExists())
            {
                knownVelField.SetValue(bestDecoy.velocity);
            }

            // For ARH seekers, break the radar lock so it has to reacquire
            if (seeker is ARHSeeker)
            {
                var lockField = Traverse.Create(seeker).Field("radarLockEstablished");
                if (lockField.FieldExists())
                    lockField.SetValue(false);
            }

            // Null out the target unit so the missile loses track of the aircraft
            Traverse.Create(seeker).Field("targetUnit").SetValue(null);
            missile.SetTarget(null);

            Plugin.Log.LogDebug(
                $"Missile redirected to active decoy at {bestDecoy.transform.position}");
        }
    }
}
