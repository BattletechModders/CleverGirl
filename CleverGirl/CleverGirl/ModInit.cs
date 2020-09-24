using CleverGirl.Patches;
using CustAmmoCategories;
using Harmony;
using IRBTModUtils.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Reflection;
using us.frostraptor.modUtils.logging;

namespace CleverGirl {

    public class Mod {

        public const string HarmonyPackage = "us.frostraptor.CleverGirl";

        public static DeferringLogger Log;
        public static string ModDir;
        public static ModConfig Config;

        public static readonly Random Random = new Random();

        public static void Init(string modDirectory, string settingsJSON) {
            ModDir = modDirectory;

            Exception settingsE;
            try {
                Mod.Config = JsonConvert.DeserializeObject<ModConfig>(settingsJSON);
            } catch (Exception e) {
                settingsE = e;
                Mod.Config = new ModConfig();
            }

            Log = new DeferringLogger(modDirectory, "clever_girl", Mod.Config.Debug, Mod.Config.Trace);

            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            Log.Info?.Write($"Assembly version: {fvi.ProductVersion}");

            Log.Debug?.Write($"ModDir is:{modDirectory}");
            Log.Debug?.Write($"mod.json settings are:({settingsJSON})");
            Mod.Config.LogConfig();

            var harmony = HarmonyInstance.Create(HarmonyPackage);

            // Initialize custom components
            CustomComponents.Registry.RegisterSimpleCustomComponents(Assembly.GetExecutingAssembly());

            // Patch for logging before all others as it's a non-interfering prefix
            if (Mod.Config.Profile) {
                ProfilePatches.PatchAllMethods(harmony);
            }

            // Hack to disable CAC processing of AI
            CustomAmmoCategories.DisableInternalWeaponChoose = true;

            harmony.PatchAll(asm);

        }
    }
}
