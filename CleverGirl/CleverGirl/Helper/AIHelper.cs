
using BattleTech;
using CleverGirl.Objects;
using CustAmmoCategories;
using Harmony;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleverGirl {
    public class AIHelper {

        public static int HeatForAttack(List<CondensedWeapon> weaponList) {
            int num = 0;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                num += ((int)cWeapon.First.HeatGenerated * cWeapon.weaponsCondensed);
            }
            return num;
        }

        public static float LowestHitChance(List<CondensedWeapon> weaponList, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive) {
            float num = float.MaxValue;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                float toHitFromPosition = cWeapon.First.GetToHitFromPosition(target, 1, attackPosition, targetPosition, true, targetIsEvasive, false);
                num = Mathf.Min(num, toHitFromPosition);
            }
            return num;
        }

        //public static float ExpectedDamageForAttack(AbstractActor attacker, AIUtil.AttackType attackType, List<CondensedWeapon> weaponList,
        //    ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext) {

        //    Mech mech = attacker as Mech;
        //    AbstractActor abstractActor = target as AbstractActor;

        //    // Attack type is melee and there's no path or ability, fail
        //    if (attackType == AIUtil.AttackType.Melee &&
        //        (abstractActor == null || mech == null || mech.Pathing.GetMeleeDestsForTarget(abstractActor).Count == 0)) {
        //        return 0f;
        //    }

        //    // Attack type is DFA and there's no path or no ability, fail
        //    if (attackType == AIUtil.AttackType.DeathFromAbove &&
        //        (abstractActor == null || mech == null || mech.JumpPathing.GetDFADestsForTarget(abstractActor).Count == 0)) {
        //        return 0f;
        //    }

        //    // Attack type is range and there's no weapons, fail
        //    if (attackType == AIUtil.AttackType.Shooting && weaponList.Count == 0) {
        //        return 0f;
        //    }

        //    AttackDetails attackParams = new AttackDetails(attackType, attacker, target as AbstractActor, attackPosition, weaponList.Count, useRevengeBonus);

        //    float totalExpectedDam = 0f;
        //    for (int i = 0; i < weaponList.Count; i++) {
        //        CondensedWeapon cWeapon = weaponList[i];
        //        totalExpectedDam += CalculateWeaponDamageEV(cWeapon, unitForBVContext.BehaviorTree, attackParams, attacker, attackPosition, target, targetPosition);
        //    }

        //    float blowQualityMultiplier = attacker.Combat.ToHit.GetBlowQualityMultiplier(attackParams.ImpactQuality);
        //    float totalDam = totalExpectedDam * blowQualityMultiplier;

        //    return totalDam;
        //}



        //// Calculate the expected value for a given weapon against the target
        //public static float CalculateWeaponDamageEV(Weapon weapon, BehaviorTree bTree, AttackDetails attackParams,
        //    AbstractActor attacker, Vector3 attackerPos, ICombatant target, Vector3 targetPos) {
           
        //    try
        //    {
        //        float attackTypeWeight = 1f;
        //        switch (attackParams.AttackType)
        //        {
        //            case AIUtil.AttackType.Shooting:
        //                {
        //                    attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                    break;
        //                }
        //            case AIUtil.AttackType.Melee:
        //                {
        //                    Mech targetMech = target as Mech;
        //                    Mech attackingMech = attacker as Mech;
        //                    if (attackParams.UseRevengeBonus && targetMech != null && attackingMech != null && attackingMech.IsMeleeRevengeTarget(targetMech))
        //                    {
        //                        attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeRevengeBonus).FloatVal;
        //                    }
        //                    if (attackingMech != null && weapon == attackingMech.MeleeWeapon)
        //                    {
        //                        attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeDamageMultiplier).FloatVal;
        //                        if (attackParams.TargetIsUnsteady)
        //                        {
        //                            attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeVsUnsteadyTargetDamageMultiplier).FloatVal;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                    }
        //                    break;
        //                }
        //            case AIUtil.AttackType.DeathFromAbove:
        //                {
        //                    Mech attackerMech = attacker as Mech;
        //                    if (attackerMech != null && weapon == attackerMech.DFAWeapon)
        //                    {
        //                        attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_DFADamageMultiplier).FloatVal;
        //                    }
        //                    else
        //                    {
        //                        attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                    }
        //                    break;
        //                }
        //            default:
        //                Debug.LogError("unknown attack type: " + attackParams.AttackType);
        //                break;
        //        }

        //        float toHitFromPos = weapon.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
        //        if (attackParams.IsBreachingShotAttack)
        //        {
        //            // Breaching shot is assumed to auto-hit... why?
        //            toHitFromPos = 1f;
        //        }
        //        Mod.Log.Debug?.Write($"Evaluating weapon: {weapon.Name} with toHitFromPos:{toHitFromPos}");

        //        float heatToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal;
        //        float stabToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;

        //        float meleeStatusWeights = 0f;
        //        if (attackParams.AttackType == AIUtil.AttackType.Melee)
        //        {
        //            float bracedMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal;
        //            if (attackParams.TargetIsBraced) { meleeStatusWeights += bracedMeleeMulti; }

        //            float evasiveMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal;
        //            if (attackParams.TargetIsEvasive) { meleeStatusWeights += evasiveMeleeMulti; }
        //        }

        //        DetermineMaxDamageAmmoModePair(weapon, attackParams, attacker, attackerPos, target, heatToDamRatio, stabToDamRatio, out float maxDamage, out AmmoModePair maxDamagePair);
        //        Mod.Log.Debug?.Write($"Max damage from ammoBox: {maxDamagePair.ammoId}_{maxDamagePair.modeId} EV: {maxDamage}");
        //        weapon.ammoAndMode = maxDamagePair;

        //        //float damagePerShotFromPos = cWeapon.First.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);
        //        //float heatDamPerShotWeight = cWeapon.First.HeatDamagePerShot * heatToDamRatio;
        //        //float stabilityDamPerShotWeight = attackParams.TargetIsUnsteady ? cWeapon.First.Instability() * stabToDamRatio : 0f;

        //        //float meleeStatusWeights = 0f;
        //        //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ? 0f : (damagePerShotFromPos * bracedMeleeMulti));
        //        //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ? 0f : (damagePerShotFromPos * evasiveMeleeMult));

        //        //int shotsWhenFired = cWeapon.First.ShotsWhenFired;
        //        //float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);
        //        float aggregateDamageEV = maxDamage * weapon.weaponsCondensed;
        //        Mod.Log.Debug?.Write($"Aggregate EV = {aggregateDamageEV} == maxDamage: {maxDamage} * weaponsCondensed: {weapon.weaponsCondensed}");

        //        return aggregateDamageEV;
        //    }
        //    catch (Exception e)
        //    {
        //        Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
        //        return 0f;
        //    }
        //}

        // --- BEHAVIOR VARIABLE BELOW
        public static BehaviorVariableValue GetCachedBehaviorVariableValue(BehaviorTree bTree, BehaviorVariableName name) {
            return ModState.BehaviorVarValuesCache.GetOrAdd(name, GetBehaviorVariableValue(bTree, name));
        }

        // TODO: EVERYTHING SHOULD CONVERT TO CACHED CALL IF POSSIBLE
        public static BehaviorVariableValue GetBehaviorVariableValue(BehaviorTree bTree, BehaviorVariableName name) {
            BehaviorVariableValue behaviorVariableValue = bTree.unitBehaviorVariables.GetVariable(name);
            if (behaviorVariableValue != null) {
                return behaviorVariableValue;
            }

            Pilot pilot = bTree.unit.GetPilot();
            if (pilot != null) {
                BehaviorVariableScope scopeForAIPersonality = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAIPersonality(pilot.pilotDef.AIPersonality);
                if (scopeForAIPersonality != null) {
                    behaviorVariableValue = scopeForAIPersonality.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                    if (behaviorVariableValue != null) {
                        return behaviorVariableValue;
                    }
                }
            }

            if (bTree.unit.lance != null) {
                behaviorVariableValue = bTree.unit.lance.BehaviorVariables.GetVariable(name);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            if (bTree.unit.team != null) {
                Traverse bvT = Traverse.Create(bTree.unit.team).Field("BehaviorVariables");
                BehaviorVariableScope bvs = bvT.GetValue<BehaviorVariableScope>();
                behaviorVariableValue = bvs.GetVariable(name);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            UnitRole unitRole = bTree.unit.DynamicUnitRole;
            if (unitRole == UnitRole.Undefined) {
                unitRole = bTree.unit.StaticUnitRole;
            }

            BehaviorVariableScope scopeForRole = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForRole(unitRole);
            if (scopeForRole != null) {
                behaviorVariableValue = scopeForRole.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            if (bTree.unit.CanMoveAfterShooting) {
                BehaviorVariableScope scopeForAISkill = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAISkill(AISkillID.Reckless);
                if (scopeForAISkill != null) {
                    behaviorVariableValue = scopeForAISkill.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                    if (behaviorVariableValue != null) {
                        return behaviorVariableValue;
                    }
                }
            }

            behaviorVariableValue = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
            if (behaviorVariableValue != null) {
                return behaviorVariableValue;
            }

            return DefaultBehaviorVariableValue.GetSingleton();
        }
    }
}
