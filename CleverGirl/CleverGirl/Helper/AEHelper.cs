using CleverGirl.Analytics;
using CleverGirl.Helper;
using CleverGirlAIDamagePrediction;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CleverGirl.Objects;
using CustAmmoCategories;
using UnityEngine;

namespace CleverGirl
{

    public static class AEHelper
    {

        // Initialize any decision-making data necessary to make an attack order. Fetch the current state of opponents
        //  and cache it for quick look-up
        public static void InitializeAttackOrderDecisionData(AbstractActor unit)
        {
            Mod.Log.Trace?.Write("AE:MAO:pre - entered.");
            ModState.BehaviorVarValuesCache.Clear();

            // Reset the analytics cache
            ModState.CombatantAnalytics.Clear();

            ModState.CurrentActorAllies.Clear();
            ModState.CurrentActorNeutrals.Clear();
            ModState.CurrentActorEnemies.Clear();

            // Prime the caches with information about all targets
            Mod.Log.Debug?.Write($"Evaluating all actors for hostility to {unit.DistinctId()}");
            foreach (ICombatant combatant in unit.Combat.GetAllImporantCombatants())
            {
                if (combatant.GUID == unit.GUID) { continue; }

                // Will only include alive actors and buildings that are 'tab' targets
                if (unit.Combat.HostilityMatrix.IsFriendly(unit.team, combatant.team))
                {
                    ModState.CurrentActorAllies[combatant.GUID] = combatant;
                    Mod.Log.Debug?.Write($"  -- actor: {combatant.DistinctId()} is an ally.");
                }
                else if (unit.Combat.HostilityMatrix.IsEnemy(unit.team, combatant.team))
                {
                    ModState.CurrentActorEnemies[combatant.GUID] = combatant;
                    Mod.Log.Debug?.Write($"  -- actor: {combatant.DistinctId()} is an enemy.");
                }
                else
                {
                    ModState.CurrentActorNeutrals[combatant.GUID] = combatant;
                    Mod.Log.Debug?.Write($"  -- actor: {combatant.DistinctId()} is neutral.");
                }

                // Add the combatant to the analytics
                ModState.CombatantAnalytics[combatant.GUID] = new CombatantAnalytics(combatant);
            }

            // TODO: Evaluate objectives
            ModState.LocalPlayerEnemyObjective.Clear();
            ModState.LocalPlayerFriendlyObjective.Clear();
        }

        public static AbstractActor FilterEnemyUnitsToDesignatedTarget(AITeam aiteam, Lance attackerLance, List<ICombatant> enemyUnits)
        {
            AbstractActor designatedTarget = null;
            if (aiteam != null && aiteam.DesignatedTargetForLance.ContainsKey(attackerLance))
            {
                designatedTarget = aiteam.DesignatedTargetForLance[attackerLance];
                if (designatedTarget != null && !designatedTarget.IsDead)
                {
                    for (int i = 0; i < enemyUnits.Count; i++)
                    {
                        if (enemyUnits[i] == designatedTarget)
                        {
                            designatedTarget = enemyUnits[i] as AbstractActor;
                            break;
                        }
                    }
                }
            }
            return designatedTarget;
        }

