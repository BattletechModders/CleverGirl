using BattleTech;
using Harmony;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using us.frostraptor.modUtils;
using static AttackEvaluator;

namespace CleverGirl.Patches {

    [HarmonyPatch(typeof(AttackEvaluator), "MakeAttackOrderForTarget")]
    public static class AttackEvaluator_MakeAttackOrderForTarget {

        public static bool Prefix(AbstractActor unit, ICombatant target, int enemyUnitIndex, bool isStationary, out BehaviorTreeResults order, ref float __result) {

            AIHelper.ResetBehaviorCache();

            __result = Original(unit, target, enemyUnitIndex, isStationary, out BehaviorTreeResults innerBTR);
            order = innerBTR;

            return false;
        }

        private static float Original(AbstractActor attackerAA, ICombatant target, int enemyUnitIndex, bool isStationary, out BehaviorTreeResults order) {
            Mod.Log.Debug($"Evaluating AttackOrder from ({CombatantUtils.Label(attackerAA)}) against ({CombatantUtils.Label(target)} at position: ({target.CurrentPosition})");

            // If the unit has no visibility to the target from the current position, they can't attack. Return immediately.
            if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(attackerAA, target)) {
                order = BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
                return 0f;
            }

            Mech attackerMech = attackerAA as Mech;
            float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
            float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech); ;
            Mod.Log.Debug($" heat: current: {currentHeat} acceptable: {acceptableHeat}");
            float attackerLegDamage = attackerMech == null ? 0f : AttackEvaluator.LegDamageLevel(attackerMech);

            float existingTargetDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            float maxAllowedLegDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            float existingTargetDamageForOverheat = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
            float weaponToHitThreshold = attackerAA.BehaviorTree.weaponToHitThreshold;

            float num4 = AttackEvaluator.MaxDamageLevel(attackerAA, target);

            // Filter weapons that cannot contribute to the battle
            CandidateWeapons candidateWeapons = new CandidateWeapons(attackerAA, target);

            Mech targetMech = target as Mech;
            bool targetIsEvasive = targetMech != null && targetMech.IsEvasive;
            List<List<CondensedWeapon>>[] array2 = new List<List<CondensedWeapon>>[3];

            float evasiveToHitFraction = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100f;

            // Evaluate ranged attacks
            if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                array2[0] = AttackEvaluatorHelper.MakeWeaponSetsForEvasive(candidateWeapons.RangedWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
            } else {
                array2[0] = AttackEvaluatorHelper.MakeWeaponSets(candidateWeapons.RangedWeapons);
            }

            // Evaluate melee attacks
            string cannotEngageInMeleeMsg = "";
            if (attackerMech == null || !attackerMech.CanEngageTarget(target, out cannotEngageInMeleeMsg)) {
                Mod.Log.Debug($" attacker cannot melee, or cannot engage due to: {cannotEngageInMeleeMsg}");
            } else {
                // Check Retaliation
                if (AttackEvaluatorHelper.MeleeDamageOutweighsRisk(attackerMech, target)) {

                    // Generate base list
                    List<List<CondensedWeapon>> meleeWeaponSets = null;
                    if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                        meleeWeaponSets = AttackEvaluatorHelper.MakeWeaponSetsForEvasive(candidateWeapons.MeleeWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                    } else {
                        meleeWeaponSets = AttackEvaluatorHelper.MakeWeaponSets(candidateWeapons.MeleeWeapons);
                    }

                    // Add melee weapons to each set
                    CondensedWeapon cMeleeWeapon = new CondensedWeapon(attackerMech.MeleeWeapon);
                    for (int i = 0; i < meleeWeaponSets.Count; i++) {
                        meleeWeaponSets[i].Add(cMeleeWeapon);
                    }

                    array2[1] = meleeWeaponSets;
                } else {
                    Mod.Log.Debug($" potential melee damage too high, skipping melee.");
                }
            }

