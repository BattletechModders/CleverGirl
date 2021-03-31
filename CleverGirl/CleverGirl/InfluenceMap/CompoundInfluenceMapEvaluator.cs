using BattleTech;
using CleverGirl.Helper;
using GraphCoroutines;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CleverGirl.InfluenceMap
{
    public class CompoundInfluenceMapEvaluator 
    {
        readonly InfluenceMapEvaluator hbsIME;

        public CompoundInfluenceMapEvaluator(InfluenceMapEvaluator hbsEvaluator) 
		{
            this.hbsIME = hbsEvaluator;
		}

		public IEnumerable<Instruction> IncrementalEvaluate()
		{
			yield return ControlFlow.Call(Eval_Initialize());

			yield return ControlFlow.Call(Eval_PositionalFactors());
			yield return ControlFlow.Call(Eval_HostileFactors());
			yield return ControlFlow.Call(Eval_AllyFactors());
			yield return ControlFlow.Call(Apply_SprintScaling());

			hbsIME.expectedDamageFactor.LogEvaluation();

			Traverse evaluationCompleteT = Traverse.Create(hbsIME).Field("evaluationComplete");
			evaluationCompleteT.SetValue(true);			

			yield return null;
		}

		private IEnumerable<Instruction> Eval_Initialize()
		{
			hbsIME.ResetWorkspace();

			Traverse unitT = Traverse.Create(hbsIME).Field("unit");
			AbstractActor unit = unitT.GetValue<AbstractActor>();

			for (int i = 0; i < unit.BehaviorTree.movementCandidateLocations.Count; i++)
			{
				if (!hbsIME.IsMovementCandidateLocationReachable(unit, unit.BehaviorTree.movementCandidateLocations[i]))
				{
					continue;
				}

				MoveDestination moveDestination = unit.BehaviorTree.movementCandidateLocations[i];
				PathNode pathNode = moveDestination.PathNode;
				float num = PathingUtil.FloatAngleFrom8Angle(pathNode.Angle);
				float num2 = 0f;
				AbstractActor abstractActor = null;
				MeleeMoveDestination meleeMoveDestination = moveDestination as MeleeMoveDestination;
				if (meleeMoveDestination != null)
				{
					abstractActor = meleeMoveDestination.Target;
				}

				switch (unit.BehaviorTree.movementCandidateLocations[i].MoveType)
				{
					case MoveType.Walking:
						num2 = unit.MaxWalkDistance;
						break;
					case MoveType.Sprinting:
						num2 = unit.MaxSprintDistance;
						break;
					case MoveType.Backward:
						num2 = unit.MaxBackwardDistance;
						break;
					case MoveType.Jumping:
						{
							Mech mech2 = unit as Mech;
							if (mech2 != null)
							{
								num2 = mech2.JumpDistance;
							}
							break;
						}
					case MoveType.Melee:
						{
							Mech mech = unit as Mech;
							if (mech != null)
							{
								num2 = mech.MaxMeleeEngageRangeDistance;
							}
							break;
						}
				}

				float num3 = Mathf.Min(unit.Pathing.GetAngleAvailable(num2 - pathNode.CostToThisNode), 180f);
				float floatVal = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_AngularSelectionResolution).FloatVal;
				if (abstractActor == null)
				{
					for (float num4 = 0f; num4 < num3; num4 += floatVal)
					{
						hbsIME.WorkspacePushPathNodeAngle(pathNode, num + num4, unit.BehaviorTree.movementCandidateLocations[i].MoveType, null);
						if (num4 > 0f)
						{
							hbsIME.WorkspacePushPathNodeAngle(pathNode, num - num4, unit.BehaviorTree.movementCandidateLocations[i].MoveType, null);
						}
					}
				}
				else
				{
					hbsIME.WorkspacePushPathNodeAngle(pathNode, num, unit.BehaviorTree.movementCandidateLocations[i].MoveType, abstractActor);
				}
			}
			yield return null;
		}
	}
}
