using Harmony;  
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CleverGirl.Patches {
    public static class ProfilePatches {

        private class Target {
            public readonly Type type;
            public readonly MethodBase method;
            public Target(Type type, MethodBase method) {
                this.type = type;
                this.method = method;
            }
            public string ToString() {
                string typeAndMethod = type.Name + ":" + method.Name;

                StringBuilder sb = new StringBuilder(":");
                foreach (ParameterInfo pi in method.GetParameters()) {
                    sb.Append(pi.Name);
                }
                
                return sb.Length > 1 ? typeAndMethod + sb.ToString() : typeAndMethod;
            }
        }

        public static void PatchAllMethods(HarmonyInstance harmony) {
            Mod.Log.Debug("=== Initializing Diagnostics Logger ====");
            
            var prefix = typeof(LogExecTime).GetMethod("Prefix");

            var postfix = typeof(LogExecTime).GetMethod("Postfix");

            var assembly = Assembly.GetAssembly(typeof(AIUtil));
            List<Target> allMethods = (
                from type in assembly.GetTypes()
                from method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                select new Target (type, method)
                ).Distinct().ToList();
            Mod.Log.Info($"Raw methods count: {allMethods.Count}");

            List<Target> filteredMethods = allMethods
                .Where(t => t.type.Name.StartsWith("AI") || t.type.Name.Contains("Node"))

                // 3rd parties
                .Where(t => !t.type.FullName.StartsWith("AkNodeType"))
                .Where(t => !t.type.FullName.StartsWith("AkSoundEngine"))
                .Where(t => !t.type.FullName.StartsWith("AStar"))
                .Where(t => !t.type.FullName.StartsWith("Gaia.Quadtree"))
                .Where(t => !t.type.FullName.StartsWith("HoudiniEngineUnity"))
                .Where(t => !t.type.FullName.StartsWith("PlaylistItem"))
                .Where(t => !t.type.FullName.StartsWith("TsNode"))
                .Where(t => !t.type.FullName.StartsWith("UIWidget"))
                .Where(t => !t.type.FullName.StartsWith("WwiseObjectID"))

                // HBS code
                .Where(t => !t.type.FullName.StartsWith("BattleTech.Data.DataManager"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.DataObjects"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.Flashpoint"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.LifepathNode"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.PathNode"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.PilotGenerator"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.Rendering"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.SimGameConversationManager"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.StarSystemNode"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.UI"))
                .Where(t => !t.type.FullName.StartsWith("HBS.Animation"))
                .Where(t => !t.type.FullName.StartsWith("HBS.Nav"))
                
                // Problematic methods
                .Where(t => !t.method.Name.Contains("CompareTo"))
                .Where(t => !t.method.Name.Contains("Equals"))
                .Where(t => !t.method.Name.Contains("ToString"))
                .Where(t => !t.method.Name.Contains("Finalize"))
                .Where(t => !t.method.Name.Contains("GetHashCode"))
                .Where(t => !t.method.Name.Contains("GetType"))
                .Where(t => !t.method.Name.Contains("MemberwiseClone"))
                .Where(t => !t.method.Name.Contains("obj_address"))
                .ToList();
            Mod.Log.Info($"FilteredMethods count: {filteredMethods.Count}");

            foreach (Target target in filteredMethods) {
                Mod.Log.Info($"  Wrapping method: ({target.ToString()})");
                if (!target.method.IsGenericMethod) {
                    var hPrefix = new HarmonyMethod(prefix);
                    var hPostfix = new HarmonyMethod(postfix);
                    harmony.Patch(target.method, hPrefix, hPostfix, null);
                    Mod.Log.Info($"AI Method ({target.ToString()}) was wrapped.");
                }
            }

            Mod.Log.Debug("=== End Diagnostics Logger ====");
        }
    }

    public static class LogExecTime {

        public static void Prefix(ExecState __state, MethodBase __originalMethod) {
            Mod.Log.Info($"PREFIX: {__originalMethod.Name}");
            __state = new ExecState(__originalMethod.Name);
            __state.Start();
        }

        public static void Postfix(ExecState __state) {
            Mod.Log.Info($"POSTFIX: {__state.name}");
            __state.Stop();
        }
    }

    public class ExecState {
        public readonly Stopwatch stopWatch;
        public readonly string name;

        public ExecState(string name) {
            stopWatch = new Stopwatch();
            this.name = name;
        }

        public void Start() {
            stopWatch.Start();
        }

        public void Stop() {
            stopWatch.Stop();
            Mod.Log.Info($"{name} took {stopWatch.ElapsedMilliseconds}ms");
        }

    }
}

