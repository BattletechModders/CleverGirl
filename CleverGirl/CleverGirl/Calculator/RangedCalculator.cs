using BattleTech;
using CleverGirl.Helper;
using CleverGirl.Objects;
using CustAmmoCategories;
using IRBTModUtils.Extension;
using System;
using System.Collections.Generic;
using static AttackEvaluator;

namespace CleverGirl.Calculator
{
    public static class RangedCalculator
    {
        // Optimization means to maximize the total damage output at this time given the weapons list. 
        //   This is within the bounds of acceptable heat, self-damage, etc. and reflect the behavior/role of the attacker
        //   The output should be an AE that's immediately consumable by vanilla AI.
        //
        // This expects that weapons has no melee weapons in it, and that target is valid!
        public static AttackEvaluation OptimizeAttack(List<Weapon> weapons, AbstractActor attacker, ICombatant target)
        {

            try
            {
                /*
                    * 1. Check that weapons, attacker, target is not null
                    * 2. Weapons are already checked for LoF and range
                    * 3. Build an EV and Heat ratio for each weapon
                    * 4. Evaluate heat - sum all heat values, but drop weapons step by step until below safe threshold
                    */

                Mod.Log.Debug?.Write($"Generating ranged AEs for {attacker.DistinctId()} vs. {target.DistinctId()}");
                AttackDetails details = new AttackDetails(attackType: AIUtil.AttackType.Shooting, attacker: attacker,
                    target: target as AbstractActor, attackPos: attacker.CurrentPosition, targetPos: target.CurrentPosition,
                    weaponCount: weapons.Count, useRevengeBonus: true);

                // Build a list of all possible outcomes from shooting
                List<WeaponAttackEval> weaponAttackEvals = new List<WeaponAttackEval>();
                for (int i = 0; i < weapons.Count; i++)
                {
                    Weapon weapon = weapons[i];
                    weaponAttackEvals.Add(EvaluateShootingAttack(weapon, details));
                }

                List<WeaponAttackEval> allowedWeapons = new List<WeaponAttackEval>();
                // If the attacker is a mech, filter attacks to an acceptable heat level
                if (attacker is Mech attackerMech)
                {
                    // Build a state for the atacker defining what level of heat/damage is acceptable.
                    float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
                    // TODO: Improve this / link to CBTBE
                    float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech);
                    float heatBudget = acceptableHeat - currentHeat;
                    Mod.Log.Debug?.Write($"Allowing up to {heatBudget} additional points of heat.");

                    // Now start optimizing. First, sort the list by heatratio 
                    Mod.Log.Debug?.Write($"Sorting {weaponAttackEvals.Count} weapons by heatRatio");
                    weaponAttackEvals.Sort(
                        (wae1, wae2) => wae1.DamagePerHeatRatio.CompareTo(wae2.DamagePerHeatRatio)
                        );
                    Mod.Log.Debug?.Write($" ..Done.");

                    // What's the highest damage solution we can get without overheating?
                    foreach (WeaponAttackEval wae in weaponAttackEvals)
                    {
                        Mod.Log.Debug?.Write($"Evaluating weapon {wae?.Weapon?.UIName} with generated heat: {wae?.Weapon?.HeatGenerated} versus budget");
                        if (wae.Weapon.HeatGenerated <= heatBudget)
                        {
                            Mod.Log.Debug?.Write($"Adding weapon {wae.Weapon.UIName}");
                            allowedWeapons.Add(wae);
                            heatBudget -= wae.Weapon.HeatGenerated;
                        }
                        else
                        {
                            Mod.Log.Debug?.Write($"Skipping weapon {wae.Weapon.UIName}");
                        }
                    }
                    Mod.Log.Debug?.Write("Done budgeting weapons");

                    // What weapons contribute to overheating but have a very low hit chance?
                    // What weapons contribute to overheating but have a high hit chance - and are they worth it?
                    // TODO: Need to handle HasBreachingShotAbility. Check to see if a single weapon that blows through cover is better. Start with highest dam weapon.

                }
                else
                {
                    // TODO: Need to handle HasBreachingShotAbility. Check to see if a single weapon that blows through cover is better. Start with highest dam weapon.

                    // Use the full list? Or sort ammo by chance to hit?
                    allowedWeapons.AddRange(weaponAttackEvals);
                }

                Mod.Log.Debug?.Write("Adding weapons to AE");
                AttackEvaluation ae = new AttackEvaluation() 
                {
                    AttackType = AIUtil.AttackType.Shooting,
                    lowestHitChance = 1,
                    WeaponList = new List<Weapon>()
                };

                foreach (WeaponAttackEval wae in allowedWeapons)
                {
                    Mod.Log.Debug?.Write($"  -- adding {wae.Weapon.UIName} to list.");
                    ae.HeatGenerated += wae.Weapon.HeatGenerated;
                    // TODO: This doesn't weight utility damage!
                    ae.ExpectedDamage += wae.ExpectedDamage + wae.ExpectedHeatDamage + wae.ExpectedStabDamage;                    
                    if (wae.ChanceToHit < ae.lowestHitChance) ae.lowestHitChance = wae.ChanceToHit;
                    ae.WeaponList.Add(wae.Weapon);
                }

                Mod.Log.Debug?.Write($"Returning AE with {ae.WeaponList.Count} weapons.");
                return ae;
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to optimize ranged attack due to error!");
                return new AttackEvaluation() { AttackType = AIUtil.AttackType.Shooting };
            }

        }

