using CustAmmoCategories;
using System.Collections.Generic;

namespace CleverGirl {

    // A condensed weapon masquerades as the parent weapon with a selected AmmoModePair, but keeps a list of all the 'real' weapons
    public class CondensedWeapon {
        public readonly List<Weapon> condensedWeapons = new List<Weapon>();
        public List<AmmoModePair> ammoModes = new List<AmmoModePair>();

        public CondensedWeapon(Weapon weapons)
        {
            AddWeapon(weapons);
        }

        public int WeaponCount => condensedWeapons.Count;

        // Invoke this after construction and every time you want to aggregate a weapon
        public void AddWeapon(Weapon weapon) {
            condensedWeapons.Add(weapon);
        }

        public Weapon First => condensedWeapons[0];

        public void AddAmmoMode(AmmoModePair ammoModePair)
        {
            ammoModes.Add(ammoModePair);
        }

        public override string ToString()
        {
            return $"{WeaponCount}x{First.defId}";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            CondensedWeapon other = (CondensedWeapon)obj;
            return First.defId.Equals(other.First.defId) && WeaponCount == other.WeaponCount;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + First.defId.GetHashCode();
                hash = hash * 23 + WeaponCount;
                return hash;
            }
        }
        
        
    }
}
