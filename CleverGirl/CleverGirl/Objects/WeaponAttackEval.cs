using BattleTech;
using CustAmmoCategories;

namespace CleverGirl.Objects
{
    public class WeaponAttackEval
    {
        public Weapon Weapon;
        // The amount of direct damage (armor) that will be done
        public float EVDirectDmg = 0f;

        // The amount of structure damage we predict will be done
        public float EVStructDam = 0f;
        
        // The amount of damage done to the attacker
        public float EVSelfDmg = 0f;
        
        // The amount of heat damage applied to the target
        public float EVHeatDmg = 0f;

        // The amount of stability damage applied to the target
        public float EVStabDmg = 0f;

        // The amount of utility applied to the target (i.e. knockdown, acid, etc)
        public float EVUtility = 0f;
        
        // The chance to hit for this weapon
        public float ChanceToHit = 0f;

        // The best AmmoModePair for this weapon and target
        public AmmoModePair OptimalAmmoMode;

        public float DirectDmgPerHeat
        {
            get
            {
                return Weapon?.HeatGenerated != 0 ? EVDirectDmg / Weapon.HeatGenerated : EVDirectDmg;
            }
            private set { }
        }

    }
}
