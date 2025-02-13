using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using IRBTModUtils;
using System.Collections.Generic;
using CleverGirl.Helper;
using UnityEngine;

namespace CleverGirl.Objects
{
    public class CandidateWeapons
    {
        public readonly List<CondensedWeapon> RangedWeapons = new List<CondensedWeapon>();
        public readonly List<CondensedWeapon> MeleeWeapons = new List<CondensedWeapon>();
        public readonly List<CondensedWeapon> DFAWeapons = new List<CondensedWeapon>();

        private readonly Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target)
        {
            // Iterate all weapons; if they can fire, add them to the Condensed Weapon list
            Mod.Log.Debug?.Write($"Calculating candidate weapons");
            for (int i = 0; i < attacker.Weapons.Count; i++)
            {
                Weapon weapon = attacker.Weapons[i];

                CondensedWeapon cWeapon = new CondensedWeapon(weapon);
                
                if (HasAvailableAmmoMode(weapon))
                {
                    Mod.Log.Debug?.Write($" -- '{weapon.defId}' included");
                    string cWepKey = weapon.defId;
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
                if (rawWeapon.CanFireInMelee(distance))
                {
                    Mod.Log.Debug?.Write($" -- can be used in melee, adding to melee sets.");
                    MeleeWeapons.Add(cWeapon);
                    
                    // CBTBE Prevents use of weapons in DFA
                    //   so just make it empty
                    // DFAWeapons.Add(cWeapon);
                }

                // WillFireAtTargetFromPosition has an implicit check in CAC for minimum range. False can mean there's a possible shot, but weapon mode is limiting the action
                bool canFireAtTarget = false;
                bool hasLOF = false;
                List<AmmoModePair> firingMethods = rawWeapon.getAvaibleFiringMethods();
                if (firingMethods != null && firingMethods.Count > 0)
                {
                    AmmoModePair currentAmmoMode = rawWeapon.getCurrentAmmoMode();
                    foreach (AmmoModePair ammoModePair in rawWeapon.getAvaibleFiringMethods())
                    {
                        rawWeapon.ApplyAmmoMode(ammoModePair);
                        bool modeWillFire = rawWeapon.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                        bool modeInRange = distance <= rawWeapon.MaxRange;
                        Mod.Log.Debug?.Write($" -- Weapon: {rawWeapon.UIName} with modeId: {ammoModePair.modeId} using ammoId: {ammoModePair.ammoId} has willFire: {modeWillFire} inRange: {modeInRange}");

                        if (modeWillFire)
                        {
                            hasLOF = true;
                        }
                        if (modeWillFire && modeInRange)
                        {
                            Mod.Log.Debug?.Write($" -- ammoMode: {ammoModePair.ammoId} modeId: {ammoModePair.modeId} has LOF and is within range, weapon will be added to ranged set");
                            canFireAtTarget = true;
                            cWeapon.AddAmmoMode(ammoModePair);
                        }
                    }
                    rawWeapon.ApplyAmmoMode(currentAmmoMode);
                    rawWeapon.ResetTempAmmo();
                }
                else
                {
                    Mod.Log.Warn?.Write($"-- No modes found for {rawWeapon.UIName}");
                }

                if (canFireAtTarget)
                {
                    RangedWeapons.Add(cWeapon);
                }
                
                if (!hasLOF)
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

        private static bool HasAvailableAmmoMode(Weapon weapon)
        {
            AmmoModePair currentAmmoMode = weapon.getCurrentAmmoMode();
            bool canFire = false;
            foreach (AmmoModePair ammoModePair in weapon.getAvaibleFiringMethods())
            {
                weapon.ApplyAmmoMode(ammoModePair);
                if (weapon.CanFire)
                {
                    canFire = true;
                    break;
                }
            }
            weapon.ApplyAmmoMode(currentAmmoMode);
            weapon.ResetTempAmmo();
            return canFire;
        }
    }
}