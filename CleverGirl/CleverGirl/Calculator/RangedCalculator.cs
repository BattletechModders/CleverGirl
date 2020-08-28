using BattleTech;
using CleverGirl.Helper;
using CleverGirl.Objects;
using CustAmmoCategories;
using IRBTModUtils;
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
        public static AttackEvaluation OptimizeAttack(AttackDetails details, List<Weapon> weapons)
        {

            try
            {
                /*
                    * 1. Check that weapons, attacker, target is not null
                    * 2. Weapons are already checked for LoF and range
                    * 3. Build an EV and Heat ratio for each weapon
                    * 4. Evaluate heat - sum all heat values, but drop weapons step by step until below safe threshold
                    */

                Mod.Log.Debug?.Write($"Generating ranged AEs for {details.Attacker.DistinctId()} vs. {details.Target.DistinctId()}");

                // Build a list of all possible outcomes from shooting
                List<WeaponAttackEval> standardEvals = new List<WeaponAttackEval>();
                List<WeaponAttackEval> breachShotEvals = new List<WeaponAttackEval>();
                for (int i = 0; i < weapons.Count; i++)
                {
                    Weapon weapon = weapons[i];
                    standardEvals.Add(EvaluateShootingAttack(weapon, details, false));
                    standardEvals.Add(EvaluateShootingAttack(weapon, details, true));
                }

                // TODO: Need to handle HasBreachingShotAbility. Make two lists, prioritize one by damage and apply breaching solid quality (SOLID)
                //   The other contains a filtered list by heat or other semantics
                List<WeaponAttackEval> standardFilteredWeapons = FilterForStandardAttack(standardEvals, details);
                List<WeaponAttackEval> breachingShotWeapons = FilterForBreachingShotAttack(standardEvals, details);
     

                Mod.Log.Debug?.Write("Adding weapons to AE");
                AttackEvaluation ae = new AttackEvaluation() 
                {
                    AttackType = AIUtil.AttackType.Shooting,
                    lowestHitChance = 1,
                    WeaponList = new List<Weapon>()
                };

                // heatDmgToOverheatTarget - gate for attack sets that would overheat the target
                // stabDmgToUnsteadyTarget - gate for attack sets that would knockdown the target
                // armorReduction bonii - benefit for armor reduction from an attack set


                foreach (WeaponAttackEval wae in allowedWeapons)
                {
                    Mod.Log.Debug?.Write($"  -- adding {wae.Weapon.UIName} to list.");
                    ae.HeatGenerated += wae.Weapon.HeatGenerated;
                    // TODO: This doesn't weight utility damage!
                    ae.ExpectedDamage += wae.EVDirectDmg + wae.EVHeatDmg + wae.EVStabDmg;                    
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

        private static List<WeaponAttackEval> FilterForStandardAttack(List<WeaponAttackEval> attackEvals, AttackDetails details)
        {
            List<WeaponAttackEval> filteredAttacks = new List<WeaponAttackEval>();

            // If the attacker is a mech, filter attacks to an acceptable heat level
            if (details.Attacker is Mech attackerMech)
            {
                // Build a state for the atacker defining what level of heat/damage is acceptable.
                float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
                // TODO: Improve this / link to CBTBE
                float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech);
                float heatBudget = acceptableHeat - currentHeat;
                Mod.Log.Debug?.Write($"Allowing up to {heatBudget} additional points of heat.");

                // Now start optimizing. First, sort the list by heatratio 
                Mod.Log.Debug?.Write($"Sorting {attackEvals.Count} weapons by heatRatio");
                attackEvals.Sort(
                    (wae1, wae2) => wae1.DirectDmgPerHeat.CompareTo(wae2.DirectDmgPerHeat)
                    );
                Mod.Log.Debug?.Write($" ..Done.");

                // What's the highest damage solution we can get without overheating?
                foreach (WeaponAttackEval wae in attackEvals)
                {
                    Mod.Log.Debug?.Write($"Evaluating weapon {wae?.Weapon?.UIName} with generated heat: {wae?.Weapon?.HeatGenerated} versus budget");
                    if (wae.Weapon.HeatGenerated <= heatBudget)
                    {
                        Mod.Log.Debug?.Write($"Adding weapon {wae.Weapon.UIName}");
                        filteredAttacks.Add(wae);
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

            }
            else
            {

                // Use the full list? Or sort ammo by chance to hit?
                filteredAttacks.AddRange(attackEvals);
            }

            return attackEvals;
        }

        private static List<WeaponAttackEval> FilterForBreachingShotAttack(List<WeaponAttackEval> attackEvals, AttackDetails details)
        {
            List<WeaponAttackEval> filteredAttacks = new List<WeaponAttackEval>();

            attackEvals.Sort(
                (wae1, wae2) => wae1.EVUtilityDmg)
            // If the attacker is a mech, filter attacks to an acceptable heat level
            if (details.Attacker is Mech attackerMech)
            {
                // Build a state for the atacker defining what level of heat/damage is acceptable.
                float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
                // TODO: Improve this / link to CBTBE
                float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech);
                float heatBudget = acceptableHeat - currentHeat;
                Mod.Log.Debug?.Write($"Allowing up to {heatBudget} additional points of heat.");

                // Now start optimizing. First, sort the list by heatratio 
                Mod.Log.Debug?.Write($"Sorting {attackEvals.Count} weapons by heatRatio");
                attackEvals.Sort(
                    (wae1, wae2) => wae1.DirectDmgPerHeat.CompareTo(wae2.DirectDmgPerHeat)
                    );
                Mod.Log.Debug?.Write($" ..Done.");

                // What's the highest damage solution we can get without overheating?
                foreach (WeaponAttackEval wae in attackEvals)
                {
                    Mod.Log.Debug?.Write($"Evaluating weapon {wae?.Weapon?.UIName} with generated heat: {wae?.Weapon?.HeatGenerated} versus budget");
                    if (wae.Weapon.HeatGenerated <= heatBudget)
                    {
                        Mod.Log.Debug?.Write($"Adding weapon {wae.Weapon.UIName}");
                        filteredAttacks.Add(wae);
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

            }
            else
            {

                // Use the full list? Or sort ammo by chance to hit?
                filteredAttacks.AddRange(attackEvals);
            }

            return attackEvals;
        }

        private static WeaponAttackEval EvaluateShootingAttack(Weapon weapon, AttackDetails details, bool isBreachingShot)
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

                AttackImpactQuality quality = details.BaseRangedImpactQuality;
                if (isBreachingShot)
                {
                    // HBS AI assumes breaching shot will hit... why?
                    Mod.Log.Debug?.Write($"Breaching shot detected, assuming to hit will be 1.0 instead of calculated: {toHitFromPos}");
                    toHitFromPos = 1f;
                    // Breaching shots always are treated as solid impacts
                    quality = AttackImpactQuality.Solid;
                }
                float impactQualityMulti = SharedState.Combat.ToHit.GetBlowQualityMultiplier(details.BaseRangedImpactQuality);

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
                float weaponDamageMulti = details.DamageMultiForWeapon(weapon) * impactQualityMulti;
                float totalDirectDamage = weapon.DamagePerShot * weapon.ShotsWhenFired * weaponDamageMulti;
                Mod.Log.Debug?.Write($" -- totalDirectDamage: {totalDirectDamage} = damagePerShotAdjusted: {weapon.DamagePerShot} x shotsWhenFired: {weapon.ShotsWhenFired} " +
                    $"x weaponDamageMulti: {weaponDamageMulti} x impactQualityMulti: {impactQualityMulti}");                
                eval.EVDirectDmg = maxDamage * toHitFromPos;
                Mod.Log.Debug?.Write($" -- directDamageEV: {eval.EVDirectDmg} = totalDamage: {totalDirectDamage} x toHitFromPos: {toHitFromPos} ");

                // If the weapon does structure damage, record that as well.


                // NOTE: Heat and Stability damage do NOT seem to be impacted by attack multipliers. See Mech.ResolveWeaponDamage

                float totalHeatDamage = weapon.HeatDamagePerShot * weapon.ShotsWhenFired;
                // TODO: Does this account for AOE?
                eval.EVHeatDmg = totalHeatDamage * toHitFromPos;
                Mod.Log.Debug?.Write($" -- expectedHeatDamage: {eval.EVHeatDmg} = totalHeatDamage: {totalHeatDamage} x toHitFromPos: {toHitFromPos} ");

                float totalStabDamage = weapon.Instability() * weapon.ShotsWhenFired;
                // TODO: Does this account for AOE?
                eval.EVStabDmg = totalStabDamage;
                Mod.Log.Debug?.Write($" -- expectedStabDamage: {eval.EVStabDmg} = totalStabDamage: {totalStabDamage} x toHitFromPos: {toHitFromPos} ");

                // TODO: Calculate some value for this, if you're in the AE?
                eval.EVSelfDmg = 0f;

            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
            }

            return eval;
        }
    }
}
