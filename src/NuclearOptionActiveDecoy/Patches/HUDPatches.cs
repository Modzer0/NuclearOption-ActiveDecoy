using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearOptionActiveDecoy.Patches
{
    /// <summary>
    /// Patches the CountermeasureIndicator to properly display the active decoy
    /// with a green box and "ACTIVE DECOY" text when it's the selected countermeasure.
    /// </summary>
    [HarmonyPatch]
    public static class HUDPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CountermeasureIndicator), "Refresh")]
        static void CountermeasureIndicator_Refresh_Postfix(CountermeasureIndicator __instance)
        {
            var aircraft = Traverse.Create(__instance).Field("aircraft").GetValue<Aircraft>();
            if (aircraft == null) return;

            var activeCM = aircraft.countermeasureManager.GetActiveCountermeasure();
            if (activeCM == null) return;

            if (!(activeCM is ActiveDecoyLauncher decoyLauncher))
                return;

            var counterName = Traverse.Create(__instance).Field("counterName").GetValue<Text>();
            var counterAmmo = Traverse.Create(__instance).Field("counterAmmo").GetValue<Text>();
            var counterImage = Traverse.Create(__instance).Field("counterImage").GetValue<Image>();

            if (counterName == null || counterAmmo == null || counterImage == null)
                return;

            int ammo = decoyLauncher.GetAmmo();
            Color color = ammo > 0 ? Color.green : Color.grey;

            counterName.text = "ACTIVE DECOY";
            counterName.color = color;
            counterAmmo.text = $"{ammo}";
            counterAmmo.color = color;

            if (counterImage.sprite != Plugin.ActiveDecoySprite)
            {
                counterImage.sprite = Plugin.ActiveDecoySprite;
                counterImage.enabled = true;
            }
            counterImage.color = color;
        }
    }
}
