using System.Reflection;
using UnityEngine;

namespace CleverGirl.Patches {

    [HarmonyPatch]
    static class GenerateJumpMoveCandidatesNode_Tick {

        static MethodBase TargetMethod() {
            var type = AccessTools.TypeByName("GenerateJumpMoveCandidatesNode");
            return AccessTools.Method(type, "Tick");
        }

        // Duplication of HBS code, avoiding prefix=true for now.
        [HarmonyPostfix]
        static void Postfix(ref BehaviorTreeResults __result, string ___name, BehaviorTree ___tree, AbstractActor ___unit) {
            Mod.Log.Trace?.Write("CJMCN:T - entered");

            Mech mech = ___unit as Mech;
            if (mech != null && mech.WorkingJumpjets > 0) {
                string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(___unit);
                
                float acceptableHeat = AIUtil.GetAcceptableHeatLevelForMech(mech);
                float currentHeat = (float)mech.CurrentHeat;
                Mod.Log.Info?.Write($"CJMCN:T - === actor:{mech.DistinctId()} has currentHeat:{currentHeat} and acceptableHeat:{acceptableHeat}");

                List<PathNode> sampledPathNodes = ___unit.JumpPathing.GetSampledPathNodes();
                Mod.Log.Info?.Write($"CJMCN:T - calculating {sampledPathNodes.Count} nodes");
                for (int i = 0; i < sampledPathNodes.Count; i++) {
                    Vector3 candidatePos = sampledPathNodes[i].Position;
                    float distanceBetween2D = AIUtil.Get2DDistanceBetweenVector3s(candidatePos, ___unit.CurrentPosition);
                    float distanceBetween3D = Vector3.Distance(candidatePos, ___unit.CurrentPosition);
                    Mod.Log.Info?.Write($"CJMCN:T - calculated distances 2D:'{distanceBetween2D}' 3D:'{distanceBetween3D} ");
                    if (distanceBetween2D >= 1f) {
                        float magnitude = (candidatePos - ___unit.CurrentPosition).magnitude;
                        float jumpHeat = (float)mech.CalcJumpHeat(magnitude);
                        Mod.Log.Info?.Write($"CJMCN:T - calculated jumpHeat:'{jumpHeat}' from magnitude:'{magnitude}. ");

                        Mod.Log.Info?.Write($"CJMCN:T - comparing heat: [jumpHeat:'{jumpHeat}' + currentHeat:'{currentHeat}'] <= acceptableHeat:'{acceptableHeat}. ");
                        if (jumpHeat + (float)mech.CurrentHeat <= acceptableHeat) {

                            if (stayInsideRegionGUID != null) {
                                MapTerrainDataCell cellAt = ___unit.Combat.MapMetaData.GetCellAt(candidatePos);
                                if (cellAt != null) {
                                    MapEncounterLayerDataCell mapEncounterLayerDataCell = cellAt.MapEncounterLayerDataCell;
                                    if (mapEncounterLayerDataCell != null 
                                        && mapEncounterLayerDataCell.regionGuidList != null 
                                        && !mapEncounterLayerDataCell.regionGuidList.Contains(stayInsideRegionGUID)) {

                                        // Skip this loop iteration if 
                                        Mod.Log.Info?.Write($"CJMCN:T - candidate outside of constraint region, ignoring.");
                                        goto CANDIDATE_OUTSIDE_REGION;
                                    }
                                }
                            }

                            Mod.Log.Info?.Write($"CJMCN:T - adding candidate position:{candidatePos}");
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
