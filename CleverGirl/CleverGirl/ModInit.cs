using CustAmmoCategories;
using IRBTModUtils.CustomInfluenceMap;
using IRBTModUtils.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace CleverGirl
{

    public class Mod
    {

        public const string HarmonyPackage = "us.frostraptor.CleverGirl";

        public static DeferringLogger Log;
        public static string ModDir;
        public static ModConfig Config;

        public static readonly Random Random = new Random();

        public static void Init(string modDirectory, string settingsJSON)
        {
            ModDir = modDirectory;

            Exception settingsE;
            try
            {
                Mod.Config = JsonConvert.DeserializeObject<ModConfig>(settingsJSON);
            }
            catch (Exception e)
            {
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

            // Initialize custom components
            CustomComponents.Registry.RegisterSimpleCustomComponents(Assembly.GetExecutingAssembly());

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), HarmonyPackage);
        }

        // Invoked by ModTek once all other mods are finished loading
        public static void FinishedLoading(List<string> loadOrder)
        {
            foreach (string name in loadOrder)
            {

                if (name.Equals("IRBTModUtils", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitInfluenceMapFactors();
                }

                if (name.Equals("RolePlayer", StringComparison.InvariantCultureIgnoreCase))
                {
                    InitRoleplayerLink();
                }
            }

            // Hack to disable CAC processing of AI
            CustomAmmoCategories.DisableInternalWeaponChoose = true;
        }

        private static void InitRoleplayerLink()
        {
            try
            {
                Mod.Log.Info?.Write(" -- Checking for RolePlayer Integration -- ");
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly.FullName.StartsWith("RolePlayer"))
                    {
                        // Find the manager and pull it's singleton instance
                        Type managerType = assembly.GetType("RolePlayer.BehaviorVariableManager");
                        if (managerType == null)
                        {
                            Mod.Log.Warn?.Write("  Failed to find RolePlayer.BehaviorVariableManager.getBehaviourVariable - RP behavior variables will be ignored!");
                            return;
                        }

                        PropertyInfo instancePropertyType = managerType.GetProperty("Instance");
                        ModState.RolePlayerBehaviorVarManager = instancePropertyType.GetValue(null);
                        if (ModState.RolePlayerBehaviorVarManager == null)
                        {
                            Mod.Log.Warn?.Write("  Failed to get RolePlayer.BehaviorVariableManager instance!");
                            return;
                        }

                        // Find the method
                        ModState.RolePlayerGetBehaviorVar = managerType.GetMethod("getBehaviourVariable", new Type[] { typeof(AbstractActor), typeof(BehaviorVariableName) });

                        if (ModState.RolePlayerGetBehaviorVar != null)
                            Mod.Log.Info?.Write("  Successfully linked with RolePlayer");
                        else
                            Mod.Log.Warn?.Write("  Failed to find RolePlayer.BehaviorVariableManager.getBehaviourVariable - RP behavior variables will be ignored!");

                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Error trying to initialize RolePlayer link!");
            }
        }

        private static void InitInfluenceMapFactors()
        {
            Mod.Log.Info?.Write($"Checking for relevant types in all assemblies");
            // Record custom types from mods
            foreach (var factor in CustomFactors.GetCustomAllyFactors())
            {
                Mod.Log.Info?.Write($"Adding ally factor: {factor.GetType().FullName}");
                ModState.CustomAllyFactors.Add(factor);
            }
            foreach (var factor in CustomFactors.GetCustomHostileFactors())
            {
                Mod.Log.Info?.Write($"Adding hostile factor: {factor.GetType().FullName}");
                ModState.CustomHostileFactors.Add(factor);
            }
            foreach (var factor in CustomFactors.GetCustomPositionFactors())
            {
                Mod.Log.Info?.Write($"Adding position factor: {factor.GetType().FullName}");
                ModState.CustomPositionFactors.Add(factor);
            }

            // Record removals from mods
            foreach (var factor in CustomFactors.GetRemovedAllyFactors())
            {
                Mod.Log.Info?.Write($"Removing ally factor: {factor.GetType().FullName}");
                ModState.AllyFactorsToRemove.Add(factor);
            }
            foreach (var factor in CustomFactors.GetRemovedHostileFactors())
            {
                Mod.Log.Info?.Write($"Removing hostile factor: {factor.GetType().FullName}");
                ModState.HostileFactorsToRemove.Add(factor);
            }
            foreach (var factor in CustomFactors.GetRemovedPositionFactors())
            {
                Mod.Log.Info?.Write($"Removing position factor: {factor.GetType().FullName}");
                ModState.PositionFactorsToRemove.Add(factor);
            }

            Mod.Log.Info?.Write($" -- Done checking for influence factors");
        }

    }
}
