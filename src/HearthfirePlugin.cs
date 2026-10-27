using System.IO;
using BepInEx;
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
        public const string PluginVersion = "0.1.0";

        private const string TranslationPrefix = "Hearthfire.translations.";

        private Harmony _harmony;

        private void Awake()
        {
            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += CaskPiece.Register;
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
