using System.Collections.Generic;
using System.Linq;
using CustAmmoCategories;

namespace CleverGirl.Objects;

public class AmmoModeAttackEvaluation
{
    public Dictionary<Weapon, AmmoModePair> WeaponAmmoModes;
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
      return -num3 != 0 ? num3 : -WeaponAmmoModes.Count.CompareTo(attackEvaluation.WeaponAmmoModes.Count);
    }

    public override string ToString()
    {
      string weaponString = string.Join(", ", WeaponAmmoModes.Select(wamp => wamp.Key.UIName + "[" + wamp.Value + "]"));
      return $"Weapons: {weaponString} AttackType: {AttackType} HeatGenerated: {HeatGenerated} ExpectedDamage: {ExpectedDamage} lowestHitChance: {lowestHitChance}";
    }

    public AttackEvaluator.AttackEvaluation ToSimpleAttackEvaluation()
    {
      AttackEvaluator.AttackEvaluation simple = new AttackEvaluator.AttackEvaluation();
      simple.AttackType = AttackType;
      simple.ExpectedDamage = ExpectedDamage;
      simple.HeatGenerated = HeatGenerated;
      simple.lowestHitChance = lowestHitChance;
      simple.WeaponList = new List<Weapon>(WeaponAmmoModes.Keys);
      return simple;
    }
}