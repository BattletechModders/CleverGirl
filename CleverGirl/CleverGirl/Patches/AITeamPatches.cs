using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using us.frostraptor.modUtils;

namespace CleverGirl.Patches {

    /*
     * At the start of the turn, for every AI lance
     *   1. Find the closest lance member to all targets
     *   2. Using the imaginaryLaserWeapon, for all hostile targets
     *     a. Evaluate the toHit, times shots from position to calculate projectedDamage
     *     b. Evaluate if that attack will remove firepower from the table
     *     c. Select the highest firepower reduction from the table.
     */
    [HarmonyPatch(typeof(AIUtil), "ChooseDesignatedTarget")]
    public static class AITeam_ChooseDesignatedTarget {
        public static void Prefix(AITeam __instance) {
            Mod.Log.Trace("AIU:CDT:Pre");
        }

        public static void Postfix(AITeam __instance) {
            Mod.Log.Trace("AIU:CDT:Post");
            foreach (KeyValuePair<Lance, AbstractActor> kvp in __instance.DesignatedTargetForLance) {
                Mod.Log.Info($"Lance: {kvp.Key.DisplayName} has designedTarget: {CombatantUtils.Label(kvp.Value)}");
            }
        }
    }
}
