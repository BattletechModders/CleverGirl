
using CleverGirl.Helper;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleverGirl {
    public class AIHelper {

        public static int HeatForAttack(List<CondensedWeaponAmmoMode> weaponList) {
            int num = 0;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeaponAmmoMode cWeapon = weaponList[i];
                cWeapon.ApplyAmmoMode();
                Weapon rawWeapon = cWeapon.First;
                num += ((int)rawWeapon.HeatGenerated * cWeapon.weaponsCondensed);
                cWeapon.RestoreBaseAmmoMode();
            }
            return num;
        }

        public static float LowestHitChance(List<CondensedWeaponAmmoMode> weaponList, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive) {
            float num = float.MaxValue;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeaponAmmoMode cWeapon = weaponList[i];
                cWeapon.ApplyAmmoMode();
                float toHitFromPosition = cWeapon.First.GetToHitFromPosition(target, 1, attackPosition, targetPosition, true, targetIsEvasive, false);
                num = Mathf.Min(num, toHitFromPosition);
                cWeapon.RestoreBaseAmmoMode();
            }
            return num;
        }

        public static float ExpectedDamageForAttack(AbstractActor unit, AIUtil.AttackType attackType, List<CondensedWeaponAmmoMode> weaponList,
            ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext, out bool isArtilleryAttack) {

            Mech mech = unit as Mech;
            AbstractActor abstractActor = target as AbstractActor;
            isArtilleryAttack = false;

            // Attack type is melee and there's no path or ability, fail
            if (attackType == AIUtil.AttackType.Melee &&
                (abstractActor == null || mech == null || mech.Pathing.GetMeleeDestsForTarget(abstractActor).Count == 0)) {
                return 0f;
            }

            // Attack type is DFA and there's no path or no ability, fail
            if (attackType == AIUtil.AttackType.DeathFromAbove &&
                (abstractActor == null || mech == null || mech.JumpPathing.GetDFADestsForTarget(abstractActor).Count == 0)) {
                return 0f;
            }

            // Attack type is range and there's no weapons, fail
            if (attackType == AIUtil.AttackType.Shooting && weaponList.Count == 0) {
                return 0f;
            }

            AttackParams attackParams = new AttackParams(attackType, unit, target as AbstractActor, attackPosition, weaponList.Count, useRevengeBonus);

            Dictionary<bool, float> totalExpectedDam = new Dictionary<bool, float>
            {
                {false, 0},
                {true, 0}
            };
            foreach (var cWeapon in weaponList)
            {
                cWeapon.ApplyAmmoMode();
                bool isArtillery = cWeapon.First.IsArtillery();
                totalExpectedDam[isArtillery] += CalculateWeaponDamageEV(cWeapon, unitForBVContext.BehaviorTree, attackParams, unit, attackPosition, target, targetPosition);
                cWeapon.RestoreBaseAmmoMode();
            }

            float blowQualityMultiplier = unit.Combat.ToHit.GetBlowQualityMultiplier(attackParams.Quality);
            float artilleryDamage = totalExpectedDam.GetValueOrDefault(true, 0f) * blowQualityMultiplier;
            float standardDamage = totalExpectedDam.GetValueOrDefault(false, 0f) * blowQualityMultiplier;

            if (artilleryDamage == 0)
            {
                Mod.Log.Debug?.Write($"Expected damage for attack is {standardDamage}");
            }
            else
            {
                Mod.Log.Debug?.Write($"Expected damage for attack is standard: {standardDamage} artillery: {artilleryDamage}");
            }

            if (artilleryDamage > standardDamage)
            {
                Mod.Log.Debug?.Write("-- Picking artillery attack");
                isArtilleryAttack = true;
                return artilleryDamage;
            }
            return standardDamage;
        }

        private class AttackParams {
            public bool TargetIsUnsteady;
            public bool TargetIsBraced;
            public bool TargetIsEvasive;

            public bool UseRevengeBonus;
            public bool IsBreachingShotAttack;

            public AIUtil.AttackType AttackType;
            public MeleeAttackType MeleeAttackType;
            public AttackImpactQuality Quality;

            public AttackParams(AIUtil.AttackType attackType, AbstractActor attacker, AbstractActor target, Vector3 attackPos, int weaponCount, bool useRevengeBonus) {
                this.AttackType = attackType;

                this.TargetIsUnsteady = target != null && target.IsUnsteady;
                this.TargetIsBraced = target != null && target.BracedLastRound;
                this.TargetIsEvasive = target != null && target.IsEvasive;

                this.UseRevengeBonus = useRevengeBonus;

                this.MeleeAttackType = (attackType != AIUtil.AttackType.Melee) ?
                    ((attackType != AIUtil.AttackType.DeathFromAbove) ? MeleeAttackType.NotSet : MeleeAttackType.DFA)
                    : MeleeAttackType.MeleeWeapon;

                this.Quality = AttackImpactQuality.Solid;
                this.Quality = attacker.Combat.ToHit.GetBlowQuality(attacker, attackPos, null, target, MeleeAttackType,
                    attacker.IsUsingBreachingShotAbility(weaponCount));

                if (attackType == AIUtil.AttackType.Shooting && weaponCount == 1 && attacker.HasBreachingShotAbility) {
                    IsBreachingShotAttack = true;
                }
            }
        }

        // Calculate the expected value for a given weapon against the target
        private static float CalculateWeaponDamageEV(CondensedWeaponAmmoMode cWeapon, BehaviorTree bTree, AttackParams attackParams,
            AbstractActor attacker, Vector3 attackerPos, ICombatant target, Vector3 targetPos) {
           
            try
            {
                cWeapon.ApplyAmmoMode();
                float toHitFromPos = cWeapon.First.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
                cWeapon.RestoreBaseAmmoMode();
                if (attackParams.IsBreachingShotAttack)
                {
                    // Breaching shot is assumed to auto-hit... why?
                    toHitFromPos = 1f;
                }
                Mod.Log.Debug?.Write($"Evaluating weapon: {cWeapon.First.Name} using ammoMode:{cWeapon.ammoModePair} with toHitFromPos:{toHitFromPos}");

                float heatToDamRatio = BehaviorHelper.GetBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal;
                float stabToDamRatio = BehaviorHelper.GetBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;

                float meleeStatusWeights = 0f;
                if (attackParams.AttackType == AIUtil.AttackType.Melee)
                {
                    float bracedMeleeMulti = BehaviorHelper.GetBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal;
                    if (attackParams.TargetIsBraced) { meleeStatusWeights += bracedMeleeMulti; }

                    float evasiveMeleeMulti = BehaviorHelper.GetBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal;
                    if (attackParams.TargetIsEvasive) { meleeStatusWeights += evasiveMeleeMulti; }
                }
                Mod.Log.Debug?.Write($"Melee status weight calculated as: {meleeStatusWeights}");

                float weaponDamageEV = DetermineDamage(cWeapon, attackParams, attacker, attackerPos, target, heatToDamRatio, stabToDamRatio);
                float aggregateDamageEV = weaponDamageEV * cWeapon.weaponsCondensed;
                Mod.Log.Debug?.Write($"Aggregate EV = {aggregateDamageEV} == damage: {weaponDamageEV} * weaponsCondensed: {cWeapon.weaponsCondensed}");

                return aggregateDamageEV;
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
                return 0f;
            }
        }
        
        private static float DetermineDamage(CondensedWeaponAmmoMode cWeapon, AttackParams attackParams, AbstractActor attacker, Vector3 attackerPos, 
            ICombatant target, float heatToDamRatio, float stabToDamRatio)
        {
            Mod.Log.Debug?.Write($"Calculating damage prediction for weapon: {cWeapon.First.UIName} for mode: {cWeapon.ammoModePair.modeId} with ammo: {cWeapon.ammoModePair.ammoId} from attacker: {attacker.DistinctId()} to target: {target.DistinctId()} at distance {Vector3.Distance(target.CurrentPosition,attacker.CurrentPosition)}");
            WeaponFirePredictedEffect weaponFirePredictedEffect = cWeapon.First.CalcPredictedEffect(attackerPos, target);

            float enemyDamage = 0f, alliedDamage = 0f, neutralDamage = 0f;
            foreach (DamagePredictionRecord dpr in weaponFirePredictedEffect.predictDamage)
            {
                if (dpr == null)
                {
                    Mod.Log.Debug?.Write($"  DPR was null, skipping!");
                    continue;
                }

                float dprEV = dpr.HitsCount * dpr.ToHit * (dpr.Normal + (dpr.Heat * heatToDamRatio) + dpr.AP);
                // Chance to knockdown... but evasion dump is more valuable?
                if (attackParams.TargetIsUnsteady)
                {
                    dprEV += dpr.HitsCount * dpr.ToHit * (dpr.Instability * stabToDamRatio);
                }
                // TODO: If mech, check if if (this._stability > this.UnsteadyThreshold && !base.IsUnsteady), 
                // 	num3 *= base.StatCollection.GetValue<float>("ReceivedInstabilityMultiplier");
                //  num3 *= base.EntrenchedMultiplier;
                // Multiply by number of pips dumped?

                // TODO: If the mech is overheating, apply different factors than just the raw 'heatToDamRatio'?
                // ASSUME CBTBE here?
                // Caculate damage from ammo explosion? Calculate potential loss of weapons from shutdown?

                // If melee - apply weights?
                // If target is braced / guarded - reduce damage?
                // If target is evasive - weight AoE attacks (since they auto-hit)?

                if (weaponFirePredictedEffect.DamageOnJamm && weaponFirePredictedEffect.JammChance != 0f)
                {
                    Mod.Log.Debug?.Write($" - Weapon will damage on jam, reducing EV by 1 - {weaponFirePredictedEffect.JammChance}.");
                    dprEV *= (1.0f - weaponFirePredictedEffect.JammChance);
                }

                // Check target damage reduction?
                //float armorReduction = 0f;
                //foreach (AmmunitionBox aBox in cWeapon.First.ammoBoxes)
                //{
                //    //Mod.Log.Debug?.Write($" -- Checking ammo box defId: {aBox.mechComponentRef.ComponentDefID}");
                //    if (aBox.componentDef.Is<CleverGirlComponent>(out CleverGirlComponent cgComp) && cgComp.ArmorDamageReduction != 0)
                //    {
                //        armorReduction = cgComp.ArmorDamageReduction;
                //    }
                //}
                //if (armorReduction != 0f)
                //{
                //    Mod.Log.Debug?.Write($" -- APPLY DAMAGE REDUCTION OF: {armorReduction}");
                //}

                // TODO: AMS provides a shield to allies

                // Need to precalc some values on every combatant - 
                //  find objective targets
                //  heat to cripple / damage / etc
                //  stability damage to unsteady / to knockdown

                // TODO: Can we weight AMS as a weapon when it covers friendlies?

                Hostility targetHostility = attacker.Combat.HostilityMatrix.GetHostility(attacker.team, dpr.Target.team);
                if (targetHostility == Hostility.FRIENDLY)
                {
                    alliedDamage += dprEV;
                }
                else if (targetHostility == Hostility.NEUTRAL)
                {
                    neutralDamage += dprEV;
                }
                else
                {
                    enemyDamage += dprEV;
                }
            }

            float damageEV = enemyDamage + neutralDamage - (alliedDamage * Mod.Config.Weights.FriendlyDamageMulti);
            Mod.Log.Debug?.Write($" - DONE Calculating damage prediction for weapon: {cWeapon.First.UIName} for mode: {cWeapon.ammoModePair.modeId} with ammo: {cWeapon.ammoModePair.ammoId}");
            return damageEV;
        }

        public static bool IsDFAAcceptable(AbstractActor attacker, ICombatant targetCombatant)
        {
            AbstractActor targetActor = targetCombatant as AbstractActor;
            if (targetActor == null)
            {
                Mod.Log.Debug?.Write($" Target {targetCombatant.DistinctId()} is not an actor, cannot DFA");
                return false;
            }

            if (!attacker.CanDFATargetFromPosition(targetActor, attacker.CurrentPosition))
            {
                Mod.Log.Debug?.Write($"Attacker cannot DFA from currentPosition.");
                return false;
            }

            float attackerLegDamage = 0f;
            Mech mech = attacker as Mech;
            if (mech != null)
            {
                attackerLegDamage = AttackEvaluator.LegDamageLevel(mech);
            }
            float ownMaxLegDam = BehaviorHelper.GetCachedBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            if (attackerLegDamage >= ownMaxLegDam)
            {
                Mod.Log.Debug?.Write($"Attack will damage own legs too much - skipping DFA! Self leg damage: {attackerLegDamage} >= OwnMaxLegDamageForDFAAttack BehVar: {ownMaxLegDam}");
                return false;
            }

            float existingTargetDam = BehaviorHelper.GetCachedBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            float maxTargetDam = AttackEvaluator.MaxDamageLevel(attacker, targetActor);
            Mod.Log.Debug?.Write($"Returning {maxTargetDam >= existingTargetDam} as maxTargetDamage: {maxTargetDam} >= ExistingTargetDamageForDFAAttack BehVar: {existingTargetDam}");
            return maxTargetDam >= existingTargetDam;
        }
    }
}
