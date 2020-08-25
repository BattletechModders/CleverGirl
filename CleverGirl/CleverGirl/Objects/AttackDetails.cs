using BattleTech;
using UnityEngine;

namespace CleverGirl.Objects
{
    public class AttackDetails
    {
        public readonly AbstractActor Attacker;
        public readonly Vector3 AttackPosition;
        public readonly ICombatant Target;
        public readonly Vector3 TargetPosition;

        public readonly bool UseRevengeBonus;
        public readonly bool IsBreachingShotAttack;

        public readonly AIUtil.AttackType AttackType;
        public readonly MeleeAttackType MeleeAttackType;
        public readonly AttackImpactQuality Quality;


        public bool TargetIsBraced
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.BracedLastRound;
            }
            private set { }
        }
        public bool TargetIsEvasive
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.IsEvasive;
            }
            private set { }
        }
        
        public bool TargetIsUnsteady
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.IsUnsteady;
            }
            private set { }
        }

        
        public AttackDetails(AIUtil.AttackType attackType, AbstractActor attacker, ICombatant target, Vector3 attackPos, Vector3 targetPos, int weaponCount, bool useRevengeBonus)
        {
            this.AttackType = attackType;

            this.Attacker = attacker;
            this.Target = target;

            this.AttackPosition = attackPos;
            this.TargetPosition = targetPos;

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