        public static List<AmmoModeAttackEvaluation> EvaluateAttacks(AbstractActor unit, ICombatant target,
            List<List<CondensedWeaponAmmoMode>>[] weaponSetListByAttack, Vector3 attackPosition, Vector3 targetPosition,
            bool targetIsEvasive)
        {

            ConcurrentBag<AmmoModeAttackEvaluation> allResults = new ConcurrentBag<AmmoModeAttackEvaluation>();

            // List 0 is ranged weapons, 1 is melee+support, 2 is DFA+support
            for (int i = 0; i < 3; i++)
            {
                List<List<CondensedWeaponAmmoMode>> weaponSetsByAttackType = weaponSetListByAttack[i];
                string attackLabel = "ranged attack";
                if (i == 1) { attackLabel = "melee attacks"; }
                if (i == 2) { attackLabel = "DFA attacks"; }
                Mod.Log.Debug?.Write($"Evaluating {weaponSetsByAttackType.Count} {attackLabel}");

                if (weaponSetsByAttackType != null)
                {
                    AIHelper.ClearCaches();
                    
                    foreach (var weaponList in weaponSetsByAttackType)
                    {
                        Mod.Log.Trace?.Write($"Evaluating {weaponList.Count} weapons for a {attackLabel}");
                        AmmoModeAttackEvaluation attackEvaluation = new AmmoModeAttackEvaluation();
                        attackEvaluation.AttackType = (AIUtil.AttackType)i;
                        attackEvaluation.HeatGenerated = (float)AIHelper.HeatForAttack(weaponList);

                        if (unit is Mech mech)
                        {
                            attackEvaluation.HeatGenerated += (float)mech.TempHeat;
                            attackEvaluation.HeatGenerated -= (float)mech.AdjustedHeatsinkCapacity;
                        }

                        attackEvaluation.ExpectedDamage = AIHelper.ExpectedDamageForAttack(unit, attackEvaluation.AttackType, weaponList, target, attackPosition, targetPosition, true, unit, out var isArtilleryAttack);
                        attackEvaluation.lowestHitChance = AIHelper.LowestHitChance(weaponList, target, attackPosition, targetPosition, targetIsEvasive);

                        // Expand the list to all weaponDefs with ammoMode, not our condensed ones
                        Mod.Log.Trace?.Write($"Expanding weapon list for AttackEvaluation");
                        Dictionary<Weapon, AmmoModePair> aeWeaponList = new Dictionary<Weapon, AmmoModePair>();
                        foreach (CondensedWeaponAmmoMode cWeapon in weaponList!)
                        {
                            List<Weapon> cWeapons = cWeapon.condensedWeapons;
                            foreach (var weapon in cWeapons)
                            {
                                if (isArtilleryAttack)
                                {
                                    cWeapon.ApplyAmmoMode();
                                    if (!weapon.IsArtillery())
                                    {
                                        continue;
                                    }
                                    cWeapon.RestoreBaseAmmoMode();
                                }
                                aeWeaponList.Add(weapon, cWeapon.ammoModePair);
                            }
                        }
                        Mod.Log.Trace?.Write($"List size {weaponList?.Count} was expanded to: {aeWeaponList.Count}");
                        attackEvaluation.WeaponList = aeWeaponList;
                        if (!ContainsSimilarAttack(allResults, attackEvaluation)) {
                            Mod.Log.Trace?.Write($"Adding new attackEvaluation: {attackEvaluation}");
                            allResults.Add(attackEvaluation);
                        }
                        else
                        {
                            Mod.Log.Trace?.Write($"Skipping duplicate attackEvaluation: {attackEvaluation}");
                        }
                    }
                }
            }

            List<AmmoModeAttackEvaluation> sortedResults = new List<AmmoModeAttackEvaluation>();
            sortedResults.AddRange(allResults);
            sortedResults.Sort((a, b) => a.CompareTo(b));
            sortedResults.Reverse();

            return sortedResults;
        }

        private static bool ContainsSimilarAttack(ConcurrentBag<AmmoModeAttackEvaluation> bag, AmmoModeAttackEvaluation evaluation)
        {
            return bag.Any(bagEvaluation => bagEvaluation.AttackType.Equals(evaluation.AttackType) 
                                            && bagEvaluation.HeatGenerated.Equals(evaluation.HeatGenerated) 
                                            && bagEvaluation.ExpectedDamage.Equals(evaluation.ExpectedDamage) 
                                            && bagEvaluation.lowestHitChance.Equals(evaluation.lowestHitChance));
        }

        public static bool MeleeDamageOutweighsRisk(float attackerMeleeDam, Mech attacker, ICombatant target)
        {

            if (attackerMeleeDam <= 0f)
            {
                Mod.Log.Debug?.Write("Attacker has no expected damage, melee is too risky.");
                return false;
            }

            Mech targetMech = target as Mech;
            if (targetMech == null)
            {
                Mod.Log.Debug?.Write("Target has no expected damage, melee is safe.");
                return true;
            }

            // Use the target mech's position, because if we melee the attacker they can probably get to us
            float targetMeleeDam = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(targetMech, attacker, targetMech.CurrentPosition, targetMech.CurrentPosition, false, attacker);
            float meleeDamageRatio = attackerMeleeDam / targetMeleeDam;
            float meleeDamageRatioCap = BehaviorHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal;
            Mod.Log.Debug?.Write($" meleeDamageRatio: {meleeDamageRatio} = target: {targetMeleeDam} / attacker: {attackerMeleeDam} vs. cap: {meleeDamageRatioCap}");

            return meleeDamageRatio > meleeDamageRatioCap;
        }

