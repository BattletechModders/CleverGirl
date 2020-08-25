using BattleTech;
using CleverGirl.Helper;
using CleverGirl.Objects;
using System.Collections.Generic;
using UnityEngine;
using static AttackEvaluator;

namespace CleverGirl.Calculator
{
    public static class RangedCalculator
    {
        public static AttackEvaluation OptimizeAttack(List<Weapon> weapons, Mech attacker, ICombatant target)
        {
            AttackEvaluation attackEvaluation = new AttackEvaluation();

            // TODO: Need to handle HasBreachingShotAbility. Check to see if a single weapon that blows through cover is better. Start with highest dam weapon.

            /*
             * 1. Check that weapons, attacker, target is not null
             * 2. Weapons are already checked for LoF and range
             * 3. Build an EV and Heat ratio for each weapon
             * 4. Evaluate heat - sum all heat values, but drop weapons step by step until below safe threshold
             */

            AttackDetails details = new AttackDetails(attackType: AIUtil.AttackType.Shooting, attacker: attacker, 
                target: target as AbstractActor, attackPos: attacker.CurrentPosition, targetPos: target.CurrentPosition, 
                weaponCount: 2, useRevengeBonus: true);

            List<WeaponAttackEval> evaluations = new List<WeaponAttackEval>();
            for (int i = 0; i < weapons.Count; i++)
            {
                Weapon weapon = weapons[i];
                float totalExpectedDam = WeaponHelper.CalculateWeaponDamageEV(weapon, attacker, target, details);

            }

            return attackEvaluation;
        }

        private static WeaponAttackEval EvaluateShootingAttack(Weapon weapon, AttackDetails details)
        {

            BehaviorTree bTree = details.Attacker.BehaviorTree;

            try
            {
                float attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal; ;

                float toHitFromPos = weapon.GetToHitFromPosition(target: details.Target, numTargets: 1, 
                    attackPosition: details.Attacker.CurrentPosition, targetPosition: details.Target.CurrentPosition, 
                    bakeInEvasiveModifier: true, targetIsEvasive: details.TargetIsEvasive, isMoraleAttack: false);
                if (details.IsBreachingShotAttack)
                {
                    // Breaching shot is assumed to auto-hit... why?
                    toHitFromPos = 1f;
                }
                Mod.Log.Debug?.Write($"Evaluating weapon: {weapon.Name} with toHitFromPos:{toHitFromPos}");

                float heatToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal;
                float stabToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;

                float meleeStatusWeights = 0f;
                if (details.AttackType == AIUtil.AttackType.Melee)
                {
                    float bracedMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal;
                    if (details.TargetIsBraced) { meleeStatusWeights += bracedMeleeMulti; }

                    float evasiveMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal;
                    if (details.TargetIsEvasive) { meleeStatusWeights += evasiveMeleeMulti; }
                }

                DetermineMaxDamageAmmoModePair(weapon, details, attacker, attackerPos, target, heatToDamRatio, stabToDamRatio,
                    out float maxDamage, out AmmoModePair maxDamagePair);
                Mod.Log.Debug?.Write($"Max damage from ammoBox: {maxDamagePair.ammoId}_{maxDamagePair.modeId} EV: {maxDamage}");
                weapon.ammoAndMode = maxDamagePair;

                //float damagePerShotFromPos = cWeapon.First.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);
                //float heatDamPerShotWeight = cWeapon.First.HeatDamagePerShot * heatToDamRatio;
                //float stabilityDamPerShotWeight = attackParams.TargetIsUnsteady ? cWeapon.First.Instability() * stabToDamRatio : 0f;

                //float meleeStatusWeights = 0f;
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ? 0f : (damagePerShotFromPos * bracedMeleeMulti));
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ? 0f : (damagePerShotFromPos * evasiveMeleeMult));

                //int shotsWhenFired = cWeapon.First.ShotsWhenFired;
                //float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);

                return aggregateDamageEV;
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
                return 0f;
            }

            WeaponAttackEval eval = new WeaponAttackEval();
        }
    }
}
