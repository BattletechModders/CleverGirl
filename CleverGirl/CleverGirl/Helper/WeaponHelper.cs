using BattleTech;
using CleverGirl.Objects;
using IRBTModUtils;
using System.Collections.Generic;

namespace CleverGirl.Helper
{
    public static class WeaponHelper
    {

        public static float GetToHitFromPosition(Weapon weapon, AttackDetails details, MeleeAttackType meleeAttackType)
        {
            return SharedState.Combat.ToHit.GetToHitChance(attacker: details.Attacker, weapon: weapon, target: details.Target, 
                attackPosition: details.AttackPosition, targetPosition: details.TargetPosition, 
                numTargets: 1, meleeAttackType: meleeAttackType, isMoraleAttack: false);
        }

        public static void FilterWeapons(AbstractActor attacker, ICombatant target,
            out List<Weapon> rangedWeapons, out List<Weapon> meleeWeapons, out List<Weapon> dfaWeapons)
        {
            rangedWeapons = new List<Weapon>();
            meleeWeapons = new List<Weapon>();
            dfaWeapons = new List<Weapon>();

            if (attacker == null) return;

            List<Weapon> allWeapons = new List<Weapon>();
            foreach (Weapon weap in attacker.Weapons)
            {
                // TODO: Ammo check should be more refined - needs to check ammo types, ammo cost across multiple weapons of the same type
                // Checks if weapon is disabled and has ammo
                if (weap.CanFire)
                {
                    allWeapons.Add(weap);
                }
                else
                {
                    Mod.Log.Debug?.Write($" Weapon ({weap.defId}) is disabled or out of ammo.");
                }
            }

            Mech attackerMech = (Mech)attacker;
            if (attackerMech != null)
            {
                Mod.Log.Debug?.Write($" Adding melee weapon {attackerMech.MeleeWeapon.defId}");
                meleeWeapons.Add(attackerMech.MeleeWeapon);

                Mod.Log.Debug?.Write($" Adding DFA weapon {attackerMech.DFAWeapon.defId}");
                dfaWeapons.Add(attackerMech.DFAWeapon);
            }

            float distance = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            foreach (Weapon weap in allWeapons)
            {
                Mod.Log.Debug?.Write($" Checking weapon ({weap.defId})");

                // Check for LOF and within range
                bool willFireAtTarget = weap.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= weap.MaxRange;
                if (willFireAtTarget && withinRange)
                {
                    Mod.Log.Debug?.Write($" -- Has LOF and is within range, adding as a ranged weapon.");
                    rangedWeapons.Add(weap);
                }
                else
                {
                    Mod.Log.Debug?.Write($" -- Has not LOF is out of range; maxRange: {weap.MaxRange} < {distance}");
                }

                if (attackerMech != null && weap.WeaponCategoryValue.IsSupport)
                {
                    Mod.Log.Debug?.Write($" -- Is a support weapon, adding to melee and DFA sets");
                    meleeWeapons.Add(weap);
                    dfaWeapons.Add(weap);
                } 
            }

        }

    }
}