        // Make multiple sets of ranged weapons, to allow for selection of the optimal set based on range
        public static List<List<CondensedWeaponAmmoMode>> MakeRangedWeaponSets(List<CondensedWeapon> potentialWeapons,
            ICombatant target, Vector3 attackPosition)
        {
            // First, filter weapons with ammoModes that won't fire
            List<CondensedWeapon> condensedWeaponAmmoModes = new List<CondensedWeapon>();
            float distance = (attackPosition - target.CurrentPosition).magnitude;
            foreach (CondensedWeapon cWeap in potentialWeapons)
            {
                Weapon wep = cWeap.First;
                
                // Decided if need to check one-shot case. CAC combines the JSON field "StartingAmmoCapacity" with the JSON object InternalAmmo, so checking the latter covers both.
                bool hasInternalAmmo = wep.exDef().isHaveInternalAmmo;
                
                AmmoModePair currentAmmoMode = wep.getCurrentAmmoMode();
                List<AmmoModePair> validAmmoModes = new List<AmmoModePair>();
                foreach (AmmoModePair ammoMode in cWeap.ammoModes)
                {
                    wep.ApplyAmmoMode(ammoMode);
                    if (distance < wep.MinRange)
                    {
                        Mod.Log.Debug?.Write($" Skipping ammoMode {ammoMode} for {wep.UIName} in ranged set as distance: {distance} < minRange: {wep.MinRange}");
                        continue;
                    }
                   
                        //Does the current mode use internal ammo and does it have enough for just a single shot?
                        //This can be a false positive if the mode had more internal ammo originally but now has exactly enough ammunition left for a single shot. But that is probably a good case for the AI To be a bit more hesitant either way.
                        if (hasInternalAmmo && wep.mode().AmmoCategory.BaseCategory.UsesInternalAmmo && wep.CurrentAmmo == wep.mode().ShotsWhenFired)
                        {
                            float toHitFromPosition = cWeap.First.GetToHitFromPosition(target, 1, attackPosition,
                                target.CurrentPosition, true, true, false);
                            if (toHitFromPosition < Mod.Config.Weights.OneShotMinimumToHit)
                            {
                                Mod.Log.Debug?.Write($" Skipping ammo mode {ammoMode} for {wep.UIName} in ranged set as toHitFromPosition: {toHitFromPosition} is below OneShotMinimumToHit: {Mod.Config.Weights.OneShotMinimumToHit}");
                                continue;
                            }
                        }
                    
                    validAmmoModes.Add(ammoMode);
                    
                    //TODO: Add more things to check for ammoMode?
                }
                
                wep.ApplyAmmoMode(currentAmmoMode);
                wep.ResetTempAmmo();
                
                if (validAmmoModes.Any())
                {
                    cWeap.ammoModes = validAmmoModes;
                    condensedWeaponAmmoModes.Add(cWeap);
                }
                else
                {
                    Mod.Log.Debug?.Write($" Skipping weapon {wep.UIName} in ranged set due to no valid ammoModes");
                }
            }

            return MakeWeaponAmmoModeSets(condensedWeaponAmmoModes);

        }

         public static List<List<CondensedWeaponAmmoMode>> MakeWeaponAmmoModeSets(List<CondensedWeapon> weapons)
        {
            var permutations = new List<List<CondensedWeaponAmmoMode>>();
            var currentPermutation = new List<CondensedWeaponAmmoMode>();
            var seenPermutations = new HashSet<List<CondensedWeaponAmmoMode>>(new PermutationComparer());

            GeneratePermutations(weapons, 0, currentPermutation, permutations, seenPermutations);

            return permutations;
        }

        private static void GeneratePermutations(List<CondensedWeapon> weapons, int weaponIndex,
            List<CondensedWeaponAmmoMode> currentPermutation,
            List<List<CondensedWeaponAmmoMode>> permutations,
            HashSet<List<CondensedWeaponAmmoMode>> seenPermutations)
        {
            if (weaponIndex == weapons.Count)
            {
                // Add a copy to the set of seen permutations
                if (seenPermutations.Add(currentPermutation.ToList()))
                {
                    // Add a copy to the list of permutations is not seen
                    permutations.Add(currentPermutation.ToList()); 
                }

                return;
            }

            CondensedWeapon currentWeapon = weapons[weaponIndex];

            foreach (AmmoModePair ammoModePair in currentWeapon.ammoModes)
            {
                currentPermutation.Add(new CondensedWeaponAmmoMode(currentWeapon, ammoModePair));
                GeneratePermutations(weapons, weaponIndex + 1, currentPermutation, permutations, seenPermutations);
                currentPermutation.RemoveAt(currentPermutation.Count - 1); // Remove the last item to backtrack
            }

            // Continue to the next weapon without adding any ammo mode pairs from the current weapon
            GeneratePermutations(weapons, weaponIndex + 1, currentPermutation, permutations, seenPermutations);
        }

        // Custom comparer for list of permutations to ignore ordering
        private class PermutationComparer : IEqualityComparer<List<CondensedWeaponAmmoMode>>
        {
            public bool Equals(List<CondensedWeaponAmmoMode> x, List<CondensedWeaponAmmoMode> y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                
                // Check if the counts are equal
                if (x.Count != y.Count)
                    return false;

                // Check if every element in x has a corresponding equal element in y
                return x.All(y.Contains);
            }

            public int GetHashCode(List<CondensedWeaponAmmoMode> obj)
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var item in obj)
                    {
                        hash = hash * 31 + item.GetHashCode();
                    }
                    return hash;
                }
            }
        }
    }
}
