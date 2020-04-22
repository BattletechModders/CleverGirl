using BattleTech;
using CleverGirl.Helper;
using CleverGirl.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AttackEvaluator;
using static CleverGirl.AIHelper;

namespace CleverGirl.Calculator
{
    public static class RangedCalculator
    {
        public static AttackEvaluation OptimizeAttack(List<Weapon> weapons, Mech attacker, ICombatant target)
        {
            AttackEvaluation attackEvaluation = new AttackEvaluation();

            // TODO: Need to handle HasBreachingShotAbility. Check to see if a single weapon that blows through cover is better. Start with highest dam weapon.

            /*
             * 1. Check that weapons, attacker, target is not null
             * 2. Weapons are already checked for LoF and range
             * 3. Build an EV and Heat ratio for each weapon
             * 4. Evaluate heat - sum all heat values, but drop weapons step by step until below safe threshold
             */

            AttackParams attackParams = new AttackParams(AIUtil.AttackType.Shooting, attacker, target as AbstractActor, attacker.CurrentPosition, 2, true);

            List<WeaponEval> evaluations = new List<WeaponEval>();
            for (int i = 0; i < weapons.Count; i++)
            {
                Weapon weapon = weapons[i];
                totalExpectedDam += WeaponHelper.CalculateWeaponDamageEV(weapon, attacker, target, attackParams);
            }

            return attackEvaluation;
        }
    }
}
