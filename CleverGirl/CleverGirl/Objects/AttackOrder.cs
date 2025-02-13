using System.Collections.Generic;
using System.Linq;
using CustAmmoCategories;

namespace CleverGirl.Objects;

public class AttackOrder
{
    internal BehaviorTreeResults Order;
    internal AmmoModeAttackEvaluation AttackEvaluation;

    internal OrderInfo OrderInfo => Order?.orderInfo;
    internal string OrderString => Order?.debugOrderString;
    internal IEnumerable<Weapon> Weapons => AttackEvaluation?.WeaponAmmoModes?.Keys ?? Enumerable.Empty<Weapon>();
    internal AmmoModePair SelectedAmmoMode(Weapon weapon) => AttackEvaluation?.WeaponAmmoModes[weapon];

    internal string TargetId => (OrderInfo as AttackOrderInfo)?.TargetUnit?.DistinctId() ?? "";
}