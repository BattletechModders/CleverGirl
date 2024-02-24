using System.Collections.Generic;
using System.Linq;
using CustAmmoCategories;

namespace CleverGirl.Objects;

public class AmmoModeAttackEvaluation
{
    public Dictionary<Weapon, AmmoModePair> WeaponList;
    public AIUtil.AttackType AttackType;
    public float HeatGenerated;
    public float ExpectedDamage;
    public float lowestHitChance;

    public int CompareTo(object otherObj)
    {
      if (!(otherObj is AmmoModeAttackEvaluation attackEvaluation))
        return -1;
      int num1 = ExpectedDamage.CompareTo(attackEvaluation.ExpectedDamage);
      if (num1 != 0)
        return num1;
      int num2 = lowestHitChance.CompareTo(attackEvaluation.lowestHitChance);
      if (num2 != 0)
        return num2;
      int num3 = HeatGenerated.CompareTo(attackEvaluation.HeatGenerated);
      return -num3 != 0 ? num3 : -WeaponList.Count.CompareTo(attackEvaluation.WeaponList.Count);
    }

    public override string ToString()
    {
      string weaponString = string.Join(", ", WeaponList.Select(wamp => wamp.Key.UIName + "[" + wamp.Value + "]"));
      return $"Weapons: {weaponString} AttackType: {AttackType} HeatGenerated: {HeatGenerated} ExpectedDamage: {ExpectedDamage} lowestHitChance: {lowestHitChance}";
    }
}