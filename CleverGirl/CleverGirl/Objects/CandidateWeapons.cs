using BattleTech;
using System.Collections.Generic;

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
            Mod.Log.Debug?.Write($"Calculating candidate weapons");

            for (int i = 0; i < attacker.Weapons.Count; i++)
            {
                Weapon weapon = attacker.Weapons[i];

                CondensedWeapon cWeapon = new CondensedWeapon(weapon);

                if (cWeapon.First.CanFire)
                {
                    Mod.Log.Debug?.Write($" -- '{cWeapon.First.defId}' included");
                    string cWepKey = cWeapon.First.weaponDef.Description.Id;
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
                    Mod.Log.Debug?.Write($" -- '{cWeapon.First.defId}' excluded (disabled or out of ammo)");
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
                if (willFireAtTarget && withinRange)
                {
                    Mod.Log.Debug?.Write($" ---- has LOF and is within range, adding ");
                    RangedWeapons.Add(cWeapon);
                }
                else
                {
                    Mod.Log.Debug?.Write($" ---- is out of range (MaxRange: {cWeapon.First.MaxRange} vs {distance}) " +
                        $"or has no LOF (willFireAtTarget = {willFireAtTarget}), skipping.");
                }

            }
        }
    }
}
