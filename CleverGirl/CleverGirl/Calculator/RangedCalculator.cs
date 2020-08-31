using BattleTech;
using CleverGirl.Objects;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using IRBTModUtils;
using IRBTModUtils.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
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
                List<WeaponAttackEval> damageOptimized = new List<WeaponAttackEval>();
                List<WeaponAttackEval> heatOptimized = new List<WeaponAttackEval>();
                List<WeaponAttackEval> stabOptimized = new List<WeaponAttackEval>();
                for (int i = 0; i < weapons.Count; i++)
                {
                    Weapon weapon = weapons[i];
                    Mod.Log.Debug?.Write($"Optimizing ammoBoxes for weapon: {weapon.UIName}");
                    (WeaponAttackEval dmgWAE, WeaponAttackEval heatWAE, WeaponAttackEval stabWAE) = OptimizeAmmoPairForAttack(weapon, details);
                    damageOptimized.Add(dmgWAE);
                    heatOptimized.Add(heatWAE);
                    stabOptimized.Add(stabWAE);
                }

                // Next, choose a strategy based upon target.
                List<WeaponAttackEval> selectedAttacks = SelectAttackStrategy(details, damageOptimized, heatOptimized, stabOptimized);

                // Now, filter the attacks based upon the attacker
                selectedAttacks = FilterForHeatBudget(selectedAttacks, details);
                selectedAttacks = FilterForAmmo(selectedAttacks, details);
                selectedAttacks = FilterForBreachingShot(selectedAttacks, details);

                // Finally, build an attackEvaluation
                Mod.Log.Debug?.Write("Adding weapons to AE");
                AttackEvaluation ae = new AttackEvaluation()
                {
                    AttackType = AIUtil.AttackType.Shooting,
                    lowestHitChance = 1,
                    WeaponList = new List<Weapon>()
                };
                foreach (WeaponAttackEval wae in selectedAttacks)
                {
                    Mod.Log.Debug?.Write($"  -- adding {wae.Weapon.UIName} to list.");
                    ae.HeatGenerated += wae.Weapon.HeatGenerated;
                    // TODO: This doesn't weight utility damage!
                    ae.ExpectedDamage += wae.EVDirectDmg + wae.EVHeat + wae.EVStab;
                    if (wae.ToHit < ae.lowestHitChance) ae.lowestHitChance = wae.ToHit;
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

        private static List<WeaponAttackEval> SelectAttackStrategy(AttackDetails details, 
            List<WeaponAttackEval> damageOptimized, List<WeaponAttackEval> heatOptimized, List<WeaponAttackEval> stabOptimized)
        {
            List<WeaponAttackEval> selectedAttacks = new List<WeaponAttackEval>();

            float sumDmg = damageOptimized.Sum(x => x.EVDirectDmg + x.EVStructDam);
            if (details.Target is Mech targetMech)
            {
                float heatDmg = heatOptimized.Sum(x => x.EVDirectDmg + x.EVStructDam);
                float sumHeat = heatOptimized.Sum(x => x.EVHeat);
                float evHeat = sumHeat + targetMech.CurrentHeat;
                if (evHeat > Mod.Config.Weights.TargetOverheat.OverheatCriticalLevel)
                {
                    Mod.Log.Debug?.Write($"Target evHeat: {evHeat} > criticalOverheat: {Mod.Config.Weights.TargetOverheat.OverheatCriticalLevel}, weighting evDamage by: {Mod.Config.Weights.TargetOverheat.OverheatCriticalMulti}");
                    heatDmg *= Mod.Config.Weights.TargetOverheat.OverheatCriticalMulti;
                }
                else if (evHeat > Mod.Config.Weights.TargetOverheat.OverheatImpactedLevel)
                {
                    Mod.Log.Debug?.Write($"Target evHeat: {evHeat} > impactedOverheat: {Mod.Config.Weights.TargetOverheat.OverheatImpactedLevel}, weighting evDamage by: {Mod.Config.Weights.TargetOverheat.OverheatImpactedMulti}");
                    heatDmg *= Mod.Config.Weights.TargetOverheat.OverheatImpactedMulti;
                }

                float stabDmg = stabOptimized.Sum(x => x.EVDirectDmg + x.EVStructDam);
                float sumStab = stabOptimized.Sum(x => x.EVStab);
                float evStab = sumStab + targetMech.CurrentStability;
                if (evStab >= targetMech.UnsteadyThreshold && targetMech.EvasivePipsCurrent > 0f)
                {
                    stabDmg += targetMech.EvasivePipsCurrent * Mod.Config.Weights.TargetInstability.DamagePerPipLost;
                }

                if (stabDmg > heatDmg && stabDmg > sumDmg)
                {
                    Mod.Log.Debug?.Write($"Using stab-optimized list as dmg: {stabDmg} > heatDmg: {heatDmg} or sumDmg: {sumDmg}");
                    selectedAttacks.AddRange(stabOptimized);
                }
                else if (heatDmg > sumDmg)
                {
                    Mod.Log.Debug?.Write($"Using heat-optimized list as dmg: {heatDmg} > sumDmg: {sumDmg}");
                    selectedAttacks.AddRange(heatOptimized);
                }
                else
                {
                    Mod.Log.Debug?.Write($"Using damage-optimized list with sumDmg: {sumDmg}");
                    selectedAttacks.AddRange(damageOptimized);
                }
            }
            else
            {
                // Stab damage is ignored, heat damage is converted to extra damage
                float heatDmg = heatOptimized.Sum(x => x.EVDirectDmg + x.EVStructDam);
                float sumHeat = heatOptimized.Sum(x => x.EVHeat);
                heatDmg += sumHeat * Mod.Config.Weights.TargetOverheat.NonMechHeatToDamageMulti;

                if (heatDmg > sumDmg)
                {
                    Mod.Log.Debug?.Write($"Using heat-optimized list as dmg: {heatDmg} > sumDmg: {sumDmg}");
                    selectedAttacks.AddRange(heatOptimized);
                }
                else
                {
                    Mod.Log.Debug?.Write($"Using damage-optimized list with sumDmg: {sumDmg}");
                    selectedAttacks.AddRange(damageOptimized);
                }
            }

            return selectedAttacks;
        }

        private static List<WeaponAttackEval> FilterForHeatBudget(List<WeaponAttackEval> attacks, AttackDetails details)
        {

            if (!(details.Attacker is Mech))
            {
                Mod.Log.Debug?.Write("Attacker is not a mech, returning all weapons from heat filter.");
                return attacks;
            }

            // Build a state for the atacker defining what level of heat/damage is acceptable.
            Mech attackerMech = details.Attacker as Mech;
            float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
            
            // TODO: Improve this / link to CBTBE
            float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech);
            float heatBudget = acceptableHeat - currentHeat;
            Mod.Log.Debug?.Write($"Allowing up to {heatBudget} additional points of heat.");

            // TODO: Allow for BVars influence on this

            // Now start optimizing. First, sort the list by heatratio 
            Mod.Log.Debug?.Write($"Sorting {attacks.Count} weapons by heatRatio");
            attacks.Sort(
                (wae1, wae2) => wae1.DirectDmgPerHeat.CompareTo(wae2.DirectDmgPerHeat)
                );
            Mod.Log.Debug?.Write($" ..Done.");

            // What's the highest damage solution we can get without overheating?
            List<WeaponAttackEval> filteredAttacks = new List<WeaponAttackEval>();
            foreach (WeaponAttackEval wae in attacks)
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

            return filteredAttacks;
        }

        private static List<WeaponAttackEval> FilterForAmmo(List<WeaponAttackEval> attacks, AttackDetails details)
        {
            return attacks
                .Where(x => x.Weapon.HasAmmo && x.ToHit > Mod.Config.Weights.AttackerAmmo.MinHitChance)
                .ToList();
        }

        private static List<WeaponAttackEval> FilterForBreachingShot(List<WeaponAttackEval> attacks, AttackDetails details)
        {
            if (!details.Attacker.HasBreachingShotAbility) return attacks;

            List<WeaponAttackEval> filteredList = new List<WeaponAttackEval>();
            // TODO: Sort by chance to hit / weight weapons with a higher chance to hit?
            attacks.Sort((wae1, wae2) => (wae1.EVDirectDmg + wae1.EVStructDam).CompareTo(wae2.EVDirectDmg + wae2.EVStructDam));

            WeaponAttackEval bShotWAE = attacks.First();
            float bShotDmg = (bShotWAE.EVDirectDmg + bShotWAE.EVStructDam) * Mod.Config.Weights.BreachingShot.Multi;
            float sumDmg = attacks.Sum(x => x.EVDirectDmg + x.EVStructDam);
            Mod.Log.Debug?.Write($" breachingShotDmg: {bShotDmg} vs. sumEVDmg: {sumDmg}");

            // TODO: Normalize toHit chance of sumDmg vs. toHit of bShotDmg before comparing damage
            if (bShotDmg > sumDmg)
            {
                Mod.Log.Debug?.Write($"Breaching shot has more damage, returning a list of one item.");
                filteredList.Add(bShotWAE);
            }
            else
            {
                Mod.Log.Debug?.Write($"Full attack has more damage, returning the entire list.");
                filteredList.AddRange(attacks);
            }

            return filteredList;
        }
        
        // Iterate the ammoModePairs on the weapon to find the highest direct damage, heat damage, and stab damage
        private static (WeaponAttackEval damage, WeaponAttackEval heat, WeaponAttackEval stab) OptimizeAmmoPairForAttack(Weapon weapon, AttackDetails details)
        {

            WeaponAttackEval damage = new WeaponAttackEval() { Weapon = weapon };
            WeaponAttackEval heat = new WeaponAttackEval() { Weapon = weapon };
            WeaponAttackEval stab = new WeaponAttackEval() { Weapon = weapon };

            BehaviorTree bTree = details.Attacker.BehaviorTree;
            try
            {
                float attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal; ;

                // Damage prediction assumes an attack quality of Solid. It doesn't apply targetMultis either
                Dictionary<AmmoModePair, WeaponFirePredictedEffect> damagePredictions = CleverGirlHelper.gatherDamagePrediction(weapon, details.AttackPosition, details.Target);
                foreach (KeyValuePair<AmmoModePair, WeaponFirePredictedEffect> kvp in damagePredictions)
                {
                    AmmoModePair ammoModePair = kvp.Key;
                    WeaponFirePredictedEffect weaponFirePredictedEffect = kvp.Value;
                    Mod.Log.Debug?.Write($" - Evaluating ammoId: {ammoModePair.ammoId} with modeId: {ammoModePair.modeId}");

                    WeaponAttackEval wae = new WeaponAttackEval() { Weapon = weapon, AmmoMode = ammoModePair };
                    foreach (DamagePredictionRecord dpr in weaponFirePredictedEffect.predictDamage)
                    {
                        Hostility targetHostility = SharedState.Combat.HostilityMatrix.GetHostility(details.Attacker.team, dpr.Target.team);
                        if (targetHostility == Hostility.FRIENDLY) 
                        {
                            // Friendly and self damage weights directly into a self damage, and doesn't contribute to any attacks
                            float damageMulti;
                            if (details.Attacker.GUID == dpr.Target.GUID)
                            {
                                damageMulti = Mod.Config.Weights.DamageMultis.Self;
                            }
                            else
                            {
                                damageMulti = Mod.Config.Weights.DamageMultis.Friendly;
                            }

                            wae.EVFriendlyDmg += dpr.ToHit * dpr.Normal * damageMulti;
                            wae.EVFriendlyDmg += dpr.ToHit * dpr.AP * damageMulti;
                            wae.EVFriendlyDmg += dpr.ToHit * dpr.Heat * damageMulti;
                            wae.EVFriendlyDmg += dpr.ToHit * dpr.Instability * damageMulti;
                        }
                        else if (targetHostility == Hostility.NEUTRAL) 
                        {
                            // Neutrals are weighted lower, to emphasize attacking enemies more directly
                            wae.EVDirectDmg += dpr.ToHit * dpr.Normal * Mod.Config.Weights.DamageMultis.Neutral;
                            wae.EVStructDam += dpr.ToHit * dpr.AP * Mod.Config.Weights.DamageMultis.Neutral;
                            wae.EVHeat += dpr.ToHit * dpr.Heat * Mod.Config.Weights.DamageMultis.Neutral;
                            wae.EVStab += dpr.ToHit * dpr.Instability * Mod.Config.Weights.DamageMultis.Neutral;
                        }
                        else 
                        {
                            wae.EVDirectDmg += dpr.ToHit * dpr.Normal;
                            wae.EVStructDam += dpr.ToHit * dpr.AP;
                            wae.EVHeat += dpr.ToHit * dpr.Heat;
                            wae.EVStab += dpr.ToHit * dpr.Instability;
                        }
                        if (!dpr.isAoE && dpr.ToHit > wae.ToHit) wae.ToHit = dpr.ToHit;
                    }

                    if ((wae.EVDirectDmg + wae.EVStructDam) >= (damage.EVDirectDmg + damage.EVStructDam)) damage = wae;
                    if (wae.EVHeat >= heat.EVHeat) heat = wae;
                    if (wae.EVStructDam >= stab.EVStructDam) stab = wae;
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate weapon damageEV!");
            }

            return (damage, heat, stab);
        }
    }
}
