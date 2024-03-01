using System.Collections.Generic;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;

namespace CleverGirl;

public class CondensedWeaponAmmoMode
{
    private readonly CondensedWeapon condensedWeapon;
    private readonly AmmoModePair baseModePair;
    public readonly AmmoModePair ammoModePair;

    public CondensedWeaponAmmoMode(CondensedWeapon condensedWeapon, AmmoModePair ammoModePair)
    {
        this.condensedWeapon = condensedWeapon;
        this.ammoModePair = ammoModePair;
        baseModePair = First.getCurrentAmmoMode();
    }

    public Weapon First => condensedWeapon.First;

    public int weaponsCondensed => condensedWeapon.weaponsCondensed;

    public List<Weapon> condensedWeapons => condensedWeapon.condensedWeapons;

    public void ApplyAmmoMode()
    {
        First.ApplyAmmoMode(ammoModePair);
    }

    public void RestoreBaseAmmoMode()
    {
        First.ApplyAmmoMode(baseModePair);
        First.ResetTempAmmo();
    }

    public override string ToString()
    {
        return First.UIName + "/" + ammoModePair.modeId + "/" + ammoModePair.ammoId;
    }
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        CondensedWeaponAmmoMode other = (CondensedWeaponAmmoMode)obj;
        return condensedWeapon.Equals(other.condensedWeapon) && ammoModePair.Equals(other.ammoModePair);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + condensedWeapon.GetHashCode();
            hash = hash * 23 + ammoModePair.GetHashCode();
            return hash;
        }
    }
}