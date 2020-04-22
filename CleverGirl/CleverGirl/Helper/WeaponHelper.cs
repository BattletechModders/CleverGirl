using BattleTech;
using CleverGirl.Components;
using CleverGirl.Objects;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using CustomComponents;
using System;
using System.Collections.Generic;
using UnityEngine;
using static CleverGirl.AIHelper;

namespace CleverGirl.Helper
{
    public static class WeaponHelper
    {

        public static void FilterWeapons(AbstractActor attacker, ICombatant target,
            out List<Weapon> rangedWeapons, out List<Weapon> meleeWeapons, out List<Weapon> dfaWeapons)
        {
            rangedWeapons = new List<Weapon>();
            meleeWeapons = new List<Weapon>();
            dfaWeapons = new List<Weapon>();

            if (attacker == null) return;

            List<Weapon> allWeapons = new List<Weapon>();
            foreach (Weapon weap in attacker.Weapons)
            {
                // TODO: Ammo check should be more refined - needs to check ammo types, ammo cost across multiple weapons of the same type
                // Checks if weapon is disabled and has ammo
                if (weap.CanFire)
                {
                    allWeapons.Add(weap);
                }
                else
                {
                    Mod.Log.Debug($" Weapon ({weap.defId}) is disabled or out of ammo.");
                }
            }

            Mech attackerMech = (Mech)attacker;
            if (attackerMech != null)
            {
                Mod.Log.Debug($" Adding melee weapon {attackerMech.MeleeWeapon.defId}");
                meleeWeapons.Add(attackerMech.MeleeWeapon);

                Mod.Log.Debug($" Adding DFA weapon {attackerMech.DFAWeapon.defId}");
                dfaWeapons.Add(attackerMech.DFAWeapon);
            }

            float distance = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            foreach (Weapon weap in allWeapons)
            {
                Mod.Log.Debug($" Checking weapon ({weap.defId})");

                // Check for LOF and within range
                bool willFireAtTarget = weap.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= weap.MaxRange;
                if (willFireAtTarget && withinRange)
                {
                    Mod.Log.Debug($" -- Has LOF and is within range, adding as a ranged weapon.");
                    rangedWeapons.Add(weap);
                }
                else
                {
                    Mod.Log.Debug($" -- Has not LOF is out of range; maxRange: {weap.MaxRange} < {distance}");
                }

                if (attackerMech != null && weap.WeaponCategoryValue.IsSupport)
                {
                    Mod.Log.Debug($" -- Is a support weapon, adding to melee and DFA sets");
                    meleeWeapons.Add(weap);
                    dfaWeapons.Add(weap);
                } 
            }

        }

        // Calculate the expected value for a given weapon against the target
        public static float CalculateWeaponDamageEV(Weapon weapon, AbstractActor attacker, ICombatant target, AttackParams attackParams)
        {

            BehaviorTree bTree = attacker.BehaviorTree;
            Vector3 attackerPos = attacker.CurrentPosition;
            Vector3 targetPos = target.CurrentPosition;

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
                            if (attackingMech != null && weapon == attackingMech.MeleeWeapon)
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
                            if (attackerMech != null && weapon == attackerMech.DFAWeapon)
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

                float toHitFromPos = weapon.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
                if (attackParams.IsBreachingShotAttack)
                {
                    // Breaching shot is assumed to auto-hit... why?
                    toHitFromPos = 1f;
                }
                Mod.Log.Debug($"Evaluating weapon: {weapon.Name} with toHitFromPos:{toHitFromPos}");

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

                DetermineMaxDamageAmmoModePair(weapon, attackParams, attacker, attackerPos, target, heatToDamRatio, stabToDamRatio, 
                    out float maxDamage, out AmmoModePair maxDamagePair);
                Mod.Log.Debug($"Max damage from ammoBox: {maxDamagePair.ammoId}_{maxDamagePair.modeId} EV: {maxDamage}");
                weapon.ammoAndMode = maxDamagePair;

                //float damagePerShotFromPos = cWeapon.First.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);
                //float heatDamPerShotWeight = cWeapon.First.HeatDamagePerShot * heatToDamRatio;
                //float stabilityDamPerShotWeight = attackParams.TargetIsUnsteady ? cWeapon.First.Instability() * stabToDamRatio : 0f;

                //float meleeStatusWeights = 0f;
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ? 0f : (damagePerShotFromPos * bracedMeleeMulti));
                //meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ? 0f : (damagePerShotFromPos * evasiveMeleeMult));

                //int shotsWhenFired = cWeapon.First.ShotsWhenFired;
                //float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);
                float aggregateDamageEV = maxDamage * weapon.weaponsCondensed;
                Mod.Log.Debug($"Aggregate EV = {aggregateDamageEV} == maxDamage: {maxDamage} * weaponsCondensed: {weapon.weaponsCondensed}");

                return aggregateDamageEV;
            }
            catch (Exception e)
            {
                Mod.Log.Error("Failed to calculate weapon damageEV!", e);
                return 0f;
            }
        }

        private static void DetermineMaxDamageAmmoModePair(Weapon weapon, AttackParams attackParams, AbstractActor attacker, Vector3 attackerPos,
                    ICombatant target, float heatToDamRatio, float stabToDamRatio, out float maxDamage, out AmmoModePair maxDamagePair)
        {
            maxDamage = 0f;
            maxDamagePair = null;
            Dictionary<AmmoModePair, WeaponFirePredictedEffect> damagePredictions = CleverGirlHelper.gatherDamagePrediction(weapon, attackerPos, target);
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
                    foreach (AmmunitionBox aBox in weapon.ammoBoxes)
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
        }

    }
}
