using BattleTech;
using Harmony;
using IRBTModUtils.Extension;
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
    [HarmonyPatch(typeof(AITeam), "ChooseDesignatedTarget")]
    static class AITeam_ChooseDesignatedTarget {

        static void Postfix(AITeam __instance) {
            Mod.Log.Trace?.Write("AIU:CDT:Post");
            foreach (KeyValuePair<Lance, AbstractActor> kvp in __instance.DesignatedTargetForLance) {
                Mod.Log.Info?.Write($"Lance: {kvp.Key.DisplayName} has designedTarget: {kvp.Value.DistinctId()}");
            }
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeActiveAbilityInvocation")]
    static class AITeam_makeActiveAbilityInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeActiveAbilityInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeActiveProbeInvocation")]
    static class AITeam_makeActiveProbeInvocation
    {
        static void Prefix(OrderInfo order)
        {
            ActiveProbeOrderInfo activeProbeOrderInfo = order as ActiveProbeOrderInfo;
            Mod.Log.Info?.Write($" === makeActiveProbeInvocation => unit {activeProbeOrderInfo.MovingUnit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeAttackInvocation")]
    static class AITeam_makeAttackInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeAttackInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeCalledShotAttackInvocation")]
    static class AITeam_makeCalledShotAttackInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeCalledShotAttackInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeClaimInspirationInvocation")]
    static class AITeam_makeClaimInspirationInvocation
    {
        static void Prefix(AbstractActor unit)
        {
            Mod.Log.Info?.Write($" === makeClaimInspirationInvocation => unit {unit.DistinctId()} invoking order");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeInvocationFromOrders")]
    static class AITeam_makeInvocationFromOrders
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeInvocationFromOrders => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeJumpMoveInvocation")]
    static class AITeam_makeJumpMoveInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeJumpMoveInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeMultiAttackInvocation")]
    static class AITeam_makeMultiAttackInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeMultiAttackInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeNormalMoveInvocation")]
    static class AITeam_makeNormalMoveInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeNormalMoveInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }

    [HarmonyPatch(typeof(AITeam), "makeSprintMoveInvocation")]
    static class AITeam_makeSprintMoveInvocation
    {
        static void Prefix(AbstractActor unit, OrderInfo order)
        {
            Mod.Log.Info?.Write($" === makeSprintMoveInvocation => unit {unit.DistinctId()} invoking order of type: {order.OrderType}");
        }
    }
}
