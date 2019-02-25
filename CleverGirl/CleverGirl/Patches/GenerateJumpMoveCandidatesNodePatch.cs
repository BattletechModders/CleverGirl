using BattleTech;
using Harmony;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CleverGirl.Patches {

    [HarmonyPatch]
    public static class GenerateJumpMoveCandidatesNode_Tick_Patch {

        public static MethodBase TargetMethod() {
            var type = AccessTools.TypeByName("GenerateJumpMoveCandidatesNode");
            return AccessTools.Method(type, "Tick");
        }

        // Duplication of HBS code, avoiding prefix=true for now.
        public static void Postfix(ref BehaviorTreeResults __result, string ___name, BehaviorTree ___tree, AbstractActor ___unit) {
            CleverGirl.Logger.Log("CJMCN:T - entered");

            Mech mech = ___unit as Mech;
            if (mech != null && mech.WorkingJumpjets > 0) {
                string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(___unit);
                
                float acceptableHeat = AIUtil.GetAcceptableHeatLevelForMech(mech);
                float currentHeat = (float)mech.CurrentHeat;
                CleverGirl.Logger.Log($"CJMCN:T - === actor:{CombatantHelper.Label(mech)} has currentHeat:{currentHeat} and acceptableHeat:{acceptableHeat}");

                List<PathNode> sampledPathNodes = ___unit.JumpPathing.GetSampledPathNodes();
                CleverGirl.Logger.Log($"CJMCN:T - calculating {sampledPathNodes.Count} nodes");
                for (int i = 0; i < sampledPathNodes.Count; i++) {
                    Vector3 candidatePos = sampledPathNodes[i].Position;
                    float distanceBetween2D = AIUtil.Get2DDistanceBetweenVector3s(candidatePos, ___unit.CurrentPosition);
                    float distanceBetween3D = Vector3.Distance(candidatePos, ___unit.CurrentPosition);
                    CleverGirl.Logger.Log($"CJMCN:T - calculated distances 2D:'{distanceBetween2D}' 3D:'{distanceBetween3D} ");
                    if (distanceBetween2D >= 1f) {
                        float magnitude = (candidatePos - ___unit.CurrentPosition).magnitude;
                        float jumpHeat = (float)mech.CalcJumpHeat(magnitude);
                        CleverGirl.Logger.Log($"CJMCN:T - calculated jumpHeat:'{jumpHeat}' from magnitude:'{magnitude}. ");

                        CleverGirl.Logger.Log($"CJMCN:T - comparing heat: [jumpHeat:'{jumpHeat}' + currentHeat:'{currentHeat}'] <= acceptableHeat:'{acceptableHeat}. ");
                        if (jumpHeat + (float)mech.CurrentHeat <= acceptableHeat) {

                            if (stayInsideRegionGUID != null) {
                                MapTerrainDataCell cellAt = ___unit.Combat.MapMetaData.GetCellAt(candidatePos);
                                if (cellAt != null) {
                                    MapEncounterLayerDataCell mapEncounterLayerDataCell = cellAt.MapEncounterLayerDataCell;
                                    if (mapEncounterLayerDataCell != null 
                                        && mapEncounterLayerDataCell.regionGuidList != null 
                                        && !mapEncounterLayerDataCell.regionGuidList.Contains(stayInsideRegionGUID)) {

                                        // Skip this loop iteration if 
                                        CleverGirl.Logger.Log($"CJMCN:T - candidate outside of constraint region, ignoring.");
                                        goto CANDIDATE_OUTSIDE_REGION;
                                    }
                                }
                            }

                            CleverGirl.Logger.Log($"CJMCN:T - adding candidate position:{candidatePos}");
                            ___tree.movementCandidateLocations.Add(new MoveDestination(sampledPathNodes[i], MoveType.Jumping));
                        }
                    }

                CANDIDATE_OUTSIDE_REGION:;
                }
            }

            // Should already be set by prefix method
            //__result = BehaviorTreeResults(BehaviorNodeState.Success);

        }
    }
}
