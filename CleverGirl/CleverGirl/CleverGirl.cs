using CleverGirl.Patches;
using Harmony;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Reflection;
using us.frostraptor.modUtils.logging;

namespace CleverGirl {

    public class Mod {

        public const string HarmonyPackage = "us.frostraptor.CleverGirl";

        public static IntraModLogger Log;
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

            Log = new IntraModLogger(modDirectory, "clever_girl", Mod.Config.Debug, Mod.Config.Trace);

            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            Log.Info($"Assembly version: {fvi.ProductVersion}");

            Log.Debug($"ModDir is:{modDirectory}");
            Log.Debug($"mod.json settings are:({settingsJSON})");
            Log.Info($"mergedConfig is:{Mod.Config}");

            var harmony = HarmonyInstance.Create(HarmonyPackage);

            // Patch for logging before all others as it's a non-interfering prefix
            ProfilePatches.PatchAllMethods(harmony);

            harmony.PatchAll(asm);

        }
    }
}
