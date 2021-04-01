using BattleTech;
using CleverGirl.InfluenceMap;
using GraphCoroutines;
using Harmony;
using IRBTModUtils.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using us.frostraptor.modUtils;

namespace CleverGirl.Patches
{
    // Replace the default InfluenceMapEvaluator to allow influenceFactors to be printed out for testing purposes

	[HarmonyPatch(typeof(InfluenceMapEvaluator), MethodType.Constructor)]
	static class InfluenceMapEvaluator_ctor
    {
		// Add custom 
		static void Postfix(InfluenceMapEvaluator __instance, ref InfluenceMapAllyFactor[] ___allyFactors, 
			ref InfluenceMapHostileFactor[] ___hostileFactors, ref InfluenceMapPositionFactor[] ___positionalFactors,
			ref PreferHigherExpectedDamageToHostileFactor ___expectedDamageFactor)
        {
			List<InfluenceMapAllyFactor> allyFactors = new List<InfluenceMapAllyFactor>(___allyFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomAllyFactors.Count} custom ally factors to influence map.");
			allyFactors.AddRange(ModState.CustomAllyFactors);
			___allyFactors = allyFactors.ToArray();

			List<InfluenceMapHostileFactor> hostileFactors = new List<InfluenceMapHostileFactor>(___hostileFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomHostileFactors.Count} custom hostile factors to influence map.");
			hostileFactors.AddRange(ModState.CustomHostileFactors); 
			___hostileFactors = hostileFactors.ToArray();

			List<InfluenceMapPositionFactor> positionFactors = new List<InfluenceMapPositionFactor>(___positionalFactors);
			Mod.Log.Info?.Write($"Adding {ModState.CustomPositionFactors.Count} custom position factors to influence map.");
			positionFactors.AddRange(ModState.CustomPositionFactors);
			___positionalFactors = positionFactors.ToArray();

		}
    }
	

    [HarmonyPatch(typeof(InfluenceMapEvaluator), "RunEvaluationForSeconds")]
	[HarmonyBefore("io.github.mpstark.AIToolkit")]
	static class InfluenceMapEvaluator_RunEvaluationForSeconds
    {

        static bool Prefix(InfluenceMapEvaluator __instance, float seconds, ref bool __result, 
			ref GraphCoroutine ___evaluationCoroutine, bool ___evaluationComplete)
        {
            Mod.Log.Trace?.Write("AIU:CDT:Post");

			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (___evaluationCoroutine == null)
			{
				Mod.Log.Info?.Write("Generating new CompoundInfluenceMapEvaluator");
				CompoundInfluenceMapEvaluator cime = new CompoundInfluenceMapEvaluator(__instance);
				___evaluationCoroutine = new GraphCoroutine(cime.IncrementalEvaluate());
			}

			while (Time.realtimeSinceStartup - realtimeSinceStartup <= seconds)
			{
				___evaluationCoroutine.Update();
				if (___evaluationComplete)
				{
					___evaluationCoroutine = null;
					break;
				}
			}
			__result = ___evaluationComplete;

			return false;
        }
    }
}