        private static WeaponAttackEval EvaluateShootingAttack(Weapon weapon, AttackDetails details)
        {

            WeaponAttackEval eval = new WeaponAttackEval();
            eval.Weapon = weapon;

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
                eval.ChanceToHit = toHitFromPos;

                // Evaluate the best weapon mode (i.e. max damage)
                float heatToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal;
                float stabToDamRatio = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;
                WeaponHelper.DetermineMaxDamageAmmoModePair(weapon, details, heatToDamRatio, stabToDamRatio,
                    out float maxDamage, out AmmoModePair maxDamagePair);
                Mod.Log.Debug?.Write($" -- Max damage from ammoBox: {maxDamagePair.ammoId}_{maxDamagePair.modeId} EV: {maxDamage}");
                eval.OptimalAmmoMode = maxDamagePair;

                // Account for the jumping modifier
                float damagePerShot = weapon.DamagePerShotAdjusted();
                float weaponDamageMulti = details.DamageMultiForWeapon(weapon);
                float totalDamage = damagePerShot * weapon.ShotsWhenFired * weaponDamageMulti;
                Mod.Log.Debug?.Write($" -- totalDamage: {totalDamage} = damagePerShotAdjusted: {damagePerShot} x shotsWhenFired: {weapon.ShotsWhenFired} x weaponDamageMulti: {weaponDamageMulti}");                
                eval.ExpectedDamage = maxDamage * toHitFromPos;
                Mod.Log.Debug?.Write($" -- ExpectedDamage: {eval.ExpectedDamage} = totalDamage: {totalDamage} x toHitFromPos: {toHitFromPos} ");

                // NOTE: Heat and Stability damage do NOT seem to be impacted by attack multipliers. See Mech.ResolveWeaponDamage

                float totalHeatDamage = weapon.HeatDamagePerShot * weapon.ShotsWhenFired;
                // TODO: Does this account for AOE?
                eval.ExpectedHeatDamage = totalHeatDamage * toHitFromPos;
                Mod.Log.Debug?.Write($" -- expectedHeatDamage: {eval.ExpectedHeatDamage} = totalHeatDamage: {totalHeatDamage} x toHitFromPos: {toHitFromPos} ");

                float totalStabDamage = weapon.Instability() * weapon.ShotsWhenFired;
                // TODO: Does this account for AOE?
                eval.ExpectedStabDamage = totalStabDamage;
                Mod.Log.Debug?.Write($" -- expectedStabDamage: {eval.ExpectedStabDamage} = totalStabDamage: {totalStabDamage} x toHitFromPos: {toHitFromPos} ");

                // TODO: Calculate some value for this, if you're in the AE?
                eval.ExpectedSelfDamage = 0f;

            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
            }

            return eval;
        }
    }
}
