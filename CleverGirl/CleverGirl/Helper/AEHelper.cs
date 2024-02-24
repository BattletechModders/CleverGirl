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
                    foreach (var weaponList in weaponSetsByAttackType)
                    {
                        Mod.Log.Debug?.Write($"Evaluating {weaponList.Count} weapons for a {attackLabel}");
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
                        Mod.Log.Debug?.Write($"Expanding weapon list for AttackEvaluation");
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
                        Mod.Log.Debug?.Write($"List size {weaponList?.Count} was expanded to: {aeWeaponList.Count}");
                        attackEvaluation.WeaponList = aeWeaponList;
                        allResults.Add(attackEvaluation);
                    }
                }
            }

            List<AmmoModeAttackEvaluation> sortedResults = new List<AmmoModeAttackEvaluation>();
            sortedResults.AddRange(allResults);
            sortedResults.Sort((a, b) => a.CompareTo(b));
            sortedResults.Reverse();

            return sortedResults;
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

                    // Check one-shot weapons for accuracy
                    if (wep.weaponDef.StartingAmmoCapacity == wep.weaponDef.ShotsWhenFired)
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
            List<List<CondensedWeaponAmmoMode>> permutations = new List<List<CondensedWeaponAmmoMode>>();
            GeneratePermutations(weapons, 0, new List<CondensedWeaponAmmoMode>(), permutations);
            return permutations;
        }

        private static void GeneratePermutations(List<CondensedWeapon> weapons, int index, List<CondensedWeaponAmmoMode> currentPermutation, List<List<CondensedWeaponAmmoMode>> permutations)
        {
            if (index == weapons.Count)
            {
                permutations.Add(currentPermutation.ToList());
                return;
            }

            // Include the current weapon and its ammo mode pairs
            CondensedWeapon currentCondensedWeapon = weapons[index];
            foreach (var ammoModePair in currentCondensedWeapon.ammoModes)
            {
                List<CondensedWeaponAmmoMode> newPermutation = currentPermutation.ToList();
                newPermutation.Add(new CondensedWeaponAmmoMode(currentCondensedWeapon,  ammoModePair));
                GeneratePermutations(weapons, index + 1, newPermutation, permutations);
            }

            // Exclude the current weapon
            GeneratePermutations(weapons, index + 1, currentPermutation, permutations);
        }
    }
}
