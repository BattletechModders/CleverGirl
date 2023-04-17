using CleverGirl.InfluenceMap;
using GraphCoroutines;
using System.Collections.Generic;
using UnityEngine;

namespace CleverGirl.Patches
{
    // Replace the default InfluenceMapEvaluator to allow influenceFactors to be printed out for testing purposes

    [HarmonyPatch(typeof(InfluenceMapEvaluator), MethodType.Constructor)]
	static class InfluenceMapEvaluator_ctor
    {
        // Add custom 
        [HarmonyPostfix]
        static void Postfix(InfluenceMapEvaluator __instance)
        {
			InfluenceMapAllyFactor[] ___allyFactors = __instance.allyFactors;
			InfluenceMapHostileFactor[] ___hostileFactors = __instance.hostileFactors;
			InfluenceMapPositionFactor[] ___positionalFactors = __instance.positionalFactors;

            List<InfluenceMapAllyFactor> mergedAllyFactors = new List<InfluenceMapAllyFactor>(___allyFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomAllyFactors.Count} custom ally factors to influence map.");
			mergedAllyFactors.AddRange(ModState.CustomAllyFactors);
			__instance.allyFactors = mergedAllyFactors.ToArray();

			List<InfluenceMapHostileFactor> mergedHostileFactors = new List<InfluenceMapHostileFactor>(___hostileFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomHostileFactors.Count} custom hostile factors to influence map.");
			mergedHostileFactors.AddRange(ModState.CustomHostileFactors); 
            __instance.hostileFactors = mergedHostileFactors.ToArray();

            List<InfluenceMapPositionFactor> mergedPositionalFactors = new List<InfluenceMapPositionFactor>(___positionalFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomPositionFactors.Count} custom position factors to influence map.");
			mergedPositionalFactors.AddRange(ModState.CustomPositionFactors);
            __instance.positionalFactors = mergedPositionalFactors.ToArray();

        }
    }
	

    [HarmonyPatch(typeof(InfluenceMapEvaluator), "RunEvaluationForSeconds")]
	[HarmonyBefore("io.github.mpstark.AIToolkit")]
	static class InfluenceMapEvaluator_RunEvaluationForSeconds
    {
        [HarmonyPrefix]
        static void Prefix(ref bool __runOriginal, InfluenceMapEvaluator __instance, float seconds, ref bool __result)
        {
			if (!__runOriginal) return;

            Mod.Log.Trace?.Write("AIU:CDT:Post");

			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (__instance.evaluationCoroutine == null)
			{
				Mod.Log.Info?.Write("Generating new CompoundInfluenceMapEvaluator");
				CompoundInfluenceMapEvaluator cime = new CompoundInfluenceMapEvaluator(__instance);
				__instance.evaluationCoroutine = new GraphCoroutine(cime.IncrementalEvaluate());
            }

			while (Time.realtimeSinceStartup - realtimeSinceStartup <= seconds)
			{
                __instance.evaluationCoroutine.Update();
				if (__instance.evaluationComplete)
				{
                    Mod.Log.Info?.Write("Evaluation complete, destroying coroutine");
                    __instance.evaluationCoroutine = null;
					break;
				}
			}
			__result = __instance.evaluationComplete;

			__runOriginal = false;
        }
    }
}
