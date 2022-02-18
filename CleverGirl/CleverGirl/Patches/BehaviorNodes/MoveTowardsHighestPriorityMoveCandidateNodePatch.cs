using BattleTech;
using CleverGirl.Helper;
using Harmony;
using IRBTModUtils.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

  //      static bool Prefix(ref BehaviorTreeResults __result, string ___name, BehaviorTree ___tree, AbstractActor ___unit, bool ___useSprintJuice)
  //      {

		//	if (___tree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0)
		//	{
		//		Mod.Log.Debug?.Write("No evaluated move candidates");
		//		__result=  new BehaviorTreeResults(BehaviorNodeState.Failure);
		//		return false;
		//	}

		//	WorkspaceEvaluationEntry workspaceEvaluationEntry = ___tree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];
		//	MoveType bestMoveType = workspaceEvaluationEntry.GetBestMoveType();
		//	Vector3 vector = workspaceEvaluationEntry.Position;
		//	BehaviorVariableValue behaviorVariableValue = BehaviorHelper.GetCachedBehaviorVariableValue (___tree, BehaviorVariableName.String_StayInsideRegionGUID);
		//	if (behaviorVariableValue != null || behaviorVariableValue.StringVal.Length == 0)
		//	{
		//		vector = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(___unit, vector);
		//	}
		//	float angle = workspaceEvaluationEntry.Angle;
		//	Vector3 vector2 = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
		//	BehaviorTreeResults behaviorTreeResults = new BehaviorTreeResults(BehaviorNodeState.Success);
		//	Vector3 lookAt = vector + vector2;
			
		//	OrderInfo orderInfo;
		//	if (bestMoveType == MoveType.None)
		//	{
		//		orderInfo = new OrderInfo(OrderType.Brace);
		//	}
		//	else
		//	{
		//		OrderInfo orderInfo2 = (orderInfo = new MovementOrderInfo(vector, lookAt));
		//		((MovementOrderInfo)orderInfo2).IsReverse = bestMoveType == MoveType.Backward;
		//		((MovementOrderInfo)orderInfo2).IsJumping = bestMoveType == MoveType.Jumping;
		//		((MovementOrderInfo)orderInfo2).IsSprinting = bestMoveType == MoveType.Sprinting;
		//		((MovementOrderInfo)orderInfo2).IsMelee = bestMoveType == MoveType.Melee;

		//		if (bestMoveType == MoveType.Melee) 
		//		{
		//			Mech mech = ___unit as Mech;
		//			AttackOrderInfo attackOrderInfo = new AttackOrderInfo(workspaceEvaluationEntry.Target, isMelee: true, isDeathFromAbove: false);
		//			orderInfo = attackOrderInfo;
		//			attackOrderInfo.AddWeapon(mech.MeleeWeapon);
		//			for (int i = 0; i < mech.Weapons.Count; i++)
		//			{
		//				Weapon weapon = mech.Weapons[i];
		//				if (weapon.CanFire && weapon.WeaponCategoryValue.CanUseInMelee)
		//				{
		//					attackOrderInfo.AddWeapon(weapon);
		//				}
		//			}
		//			attackOrderInfo.AttackFromLocation = workspaceEvaluationEntry.Position;
		//		}
		//	}
		//	behaviorTreeResults.orderInfo = orderInfo;
		//	string text = "undefined movement";
		//	bool flag = false;
		//	switch (bestMoveType)
		//	{
		//		case MoveType.None:
		//			text = "bracing";
		//			flag = true;
		//			break;
		//		case MoveType.Walking:
		//			text = "walking";
		//			flag = true;
		//			break;
		//		case MoveType.Backward:
		//			text = "reversing";
		//			flag = true;
		//			break;
		//		case MoveType.Sprinting:
		//			text = "sprinting";
		//			flag = false;
		//			break;
		//		case MoveType.Jumping:
		//			text = "jumping";
		//			flag = true;
		//			break;
		//		case MoveType.Melee:
		//			text = "engaging";
		//			flag = true;
		//			break;
		//		default:
		//			text = "moving in an unknown way";
		//			break;
		//	}

		//	if (___tree.HasProximityTaggedTargets() && ___tree.IsOutsideProximityTargetDistance())
		//	{
		//		___useSprintJuice = false;
		//	}

		//	if (___useSprintJuice)
		//	{
		//		if (flag)
		//		{
		//			___unit.BehaviorTree.IncreaseSprintHysteresisLevel();
		//		}
		//		else
		//		{
		//			___unit.BehaviorTree.DecreaseSprintHysteresisLevel();
		//		}
		//	}

		//	float magnitude = (workspaceEvaluationEntry.Position - ___unit.CurrentPosition).magnitude;
			
		//	behaviorTreeResults.debugOrderString = $"{___name} {text} toward dest: {workspaceEvaluationEntry.Position} from {___unit.CurrentPosition} dist {magnitude}";
		//	LogAI("movement order: " + behaviorTreeResults.debugOrderString);
		//	LogAI("move verb " + text);
		//	LogAI("distance: " + (___unit.CurrentPosition - workspaceEvaluationEntry.Position).magnitude);

		//	__result = behaviorTreeResults;
		//	return false;
		//}
    }
}
