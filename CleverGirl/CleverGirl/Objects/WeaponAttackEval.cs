using BattleTech;
using CustAmmoCategories;

namespace CleverGirl.Objects
{
    public class WeaponAttackEval
    {
        public Weapon Weapon;
        public float ExpectedDamage = 0f;
        public float ExpectedSelfDamage = 0f;
        public float ExpectedHeatDamage = 0f;
        public float ExpectedStabDamage = 0f;
        public float ChanceToHit = 0f;
        public AmmoModePair OptimalAmmoMode;

        public float DamagePerHeatRatio
        {
            get
            {
                return Weapon?.HeatGenerated != 0 ? ExpectedDamage / Weapon.HeatGenerated : ExpectedDamage;
            }
            private set { }
        }
    }
}
