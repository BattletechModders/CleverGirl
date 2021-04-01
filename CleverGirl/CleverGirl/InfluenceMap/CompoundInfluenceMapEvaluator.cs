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
		AbstractActor unit;

        public CompoundInfluenceMapEvaluator(InfluenceMapEvaluator hbsEvaluator) 
		{
            this.hbsIME = hbsEvaluator;
		}

		public IEnumerable<Instruction> IncrementalEvaluate()
		{
			yield return ControlFlow.Call(Eval_Initialize());

			// Prep the unit
			Traverse unitT = Traverse.Create(hbsIME).Field("unit");
			this.unit = unitT.GetValue<AbstractActor>();

			yield return ControlFlow.Call(Eval_PositionalFactors());
			yield return ControlFlow.Call(Eval_HostileFactors());
			yield return ControlFlow.Call(Eval_AllyFactors());
			yield return ControlFlow.Call(Apply_SprintScaling());

			hbsIME.expectedDamageFactor.LogEvaluation();

			Traverse evaluationCompleteT = Traverse.Create(hbsIME).Field("evaluationComplete");
			evaluationCompleteT.SetValue(true);			

			yield return null;
		}

		// Prep the influence map to be evaluated
		private IEnumerable<Instruction> Eval_Initialize()
		{
			hbsIME.ResetWorkspace();

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

		// Evaluate position data
		private IEnumerable<Instruction> Eval_PositionalFactors()
		{
			int workspaceIndex = 0;
			while (workspaceIndex < hbsIME.firstFreeWorkspaceEvaluationEntryIndex)
			{
				hbsIME.WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator = 0f;
				hbsIME.WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator = 0f;
				int num = workspaceIndex + 1;
				workspaceIndex = num;
			}

			Traverse positionalFactorsT = Traverse.Create(hbsIME).Field("positionalFactors");
			InfluenceMapPositionFactor[] positionalFactors = positionalFactorsT.GetValue<InfluenceMapPositionFactor[]>();

			int posFactorIndex = 0;
			while (posFactorIndex < positionalFactors.Length)
			{
				InfluenceMapPositionFactor factor = positionalFactors[posFactorIndex];
				float regularWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, factor.GetRegularMoveWeightBVName()).FloatVal;
				float sprintWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, factor.GetSprintMoveWeightBVName()).FloatVal;
				int num;
				if (regularWeight != 0f && sprintWeight != 0f)
				{
					float minValue = float.MaxValue;
					float maxValue = float.MinValue;
					factor.InitEvaluationForPhaseForUnit(unit);
					for (workspaceIndex = 0; workspaceIndex < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; workspaceIndex = num)
					{
						WorkspaceEvaluationEntry workspaceEvaluationEntry = hbsIME.WorkspaceEvaluationEntries[workspaceIndex];
						MoveType moveType = ((!workspaceEvaluationEntry.HasSprintMove) ? MoveType.Walking : MoveType.Sprinting);
						PathNode pathNode = null;
						if (workspaceEvaluationEntry.PathNodes.ContainsKey(moveType))
						{
							pathNode = workspaceEvaluationEntry.PathNodes[moveType];
						}
						else if (moveType == MoveType.Walking && workspaceEvaluationEntry.PathNodes.ContainsKey(MoveType.Backward))
						{
							pathNode = workspaceEvaluationEntry.PathNodes[MoveType.Backward];
						}
						else
						{
							using (Dictionary<MoveType, PathNode>.KeyCollection.Enumerator enumerator = workspaceEvaluationEntry.PathNodes.Keys.GetEnumerator())
							{
								if (enumerator.MoveNext())
								{
									MoveType current = enumerator.Current;
									pathNode = workspaceEvaluationEntry.PathNodes[current];
								}
							}
						}

						float a = (workspaceEvaluationEntry.FactorValue = factor.EvaluateInfluenceMapFactorAtPosition(unit, workspaceEvaluationEntry.Position, workspaceEvaluationEntry.Angle, moveType, pathNode));
						minValue = Mathf.Min(a, minValue);
						maxValue = Mathf.Max(a, maxValue);
						if (workspaceIndex % 16 == 0)
						{
							yield return null;
						}
						num = workspaceIndex + 1;
					}

					if (minValue >= maxValue)
					{
						yield return null;
					}
					else
					{
						for (workspaceIndex = 0; workspaceIndex < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; workspaceIndex = num)
						{
							WorkspaceEvaluationEntry workspaceEvaluationEntry2 = hbsIME.WorkspaceEvaluationEntries[workspaceIndex];
							float factorValue = workspaceEvaluationEntry2.FactorValue;
							float num2 = (factorValue - minValue) / (maxValue - minValue);
							float num3 = num2 * regularWeight;
							float num4 = num2 * sprintWeight;
							workspaceEvaluationEntry2.ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = new EvaluationDebugLogRecord(factorValue, num2, num3, regularWeight, num4, sprintWeight);
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator += num3;
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator += num4;
							num = workspaceIndex + 1;
						}
						yield return null;
					}
				}

				num = posFactorIndex + 1;
				posFactorIndex = num;
			}
		}

		// Factors based upon opposing units
		private IEnumerable<Instruction> Eval_HostileFactors()
		{
			for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; i++)
			{
				(unit.BehaviorTree.enemyUnits[i] as AbstractActor)?.EvaluateExpectedArmor();
			}
			unit.EvaluateExpectedArmor();
			yield return null;
			int intVal = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Int_HostileInfluenceCount).IntVal;
			List<ICombatant> hostiles = getNClosestCombatants(unit.BehaviorTree.enemyUnits, intVal);

			Traverse hostileFactorsT = Traverse.Create(hbsIME).Field("hostileFactors");
			InfluenceMapHostileFactor[] hostileFactors = hostileFactorsT.GetValue<InfluenceMapHostileFactor[]>();
			AIUtil.LogAI($"evaluating vs {hostiles.Count} hostiles");
			int hostileFactorIndex = 0;
			while (hostileFactorIndex < hostileFactors.Length)
			{
				InfluenceMapHostileFactor factor = hostileFactors[hostileFactorIndex];
				Debug.Log("evaluating " + factor.Name);
				bool specialLogging = false;
				if (factor.Name == "prefer lower damage from hostiles")
				{
					specialLogging = true;
				}
				BehaviorVariableName regularMoveWeightBVName = factor.GetRegularMoveWeightBVName();
				BehaviorVariableName sprintMoveWeightBVName = factor.GetSprintMoveWeightBVName();
				float regularMoveWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, regularMoveWeightBVName).FloatVal;
				float sprintMoveWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, sprintMoveWeightBVName).FloatVal;
				int num2;
				if (regularMoveWeight != 0f && sprintMoveWeight != 0f)
				{
					float minValue = float.MaxValue;
					float maxValue = float.MinValue;
					factor.InitEvaluationForPhaseForUnit(unit);
					for (int workspaceIndex2 = 0; workspaceIndex2 < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; workspaceIndex2 = num2)
					{
						WorkspaceEvaluationEntry workspaceEvaluationEntry = hbsIME.WorkspaceEvaluationEntries[workspaceIndex2];
						workspaceEvaluationEntry.FactorValue = 0f;
						for (int j = 0; j < hostiles.Count; j++)
						{
							ICombatant hostileUnit = hostiles[j];
							MoveType moveType = ((!workspaceEvaluationEntry.HasSprintMove) ? MoveType.Walking : MoveType.Sprinting);
							float num = factor.EvaluateInfluenceMapFactorAtPositionWithHostile(unit, workspaceEvaluationEntry.Position, workspaceEvaluationEntry.Angle, moveType, hostileUnit);
							workspaceEvaluationEntry.FactorValue += num;
							minValue = Mathf.Min(minValue, workspaceEvaluationEntry.FactorValue);
							maxValue = Mathf.Max(maxValue, workspaceEvaluationEntry.FactorValue);
						}
						if (workspaceIndex2 % 16 == 0)
						{
							yield return null;
						}
						num2 = workspaceIndex2 + 1;
					}
					if (specialLogging)
					{
						Debug.Log("minVal: " + minValue);
						Debug.Log("maxVal: " + maxValue);
					}
					if (minValue >= maxValue)
					{
						yield return null;
					}
					else
					{
						for (int workspaceIndex2 = 0; workspaceIndex2 < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; workspaceIndex2 = num2)
						{
							float factorValue = hbsIME.WorkspaceEvaluationEntries[workspaceIndex2].FactorValue;
							float num3 = (factorValue - minValue) / (maxValue - minValue);
							float num4 = num3 * regularMoveWeight;
							float num5 = num3 * sprintMoveWeight;
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex2].RegularMoveAccumulator += num4;
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex2].SprintMoveAccumulator += num5;
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex2].ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = 
								new EvaluationDebugLogRecord(factorValue, num3, num4, regularMoveWeight, num5, sprintMoveWeight);
							num2 = workspaceIndex2 + 1;
						}
						yield return null;
					}
				}
				num2 = hostileFactorIndex + 1;
				hostileFactorIndex = num2;
			}
		}

		// Factors based upon our allies
		private IEnumerable<Instruction> Eval_AllyFactors()
		{
			int intVal = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Int_AllyInfluenceCount).IntVal;
			List<ICombatant> allies = getNClosestCombatants(unit.BehaviorTree.GetAllyUnits().ConvertAll((Converter<AbstractActor, ICombatant>)((AbstractActor X) => X)), intVal);

			Traverse allyFactorsT = Traverse.Create(hbsIME).Field("allyFactors");
			InfluenceMapAllyFactor[] allyFactors = allyFactorsT.GetValue<InfluenceMapAllyFactor[]>();
			AIUtil.LogAI($"evaluating vs {allies.Count} allies");
			int allyFactorIndex = 0;
			while (allyFactorIndex < allyFactors.Length)
			{
				InfluenceMapAllyFactor factor = allyFactors[allyFactorIndex];
				AIUtil.LogAI("evaluating " + factor.Name);
				BehaviorVariableName regularMoveWeightBVName = factor.GetRegularMoveWeightBVName();
				BehaviorVariableName sprintMoveWeightBVName = factor.GetSprintMoveWeightBVName();
				float regularMoveWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, regularMoveWeightBVName).FloatVal;
				float sprintMoveWeight = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, sprintMoveWeightBVName).FloatVal;
				int num2;
				if (regularMoveWeight != 0f || sprintMoveWeight != 0f)
				{
					float minValue = float.MaxValue;
					float maxValue = float.MinValue;
					factor.InitEvaluationForPhaseForUnit(unit);
					for (int workspaceIndex = 0; workspaceIndex < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; workspaceIndex = num2)
					{
						WorkspaceEvaluationEntry workspaceEvaluationEntry = hbsIME.WorkspaceEvaluationEntries[workspaceIndex];
						hbsIME.WorkspaceEvaluationEntries[workspaceIndex].FactorValue = 0f;
						for (int i = 0; i < allies.Count; i++)
						{
							ICombatant allyUnit = allies[i];
							float num = factor.EvaluateInfluenceMapFactorAtPositionWithAlly(unit, workspaceEvaluationEntry.Position, workspaceEvaluationEntry.Angle, allyUnit);
							hbsIME.WorkspaceEvaluationEntries[workspaceIndex].FactorValue += num;
							minValue = Mathf.Min(minValue, hbsIME.WorkspaceEvaluationEntries[workspaceIndex].FactorValue);
							maxValue = Mathf.Max(maxValue, hbsIME.WorkspaceEvaluationEntries[workspaceIndex].FactorValue);
						}
						if (workspaceIndex % 16 == 0)
						{
							yield return null;
						}
						num2 = workspaceIndex + 1;
					}
					if (minValue >= maxValue)
					{
						yield return null;
					}
					else
					{
						for (int j = 0; j < hbsIME.firstFreeWorkspaceEvaluationEntryIndex; j++)
						{
							float factorValue = hbsIME.WorkspaceEvaluationEntries[j].FactorValue;
							float num3 = (factorValue - minValue) / (maxValue - minValue);
							float num4 = num3 * regularMoveWeight;
							float num5 = num3 * sprintMoveWeight;
							hbsIME.WorkspaceEvaluationEntries[j].RegularMoveAccumulator += num4;
							hbsIME.WorkspaceEvaluationEntries[j].SprintMoveAccumulator += num5;
							hbsIME.WorkspaceEvaluationEntries[j].ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = new EvaluationDebugLogRecord(factorValue, num3, num4, regularMoveWeight, num5, sprintMoveWeight);
						}
						yield return null;
					}
				}
				num2 = allyFactorIndex + 1;
				allyFactorIndex = num2;
			}
		}

		private IEnumerable<Instruction> Apply_SprintScaling()
		{
			float sprintBiasMult = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_SprintWeightBiasMultiplicative).FloatVal;
			float sprintBiasAdd = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_SprintWeightBiasAdditive).FloatVal;
			float sprintHysteresisLevel = unit.BehaviorTree.GetSprintHysteresisLevel();
			if (unit.BehaviorTree.HasPriorityTargets() && unit.BehaviorTree.IsOutsideProximityTargetDistance())
			{
				sprintHysteresisLevel = 1f;
			}
			int workspaceIndex = 0;
			while (workspaceIndex < hbsIME.firstFreeWorkspaceEvaluationEntryIndex)
			{
				WorkspaceEvaluationEntry workspaceEvaluationEntry = hbsIME.WorkspaceEvaluationEntries[workspaceIndex];
				workspaceEvaluationEntry.ScaledSprintMoveAccumulator = (workspaceEvaluationEntry.SprintMoveAccumulator * sprintBiasMult + sprintBiasAdd) * sprintHysteresisLevel;
				if (workspaceIndex % 16 == 0)
				{
					yield return null;
				}
				int num = workspaceIndex + 1;
				workspaceIndex = num;
			}
		}

		// Utility methods
		private class CombatantDistanceComparer : IComparer<ICombatant>
		{
			public AbstractActor actingUnit;

			public CombatantDistanceComparer(AbstractActor unit)
			{
				actingUnit = unit;
			}

			public int Compare(ICombatant x, ICombatant y)
			{
				float sqrMagnitude = (x.CurrentPosition - actingUnit.CurrentPosition).sqrMagnitude;
				float sqrMagnitude2 = (y.CurrentPosition - actingUnit.CurrentPosition).sqrMagnitude;
				return sqrMagnitude.CompareTo(sqrMagnitude2);
			}
		}

		private List<ICombatant> getNClosestCombatants(List<ICombatant> combatants, int count)
		{
			if (combatants.Count > count)
			{
				CombatantDistanceComparer comparer = new CombatantDistanceComparer(unit);
				combatants.Sort(comparer);
				combatants.RemoveRange(count, combatants.Count - count);
			}
			return combatants;
		}
	}
}
