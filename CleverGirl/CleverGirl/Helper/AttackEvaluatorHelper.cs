using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CleverGirl {

    public class CondensedWeapon : Weapon {
        public int weaponCount = 0;

        public CondensedWeapon() : base() {}
        public CondensedWeapon(Mech parent, CombatGameState combat, MechComponentRef mcRef, string UID) : 
            base(parent, combat, mcRef, UID) { }
        public CondensedWeapon(Vehicle parent, CombatGameState combat, VehicleComponentRef vcRef, string UID) : 
            base(parent, combat, vcRef, UID) { }
        public CondensedWeapon(Turret parent, CombatGameState combat, TurretComponentRef tcRef, string UID) :
            base(parent, combat, tcRef, UID) { }
    }

    public class CandidateWeapons {
        readonly public List<CondensedWeapon> RangedWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> MeleeWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> DFAWeapons = new List<CondensedWeapon>();

        readonly private Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target) {
            float positionDelta = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            for (int i = 0; i < attacker.Weapons.Count; i++) {
                Weapon weapon = attacker.Weapons[i];
                CondensedWeapon cWeapon;
                if (attacker is Mech) {
                    cWeapon = new CondensedWeapon(weapon.parent as Mech, attacker.Combat, weapon.mechComponentRef, weapon.uid);
                } else if (attacker is Vehicle) {
                    cWeapon = new CondensedWeapon(weapon.parent as Vehicle, attacker.Combat, weapon.vehicleComponentRef, weapon.uid);
                } else {
                    cWeapon = new CondensedWeapon(weapon.parent as Turret, attacker.Combat, weapon.turretComponentRef, weapon.uid);
                }
                string cWepKey = cWeapon.weaponDef.Description.Id;
                if (condensed.ContainsKey(cWepKey)) {
                    condensed[cWepKey].weaponCount++;
                } else {
                    condensed[cWepKey] = cWeapon;
                }
            }

            foreach (KeyValuePair<string, CondensedWeapon> kvp in condensed) {
                CondensedWeapon cWeapon = kvp.Value;
                if (cWeapon.CanFire) {
                    bool willFireAtTarget = cWeapon.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                    bool withinRange = positionDelta <= cWeapon.MaxRange;
                    if (willFireAtTarget && withinRange) {
                        Mod.Log.Debug($" adding weapon: ({cWeapon.defId}) to ranged set");
                        RangedWeapons.Add(cWeapon);
                    } else {
                        Mod.Log.Debug($" weapon: ({cWeapon.defId}) out of range or has not LOF, skipping.");
                    }

                    if (cWeapon.Category == WeaponCategory.AntiPersonnel) {
                        Mod.Log.Debug($" adding support weapon: ({cWeapon.defId}) to melee and DFA attacks.");
                        MeleeWeapons.Add(cWeapon);
                        DFAWeapons.Add(cWeapon);
                    }

                } else {
                    Mod.Log.Debug($" weapon: ({cWeapon.defId}) is disabled or out of ammo, skipping.");
                }
            }

        }
    }

    public static class AttackEvaluatorHelper {


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
        public static List<List<Weapon>> MakeWeaponSets(List<Weapon> potentialWeapons) {
            List<List<Weapon>> list = new List<List<Weapon>>();
            if (potentialWeapons.Count > 0) {
                Weapon item = potentialWeapons[0];
                List<Weapon> range = potentialWeapons.GetRange(1, potentialWeapons.Count - 1);
                List<List<Weapon>> list2 = MakeWeaponSets(range);
                for (int i = 0; i < list2.Count; i++) {
                    List<Weapon> list3 = list2[i];
                    list.Add(list3);
                    list.Add(new List<Weapon>(list3)
                    {
                    item
                });
                }
            } else {
                List<Weapon> item2 = new List<Weapon>();
                list.Add(item2);
            }
            return list;
        }

        // CLONE OF HBS CODE - LIKELY BRITTLE!
        public static List<List<Weapon>> MakeWeaponSetsForEvasive(List<Weapon> potentialWeapons, float toHitFrac, ICombatant target, Vector3 shooterPosition) {
            List<Weapon> list = new List<Weapon>();
            List<Weapon> list2 = new List<Weapon>();
            List<Weapon> list3 = new List<Weapon>();
            for (int i = 0; i < potentialWeapons.Count; i++) {
                Weapon weapon = potentialWeapons[i];
                if (weapon.CanFire) {
                    float toHitFromPosition = weapon.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    if (toHitFromPosition < toHitFrac) {
                        if (weapon.AmmoCategory == AmmoCategory.NotSet) {
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
            Weapon weapon2 = null;
            for (int j = 0; j < list2.Count; j++) {
                Weapon weapon3 = list2[j];
                float toHitFromPosition2 = weapon3.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                float num2 = toHitFromPosition2 * (float)weapon3.ShotsWhenFired * weapon3.DamagePerShot;
                if (num2 > num) {
                    num = num2;
                    weapon2 = weapon3;
                }
            }
            if (weapon2 == null) {
                for (int k = 0; k < list3.Count; k++) {
                    Weapon weapon4 = list3[k];
                    float toHitFromPosition3 = weapon4.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true, false);
                    float num3 = toHitFromPosition3 * (float)weapon4.ShotsWhenFired * weapon4.DamagePerShot;
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
