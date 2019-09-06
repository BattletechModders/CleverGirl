using BattleTech;
using Harmony;
using System.Collections.Generic;
using UnityEngine;
using us.frostraptor.modUtils;

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
            List<List<Weapon>>[] array2 = new List<List<Weapon>>[3];
            for (int j = 0; j < 3; j++) {
                Mod.Log.Debug("considering attack type " + j);
                string str;
                if (attackerMech == null && (j == 1 || j == 2)) {
                    Mod.Log.Debug("this unit can't melee or dfa");
                } else if (j == 1 && !attackerMech.CanEngageTarget(target, out str)) {
                    Mod.Log.Debug("unit.CanEngageTarget returned FALSE because: " + str);
                } else {
                    if (j == 1 && targetMech != null) {
                        float num5 = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(targetMech, attackerAA, targetMech.CurrentPosition, attackerMech.CurrentPosition, false, attackerAA);
                        float num6 = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(attackerMech, target, attackerMech.CurrentPosition, target.CurrentPosition, false, attackerAA);
                        if (num6 <= 0f) {
                            Mod.Log.Debug("expected damage: " + num6);
                            goto IL_549;
                        }
                        float num7 = num5 / num6;
                        float floatVal4 = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal;
                        if (num7 > floatVal4) {
                            Mod.Log.Debug($" melee ratio too high: {num7} vs {floatVal4}");
                            goto IL_549;
                        }
                    }
                    if (j == 2 && !AIUtil.IsDFAAcceptable(attackerAA, target)) {
                        Mod.Log.Debug("unit cannot DFA");
                    } else {
                        if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                            float toHitFrac = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100f;
                            array2[j] = AttackEvaluator.MakeWeaponSetsForEvasive(array[j], toHitFrac, target, attackerAA.CurrentPosition);
                        } else {
                            array2[j] = AttackEvaluator.MakeWeaponSets(array[j]);
                        }
                        if (attackerMech != null && (j == 1 || j == 2)) {
                            for (int k = 0; k < array2[j].Count; k++) {
                                if (j == 1) {
                                    array2[j][k].Add(attackerMech.MeleeWeapon);
                                }
                                if (j == 2) {
                                    array2[j][k].Add(attackerMech.DFAWeapon);
                                }
                                for (int l = 0; l < attackerMech.Weapons.Count; l++) {
                                    Weapon weapon2 = attackerMech.Weapons[l];
                                    if (weapon2.CanFire && weapon2.Category == WeaponCategory.AntiPersonnel && !array2[j][k].Contains(weapon2)) {
                                        array2[j][k].Add(weapon2);
                                    }
                                }
                            }
                        }
                    }
                }
            IL_549:;
            }


            List<AttackEvaluator.AttackEvaluation> list = AttackEvaluator.AttackEvaluation.EvaluateAttacks(attackerAA, target, array2, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug(string.Format("found {0} different attack solutions", list.Count));
            float num8 = 0f;
            float num9 = 0f;
            float num10 = 0f;
            for (int m = 0; m < list.Count; m++) {
                AttackEvaluator.AttackEvaluation attackEvaluation = list[m];
                Mod.Log.Debug(string.Format("evaluated attack of type {0} with {1} weapons and a result of {2}", attackEvaluation.AttackType, attackEvaluation.WeaponList.Count, attackEvaluation.ExpectedDamage));
                switch (attackEvaluation.AttackType) {
                    case AIUtil.AttackType.Shooting:
                        num8 = Mathf.Max(num8, attackEvaluation.ExpectedDamage);
                        break;
                    case AIUtil.AttackType.Melee:
                        num9 = Mathf.Max(num9, attackEvaluation.ExpectedDamage);
                        break;
                    case AIUtil.AttackType.DeathFromAbove:
                        num10 = Mathf.Max(num10, attackEvaluation.ExpectedDamage);
                        break;
                    default:
                        Debug.Log("unknown attack type: " + attackEvaluation.AttackType);
                        break;
                }
            }
            Mod.Log.Debug("best shooting: " + num8);
            Mod.Log.Debug("best melee: " + num9);
            Mod.Log.Debug("best dfa: " + num10);
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
                            CalledShotAttackOrderInfo calledShotAttackOrderInfo = AttackEvaluator.MakeOffensivePushOrder(attackerAA, attackEvaluation2, enemyUnitIndex);
                            CalledShotAttackOrderInfo orderInfo;
                            MultiTargetAttackOrderInfo orderInfo2;
                            if (calledShotAttackOrderInfo != null) {
                                behaviorTreeResults.orderInfo = calledShotAttackOrderInfo;
                                behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using offensive push";
                            } else if ((orderInfo = AttackEvaluator.MakeCalledShotOrder(attackerAA, attackEvaluation2, enemyUnitIndex, false)) != null) {
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

    [HarmonyPatch(typeof(AttackEvaluator), "EvaluateAttacks")]
    public static class AttackEvaluator_EvaluateAttacks {

        public static bool Prefix(AbstractActor unit, ICombatant target, List<List<Weapon>>[] weaponSetListByAttack, 
            Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive, ref List<AttackEvaluator.AttackEvaluation> __result) {

            List<AttackEvaluator.AttackEvaluation> list = new List<AttackEvaluator.AttackEvaluation>();

            // List 0 is ranged weapons, 1 is melee+support, 2 is DFA+support
            for (int i = 0; i < 3; i++) {
                List<List<Weapon>> list2 = weaponSetListByAttack[i];
                if (list2 != null) {
                    for (int j = 0; j < list2.Count; j++) {
                        List<Weapon> weaponList = list2[j];
                        AttackEvaluator.AttackEvaluation attackEvaluation = new AttackEvaluator.AttackEvaluation();
                        attackEvaluation.WeaponList = weaponList;
                        attackEvaluation.AttackType = (AIUtil.AttackType)i;
                        attackEvaluation.HeatGenerated = (float)AIUtil.HeatForAttack(weaponList);
                        Mech mech = unit as Mech;
                        if (mech != null) {
                            attackEvaluation.HeatGenerated += (float)mech.TempHeat;
                            attackEvaluation.HeatGenerated -= (float)mech.AdjustedHeatsinkCapacity;
                        }
                        attackEvaluation.ExpectedDamage = AIUtil.ExpectedDamageForAttack(unit, attackEvaluation.AttackType, weaponList, target, attackPosition, targetPosition, true, unit);
                        attackEvaluation.lowestHitChance = AIUtil.LowestHitChance(weaponList, target, attackPosition, targetPosition, targetIsEvasive);
                        list.Add(attackEvaluation);
                    }
                }
            }

            list.Sort((AttackEvaluator.AttackEvaluation a, AttackEvaluator.AttackEvaluation b) => a.ExpectedDamage.CompareTo(b.ExpectedDamage));
            list.Reverse();

            __result = list;
            return false;
        }
      
    }

}
