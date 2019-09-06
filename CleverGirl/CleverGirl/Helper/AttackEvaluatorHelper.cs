using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CleverGirl {

    public class CandidateWeapons {
        readonly List<Weapon> RangedWeapons = new List<Weapon>();
        readonly List<Weapon> MeleeWeapons = new List<Weapon>();
        readonly List<Weapon> DFAWeapons = new List<Weapon>();

        public CandidateWeapons(AbstractActor attacker, ICombatant target) {
            float positionDelta = (target.CurrentPosition - attacker.CurrentPosition).magnitude;
            for (int i = 0; i < attacker.Weapons.Count; i++) {
                Weapon weapon = attacker.Weapons[i];
                if (weapon.CanFire) {
                    bool willFireAtTarget = weapon.WillFireAtTargetFromPosition(target, attacker.CurrentPosition, attacker.CurrentRotation);
                    bool withinRange = positionDelta <= weapon.MaxRange;
                    if (willFireAtTarget && withinRange) {
                        Mod.Log.Debug($" adding weapon: ({weapon.defId}) to ranged set");
                        RangedWeapons.Add(weapon);
                    } else {
                        Mod.Log.Debug($" weapon: ({weapon.defId}) out of range or has not LOF, skipping.");
                    }

                    if (weapon.Category == WeaponCategory.AntiPersonnel) {
                        Mod.Log.Debug($" adding support weapon: ({weapon.defId}) to melee and DFA attacks.");
                        MeleeWeapons.Add(weapon);
                        DFAWeapons.Add(weapon);
                    }

                } else {
                    Mod.Log.Debug($" weapon: ({weapon.defId}) is disabled or out of ammo, skipping.");
                }
            }
        }
    }

    public static class AttackEvaluatorHelper {
    }
}