            // Evaluate DFA attacks
            if (attackerMech == null || !!AIUtil.IsDFAAcceptable(attackerMech, target)) {
                Mod.Log.Debug("this unit cannot dfa");

                // TODO: Check Retaliation

                List<List<CondensedWeapon>> dfaWeaponSets = null;
                if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                    dfaWeaponSets = AttackEvaluatorHelper.MakeWeaponSetsForEvasive(candidateWeapons.DFAWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                } else {
                    dfaWeaponSets = AttackEvaluatorHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);
                }

                // Add DFA weapons to each set
                CondensedWeapon cDFAWeapon = new CondensedWeapon(attackerMech.DFAWeapon);
                for (int i = 0; i < dfaWeaponSets.Count; i++) {
                    dfaWeaponSets[i].Add(cDFAWeapon);
                }

                array2[2] = dfaWeaponSets;
            }

            List<AttackEvaluation> list = AttackEvaluatorHelper.EvaluateAttacks(attackerAA, target, array2, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug(string.Format("found {0} different attack solutions", list.Count));
            float bestRangedEDam = 0f;
            float bestMeleeEDam = 0f;
            float bestDFAEDam = 0f;
            for (int m = 0; m < list.Count; m++) {
                AttackEvaluation attackEvaluation = list[m];
                Mod.Log.Debug(string.Format("evaluated attack of type {0} with {1} weapons and a result of {2}", attackEvaluation.AttackType, attackEvaluation.WeaponList.Count, attackEvaluation.ExpectedDamage));
                switch (attackEvaluation.AttackType) {
                    case AIUtil.AttackType.Shooting:
                        bestRangedEDam = Mathf.Max(bestRangedEDam, attackEvaluation.ExpectedDamage);
                        break;
                    case AIUtil.AttackType.Melee:
                        bestMeleeEDam = Mathf.Max(bestMeleeEDam, attackEvaluation.ExpectedDamage);
                        break;
                    case AIUtil.AttackType.DeathFromAbove:
                        bestDFAEDam = Mathf.Max(bestDFAEDam, attackEvaluation.ExpectedDamage);
                        break;
                    default:
                        Debug.Log("unknown attack type: " + attackEvaluation.AttackType);
                        break;
                }
            }
            Mod.Log.Debug("best shooting: " + bestRangedEDam);
            Mod.Log.Debug("best melee: " + bestMeleeEDam);
            Mod.Log.Debug("best dfa: " + bestDFAEDam);

            for (int n = 0; n < list.Count; n++) {
                AttackEvaluator.AttackEvaluation attackEvaluation2 = list[n];
                Mod.Log.Debug("evaluating attack solution #" + n);
                Mod.Log.Debug("------");
                Mod.Log.Debug("Weapons:");
                foreach (Weapon weapon3 in attackEvaluation2.WeaponList) {
                    Mod.Log.Debug("Weapon: " + weapon3.Name);
                }
                Mod.Log.Debug("heat generated for attack solution: " + attackEvaluation2.HeatGenerated);
                Mod.Log.Debug("current heat: " + currentHeat);
                Mod.Log.Debug("acceptable heat: " + acceptableHeat);
                bool flag5 = attackEvaluation2.HeatGenerated + currentHeat > acceptableHeat;
                Mod.Log.Debug("will overheat? " + flag5);
                if (flag5 && attackerMech.OverheatWillCauseDeath()) {
                    Mod.Log.Debug("rejecting attack because overheat would cause own death");
                } else {
                    bool flag6 = num4 >= existingTargetDamageForOverheat;
                    Mod.Log.Debug("but enough damage for overheat attack? " + flag6);
                    bool flag7 = attackEvaluation2.lowestHitChance >= weaponToHitThreshold;
                    Mod.Log.Debug("but enough accuracy for overheat attack? " + flag7);
                    AbstractActor abstractActor = target as AbstractActor;
                    if (attackEvaluation2.AttackType == AIUtil.AttackType.Melee && (!attackerAA.CanEngageTarget(target) || abstractActor == null || !isStationary)) {
                        Mod.Log.Debug("Can't Melee");
                    } else if (attackEvaluation2.AttackType == AIUtil.AttackType.DeathFromAbove && (!attackerAA.CanDFATargetFromPosition(target, attackerAA.CurrentPosition) || num4 < existingTargetDamageForDFA || attackerLegDamage > maxAllowedLegDamageForDFA)) {
                        Mod.Log.Debug("Can't DFA");
                    } else if (flag5 && (!flag6 || !flag7)) {
                        Mod.Log.Debug("rejecting attack for not enough damage or accuracy on an attack that will overheat");
                    } else if (attackEvaluation2.WeaponList.Count == 0) {
                        Mod.Log.Debug("rejecting attack for not having any weapons");
                    } else {
                        if (attackEvaluation2.ExpectedDamage > 0f) {
                            BehaviorTreeResults behaviorTreeResults = new BehaviorTreeResults(BehaviorNodeState.Success);
                            CalledShotAttackOrderInfo calledShotAttackOrderInfo = AttackEvaluatorHelper.MakeOffensivePushOrder(attackerAA, attackEvaluation2, enemyUnitIndex);
                            CalledShotAttackOrderInfo orderInfo;
                            MultiTargetAttackOrderInfo orderInfo2;
                            if (calledShotAttackOrderInfo != null) {
                                behaviorTreeResults.orderInfo = calledShotAttackOrderInfo;
                                behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using offensive push";
                            } else if ((orderInfo = AttackEvaluatorHelper.MakeCalledShotOrder(attackerAA, attackEvaluation2, enemyUnitIndex, false)) != null) {
                                behaviorTreeResults.orderInfo = orderInfo;
                                behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using called shot";
                            } else if (!flag5 && (orderInfo2 = MultiAttack.MakeMultiAttackOrder(attackerAA, attackEvaluation2, enemyUnitIndex)) != null) {
                                behaviorTreeResults.orderInfo = orderInfo2;
                                behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using multi attack";
                            } else {
                                AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target);
                                attackOrderInfo.Weapons = attackEvaluation2.WeaponList;
                                attackOrderInfo.TargetUnit = target;
                                attackOrderInfo.VentFirst = (flag5 && attackerAA.HasVentCoolantAbility && attackerAA.CanVentCoolant);
                                AIUtil.AttackType attackType = attackEvaluation2.AttackType;
                                if (attackType != AIUtil.AttackType.Melee) {
                                    if (attackType == AIUtil.AttackType.DeathFromAbove) {
                                        attackOrderInfo.IsDeathFromAbove = true;
                                        attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                                        attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                                        List<PathNode> dfadestsForTarget = attackerMech.JumpPathing.GetDFADestsForTarget(abstractActor);
                                        if (dfadestsForTarget.Count == 0) {
                                            Mod.Log.Debug("Failing for lack of DFA destinations");
                                            goto IL_B74;
                                        }
                                        attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(abstractActor, dfadestsForTarget);
                                    }
                                } else {
                                    attackOrderInfo.IsMelee = true;
                                    attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                                    attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                                    List<PathNode> meleeDestsForTarget = attackerMech.Pathing.GetMeleeDestsForTarget(abstractActor);
                                    if (meleeDestsForTarget.Count == 0) {
                                        Mod.Log.Debug("Failing for lack of melee destinations");
                                        goto IL_B74;
                                    }
                                    attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(abstractActor, meleeDestsForTarget);
                                }
                                behaviorTreeResults.orderInfo = attackOrderInfo;
                                behaviorTreeResults.debugOrderString = string.Concat(new object[]
                                {
                                attackerAA.DisplayName,
                                " using attack type: ",
                                attackEvaluation2.AttackType,
                                " against: ",
                                target.DisplayName
                                });
                            }
                            Mod.Log.Debug("attack order: " + behaviorTreeResults.debugOrderString);
                            order = behaviorTreeResults;
                            return attackEvaluation2.ExpectedDamage;
                        }
                        Mod.Log.Debug("rejecting attack for not having any expected damage");
                    }
                }
            IL_B74:;
            }
            Mod.Log.Debug("There are no targets I can shoot at without overheating.");
            order = null;
            return 0f;
        }
    }

}
