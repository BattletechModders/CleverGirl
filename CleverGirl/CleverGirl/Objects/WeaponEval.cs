using BattleTech;
using CustAmmoCategories;

namespace CleverGirl.Objects
{
    public class WeaponEval
    {
        public Weapon Weapon;
        public float ExpectedValue = 0f;
        public float HeatRatio = 0f;
        public AmmoModePair AmmoMode;
    }
}
