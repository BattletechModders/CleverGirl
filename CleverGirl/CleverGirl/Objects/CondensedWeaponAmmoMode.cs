using System.Collections.Generic;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;

namespace CleverGirl;

public class CondensedWeaponAmmoMode
{
    public int weaponsCondensed;
    public List<Weapon> condensedWeapons;
    public AmmoModePair ammoModePair;
    private AmmoModePair baseModePair;

    public CondensedWeaponAmmoMode(CondensedWeapon condensedWeapon, AmmoModePair ammoModePair)
    {
        weaponsCondensed = condensedWeapon.weaponsCondensed;
        condensedWeapons = condensedWeapon.condensedWeapons;
        this.ammoModePair = ammoModePair;
        baseModePair = First.getCurrentAmmoMode();
    }

    public Weapon First => condensedWeapons[0];

    public void ApplyAmmoMode()
    {
        First.ApplyAmmoMode(ammoModePair);
    }

    public void RestoreBaseAmmoMode()
    {
        First.ApplyAmmoMode(baseModePair);
        First.ResetTempAmmo();
    }
}