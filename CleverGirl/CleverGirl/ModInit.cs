using CleverGirl.InfluenceMap;
using CleverGirl.Patches;
using CustAmmoCategories;
using Harmony;
using IRBTModUtils.CustomInfluenceMap;
using IRBTModUtils.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            // Scan packages for instances of our interface
            InitInfluenceMapFactors();

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

        private static void InitInfluenceMapFactors()
        {
            Mod.Log.Info?.Write($"Checking for relevant types in all assemblies");
            // Record custom types from mods
            foreach (var type in GetAllTypesThatImplementInterface<CustomInfluenceMapAllyFactor>())
            {
                Mod.Log.Info?.Write($"Adding ally factor: {type.FullName}");
                CustomInfluenceMapAllyFactor instance = (CustomInfluenceMapAllyFactor)Activator.CreateInstance(type);
                ModState.CustomAllyFactors.Add(instance);
            }

            foreach (var type in GetAllTypesThatImplementInterface<CustomInfluenceMapHostileFactor>())
            {
                Mod.Log.Info?.Write($"Adding hostile factor: {type.FullName}");
                CustomInfluenceMapHostileFactor instance = (CustomInfluenceMapHostileFactor)Activator.CreateInstance(type);
                ModState.CustomHostileFactors.Add(instance);
            }

            foreach (var type in GetAllTypesThatImplementInterface<CustomInfluenceMapPositionFactor>())
            {
                Mod.Log.Info?.Write($"Adding position factor: {type.FullName}");
                CustomInfluenceMapPositionFactor instance = (CustomInfluenceMapPositionFactor)Activator.CreateInstance(type);
                ModState.CustomPositionFactors.Add(instance);
            }

            // Record removals from mods
            foreach (var type in GetAllTypesThatImplementInterface<InfluenceMapFactorsToRemove>())
            {
                Mod.Log.Info?.Write($"Found factors to remove: {type.FullName}");
                InfluenceMapFactorsToRemove factors = (InfluenceMapFactorsToRemove)Activator.CreateInstance(type);
                ModState.AllyFactorsToRemove.AddRange(factors.AllyFactorsToRemove());
                ModState.HostileFactorsToRemove.AddRange(factors.HostileFactorsToRemove());
                ModState.PositionFactorsToRemove.AddRange(factors.PositionFactorsToRemove());
            }
            Mod.Log.Info?.Write($" -- Done checking for influence factors");
        }

        private static IEnumerable<Type> GetAllTypesThatImplementInterface<T>()
        {
            var targetType = typeof(T);
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(s => s.GetTypes())
                .Where(p => !p.IsInterface && !p.IsAbstract)
                .Where(p => targetType.IsAssignableFrom(p));
        }
    }
}
