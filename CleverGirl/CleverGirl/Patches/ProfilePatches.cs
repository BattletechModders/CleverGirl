using Harmony;  
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CleverGirl.Patches {

    [HarmonyPatch(typeof(AITeam), "OnUpdate")]
    public static class AITeam_OnUpdate_Patch {
        public static void Prefix() {
            if (Mod.Config.Profile) {
                Mod.Log.Info("== CLEARING INVOKE COUNTS ==");
                State.InvokeCounts.Clear();
            }
        }

        public static void Postfix() {
            if (Mod.Config.Profile) {
                Mod.Log.Info("== INVOKE COUNTS ==");
                var sortedKeys = State.InvokeCounts.Keys.ToList();
                sortedKeys.Sort();

                foreach (string key in sortedKeys) {
                    List<long> invocations = State.InvokeCounts[key];

                    long max = 0;
                    long min = long.MaxValue;
                    long sum = 0;
                    foreach (long tick in invocations) {
                        if (tick > max) { max = tick; }
                        if (min > tick) { min = tick; }
                        sum += tick;
                    }
                    long average = sum / invocations.Count;
                    Mod.Log.Info($"  in:{invocations.Count,7:0000000}  av:{average,7:0000000}  ma:{max,7:0000000}  mi:{min,7:0000000}  " +
                        $"rn:{(max - min),7:0000000}  => {key}");
                }
            }
        }
    }


    public static class ProfilePatches {

        public class Target {
            public readonly Type type;
            public readonly MethodBase method;
            public Target(Type type, MethodBase method) {
                this.type = type;
                this.method = method;
            }
            public override string ToString() {
                string typeAndMethod = type.FullName + "::" + method.Name;

                StringBuilder sb = new StringBuilder("::");
                foreach (ParameterInfo pi in method.GetParameters()) {
                    sb.Append(pi.GetType().Name);
                    sb.Append(",");
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
                .Where(t =>
                    t.type.Name.StartsWith("Prefer") ||
                    t.type.Name.StartsWith("Behavior") ||
                    t.type.Name.Contains("AI") || 
                    t.type.Name.Contains("Node") ||
                    t.type.Name.Contains("AttackEvaluator"))

                // 3rd parties
                .Where(t => !t.type.FullName.StartsWith("AkNodeType"))
                .Where(t => !t.type.FullName.StartsWith("AkSoundEngine"))
                .Where(t => !t.type.FullName.StartsWith("AStar"))
                .Where(t => !t.type.FullName.StartsWith("Gaia.Quadtree"))
                .Where(t => !t.type.FullName.StartsWith("HoudiniEngineUnity"))
                .Where(t => !t.type.FullName.StartsWith("PlaylistItem"))
                .Where(t => !t.type.FullName.StartsWith("System"))
                .Where(t => !t.type.FullName.StartsWith("TScript"))
                .Where(t => !t.type.FullName.StartsWith("UIWidget"))
                .Where(t => !t.type.FullName.StartsWith("Unity"))
                .Where(t => !t.type.FullName.StartsWith("WwiseObjectID"))

                // HBS code
                .Where(t => !t.type.FullName.StartsWith("AILogCache"))
                .Where(t => !t.type.FullName.StartsWith("BattleTech.AIOrder"))
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

                // Prevent it so we can log it
                .Where(t => !t.method.Name.Contains("OnInterruptUpdate"))

                // TODO: Possible Memoize, but currently just log spam
                .Where(t => !t.method.Name.Contains("IsFriendly"))
                .Where(t => !t.method.Name.Contains("IsEnemy"))
                .Where(t => !t.method.Name.Contains("IsNeutral"))
                .Where(t => !t.method.Name.Contains("Read"))
                .Where(t => !t.method.Name.Contains("AddChild"))
                .Where(t => !t.method.Name.Contains("Get2DDistanceBetweenVector3s"))

                .Where(t => !t.method.Name.Contains("LogAI"))
                .Where(t => !t.method.Name.Contains("MakeBehaviorTreeLogWriter"))
                .Where(t => !t.method.Name.Contains("GetVariable"))

                .Where(t => !t.method.Name.Contains("get_HeraldryDef"))
                .Where(t => !t.method.Name.Contains("get_IsLocalPlayer"))

                // Unity methods
                .Where(t => !t.method.Name.Contains("GetComponents"))
                .Where(t => !t.method.Name.Contains("GetInstanceID"))

                .ToList();
            Mod.Log.Info($"FilteredMethods count: {filteredMethods.Count}");

            foreach (Target target in filteredMethods) {
                Mod.Log.Trace($"  Potential wrappable method: ({target.ToString()})");
                if (!target.method.IsGenericMethod && !target.method.IsAbstract 
                    && ((target.method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0)
                    ) { 
                    var hPrefix = new HarmonyMethod(prefix);
                    var hPostfix = new HarmonyMethod(postfix);
                    harmony.Patch(target.method, hPrefix, hPostfix, null);
                    Mod.Log.Info($"AI Method ({target.ToString()}) was wrapped.");
                }
            }

            Mod.Log.Info("=== End Diagnostics Logger ====");
        }
    }

    public static class LogExecTime {

        public static void Prefix(ref ExecState __state, MethodBase __originalMethod) {
            //Mod.Log.Info($"PREFIX: {__originalMethod.Name}");
            ProfilePatches.Target target = new ProfilePatches.Target(__originalMethod.DeclaringType, __originalMethod);
            __state = new ExecState(target);
            __state.Start();
            State.StackDepth++;
        }

        public static void Postfix(ref ExecState __state, Object __instance) {
            //Mod.Log.Info($"POSTFIX: {__state.name}");
            if (State.StackDepth > 0) { State.StackDepth--; }
            if (__state != null) {
                __state.Stop(__instance);

                // Increment our counter
                if (!State.InvokeCounts.ContainsKey(__state.target.ToString())) {
                    State.InvokeCounts[__state.target.ToString()] = new List<long>();
                }
                State.InvokeCounts[__state.target.ToString()].Add(__state.stopWatch.ElapsedTicks);
            }
        }
    }

    public class ExecState {
        public readonly Stopwatch stopWatch;
        public readonly ProfilePatches.Target target;

        public ExecState(ProfilePatches.Target target) {
            stopWatch = new Stopwatch();
            this.target = target;
        }

        public void Start() {
            string spaces = new string('=', State.StackDepth);
            Mod.Log.Trace($" {spaces} {target.ToString()} entered");
            stopWatch.Start();

        }

        public void Stop(Object instance) {
            stopWatch.Stop();

            string spaces = new string('=', State.StackDepth);
            if (target.type.Name == "BehaviorNode") {
                // Pull out the BehaviorNode name for easier traversing of tree
                BehaviorNode bn = instance as BehaviorNode;
                Traverse bnT = Traverse.Create(bn).Field("name");
                string bnName = bnT.GetValue<string>();

                Mod.Log.Trace($" {spaces} BehaviorNode:{bnName} took {stopWatch.Elapsed.Ticks}ticks / {stopWatch.Elapsed.TotalMilliseconds}ms");
            } else {
                Mod.Log.Trace($" {spaces} {target.ToString()} took {stopWatch.Elapsed.Ticks}ticks / {stopWatch.Elapsed.TotalMilliseconds}ms");
            }
        }

    }
}

