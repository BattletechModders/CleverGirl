using CleverGirl.Helper;
using System.Reflection;
using UnityEngine;

namespace CleverGirl.Patches.BehaviorNodes
{
    [HarmonyPatch(typeof(MoveTowardsHighestPriorityMoveCandidateNode), nameof(MoveTowardsHighestPriorityMoveCandidateNode.Tick))]
    static class MoveTowardsHighestPriorityMoveCandidateNode_Tick
    {

        [HarmonyPostfix]
        static void Postfix(MoveTowardsHighestPriorityMoveCandidateNode __instance, ref BehaviorTreeResults __result)
        {
            if (__result == null || !(__result.orderInfo is AttackOrderInfo)) return; // Nothing to do

            BehaviorTree NodeBehaviorTree = __instance.tree;
            AbstractActor NodeContextUnit = __instance.unit;

            Mod.Log.Info?.Write($"MoveTowardsHighestPriorityMoveCandidateNode generated an AttackOrder for unit: {NodeContextUnit.DistinctId()}, without evaluating CBTBE options!");
            Mod.Log.Info?.Write($"  Disabling the attack but leaving the move oder intact.");

            // Recalculate the best movement type
            WorkspaceEvaluationEntry workspaceEvaluationEntry = NodeBehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];
            MoveType bestMoveType = workspaceEvaluationEntry.GetBestMoveType();
            Vector3 vector = workspaceEvaluationEntry.Position;
            BehaviorVariableValue behaviorVariableValue = BehaviorHelper.GetCachedBehaviorVariableValue(NodeBehaviorTree, BehaviorVariableName.String_StayInsideRegionGUID);
            if (behaviorVariableValue != null || behaviorVariableValue.StringVal.Length == 0)
            {
                vector = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(NodeContextUnit, vector);
            }
            float angle = workspaceEvaluationEntry.Angle;
            Vector3 vector2 = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 lookAt = vector + vector2;

            MovementOrderInfo orderInfo = new MovementOrderInfo(vector, lookAt);
            orderInfo.IsReverse = bestMoveType == MoveType.Backward;
            orderInfo.IsJumping = bestMoveType == MoveType.Jumping;
            orderInfo.IsSprinting = bestMoveType == MoveType.Sprinting;

            Mod.Log.Info?.Write($"  Returning MovementOrderInfo with isReverse: {orderInfo.IsReverse}  isJumping: {orderInfo.IsJumping}  isSprinting: {orderInfo.IsSprinting}.");
            __result.orderInfo = orderInfo;
        }
    }
}
