using BattleTech;
using BattleTech.StringInterpolation;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using IRBTModUtils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CleverGirl
{
    public class CandidateWeapons
    {
        readonly public List<CondensedWeapon> RangedWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> MeleeWeapons = new List<CondensedWeapon>();
        readonly public List<CondensedWeapon> DFAWeapons = new List<CondensedWeapon>();

        readonly private Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target)
        {

            // Iterate all weapons; if they can fire, add them to the Condensed Weapon list
            Mod.Log.Debug?.Write($"Calculating candidate weapons");
            for (int i = 0; i < attacker.Weapons.Count; i++)
            {
                Weapon weapon = attacker.Weapons[i];

                CondensedWeapon cWeapon = new CondensedWeapon(weapon);
                if (weapon.CanFire)
                {
                    Mod.Log.Debug?.Write($" -- '{weapon.defId}' included");
                    string cWepKey = weapon.weaponDef.Description.Id;
                    if (condensed.ContainsKey(cWepKey))
                    {
                        condensed[cWepKey].AddWeapon(weapon);
                    }
                    else
                    {
                        condensed[cWepKey] = cWeapon;
                    }
                }
                else
                {
                    Mod.Log.Debug?.Write($" -- '{weapon.defId}' excluded (disabled or out of ammo)");
                }
            }
            Mod.Log.Debug?.Write("  -- DONE");

            // TODO: Can fire only evaluates ammo once... check for enough ammo for all shots?

            float distance = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            Mod.Log.Debug?.Write($" Checking range {distance} and LOF from attacker: ({attacker.CurrentPosition}) to " +
                $"target: ({target.CurrentPosition})");
            foreach (KeyValuePair<string, CondensedWeapon> kvp in condensed)
            {
                CondensedWeapon cWeapon = kvp.Value;
                Mod.Log.Debug?.Write($" -- weapon => '{cWeapon.First.UIName}'");

                Weapon rawWeapon = cWeapon.First;
                if (rawWeapon.WeaponCategoryValue.CanUseInMelee)
                {
                    Mod.Log.Debug?.Write($" -- can be used in melee, adding to melee sets.");
                    MeleeWeapons.Add(cWeapon);
                    
                    // CBTBE Prevents use of weapons in DFA
                    //   so just make it empty
                    // DFAWeapons.Add(cWeapon);
                }

                // WillFireAtTargetFromPosition has an implicit check in CAC for minimum range. False can mean there's a possible shot, but weapon mode is limiting the action
                bool willFireAtTarget = rawWeapon.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= rawWeapon.MaxRange;
                Mod.Log.Debug?.Write($" -- base weaponAndAmmo has willFire: {willFireAtTarget}  willFire: {withinRange}");

                bool canAttack = willFireAtTarget && withinRange;

                // Iterate weapon and ammo modes
                List<AmmoModePair> firingMethods = rawWeapon.getAvaibleFiringMethods();
                if (firingMethods != null && firingMethods.Count > 0)
                {
                    foreach (AmmoModePair item in rawWeapon.getAvaibleFiringMethods())
                    {
                        rawWeapon.ApplyAmmoMode(item);
                        bool modeWillFire = rawWeapon.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                        bool modeInRange = distance <= rawWeapon.MaxRange;
                        Mod.Log.Debug?.Write($" -- ammoMode: {item.ammoId}_{item.modeId} for weapon: {rawWeapon.UIName} has willFire: {modeWillFire} willFire: {modeInRange}");

                        if (modeWillFire && modeInRange && !canAttack)
                        {
                            canAttack = true;
                        }
                    }
                }

                if (canAttack)
                {
                    Mod.Log.Debug?.Write($" -- weapon has LOF and is within range, adding to ranged set");
                    RangedWeapons.Add(cWeapon);
                }

                if (!willFireAtTarget)
                {
                    LineOfFireLevel lofLevel = SharedState.Combat.LOS.GetLineOfFire(attacker, attacker.CurrentPosition, target, target.CurrentPosition, target.CurrentRotation, out Vector3 collisionWorldPos);
                    bool inArc = attacker.IsTargetPositionInFiringArc(target, attacker.CurrentPosition, attacker.CurrentRotation, target.CurrentPosition);
                    float attackAngle = PathingUtil.GetAngle(attacker.CurrentRotation * Vector3.forward);
                    float positionDeltaAngle = PathingUtil.GetAngle(target.CurrentPosition - attacker.CurrentPosition);
                    float deltaAngle = Mathf.DeltaAngle(attackAngle, positionDeltaAngle);
                    float absAngle = Mathf.Abs(deltaAngle);
                    Mod.Log.Info?.Write($"GetLineOfFire has losLevel: {lofLevel}  with collisionWorldPos: {collisionWorldPos}  inArc: {inArc}");
                    Mod.Log.Info?.Write($"  -- attacker pos: {attacker.CurrentPosition}  rot: {attacker.CurrentRotation}");
                    Mod.Log.Info?.Write($"  -- attacker firingArc: {attacker.FiringArc()}  CombatConstants.FiringArc: {SharedState.Combat.Constants.ToHit.FiringArcDegrees}");
                    Mod.Log.Info?.Write($"  -- attackAngle: {attackAngle}  positionDeltaAngle: {positionDeltaAngle}  deltaAngle: {deltaAngle}  absAngle: {absAngle}");
                    Mod.Log.Info?.Write($"  -- target pos: {target.CurrentPosition}  rot: {target.CurrentRotation}");
                }

            }
        }
    }
}
