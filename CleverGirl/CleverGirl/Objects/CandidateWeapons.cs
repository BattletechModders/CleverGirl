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

                if (cWeapon.First.WeaponCategoryValue.CanUseInMelee)
                {
                    Mod.Log.Debug?.Write($" -- can be used in melee, adding to melee and DFA sets.");
                    MeleeWeapons.Add(cWeapon);
                    
                    // CBTRBE Prevents use of weapons in DFA
                    //   so just make it empty
                    // DFAWeapons.Add(cWeapon);
                }

                // Evaluate being able to hit the target
                bool willFireAtTarget = cWeapon.First.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                bool withinRange = distance <= cWeapon.First.MaxRange;
                Mod.Log.Debug?.Write($" -- willFireAtTarget: {willFireAtTarget}  withinRange: {withinRange}");
                if (willFireAtTarget && withinRange)
                {
                    Mod.Log.Debug?.Write($" -- has LOF and is within range, adding to ranged set");
                    RangedWeapons.Add(cWeapon);
                }

            }
        }
    }
}
