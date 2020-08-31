using BattleTech;
using CustAmmoCategories;

namespace CleverGirl.Objects
{
    public class WeaponAttackEval
    {
        public Weapon Weapon;

        public float EVDirectDmg = 0f;
        public float EVStructDam = 0f;
        public float EVHeat = 0f;
        public float EVStab = 0f;

        // The sum of all friendly damage (including self) likely to be created
        public float EVFriendlyDmg = 0f;
       
        // The to hit chance for this weapon; the greatest value for non-AoE attacks.
        public float ToHit = 0f;

        // The best AmmoModePair for this weapon and target
        public AmmoModePair AmmoMode;

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
