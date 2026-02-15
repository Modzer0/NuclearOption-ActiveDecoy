using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionActiveDecoy
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.nuclearoption.rcsmod", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static Sprite ActiveDecoySprite;

        // Config entries
        public static ConfigEntry<bool> EnableActiveDecoy;
        public static ConfigEntry<float> PenaltyMultiplier;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded");

            // Bind config entries
            EnableActiveDecoy = Config.Bind(
                "General",
                "EnableActiveDecoy",
                true,
                "Enable or disable the active decoy countermeasure system entirely.");

            PenaltyMultiplier = Config.Bind(
                "Balance",
                "PenaltyMultiplier",
                0.25f,
                new ConfigDescription(
                    "Effectiveness multiplier when the aircraft is heading toward the missile or has " +
                    "radar active. At 0.25 (default), the decoy's radar return is reduced to 25% — " +
                    "it can still work but requires multiple decoys. Set to 1.0 to disable the penalty.",
                    new AcceptableValueRange<float>(0f, 1f)));

            if (!EnableActiveDecoy.Value)
            {
                Logger.LogInfo("Active decoy is disabled via config");
                return;
            }

            ActiveDecoySprite = CreateActiveDecoySprite();

            // Detect ActualStealth mod and read its RCS divisors
            StealthModCompat.Initialize();

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"Harmony patches applied (penalty multiplier: {PenaltyMultiplier.Value})");
        }

        /// <summary>
        /// Creates a green box sprite with "ACTIVE" above "DECOY" centered for the countermeasure HUD icon.
        /// </summary>
        private static Sprite CreateActiveDecoySprite()
        {
            int width = 64;
            int height = 64;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color greenBorder = new Color(0f, 0.8f, 0f, 1f);
            Color greenFill = new Color(0f, 0.3f, 0f, 0.6f);
            Color textColor = new Color(0f, 1f, 0f, 1f);

            // Fill background
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isBorder = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                    tex.SetPixel(x, y, isBorder ? greenBorder : greenFill);
                }
            }

            // Draw "ACTIVE" on top half and "DECOY" on bottom half using simple pixel font
            DrawText(tex, "ACTIVE", width, height, 38, textColor); // upper line
            DrawText(tex, "DECOY", width, height, 18, textColor);  // lower line

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Draws a simple pixel-font string centered horizontally at the given y position.
        /// Each character is 5 wide x 7 tall pixels, with 1px spacing.
        /// </summary>
        private static void DrawText(Texture2D tex, string text, int texW, int texH, int baseY, Color color)
        {
            int charW = 5;
            int spacing = 1;
            int totalW = text.Length * (charW + spacing) - spacing;
            int startX = (texW - totalW) / 2;

            for (int i = 0; i < text.Length; i++)
            {
                int cx = startX + i * (charW + spacing);
                DrawChar(tex, text[i], cx, baseY, color);
            }
        }

        private static void DrawChar(Texture2D tex, char c, int ox, int oy, Color color)
        {
            // Minimal 5x7 pixel font for uppercase letters and digits
            byte[] pattern = GetCharPattern(c);
            if (pattern == null) return;

            for (int row = 0; row < 7 && row < pattern.Length; row++)
            {
                byte bits = pattern[row];
                for (int col = 0; col < 5; col++)
                {
                    if ((bits & (1 << (4 - col))) != 0)
                    {
                        int px = ox + col;
                        int py = oy + (6 - row); // flip Y since texture 0,0 is bottom-left
                        if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                            tex.SetPixel(px, py, color);
                    }
                }
            }
        }

        private static byte[] GetCharPattern(char c)
        {
            switch (c)
            {
                case 'A': return new byte[] { 0x04, 0x0A, 0x11, 0x1F, 0x11, 0x11, 0x11 };
                case 'C': return new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
                case 'D': return new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E };
                case 'E': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
                case 'I': return new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
                case 'O': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
                case 'T': return new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
                case 'V': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x0A, 0x0A, 0x04 };
                case 'Y': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
                default: return null;
            }
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.nuclearoption.activedecoy";
        public const string PLUGIN_NAME = "Active Decoy Countermeasure";
        public const string PLUGIN_VERSION = "0.5.0";
    }
}
