using System;
using System.Collections.Generic;
using System.Linq;
using CBTBehaviorsEnhanced.Helper;
using CBTBehaviorsEnhanced.MeleeStates;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using CustomComponents;
using UnityEngine;

namespace CleverGirl.Helper;

public static class MeleeHelper
{
    // Code below copied from CBTBE to be able to use condensed weapons and ammo/mode iteration
    //
    // Determine the best possible melee attack for a given attacker, target, and position
    //  - Usable weapons will include a MeleeWeapon/DFAWeapon with the damage set to the expected virtual damage BEFORE toHit is applied
    //     this is necessary to allow the EV calculations to proccess in CG
    //  - attackPos has to be a valid attackPosition for the target. This can be the 'safest' position as evaluated by 
    //     FindBestPositionToMeleeFrom
    public static void OptimizeMelee(Mech attacker, AbstractActor target, Vector3 attackPos,
        List<CondensedWeapon> canFireInMeleeWeapons,
        out List<CondensedWeapon> usableWeapons,
        out float virtualMeleeDamage, out float totalStateDamage)
    {
        usableWeapons = new List<CondensedWeapon>();
        virtualMeleeDamage = 0f;
        totalStateDamage = 0f;
        MeleeAttack selectedState = null;

        Mech targetMech = target as Mech;

        try
        {
            Mod.MeleeLog.Info?.Write($"=== Optimizing melee attack for attacker: {attacker.DistinctId()} vs. " +
                                  $"target: {target.DistinctId()} at attackPos: {attackPos} with {canFireInMeleeWeapons.Count} melee ranged weapons.");

            Mod.MeleeLog.Info?.Write($"Generating melee state - see melee log.");
            MeleeState meleeStates = CBTBehaviorsEnhanced.Helper.MeleeHelper.GetMeleeStates(attacker, attackPos, target);

            // Iterate each state, add physical and weapon damage and evaluate virtual benefit from the sum
            Mod.MeleeLog.Info?.Write($"Iterating non-DFA melee states.");
            float highestStateDamage = 0f;
            List<MeleeAttack> allStates = new List<MeleeAttack> { meleeStates.Charge, meleeStates.Kick, meleeStates.PhysicalWeapon, meleeStates.Punch };
            foreach (MeleeAttack meleeState in allStates)
            {
                Mod.MeleeLog.Info?.Write($"Evaluating damage for state: {meleeState.Label}");
                if (!meleeState.IsValid)
                {
                    Mod.MeleeLog.Info?.Write($" -- melee state is invalid, skipping.");
                    continue;
                }

                Mod.MeleeLog.Info?.Write($"  -- Checking ranged weapons");
                float rangedDamage = 0;
                float rangedStab = 0;
                float rangedHeat = 0;
                List<CondensedWeapon> stateWeapons = new List<CondensedWeapon>();
                foreach (CondensedWeapon cWeapon in canFireInMeleeWeapons)
                {
                    Weapon weapon = cWeapon.First;
                    if (meleeState.IsRangedWeaponAllowed(weapon) && !weapon.AOECapable)
                    {
                        stateWeapons.Add(cWeapon);

                        AmmoModePair currentAmmoMode = weapon.getCurrentAmmoMode();
                        float weaponMaxRangedDamage = 0f;
                        float weaponMaxRangedStab = 0f;
                        float weaponMaxRangedHeat = 0f;
                        
                        foreach (AmmoModePair ammoMode in cWeapon.ammoModes)
                        {
                            weapon.ApplyAmmoMode(ammoMode);

                            weaponMaxRangedDamage = Mathf.Max(weaponMaxRangedDamage, weapon.DamagePerShot * weapon.ShotsWhenFired + weapon.StructureDamagePerShot * weapon.ShotsWhenFired);
                            weaponMaxRangedStab = Mathf.Max(weaponMaxRangedStab, weapon.Instability() * weapon.ShotsWhenFired);
                            weaponMaxRangedHeat = Mathf.Max(weaponMaxRangedHeat, weapon.HeatDamagePerShot * weapon.ShotsWhenFired);
                        }

                        weapon.ApplyAmmoMode(currentAmmoMode);

                        rangedDamage += weaponMaxRangedDamage * cWeapon.WeaponCount;
                        rangedStab += weaponMaxRangedStab * cWeapon.WeaponCount;
                        rangedHeat += weaponMaxRangedHeat * cWeapon.WeaponCount;
                        
                        Mod.MeleeLog.Info?.Write($"  weapon: {weapon.UIName} adds damage: {weaponMaxRangedDamage}  instab: {weaponMaxRangedStab}  heat: {weaponMaxRangedHeat}");
                    }
                }

                float meleeDamage = meleeState.TargetDamageClusters.Sum();
                float stateTotalDamage = meleeDamage + rangedDamage;
                if (targetMech != null)
                {
                    float virtualDamage = CleverGirlCalculator.CalculateUtilityVirtualDamage(attacker, attackPos, targetMech, rangedStab, meleeState);

                    stateTotalDamage += virtualDamage;
                    // Add to melee damage as well, to so it can be set on the melee weapon
                    meleeDamage += virtualDamage;
                }

                if (stateTotalDamage > highestStateDamage)
                {
                    Mod.MeleeLog.Debug?.Write($"  State {meleeState.Label} exceeds previous state damages, adding it as highest damage state");
                    highestStateDamage = stateTotalDamage;

                    totalStateDamage = stateTotalDamage;
                    virtualMeleeDamage = meleeDamage;

                    selectedState = meleeState;

                    usableWeapons.Clear();
                    usableWeapons.AddRange(stateWeapons);
                }
            }

            Mod.MeleeLog.Info?.Write($"Iteration complete.");

            Mod.MeleeLog.Info?.Write($"=== Best state for attacker: {attacker.DistinctId()} vs. " +
                                  $"target: {target.DistinctId()} at attackPos: {attackPos} is state: {selectedState?.Label} with " +
                                  $"virtualMeleeDamage: {virtualMeleeDamage} and totalStateDamage: {totalStateDamage}");
            Mod.MeleeLog.Info?.Write("");
        }
        catch (Exception e)
        {
            Mod.MeleeLog.Warn?.Write(e, $"Failed to optimize melee attack! ");
            Mod.MeleeLog.Warn?.Write($"  Attacker: {(attacker == null ? "IS NULL" : attacker.DistinctId())}");
            Mod.MeleeLog.Warn?.Write($"  Target: {(target == null ? "IS NULL" : target.DistinctId())}");
            Mod.MeleeLog.Warn?.Write("");
        }

        return;
    }

    public static bool CanFireInMelee(this Weapon weapon, float distance)
    {
        if (!weapon.CanFireInMelee())
        {
            return false;
        }

        foreach (AmmoModePair ammoModePair in weapon.getAvaibleFiringMethods())
        {
            weapon.ApplyAmmoMode(ammoModePair);
            if (weapon.ForbiddenRange() < distance)
            {
                return true;
            }
        }

        return false;
    } 

    public static bool CanFireInMelee(this Weapon weapon)
    {
        if (!weapon.WeaponCategoryValue.CanUseInMelee)
        {
            return false;
        }

        return !weapon.baseComponentRef?.GetComponents<Category>().Any(cat => cat.CategoryID.Equals(Mod.Config.NoMeleeWeaponCategory)) ?? false;
    } 
}