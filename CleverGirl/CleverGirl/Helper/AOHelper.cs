﻿using BattleTech;
using System.Collections.Generic;
using UnityEngine;
using us.frostraptor.modUtils;
using static AttackEvaluator;

namespace CleverGirl.Helper {

    public static class AOHelper {

        public static float MakeAttackOrderForTarget(AbstractActor attackerAA, ICombatant target, bool isStationary, out BehaviorTreeResults order) {
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

            float weaponToHitThreshold = attackerAA.BehaviorTree.weaponToHitThreshold;


            // Filter weapons that cannot contribute to the battle
            CandidateWeapons candidateWeapons = new CandidateWeapons(attackerAA, target);

            Mech targetMech = target as Mech;
            bool targetIsEvasive = targetMech != null && targetMech.IsEvasive;
            List<List<CondensedWeapon>>[] weaponSetsByAttackType = {
                new List<List<CondensedWeapon>>() { },
                new List<List<CondensedWeapon>>() { },
                new List<List<CondensedWeapon>>() { }
            };

            float evasiveToHitFraction = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100f;

            // Evaluate ranged attacks 
            if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                Mod.Log.Debug($"Checking evasive shots against target, needs {evasiveToHitFraction} or higher to be included.");
                weaponSetsByAttackType[0] = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.RangedWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
            } else {
                Mod.Log.Debug($"Checking non-evasive target.");
                weaponSetsByAttackType[0] = AEHelper.MakeWeaponSets(candidateWeapons.RangedWeapons);
            }

            // Evaluate melee attacks
            string cannotEngageInMeleeMsg = "";
            if (attackerMech == null || !attackerMech.CanEngageTarget(target, out cannotEngageInMeleeMsg)) {
                Mod.Log.Debug($" attacker cannot melee, or cannot engage due to: '{cannotEngageInMeleeMsg}'");
            } else {
                // Check Retaliation
                if (AEHelper.MeleeDamageOutweighsRisk(attackerMech, target)) {

                    // Generate base list
                    List<List<CondensedWeapon>> meleeWeaponSets = null;
                    if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                        meleeWeaponSets = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.MeleeWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                    } else {
                        meleeWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.MeleeWeapons);
                    }

                    // Add melee weapons to each set
                    CondensedWeapon cMeleeWeapon = new CondensedWeapon(attackerMech.MeleeWeapon);
                    for (int i = 0; i < meleeWeaponSets.Count; i++) {
                        meleeWeaponSets[i].Add(cMeleeWeapon);
                    }

