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

    [HarmonyPatch(typeof(InfluenceMapEvaluator), "RunEvaluationForSeconds")]
    public static class InfluenceMapEvaluator_RunEvaluationForSeconds
    {

        public static bool Prefix(InfluenceMapEvaluator __instance, float seconds, ref bool __result, 
			ref GraphCoroutine ___evaluationCoroutine, bool ___evaluationComplete)
        {
            Mod.Log.Trace?.Write("AIU:CDT:Post");

			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (___evaluationCoroutine == null)
			{
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
