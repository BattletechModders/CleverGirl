using CleverGirl.Helper;
using System.Reflection;
using UnityEngine;

namespace CleverGirl.Patches.BehaviorNodes
{
    [HarmonyPatch]
    static class MoveTowardsHighestPriorityMoveCandidateNode_Tick
	{

        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("MoveTowardsHighestPriorityMoveCandidateNode");
            return AccessTools.Method(type, "Tick");
        }

        [HarmonyPostfix]
        static void Postfix(ref BehaviorTreeResults __result, string ___name, BehaviorTree ___tree, AbstractActor ___unit, bool ___useSprintJuice)
        {
			if (__result == null || !(__result.orderInfo is AttackOrderInfo)) return; // Nothing to do
			
			Mod.Log.Info?.Write($"MoveTowardsHighestPriorityMoveCandidateNode generated an AttackOrder for unit: {___unit.DistinctId()}, without evaluating CBTBE options!");
			Mod.Log.Info?.Write($"  Disabling the attack but leaving the move oder intact.");

			// Recalculate the best movement type
			WorkspaceEvaluationEntry workspaceEvaluationEntry = ___tree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];
			MoveType bestMoveType = workspaceEvaluationEntry.GetBestMoveType();
			Vector3 vector = workspaceEvaluationEntry.Position;
			BehaviorVariableValue behaviorVariableValue = BehaviorHelper.GetCachedBehaviorVariableValue(___tree, BehaviorVariableName.String_StayInsideRegionGUID);
			if (behaviorVariableValue != null || behaviorVariableValue.StringVal.Length == 0)
			{
				vector = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(___unit, vector);
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
