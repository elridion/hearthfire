using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Hearthfire
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class HearthfirePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "elridion.hearthfire";
        public const string PluginName = "Hearthfire";
        public const string PluginVersion = "0.1.2";

        private const string TranslationPrefix = "Hearthfire.translations.";

        public static ConfigEntry<float> MinGlow;
        public static ConfigEntry<float> MaxGlow;

        private Harmony _harmony;

        private void Awake()
        {
            MinGlow = Config.Bind("Rune carvings", "MinGlow", 0f,
                new ConfigDescription("How strongly rune carvings glow at noon, the brightest time, as a fraction of the " +
                    "ember glow (0 = burnt-in black). The glow changes in a straight line from this to MaxGlow at midnight. " +
                    "Client-side only.",
                    new AcceptableValueRange<float>(0f, 1f)));
            MaxGlow = Config.Bind("Rune carvings", "MaxGlow", 0.8f,
                new ConfigDescription("How strongly rune carvings glow at midnight, the darkest time, and in always-dark " +
                    "places, as a fraction of the ember glow (above 1 the game's bloom haloes it). Client-side only.",
                    new AcceptableValueRange<float>(0f, 2f)));

            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += CaskPiece.Register;
            PrefabManager.OnVanillaPrefabsAvailable += CratePiece.Register;
            PrefabManager.OnVanillaPrefabsAvailable += IdolPieces.Register;
            PrefabManager.OnVanillaPrefabsAvailable += RuneCarvingPieces.Register;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(HearthfirePlugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Each embedded assets/translations/<Language>.json is named after a Valheim language. Jotunn
        // falls back to English for languages and keys without a translation.
        private static void AddLocalizations()
        {
            var localization = LocalizationManager.Instance.GetLocalization();
            var assembly = typeof(HearthfirePlugin).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(TranslationPrefix) || !resource.EndsWith(".json")) continue;
                string language = resource.Substring(TranslationPrefix.Length, resource.Length - TranslationPrefix.Length - ".json".Length);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                    localization.AddJsonFile(language, reader.ReadToEnd());
            }
        }
    }
}
