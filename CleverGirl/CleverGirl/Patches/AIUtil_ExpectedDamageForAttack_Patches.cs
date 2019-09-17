using BattleTech;
using Harmony;
using System.Collections.Generic;
using UnityEngine;
using static CleverGirl.AIHelper;

namespace CleverGirl.Patches {

    //[HarmonyPatch(typeof(AIUtil), "ExpectedDamageForAttack")]
    public static class AIUtil_ExpectedDamageForAttack {

        //public static bool Prefix(AIUtil __instance, AbstractActor unit, AIUtil.AttackType attackType, List<Weapon> weaponList, 
        //    ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext,
        //    ref float __result) {

        //    Mech mech = unit as Mech;
        //    AbstractActor abstractActor = target as AbstractActor;

        //    // Attack type is melee and there's no path or ability, fail
        //    if (attackType == AIUtil.AttackType.Melee && 
        //        (abstractActor == null || mech == null || mech.Pathing.GetMeleeDestsForTarget(abstractActor).Count == 0)) {
        //        __result = 0f;
        //        return false;
        //    }

        //    // Attack type is DFA and there's no path or no ability, fail
        //    if (attackType == AIUtil.AttackType.DeathFromAbove && 
        //        (abstractActor == null || mech == null || mech.JumpPathing.GetDFADestsForTarget(abstractActor).Count == 0)) {
        //        __result = 0f;
        //        return false;
        //    }

        //    // Attack type is range and there's no weapons, fail
        //    if (attackType == AIUtil.AttackType.Shooting && weaponList.Count == 0) {
        //        __result = 0f;
        //        return false;
        //    }

        //    AttackParams attackParams = new AttackParams(attackType, unit, target as AbstractActor, attackPosition, weaponList.Count, useRevengeBonus);

        //    List<AggregateWeapon> aggregatedWeps = AggregateWeaponList(weaponList);

        //    float totalExpectedDam = 0f;
        //    for (int i = 0; i < aggregatedWeps.Count; i++) {
        //        AggregateWeapon aWeapon = aggregatedWeps[i];
        //        totalExpectedDam += CalculateWeaponDamageEV(aWeapon, unitForBVContext.BehaviorTree, attackParams, unit, attackPosition, target, targetPosition);
        //    }

        //    float blowQualityMultiplier = unit.Combat.ToHit.GetBlowQualityMultiplier(attackParams.Quality);
        //    __result = totalExpectedDam * blowQualityMultiplier;

        //    return false;
        //}

        //private class AttackParams {
        //    public bool TargetIsUnsteady;
        //    public bool TargetIsBraced;
        //    public bool TargetIsEvasive;

        //    public bool UseRevengeBonus;
        //    public bool IsBreachingShotAttack;

        //    public AIUtil.AttackType AttackType;
        //    public MeleeAttackType MeleeAttackType;
        //    public AttackImpactQuality Quality;

        //    public AttackParams(AIUtil.AttackType attackType, AbstractActor attacker, AbstractActor target, Vector3 attackPos, int weaponCount, bool useRevengeBonus) {
        //        this.AttackType = attackType;

        //        this.TargetIsUnsteady = target != null && target.IsUnsteady;
        //        this.TargetIsBraced = target != null && target.BracedLastRound;
        //        this.TargetIsEvasive = target != null && target.IsEvasive;

        //        this.UseRevengeBonus = useRevengeBonus;

        //        this.MeleeAttackType = (attackType != AIUtil.AttackType.Melee) ?
        //            ((attackType != AIUtil.AttackType.DeathFromAbove) ? MeleeAttackType.NotSet : MeleeAttackType.DFA)
        //            : MeleeAttackType.MeleeWeapon;

        //        this.Quality = AttackImpactQuality.Solid;
        //        this.Quality = attacker.Combat.ToHit.GetBlowQuality(attacker, attackPos, null, target, MeleeAttackType, 
        //            attacker.IsUsingBreachingShotAbility(weaponCount));

        //        if (attackType == AIUtil.AttackType.Shooting && weaponCount == 1 && attacker.HasBreachingShotAbility) {
        //            IsBreachingShotAttack = true;
        //        }
        //    }
        //}

        //private static float CalculateWeaponDamageEV(AggregateWeapon aWeapon, BehaviorTree bTree, AttackParams attackParams, 
        //    AbstractActor attacker, Vector3 attackerPos, ICombatant target, Vector3 targetPos) {

        //    Weapon weapon = aWeapon.weapon;
        //    int shotsWhenFired = weapon.ShotsWhenFired;

        //    float attackTypeWeight = 1f;
        //    switch (attackParams.AttackType) {
        //        case AIUtil.AttackType.Shooting: {
        //                attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                break;
        //            }
        //        case AIUtil.AttackType.Melee: {
        //                Mech targetMech = target as Mech;
        //                Mech attackingMech = attacker as Mech;
        //                if (attackParams.UseRevengeBonus && targetMech != null && attackingMech != null && attackingMech.IsMeleeRevengeTarget(targetMech)) {
        //                    attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeRevengeBonus).FloatVal;
        //                }
        //                if (attackingMech != null && weapon == attackingMech.MeleeWeapon) {
        //                    attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeDamageMultiplier).FloatVal;
        //                    if (attackParams.TargetIsUnsteady) {
        //                        attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeVsUnsteadyTargetDamageMultiplier).FloatVal;
        //                    }
        //                } else {
        //                    attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                }
        //                break;
        //            }
        //        case AIUtil.AttackType.DeathFromAbove: {
        //                Mech attackerMech = attacker as Mech;
        //                if (attackerMech != null && weapon == attackerMech.DFAWeapon) {
        //                    attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_DFADamageMultiplier).FloatVal;
        //                } else {
        //                    attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
        //                }
        //                break;
        //            }
        //        default:
        //            Debug.LogError("unknown attack type: " + attackParams.AttackType);
        //            break;
        //    }

        //    float toHitFromPos = weapon.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
        //    if (attackParams.IsBreachingShotAttack) {
        //        // Breaching shot is assumed to auto-hit... why?
        //        toHitFromPos = 1f;
        //    }

        //    float damagePerShotFromPos = weapon.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);

        //    float heatDamPerShotWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal * weapon.HeatDamagePerShot;

        //    float stabilityDamPerShotWeight = 0f;
        //    if (attackParams.TargetIsUnsteady) {
        //        stabilityDamPerShotWeight = weapon.Instability() * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;
        //    }

        //    float meleeStatusWeights = 0f;
        //    meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ?
        //        0f : (damagePerShotFromPos * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal));
        //    meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ?
        //        0f : (damagePerShotFromPos * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal));

        //    // If this is an aggregate weapon and the toHitFromPos isn't multiplied by count, does it make a diff?
        //    // i.e. this would be (2 * 0.7 * X) => 1.4x per, w/ 8 weapons -> 8 * 1.4 => 11.2
        //    //   with (8 * 2 * 0.7 * X) => 11.2
        //    float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);

        //    float aggregateDamageEV = weaponDamageEV * aWeapon.count;

        //    return aggregateDamageEV;
        //}
    }
}
