using BattleTech;
using CustAmmoCategories;

namespace CleverGirl.Objects
{
    public class WeaponAttackEval
    {
        public Weapon Weapon;
        public float ExpectedDamage = 0f;
        public float ExpectedSelfDamage = 0f;
        public float ExpectedHeat = 0f;
        public AmmoModePair OptimalAmmoMode;
    }
}
