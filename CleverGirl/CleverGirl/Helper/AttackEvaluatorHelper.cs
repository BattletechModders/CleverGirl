using BattleTech;

#if USE_CAC
using CustAmmoCategories;
#endif

using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using static AttackEvaluator;

namespace CleverGirl {

    // A condensed weapon masquerades as the parent weapon, but keeps a list of all the 'real' weapons     
    public class CondensedWeapon {
        public int weaponsCondensed = 0;
        public List<Weapon> condensedWeapons = new List<Weapon>();

#if USE_CAC
        public WeaponMode CACWeaponMode = null;
#endif

        public CondensedWeapon() {}
        public CondensedWeapon(Weapon weapon) {
            AddWeapon(weapon);

        }

        // Invoke this after construction and every time you want to aggregate a weapon
        public void AddWeapon(Weapon weapon) {
            weaponsCondensed++;
            this.condensedWeapons.Add(weapon);
        }

        public Weapon First {
            get { return this.condensedWeapons[0]; }
        }

    }

    public class CandidateWeapons {
        readonly public List<CondensedWeapon> RangedWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> MeleeWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> DFAWeapons = new List<CondensedWeapon>();

        readonly private Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target) {
            Mod.Log.Debug($"Calculating candidate weapons");

            for (int i = 0; i < attacker.Weapons.Count; i++) {
                Weapon weapon = attacker.Weapons[i];

                CondensedWeapon cWeapon = new CondensedWeapon(weapon);

                if (cWeapon.First.CanFire) {
                    Mod.Log.Debug($" ({cWeapon.First.defId}) can fire, adding as candidate");
                    string cWepKey = cWeapon.First.weaponDef.Description.Id;
                    if (condensed.ContainsKey(cWepKey)) {
                        condensed[cWepKey].AddWeapon(weapon);
                    } else {
                        condensed[cWepKey] = cWeapon;
                    }
                } else {
                    Mod.Log.Debug($" ({cWeapon.First.defId}) is disabled or out of ammo, skipping.");
                }
            }

            // TODO: Can fire only evaluates ammo once... check for enough ammo for all shots?

#if USE_CAC
            // Evaluate CAC ammo boxes if defined.
            foreach (KeyValuePair<string, CondensedWeapon> kvp in condensed) {
                CondensedWeapon cWeapon = kvp.Value;
                
            }
#endif

            float distance = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            Mod.Log.Debug($" Checking range {distance} and LOF from attacker: ({attacker.CurrentPosition}) to " +
                $"target: ({target.CurrentPosition})");
            foreach (KeyValuePair<string, CondensedWeapon> kvp in condensed) {
                CondensedWeapon cWeapon = kvp.Value;
                // Evaluate being able to hit the target
                bool willFireAtTarget = cWeapon.First.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= cWeapon.First.MaxRange;
                if (willFireAtTarget && withinRange) {
                    Mod.Log.Debug($" ({cWeapon.First.defId}) has LOF and is within range, adding ");
                    RangedWeapons.Add(cWeapon);
                } else {
                    Mod.Log.Debug($" ({cWeapon.First.defId}) is out of range (MaxRange: {cWeapon.First.MaxRange} vs {distance}) " +
                        $"or has no LOF, skipping.");
                }

                if (cWeapon.First.Category == WeaponCategory.AntiPersonnel) {
                    Mod.Log.Debug($" ({cWeapon.First.defId}) is anti-personnel, adding to melee and DFA sets.");
                    MeleeWeapons.Add(cWeapon);
                    DFAWeapons.Add(cWeapon);
                }
            }
        }
    }

    public static class AttackEvaluatorHelper {

        public static List<AttackEvaluation> EvaluateAttacks(AbstractActor unit, ICombatant target, 
            List<List<CondensedWeapon>>[] weaponSetListByAttack, Vector3 attackPosition, Vector3 targetPosition, 
            bool targetIsEvasive) {

            ConcurrentBag<AttackEvaluation> allResults = new ConcurrentBag<AttackEvaluation>();

            // List 0 is ranged weapons, 1 is melee+support, 2 is DFA+support
            for (int i = 0; i < 3; i++) {

                List<List<CondensedWeapon>> weaponSetsByAttackType = weaponSetListByAttack[i];
                if (i == 0) { Mod.Log.Debug($"Evaluating {weaponSetsByAttackType.Count} ranged attacks."); }
                else if (i == 1) { Mod.Log.Debug($"Evaluating {weaponSetsByAttackType.Count} melee attacks."); }
                else if (i == 2) { Mod.Log.Debug($"Evaluating {weaponSetsByAttackType.Count} DFA attacks."); }

                if (weaponSetsByAttackType != null) {

                    //ConcurrentQueue<List<CondensedWeapon>> workQueue = new ConcurrentQueue<List<CondensedWeapon>>();
                    //for (int j = 0; j < weaponSetsByAttackType.Count; j++) {
                    //    workQueue.Enqueue(weaponSetsByAttackType[j]);
                    //}

                    //void evaluateWeaponSet() {
                    //    Mod.Log.Debug($" New action started.");
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
                    //            Mod.Log.Debug($"Processed a weaponSet, {workQueue.Count} remaining");
                    //        } else {
                    //            Mod.Log.Debug($"Failed to dequeue, {workQueue.Count} remaining");
                    //            if (workQueue.Count == 0) { break; } else { spin.SpinOnce(); }
                    //        }
                    //    }
                    //    Mod.Log.Debug($" New action ending.");
                    //};
                    //Parallel.Invoke(evaluateWeaponSet, evaluateWeaponSet, evaluateWeaponSet);

                    for (int j = 0; j < weaponSetsByAttackType.Count; j++) {
                        List<CondensedWeapon> weaponList = weaponSetsByAttackType[j];
                        Mod.Log.Debug($"Evaluating {weaponList?.Count} weapons.");
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
                        Mod.Log.Debug($"Expanding weapon list for AttackEvaluation");
                        List<Weapon> aeWeaponList = new List<Weapon>();
                        foreach (CondensedWeapon cWeapon in weaponList) {
                            aeWeaponList.AddRange(cWeapon.condensedWeapons);
                        }
                        Mod.Log.Debug($"List size {weaponList?.Count} was expanded to: {aeWeaponList?.Count}");
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
                Mod.Log.Debug("Attacker has no expected damage, melee is too risky.");
                return false;
            }

            Mech targetMech = target as Mech;
            if (targetMech == null) {
                Mod.Log.Debug("Target has no expected damage, melee is safe.");
                return true;
            }

            // Use the target mech's position, because if we melee the attacker they can probably get to us
            float targetMeleeDam = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(targetMech, attacker, targetMech.CurrentPosition, targetMech.CurrentPosition, false, attacker);
            float meleeDamageRatio = targetMeleeDam / attackerMeleeDam;
            float meleeDamageRatioCap = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal;
            Mod.Log.Debug($" meleeDamageRatio: {meleeDamageRatio} = target: {targetMeleeDam} / attacker: {attackerMeleeDam} vs. cap: {meleeDamageRatioCap}");

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
            List<CondensedWeapon> list = new List<CondensedWeapon>();
            List<CondensedWeapon> list2 = new List<CondensedWeapon>();
            List<CondensedWeapon> list3 = new List<CondensedWeapon>();
            for (int i = 0; i < potentialWeapons.Count; i++) {
                CondensedWeapon weapon = potentialWeapons[i];
                if (weapon.First.CanFire) {
                    float toHitFromPosition = weapon.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    if (toHitFromPosition < toHitFrac) {
                        if (weapon.First.AmmoCategory == AmmoCategory.NotSet) {
                            list2.Add(weapon);
                        } else {
                            list3.Add(weapon);
                        }
                    } else {
                        list.Add(weapon);
                    }
                }
            }
            float num = float.MinValue;
            CondensedWeapon weapon2 = null;
            for (int j = 0; j < list2.Count; j++) {
                CondensedWeapon weapon3 = list2[j];
                float toHitFromPosition2 = weapon3.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                float num2 = toHitFromPosition2 * (float)weapon3.First.ShotsWhenFired * weapon3.First.DamagePerShot;
                if (num2 > num) {
                    num = num2;
                    weapon2 = weapon3;
                }
            }
            if (weapon2 == null) {
                for (int k = 0; k < list3.Count; k++) {
                    CondensedWeapon weapon4 = list3[k];
                    float toHitFromPosition3 = weapon4.First.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    float num3 = toHitFromPosition3 * (float)weapon4.First.ShotsWhenFired * weapon4.First.DamagePerShot;
                    if (num3 > num) {
                        num = num3;
                        weapon2 = weapon4;
                    }
                }
            }
            if (weapon2 != null) {
                list.Add(weapon2);
            }
            return MakeWeaponSets(list);
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static CalledShotAttackOrderInfo MakeOffensivePushOrder(AbstractActor attackingUnit, AttackEvaluator.AttackEvaluation evaluatedAttack, int enemyUnitIndex) {
            if (!attackingUnit.CanUseOffensivePush() || !ShouldUnitUseInspire(attackingUnit)) {
                return null;
            }
            return MakeCalledShotOrder(attackingUnit, evaluatedAttack, enemyUnitIndex, true);
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
        public static CalledShotAttackOrderInfo MakeCalledShotOrder(AbstractActor attackingUnit, AttackEvaluator.AttackEvaluation evaluatedAttack, int enemyUnitIndex, bool isMoraleAttack) {
            ICombatant combatant = attackingUnit.BehaviorTree.enemyUnits[enemyUnitIndex];
            Mech mech = combatant as Mech;
            if (mech == null || !mech.IsVulnerableToCalledShots() || evaluatedAttack.AttackType == AIUtil.AttackType.Melee || evaluatedAttack.AttackType == AIUtil.AttackType.DeathFromAbove) {
                return null;
            }
            Mech mech2 = attackingUnit as Mech;
            for (int i = 0; i < evaluatedAttack.WeaponList.Count; i++) {
                Weapon weapon = evaluatedAttack.WeaponList[i];
                if (weapon.Category == WeaponCategory.Melee || weapon.Type == WeaponType.Melee || (mech2 != null && (weapon == mech2.DFAWeapon || weapon == mech2.MeleeWeapon))) {
                    return null;
                }
            }
            List<ArmorLocation> list = new List<ArmorLocation>
            {
            ArmorLocation.Head,
            ArmorLocation.CenterTorso,
            ArmorLocation.LeftTorso,
            ArmorLocation.LeftArm,
            ArmorLocation.LeftLeg,
            ArmorLocation.RightTorso,
            ArmorLocation.RightArm,
            ArmorLocation.RightLeg
        };
            List<ChassisLocations> list2 = new List<ChassisLocations>
            {
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
