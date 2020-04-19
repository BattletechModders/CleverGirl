
using BattleTech;
using CleverGirl.Components;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using CustomComponents;
using Harmony;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        public static float ExpectedDamageForAttack(AbstractActor unit, AIUtil.AttackType attackType, List<CondensedWeapon> weaponList,
            ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext) {

            Mech mech = unit as Mech;
            AbstractActor abstractActor = target as AbstractActor;

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

            float totalExpectedDam = 0f;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                totalExpectedDam += CalculateWeaponDamageEV(cWeapon, unitForBVContext.BehaviorTree, attackParams, unit, attackPosition, target, targetPosition);
            }

            float blowQualityMultiplier = unit.Combat.ToHit.GetBlowQualityMultiplier(attackParams.Quality);
            float totalDam = totalExpectedDam * blowQualityMultiplier;

            return totalDam;
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
        private static float CalculateWeaponDamageEV(CondensedWeapon cWeapon, BehaviorTree bTree, AttackParams attackParams,
            AbstractActor attacker, Vector3 attackerPos, ICombatant target, Vector3 targetPos) {
           
            try
            {
                float attackTypeWeight = 1f;
                switch (attackParams.AttackType)
                {
                    case AIUtil.AttackType.Shooting:
                        {
                            attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                            break;
                        }
                    case AIUtil.AttackType.Melee:
                        {
                            Mech targetMech = target as Mech;
                            Mech attackingMech = attacker as Mech;
                            if (attackParams.UseRevengeBonus && targetMech != null && attackingMech != null && attackingMech.IsMeleeRevengeTarget(targetMech))
                            {
                                attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeRevengeBonus).FloatVal;
                            }
                            if (attackingMech != null && cWeapon.First == attackingMech.MeleeWeapon)
                            {
                                attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeDamageMultiplier).FloatVal;
                                if (attackParams.TargetIsUnsteady)
                                {
                                    attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeVsUnsteadyTargetDamageMultiplier).FloatVal;
                                }
                            }
                            else
                            {
                                attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                            }
                            break;
                        }
                    case AIUtil.AttackType.DeathFromAbove:
                        {
                            Mech attackerMech = attacker as Mech;
                            if (attackerMech != null && cWeapon.First == attackerMech.DFAWeapon)
                            {
                                attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_DFADamageMultiplier).FloatVal;
                            }
                            else
                            {
                                attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                            }
                            break;
                        }
                    default:
                        Debug.LogError("unknown attack type: " + attackParams.AttackType);
                        break;
                }

                float toHitFromPos = cWeapon.First.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
                if (attackParams.IsBreachingShotAttack)
                {
                    // Breaching shot is assumed to auto-hit... why?
                    toHitFromPos = 1f;
                }
                Mod.Log.Debug($"Evaluating weapon: {cWeapon.First.Name} with toHitFromPos:{toHitFromPos}");

                float heatToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal;
                float stabToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;

                float meleeStatusWeights = 0f;
                if (attackParams.AttackType == AIUtil.AttackType.Melee)
                {
                    float bracedMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal;
                    if (attackParams.TargetIsBraced) { meleeStatusWeights += bracedMeleeMulti; }

                    float evasiveMeleeMulti = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal;
                    if (attackParams.TargetIsEvasive) { meleeStatusWeights += evasiveMeleeMulti; }
                }

                float maxDamage = 0f;
                AmmoModePair maxDamagePair = null;
                Dictionary<AmmoModePair, WeaponFirePredictedEffect> damagePredictions = CleverGirlHelper.gatherDamagePrediction(cWeapon.First, attackerPos, target);
                foreach (KeyValuePair<AmmoModePair, WeaponFirePredictedEffect> kvp in damagePredictions)
                {
                    AmmoModePair ammoModePair = kvp.Key;
                    WeaponFirePredictedEffect weaponFirePredictedEffect = kvp.Value;
                    Mod.Log.Debug($" - Evaluating ammoId: {ammoModePair.ammoId} with modeId: {ammoModePair.modeId}");

                    float enemyDamage = 0f, alliedDamage = 0f, neutralDamage = 0f;
                    foreach (DamagePredictionRecord dpr in weaponFirePredictedEffect.predictDamage)
                    {
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
                            Mod.Log.Debug($" - Weapon will damage on jam, and jam x{weaponFirePredictedEffect.JammChance} of the time. Reducing EV by 1 - jammChance.");
                            dprEV *= (1.0f - weaponFirePredictedEffect.JammChance);
                        }

                        // Check target damage reduction?
                        float armorReduction = 0f;
                        foreach (AmmunitionBox aBox in cWeapon.First.ammoBoxes)
                        {
                            //Mod.Log.Debug($" -- Checking ammo box defId: {aBox.mechComponentRef.ComponentDefID}");
                            if (aBox.mechComponentRef.Def.Is<CleverGirlComponent>(out CleverGirlComponent cgComp) && cgComp.ArmorDamageReduction != 0)
                            {
                                armorReduction = cgComp.ArmorDamageReduction;
                            }
                        }
                        if (armorReduction != 0f)
                        {
                            Mod.Log.Debug($" -- APPLY DAMAGE REDUCTION OF: {armorReduction}");
                        }

                        // TODO: AMS provides a shield to allies


                        // Need to precalc some values on every combatant - 
                        //  find objective targets
                        //  heat to cripple / damage / etc
                        //  stability damage to unsteady / to knockdown

                        // TODO: Can we weight AMS as a weapon when it covers friendlies?

                        Hostility targetHostility = attacker.Combat.HostilityMatrix.GetHostility(attacker.team, dpr.Target.team);
                        if (targetHostility == Hostility.FRIENDLY) { alliedDamage += dprEV; }
                        else if (targetHostility == Hostility.NEUTRAL) { neutralDamage += dprEV; }
                        else { enemyDamage += dprEV; }
                    }
                    float damageEV = enemyDamage + neutralDamage - (alliedDamage * Mod.Config.Weights.FriendlyDamageMulti);
                    Mod.Log.Debug($"  == ammoBox: {ammoModePair.ammoId}_{ammoModePair.modeId} => enemyDamage: {enemyDamage} + neutralDamage: {neutralDamage} - alliedDamage: {alliedDamage} -> damageEV: {damageEV}");
                    if (damageEV >= maxDamage)
                    {
                        maxDamage = damageEV;
                        maxDamagePair = ammoModePair;
                    }
                }
                Mod.Log.Debug($"Max damage from ammoBox: {maxDamagePair.ammoId}_{maxDamagePair.modeId} EV: {maxDamage}");
                cWeapon.ammoAndMode = maxDamagePair;

                //float damagePerShotFromPos = cWeapon.First.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);
                //float heatDamPerShotWeight = cWeapon.First.HeatDamagePerShot * heatToDamRatio;
                //float stabilityDamPerShotWeight = attackParams.TargetIsUnsteady ? cWeapon.First.Instability() * stabToDamRatio : 0f;

                //float meleeStatusWeights = 0f;
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ? 0f : (damagePerShotFromPos * bracedMeleeMulti));
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ? 0f : (damagePerShotFromPos * evasiveMeleeMult));

                //int shotsWhenFired = cWeapon.First.ShotsWhenFired;
                //float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);
                float aggregateDamageEV = maxDamage * cWeapon.weaponsCondensed;
                Mod.Log.Debug($"Aggregate EV = {aggregateDamageEV} == maxDamage: {maxDamage} * weaponsCondensed: {cWeapon.weaponsCondensed}");

                return aggregateDamageEV;
            }
            catch (Exception e)
            {
                Mod.Log.Error("Failed to calculate weapon damageEV!", e);
                return 0f;
            }
        }

        public static void TargetsWithinAoE(AbstractActor attacker, Vector3 position, float radius, 
            out int alliesWithin, out int neutralWithin, out int enemyWithin) {

            alliesWithin = 0;
            neutralWithin = 0;
            enemyWithin = 0;
            foreach (ICombatant target in attacker.Combat.GetAllLivingCombatants()) {
                float distance = (target.CurrentPosition - position).magnitude;
                if (distance <= radius) {
                    Hostility targetHostility = attacker.Combat.HostilityMatrix.GetHostility(attacker.TeamId, target.team?.GUID);
                    if (targetHostility == Hostility.ENEMY) {
                        enemyWithin++;
                    } else if (targetHostility == Hostility.NEUTRAL) {
                        neutralWithin++;
                    } else if (targetHostility == Hostility.FRIENDLY) {
                        alliesWithin++;
                    }
                }
            }

        }

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
