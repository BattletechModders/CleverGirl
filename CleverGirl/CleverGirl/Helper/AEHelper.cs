using BattleTech;
using CleverGirl.Analytics;
using CleverGirlAIDamagePrediction;
using IRBTModUtils.Extension;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using us.frostraptor.modUtils;
using static AttackEvaluator;

namespace CleverGirl {

    public class CandidateWeapons {
        readonly public List<CondensedWeapon> RangedWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> MeleeWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> DFAWeapons = new List<CondensedWeapon>();

        readonly private Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target) {
            Mod.Log.Debug?.Write($"Calculating candidate weapons");

            for (int i = 0; i < attacker.Weapons.Count; i++) {
                Weapon weapon = attacker.Weapons[i];

                CondensedWeapon cWeapon = new CondensedWeapon(weapon);

                if (cWeapon.First.CanFire) {
                    Mod.Log.Debug?.Write($" -- '{cWeapon.First.defId}' included");
                    string cWepKey = cWeapon.First.weaponDef.Description.Id;
                    if (condensed.ContainsKey(cWepKey)) {
                        condensed[cWepKey].AddWeapon(weapon);
                    } else {
                        condensed[cWepKey] = cWeapon;
                    }
                } else {
                    Mod.Log.Debug?.Write($" -- '{cWeapon.First.defId}' excluded (disabled or out of ammo)");
                }
            }
            Mod.Log.Debug?.Write("  -- DONE");

            // TODO: Can fire only evaluates ammo once... check for enough ammo for all shots?

            float distance = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            Mod.Log.Debug?.Write($" Checking range {distance} and LOF from attacker: ({attacker.CurrentPosition}) to " +
                $"target: ({target.CurrentPosition})");
            foreach (KeyValuePair<string, CondensedWeapon> kvp in condensed) {
                CondensedWeapon cWeapon = kvp.Value;
                Mod.Log.Debug?.Write($" -- weapon => {cWeapon.First.defId}");

                if (cWeapon.First.WeaponCategoryValue.CanUseInMelee)
                {
                    Mod.Log.Debug?.Write($" ---- can be used in melee, adding to melee and DFA sets.");
                    MeleeWeapons.Add(cWeapon);
                    DFAWeapons.Add(cWeapon);
                }

                // Evaluate being able to hit the target
                bool willFireAtTarget = cWeapon.First.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= cWeapon.First.MaxRange;
                if (willFireAtTarget && withinRange) {
                    Mod.Log.Debug?.Write($" ---- has LOF and is within range, adding ");
                    RangedWeapons.Add(cWeapon);
                } else {
                    Mod.Log.Debug?.Write($" ---- is out of range (MaxRange: {cWeapon.First.MaxRange} vs {distance}) " +
                        $"or has no LOF (willFireAtTarget = {willFireAtTarget}), skipping.");
                }

            }
        }
    }

    public static class AEHelper {

        // Initialize any decision-making data necessary to make an attack order. Fetch the current state of opponents
        //  and cache it for quick look-up
        public static void InitializeAttackOrderDecisionData(AbstractActor unit) {
            Mod.Log.Trace?.Write("AE:MAO:pre - entered.");
            ModState.BehaviorVarValuesCache.Clear();

            // Reset the analytics cache
            ModState.CombatantAnalytics.Clear();

            ModState.CurrentActorAllies.Clear();
            ModState.CurrentActorNeutrals.Clear();
            ModState.CurrentActorEnemies.Clear();

            // Prime the caches with information about all targets
            Mod.Log.Debug?.Write($"Evaluating all actors for hostility to {unit.DistinctId()}");
            foreach (ICombatant combatant in unit.Combat.GetAllImporantCombatants()) {
                if (combatant.GUID == unit.GUID) { continue; }

                // Will only include alive actors and buildings that are 'tab' targets
                if (unit.Combat.HostilityMatrix.IsFriendly(unit.team, combatant.team)) {
                    ModState.CurrentActorAllies[combatant.GUID] = combatant;
                    Mod.Log.Debug?.Write($"  -- actor: {combatant.DistinctId()} is an ally.");
                } else if (unit.Combat.HostilityMatrix.IsEnemy(unit.team, combatant.team)) {
                    ModState.CurrentActorEnemies[combatant.GUID] = combatant;
                    Mod.Log.Debug?.Write($"  -- actor: {combatant.DistinctId()} is an enemy.");
                } else {
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

        public static AbstractActor FilterEnemyUnitsToDesignatedTarget(AITeam aiteam, Lance attackerLance, List<ICombatant> enemyUnits) {
            AbstractActor designatedTarget = null;
            if (aiteam != null && aiteam.DesignatedTargetForLance.ContainsKey(attackerLance)) {
                designatedTarget = aiteam.DesignatedTargetForLance[attackerLance];
                if (designatedTarget != null && !designatedTarget.IsDead) {
                    for (int i = 0; i < enemyUnits.Count; i++) {
                        if (enemyUnits[i] == designatedTarget) {
                            designatedTarget = enemyUnits[i] as AbstractActor;
                            break;
                        }
                    }
                }
            }
            return designatedTarget;
        }

        public static List<AttackEvaluation> EvaluateAttacks(AbstractActor unit, ICombatant target, 
            List<List<CondensedWeapon>>[] weaponSetListByAttack, Vector3 attackPosition, Vector3 targetPosition, 
            bool targetIsEvasive) {

            ConcurrentBag<AttackEvaluation> allResults = new ConcurrentBag<AttackEvaluation>();

            // List 0 is ranged weapons, 1 is melee+support, 2 is DFA+support
            for (int i = 0; i < 3; i++) {

                List<List<CondensedWeapon>> weaponSetsByAttackType = weaponSetListByAttack[i];
                string attackLabel = "ranged attack";
                if (i == 1) { attackLabel = "melee attacks"; }
                if (i == 2) { attackLabel = "DFA attacks"; }
                Mod.Log.Debug?.Write($"Evaluating {weaponSetsByAttackType.Count} {attackLabel}");

                if (weaponSetsByAttackType != null) {

                    //ConcurrentQueue<List<CondensedWeapon>> workQueue = new ConcurrentQueue<List<CondensedWeapon>>();
                    //for (int j = 0; j < weaponSetsByAttackType.Count; j++) {
                    //    workQueue.Enqueue(weaponSetsByAttackType[j]);
                    //}

                    //void evaluateWeaponSet() {
                    //    Mod.Log.Debug?.Write($" New action started.");
                    //    SpinWait spin = new SpinWait();
                    //    while (true) {

                    //        if (workQueue.TryDequeue(out List<Weapon> weaponSet)) {
                    //            AttackEvaluator.AttackEvaluation attackEvaluation = new AttackEvaluator.AttackEvaluation();
                    //            attackEvaluation.WeaponList = weaponSet;
                    //            attackEvaluation.AttackType = (AIUtil.AttackType)i;
                    //            attackEvaluation.HeatGenerated = (float)AIUtil.HeatForAttack(weaponSet);

                    //            if (unit is Mech mech) {
                    //                attackEvaluation.HeatGenerated += (float)mech.TempHeat;
                    //                attackEvaluation.HeatGenerated -= (float)mech.AdjustedHeatsinkCapacity;
                    //            }

                    //            attackEvaluation.ExpectedDamage = AIUtil.ExpectedDamageForAttack(unit, attackEvaluation.AttackType, weaponSet, target, attackPosition, targetPosition, true, unit);
                    //            attackEvaluation.lowestHitChance = AIUtil.LowestHitChance(weaponSet, target, attackPosition, targetPosition, targetIsEvasive);
                    //            allResults.Add(attackEvaluation);
                    //            Mod.Log.Debug?.Write($"Processed a weaponSet, {workQueue.Count} remaining");
                    //        } else {
                    //            Mod.Log.Debug?.Write($"Failed to dequeue, {workQueue.Count} remaining");
                    //            if (workQueue.Count == 0) { break; } else { spin.SpinOnce(); }
                    //        }
                    //    }
                    //    Mod.Log.Debug?.Write($" New action ending.");
                    //};
                    //Parallel.Invoke(evaluateWeaponSet, evaluateWeaponSet, evaluateWeaponSet);

                    for (int j = 0; j < weaponSetsByAttackType.Count; j++) {
                        List<CondensedWeapon> weaponList = weaponSetsByAttackType[j];
                        Mod.Log.Debug?.Write($"Evaluating {weaponList?.Count} weapons for a {attackLabel}");
                        AttackEvaluator.AttackEvaluation attackEvaluation = new AttackEvaluator.AttackEvaluation();
                        attackEvaluation.AttackType = (AIUtil.AttackType)i;
                        attackEvaluation.HeatGenerated = (float)AIHelper.HeatForAttack(weaponList);

                        if (unit is Mech mech) {
                            attackEvaluation.HeatGenerated += (float)mech.TempHeat;
                            attackEvaluation.HeatGenerated -= (float)mech.AdjustedHeatsinkCapacity;
                        }

                        attackEvaluation.ExpectedDamage = AIHelper.ExpectedDamageForAttack(unit, attackEvaluation.AttackType, weaponList, target, attackPosition, targetPosition, true, unit);
                        attackEvaluation.lowestHitChance = AIHelper.LowestHitChance(weaponList, target, attackPosition, targetPosition, targetIsEvasive);

                        // Expand the list to all weaponDefs, not our condensed ones
                        Mod.Log.Debug?.Write($"Expanding weapon list for AttackEvaluation");
                        List<Weapon> aeWeaponList = new List<Weapon>();
                        foreach (CondensedWeapon cWeapon in weaponList) {
                            List<Weapon> cWeapons = cWeapon.condensedWeapons;
                            if (cWeapon.ammoAndMode != null) {
                                foreach (Weapon wep in cWeapons) {
                                    Mod.Log.Debug?.Write($" -- Setting ammoMode to: {cWeapon.ammoAndMode.ammoId}_{cWeapon.ammoAndMode.modeId} for weapon: {wep.UIName}");
                                    CleverGirlHelper.ApplyAmmoMode(wep, cWeapon.ammoAndMode);
                                }
                            }

                            aeWeaponList.AddRange(cWeapon.condensedWeapons);
                        }
                        Mod.Log.Debug?.Write($"List size {weaponList?.Count} was expanded to: {aeWeaponList?.Count}");
                        attackEvaluation.WeaponList = aeWeaponList;
                        allResults.Add(attackEvaluation);
                    }
                }
            }

            List<AttackEvaluator.AttackEvaluation> sortedResults = new List<AttackEvaluator.AttackEvaluation>();
            sortedResults.AddRange(allResults);
            sortedResults.Sort((AttackEvaluator.AttackEvaluation a, AttackEvaluator.AttackEvaluation b) => a.ExpectedDamage.CompareTo(b.ExpectedDamage));
            sortedResults.Reverse();

            return sortedResults;
        }


        public static bool MeleeDamageOutweighsRisk(Mech attacker, ICombatant target) {
            
            float attackerMeleeDam = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(attacker, target, attacker.CurrentPosition, target.CurrentPosition, false, attacker);
            if (attackerMeleeDam <= 0f) {
                Mod.Log.Debug?.Write("Attacker has no expected damage, melee is too risky.");
                return false;
            }

            Mech targetMech = target as Mech;
            if (targetMech == null) {
                Mod.Log.Debug?.Write("Target has no expected damage, melee is safe.");
                return true;
            }

            // Use the target mech's position, because if we melee the attacker they can probably get to us
            float targetMeleeDam = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(targetMech, attacker, targetMech.CurrentPosition, targetMech.CurrentPosition, false, attacker);
            float meleeDamageRatio = attackerMeleeDam / targetMeleeDam;
            float meleeDamageRatioCap = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal;
            Mod.Log.Debug?.Write($" meleeDamageRatio: {meleeDamageRatio} = target: {targetMeleeDam} / attacker: {attackerMeleeDam} vs. cap: {meleeDamageRatioCap}");

            return meleeDamageRatio > meleeDamageRatioCap;
        }

        // === CLONE METHODS BELOW ==

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static List<List<CondensedWeapon>> MakeWeaponSets(List<CondensedWeapon> potentialWeapons) {
            List<List<CondensedWeapon>> list = new List<List<CondensedWeapon>>();
            if (potentialWeapons.Count > 0) {
                CondensedWeapon item = potentialWeapons[0];
                List<CondensedWeapon> range = potentialWeapons.GetRange(1, potentialWeapons.Count - 1);
                List<List<CondensedWeapon>> list2 = MakeWeaponSets(range);
                for (int i = 0; i < list2.Count; i++) {
                    List<CondensedWeapon> list3 = list2[i];
                    list.Add(list3);
                    list.Add(new List<CondensedWeapon>(list3) { item });
                }
            } else {
                List<CondensedWeapon> item2 = new List<CondensedWeapon>();
                list.Add(item2);
            }
            return list;
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static List<List<CondensedWeapon>> MakeWeaponSetsForEvasive(List<CondensedWeapon> potentialWeapons, float toHitFrac, ICombatant target, Vector3 shooterPosition) {
            List<CondensedWeapon> likelyToHitWeapons = new List<CondensedWeapon>();
            List<CondensedWeapon> unlikelyNonAmmoWeapons = new List<CondensedWeapon>();
            List<CondensedWeapon> unlikelyAmmoWeapons = new List<CondensedWeapon>();

            // Separate weapons into likely to hit, and unlikely to hit. Add only a single unlikely to hit weapon to the sets to be created.
            // TODO: Make this multi-step marginal... don't fire 9% weapons, but do fire 30%?
            for (int i = 0; i < potentialWeapons.Count; i++) {
                CondensedWeapon weapon = potentialWeapons[i];
                if (weapon.First.CanFire) {
                    float toHitFromPosition = weapon.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    if (toHitFromPosition < toHitFrac) {
                        if (weapon.First.AmmoCategoryValue.Is_NotSet) {
                            unlikelyNonAmmoWeapons.Add(weapon);
                        } else {
                            unlikelyAmmoWeapons.Add(weapon);
                        }
                    } else {
                        likelyToHitWeapons.Add(weapon);
                    }
                }
            }

            float unlikelyWeaponChanceToHit = float.MinValue;
            CondensedWeapon weapon2 = null;
            for (int j = 0; j < unlikelyNonAmmoWeapons.Count; j++) {
                CondensedWeapon nonAmmoWeapon = unlikelyNonAmmoWeapons[j];
                float toHitFromPosition2 = nonAmmoWeapon.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                float weaponChanceToHit = toHitFromPosition2 * (float)nonAmmoWeapon.First.ShotsWhenFired * nonAmmoWeapon.First.DamagePerShot;
                if (weaponChanceToHit > unlikelyWeaponChanceToHit) {
                    unlikelyWeaponChanceToHit = weaponChanceToHit;
                    weapon2 = nonAmmoWeapon;
                }
            }

            if (weapon2 == null) {
                for (int k = 0; k < unlikelyAmmoWeapons.Count; k++) {
                    CondensedWeapon weapon4 = unlikelyAmmoWeapons[k];
                    float toHitFromPosition3 = weapon4.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    float weaponChanceToHit = toHitFromPosition3 * (float)weapon4.First.ShotsWhenFired * weapon4.First.DamagePerShot;
                    if (weaponChanceToHit > unlikelyWeaponChanceToHit) {
                        unlikelyWeaponChanceToHit = weaponChanceToHit;
                        weapon2 = weapon4;
                    }
                }
            }

            if (weapon2 != null) {
                likelyToHitWeapons.Add(weapon2);
            }

            return MakeWeaponSets(likelyToHitWeapons);
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static CalledShotAttackOrderInfo MakeOffensivePushOrder(AbstractActor attackingUnit, AttackEvaluator.AttackEvaluation evaluatedAttack, ICombatant target) {
            if (!attackingUnit.CanUseOffensivePush() || !ShouldUnitUseInspire(attackingUnit)) {
                return null;
            }
            return MakeCalledShotOrder(attackingUnit, evaluatedAttack, target, true);
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static bool ShouldUnitUseInspire(AbstractActor unit) {
            float num = AIUtil.CalcMaxInspirationDelta(unit, true);
            AITeam aiteam = unit.team as AITeam;
            if (aiteam == null || !unit.CanBeInspired) {
                return false;
            }
            if (num < AIHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_MinimumInspirationDamage).FloatVal) {
                return false;
            }
            float num2 = 1f - aiteam.GetInspirationWindow();
            return num > aiteam.GetInspirationTargetDamage() * num2;
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static CalledShotAttackOrderInfo MakeCalledShotOrder(AbstractActor attackingUnit, AttackEvaluator.AttackEvaluation evaluatedAttack, ICombatant target, bool isMoraleAttack) {
            
            Mech mech = target as Mech;
            if (mech == null || !mech.IsVulnerableToCalledShots() || evaluatedAttack.AttackType == AIUtil.AttackType.Melee || evaluatedAttack.AttackType == AIUtil.AttackType.DeathFromAbove) {
                return null;
            }

            Mech mech2 = attackingUnit as Mech;
            for (int i = 0; i < evaluatedAttack.WeaponList.Count; i++) {
                Weapon weapon = evaluatedAttack.WeaponList[i];
                if (weapon.WeaponCategoryValue.IsMelee|| weapon.Type == WeaponType.Melee || (mech2 != null && (weapon == mech2.DFAWeapon || weapon == mech2.MeleeWeapon))) {
                    return null;
                }
            }

            List<ArmorLocation> list = new List<ArmorLocation> {
                ArmorLocation.Head,
                ArmorLocation.CenterTorso,
                ArmorLocation.LeftTorso,
                ArmorLocation.LeftArm,
                ArmorLocation.LeftLeg,
                ArmorLocation.RightTorso,
                ArmorLocation.RightArm,
                ArmorLocation.RightLeg
            };

            List<ChassisLocations> list2 = new List<ChassisLocations> {
                ChassisLocations.Head,
                ChassisLocations.CenterTorso,
                ChassisLocations.LeftTorso,
                ChassisLocations.LeftArm,
                ChassisLocations.LeftLeg,
                ChassisLocations.RightTorso,
                ChassisLocations.RightArm,
                ChassisLocations.RightLeg
            };

            List<float> list3 = new List<float>(list.Count);
            float num = 0f;
            for (int j = 0; j < list.Count; j++) {
                float num2 = CalcCalledShotLocationTargetChance(mech, list[j], list2[j]);
                list3.Add(num2);
                num += num2;
            }

            float num3 = UnityEngine.Random.Range(0f, num);
            CalledShotAttackOrderInfo calledShotAttackOrderInfo = null;
            for (int k = 0; k < list.Count; k++) {
                float num4 = list3[k];
                if (num3 < num4) {
                    calledShotAttackOrderInfo = new CalledShotAttackOrderInfo(mech, list[k], isMoraleAttack);
                    break;
                }
                num3 -= num4;
            }

            if (calledShotAttackOrderInfo == null) {
                Debug.LogError("Failed to calculate called shot. Targeting head as fallback.");
                calledShotAttackOrderInfo = new CalledShotAttackOrderInfo(mech, ArmorLocation.Head, isMoraleAttack);
            }

            for (int l = 0; l < evaluatedAttack.WeaponList.Count; l++) {
                Weapon weapon2 = evaluatedAttack.WeaponList[l];
                AIUtil.LogAI("Called Shot: Adding weapon " + weapon2.Name, "AI.DecisionMaking");
                calledShotAttackOrderInfo.AddWeapon(weapon2);
            }

            return calledShotAttackOrderInfo;
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static float CalcCalledShotLocationTargetChance(Mech targetMech, ArmorLocation armorLoc, ChassisLocations chassisLoc) {
            LocationDamageLevel locationDamageLevel = targetMech.GetLocationDamageLevel(chassisLoc);
            if (locationDamageLevel == LocationDamageLevel.Destroyed) {
                return 0f;
            }
            float num;
            if (armorLoc != ArmorLocation.Head) {
                if (armorLoc != ArmorLocation.CenterTorso) {
                    num = AIHelper.GetBehaviorVariableValue(targetMech.BehaviorTree, BehaviorVariableName.Float_CalledShotOtherBaseChance).FloatVal;
                } else {
                    num = AIHelper.GetBehaviorVariableValue(targetMech.BehaviorTree, BehaviorVariableName.Float_CalledShotCenterTorsoBaseChance).FloatVal;
                }
            } else {
                num = AIHelper.GetBehaviorVariableValue(targetMech.BehaviorTree, BehaviorVariableName.Float_CalledShotHeadBaseChance).FloatVal;
            }
            if (locationDamageLevel == LocationDamageLevel.Penalized || locationDamageLevel == LocationDamageLevel.NonFunctional) {
                num *= AIHelper.GetBehaviorVariableValue(targetMech.BehaviorTree, BehaviorVariableName.Float_CalledShotDamagedChanceMultiplier).FloatVal;
            }
            List<MechComponent> componentsForLocation = targetMech.GetComponentsForLocation(chassisLoc, ComponentType.Weapon);
            float num2 = 0f;
            for (int i = 0; i < componentsForLocation.Count; i++) {
                Weapon weapon = componentsForLocation[i] as Weapon;
                if (weapon != null && weapon.CanFire) {
                    float num3 = (float)weapon.ShotsWhenFired * weapon.DamagePerShot;
                    num2 += num3;
                }
            }
            return num + AIHelper.GetBehaviorVariableValue(targetMech.BehaviorTree, BehaviorVariableName.Float_CalledShotWeaponDamageChance).FloatVal * num2;
        }
    }
}
