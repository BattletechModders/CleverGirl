using BattleTech;
using CustAmmoCategories;
using System;
using System.Collections.Generic;

namespace CleverGirl {
    public static class CACHelper {

        // largely a copy of AIWeaponChoose::getWeaponDamagePredict
        public static Dictionary<WeaponMode, int> UsableModes(Weapon weapon) {
            Dictionary<WeaponMode, int> filteredModes = new Dictionary<WeaponMode, int>();

            ExtWeaponDef extWeaponDef = CustomAmmoCategories.getExtWeaponDef(weapon.defId);
            if (extWeaponDef.Modes.Count < 1) {
                Mod.Log.Error($" Weapon: ({weapon.defId}) has no base or custom modes!");
                return filteredModes;
            }

            List<WeaponMode> availableModes = weapon.AvaibleModes();
            if (availableModes.Count < 1) {
                Mod.Log.Debug($" Weapon: ({weapon.defId}) has no modes that it can use.");
                return filteredModes;
            }
            Mod.Log.Debug($"  weapon: {weapon.defId} has: {availableModes.Count} modes");

            foreach (WeaponMode weaponMode in availableModes) {
                CustomAmmoCategory ammoCategory = CustomAmmoCategories.find(weapon.AmmoCategoryValue.ToString());
                // The weapon defaults to the base ammo type
                if (extWeaponDef.AmmoCategory.BaseCategory == weapon.AmmoCategoryValue) { ammoCategory = extWeaponDef.AmmoCategory; }
                // The weapon mode doesn't match the default ammo type, change
                if (weaponMode.AmmoCategory.Index != ammoCategory.Index) { ammoCategory = weaponMode.AmmoCategory; }
                // Hardcode the comparison here b/c CustomAmmoCategories.NotSetCustomAmmoCategoty.Index is private 
                if (ammoCategory.Index == 0) {
                    Mod.Log.Info("Ammo type is unset, skipping.");
                }
                foreach (AmmunitionBox ammoBox in weapon.ammoBoxes) {
                    if (ammoBox.IsFunctional == false) { continue; }
                    if (ammoBox.CurrentAmmo <= 0 ) { continue; }
                    CustomAmmoCategory boxAmmoCategory = CustomAmmoCategories.getAmmoAmmoCategory(ammoBox.ammoDef);
                    if (boxAmmoCategory.Index == ammoCategory.Index) {
                        Mod.Log.Info($" found ammoBox:{ammoBox.Name} with count: {ammoBox.CurrentAmmo} for weapon: {weapon.defId} that has shots: {weaponMode.ShotsWhenFired}");
                        if (filteredModes.ContainsKey(weaponMode)) {
                            filteredModes[weaponMode] += (int)Math.Floor((double)ammoBox.CurrentAmmo / weaponMode.ShotsWhenFired);
                        } else {
                            filteredModes[weaponMode] = (int)Math.Floor((double)ammoBox.CurrentAmmo / weaponMode.ShotsWhenFired);
                        }
                    }
                }
            }

            // Unnecessary here, but useful elsewhere to set the active mode
            //string currentMode = extWeaponDef.baseModeId;
            //if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.WeaponModeStatisticName) == true) {
            //    currentMode = weapon.StatCollection.GetStatistic(CustomAmmoCategories.WeaponModeStatisticName).Value<string>();
            //}

            //string currentAmmo = "";
            //if (CustomAmmoCategories.checkExistance(weapon.StatCollection, CustomAmmoCategories.AmmoIdStatName) == true) {
            //    currentAmmo = weapon.StatCollection.GetStatistic(CustomAmmoCategories.AmmoIdStatName).Value<string>();
            //}

            Mod.Log.Debug($"Returning {filteredModes.Count} for weapon: {weapon.defId}");
            return filteredModes;
        }

    }
}