                    weaponSetsByAttackType[1] = meleeWeaponSets;
                } else {
                    Mod.Log.Debug($" potential melee damage too high, skipping melee.");
                }
            }

            // Evaluate DFA attacks
            if (attackerMech == null || !AIUtil.IsDFAAcceptable(attackerMech, target)) {
                Mod.Log.Debug("this unit cannot dfa");
            } else {

                // TODO: Check Retaliation

                List<List<CondensedWeapon>> dfaWeaponSets = null;
                if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                    dfaWeaponSets = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.DFAWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                } else {
                    dfaWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);
                }

                // Add DFA weapons to each set
                CondensedWeapon cDFAWeapon = new CondensedWeapon(attackerMech.DFAWeapon);
                for (int i = 0; i < dfaWeaponSets.Count; i++) {
                    dfaWeaponSets[i].Add(cDFAWeapon);
                }

                weaponSetsByAttackType[2] = dfaWeaponSets;
            }

            List<AttackEvaluation> list = AEHelper.EvaluateAttacks(attackerAA, target, weaponSetsByAttackType, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug(string.Format("found {0} different attack solutions", list.Count));
            float bestRangedEDam = 0f;
            float bestMeleeEDam = 0f;
            float bestDFAEDam = 0f;
            for (int m = 0; m < list.Count; m++) {
                AttackEvaluation attackEvaluation = list[m];
                Mod.Log.Debug($"evaluated attack of type {attackEvaluation.AttackType} with {attackEvaluation.WeaponList.Count} weapons " +
                    $"and a result of {attackEvaluation.ExpectedDamage}");
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
            Mod.Log.Debug($"best shooting: {bestRangedEDam}  melee: {bestMeleeEDam}  dfa: {bestDFAEDam}");

            float targetMaxArmorFractionFromHittableLocations = AttackEvaluator.MaxDamageLevel(attackerAA, target);
            float existingTargetDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            float existingTargetDamageForOverheat = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
            float attackerLegDamage = attackerMech == null ? 0f : AttackEvaluator.LegDamageLevel(attackerMech);
            float maxAllowedLegDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            for (int n = 0; n < list.Count; n++) {
                AttackEvaluator.AttackEvaluation attackEvaluation2 = list[n];
                Mod.Log.Debug("------");
                AbstractActor targetActor = target as AbstractActor;
                Mod.Log.Debug($"Evaluating attack solution #{n} vs target: {CombatantUtils.Label(targetActor)}");
                
                // TODO: Do we really need this spam?
                Mod.Log.Debug(" Weapons:");
                foreach (Weapon weapon3 in attackEvaluation2.WeaponList) {
                    Mod.Log.Debug("Weapon: " + weapon3.Name);
                }
                if (attackEvaluation2.WeaponList.Count == 0) {
                    Mod.Log.Debug("SOLUTION REJECTED - no weapons!");
                }

                bool willCauseOverheat = attackEvaluation2.HeatGenerated + currentHeat > acceptableHeat;
                Mod.Log.Debug($"heat generated: {attackEvaluation2.HeatGenerated}  current: {currentHeat}  acceptable: {acceptableHeat}  willOverheat: {willCauseOverheat}");
                if (willCauseOverheat && attackerMech.OverheatWillCauseDeath()) {
                    Mod.Log.Debug("SOLUTION REJECTED - overheat would cause own death");
                    continue;
                }
                // TODO: Check for acceptable damage from overheat - as per below
                //bool flag6 = num4 >= existingTargetDamageForOverheat;
                //Mod.Log.Debug("but enough damage for overheat attack? " + flag6);
                //bool flag7 = attackEvaluation2.lowestHitChance >= weaponToHitThreshold;
                //Mod.Log.Debug("but enough accuracy for overheat attack? " + flag7);
                //if (willCauseOverheat && (!flag6 || !flag7)) {
                //    Mod.Log.Debug("SOLUTION REJECTED - not enough damage or accuracy on an attack that will overheat");
                //    continue;
                //}

                if (attackEvaluation2.AttackType == AIUtil.AttackType.Melee && (!attackerAA.CanEngageTarget(target) ||
                    targetActor == null || !isStationary)) {
                    Mod.Log.Debug("SOLUTION REJECTED - can't melee");
                    continue;
                }

                if (attackEvaluation2.AttackType == AIUtil.AttackType.DeathFromAbove && (!attackerAA.CanDFATargetFromPosition(target, attackerAA.CurrentPosition) ||
                    targetMaxArmorFractionFromHittableLocations < existingTargetDamageForDFA || 
                    attackerLegDamage > maxAllowedLegDamageForDFA)) {
                    Mod.Log.Debug("SOLUTION REJECTED - DFA not applicable!");
                    continue;
                }

                if (attackEvaluation2.ExpectedDamage > 0f) {
                    BehaviorTreeResults behaviorTreeResults = new BehaviorTreeResults(BehaviorNodeState.Success);
                    
                    CalledShotAttackOrderInfo orderInfo = AEHelper.MakeCalledShotOrder(attackerAA, attackEvaluation2, target, false);
                    MultiTargetAttackOrderInfo orderInfo2 = MultiAttack.MakeMultiAttackOrder(attackerAA, attackEvaluation2, enemyUnitIndex);

                    CalledShotAttackOrderInfo calledShotAttackOrderInfo = AEHelper.MakeOffensivePushOrder(attackerAA, attackEvaluation2, target);
                    if (calledShotAttackOrderInfo != null) {
                        behaviorTreeResults.orderInfo = calledShotAttackOrderInfo;
                        behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using offensive push";
                    } else if (orderInfo != null) {
                        behaviorTreeResults.orderInfo = orderInfo;
                        behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using called shot";
                    } else if (!willCauseOverheat && orderInfo2 != null) {
                        // Multi-attack in RT / BTA only makes sense to:
                        //  1. maximize breaching shot (which ignores cover/etc) if you a single weapon
                        //  2. spread status effects around while firing on a single target
                        //  3. maximizing total damage across N targets, while sacrificing potential damage at a specific target
                        //    3a. Especially with set sof weapons across range brackets, where you can split short-range weapons and long-range weapons                                
                        behaviorTreeResults.orderInfo = orderInfo2;
                        behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using multi attack";
                    } else {
                        AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target);
                        attackOrderInfo.Weapons = attackEvaluation2.WeaponList;
                        attackOrderInfo.TargetUnit = target;
                        attackOrderInfo.VentFirst = (willCauseOverheat && attackerAA.HasVentCoolantAbility && attackerAA.CanVentCoolant);
                        AIUtil.AttackType attackType = attackEvaluation2.AttackType;

                        if (attackType != AIUtil.AttackType.Melee) {
                            if (attackType == AIUtil.AttackType.DeathFromAbove) {
                                attackOrderInfo.IsDeathFromAbove = true;
                                attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                                attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                                List<PathNode> dfadestsForTarget = attackerMech.JumpPathing.GetDFADestsForTarget(targetActor);
                                if (dfadestsForTarget.Count == 0) {
                                    Mod.Log.Debug("Failing for lack of DFA destinations");
                                    goto IL_B74;
                                }
                                attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(targetActor, dfadestsForTarget);
                            }
                        } else {
                            attackOrderInfo.IsMelee = true;
                            attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                            attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                            List<PathNode> meleeDestsForTarget = attackerMech.Pathing.GetMeleeDestsForTarget(targetActor);
                            if (meleeDestsForTarget.Count == 0) {
                                Mod.Log.Debug("Failing for lack of melee destinations");
                                goto IL_B74;
                            }
                            attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(targetActor, meleeDestsForTarget);
                        }

                        behaviorTreeResults.orderInfo = attackOrderInfo;
                        behaviorTreeResults.debugOrderString = $" using attack type: {attackEvaluation2.AttackType} against: {target.DisplayName}";
                    }

                    Mod.Log.Debug("attack order: " + behaviorTreeResults.debugOrderString);
                    order = behaviorTreeResults;
                    return attackEvaluation2.ExpectedDamage;
                }
                Mod.Log.Debug("rejecting attack for not having any expected damage");
            IL_B74:;
            }

            Mod.Log.Debug("There are no targets I can shoot at without overheating.");
            order = null;
            return 0f;
        }

    }
}
