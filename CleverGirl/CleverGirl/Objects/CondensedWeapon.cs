using CustAmmoCategories;
using System.Collections.Generic;

namespace CleverGirl {

    // A condensed weapon masquerades as the parent weapon with a selected AmmoModePair, but keeps a list of all the 'real' weapons
    public class CondensedWeapon {
        public int weaponsCondensed = 0;
        public List<Weapon> condensedWeapons = new List<Weapon>();
        public List<AmmoModePair> ammoModes = new List<AmmoModePair>();

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

        public void AddAmmoMode(AmmoModePair ammoModePair)
        {
            ammoModes.Add(ammoModePair);
        }
    }
}
