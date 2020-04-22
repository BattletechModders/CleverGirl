using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CleverGirl.Objects
{
    public class AttackParams
    {
        public bool TargetIsUnsteady;
        public bool TargetIsBraced;
        public bool TargetIsEvasive;

        public bool UseRevengeBonus;
        public bool IsBreachingShotAttack;

        public AIUtil.AttackType AttackType;
        public MeleeAttackType MeleeAttackType;
        public AttackImpactQuality Quality;

        public AttackParams(AIUtil.AttackType attackType, AbstractActor attacker, AbstractActor target, Vector3 attackPos, int weaponCount, bool useRevengeBonus)
        {
            this.AttackType = attackType;

            this.TargetIsUnsteady = target != null && target.IsUnsteady;
            this.TargetIsBraced = target != null && target.BracedLastRound;
            this.TargetIsEvasive = target != null && target.IsEvasive;

            this.UseRevengeBonus = useRevengeBonus;

            this.MeleeAttackType = (attackType != AIUtil.AttackType.Melee) ?
                ((attackType != AIUtil.AttackType.DeathFromAbove) ? MeleeAttackType.NotSet : MeleeAttackType.DFA)
                : MeleeAttackType.MeleeWeapon;

            this.Quality = AttackImpactQuality.Solid;
            this.Quality = attacker.Combat.ToHit.GetBlowQuality(attacker, attackPos, null, target, MeleeAttackType,
                attacker.IsUsingBreachingShotAbility(weaponCount));

            if (attackType == AIUtil.AttackType.Shooting && weaponCount == 1 && attacker.HasBreachingShotAbility)
            {
                IsBreachingShotAttack = true;
            }
        }
    }
}
