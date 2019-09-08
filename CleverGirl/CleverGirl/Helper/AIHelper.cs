
using BattleTech;
using Harmony;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CleverGirl {
    public class AIHelper {

        public static int HeatForAttack(List<CondensedWeapon> weaponList) {
            int num = 0;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                num += ((int)cWeapon.First.HeatGenerated * cWeapon.weaponsCondensed);
            }
            return num;
        }

        public static float LowestHitChance(List<CondensedWeapon> weaponList, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive) {
            float num = float.MaxValue;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                float toHitFromPosition = cWeapon.First.GetToHitFromPosition(target, 1, attackPosition, targetPosition, true, targetIsEvasive, false);
                num = Mathf.Min(num, toHitFromPosition);
            }
            return num;
        }

        public static float ExpectedDamageForAttack(AbstractActor unit, AIUtil.AttackType attackType, List<CondensedWeapon> weaponList,
            ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext) {

            Mech mech = unit as Mech;
            AbstractActor abstractActor = target as AbstractActor;

            // Attack type is melee and there's no path or ability, fail
            if (attackType == AIUtil.AttackType.Melee &&
                (abstractActor == null || mech == null || mech.Pathing.GetMeleeDestsForTarget(abstractActor).Count == 0)) {
                return 0f;
            }

            // Attack type is DFA and there's no path or no ability, fail
            if (attackType == AIUtil.AttackType.DeathFromAbove &&
                (abstractActor == null || mech == null || mech.JumpPathing.GetDFADestsForTarget(abstractActor).Count == 0)) {
                return 0f;
            }

            // Attack type is range and there's no weapons, fail
            if (attackType == AIUtil.AttackType.Shooting && weaponList.Count == 0) {
                return 0f;
            }

            AttackParams attackParams = new AttackParams(attackType, unit, target as AbstractActor, attackPosition, weaponList.Count, useRevengeBonus);

            float totalExpectedDam = 0f;
            for (int i = 0; i < weaponList.Count; i++) {
                CondensedWeapon cWeapon = weaponList[i];
                totalExpectedDam += CalculateWeaponDamageEV(cWeapon, unitForBVContext.BehaviorTree, attackParams, unit, attackPosition, target, targetPosition);
            }

            float blowQualityMultiplier = unit.Combat.ToHit.GetBlowQualityMultiplier(attackParams.Quality);
            float totalDam = totalExpectedDam * blowQualityMultiplier;

            return totalDam;
        }

        private class AttackParams {
            public bool TargetIsUnsteady;
            public bool TargetIsBraced;
            public bool TargetIsEvasive;

            public bool UseRevengeBonus;
            public bool IsBreachingShotAttack;

            public AIUtil.AttackType AttackType;
            public MeleeAttackType MeleeAttackType;
            public AttackImpactQuality Quality;

            public AttackParams(AIUtil.AttackType attackType, AbstractActor attacker, AbstractActor target, Vector3 attackPos, int weaponCount, bool useRevengeBonus) {
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

                if (attackType == AIUtil.AttackType.Shooting && weaponCount == 1 && attacker.HasBreachingShotAbility) {
                    IsBreachingShotAttack = true;
                }
            }
        }

        private static float CalculateWeaponDamageEV(CondensedWeapon cWeapon, BehaviorTree bTree, AttackParams attackParams,
            AbstractActor attacker, Vector3 attackerPos, ICombatant target, Vector3 targetPos) {

            int shotsWhenFired = cWeapon.First.ShotsWhenFired;

            float attackTypeWeight = 1f;
            switch (attackParams.AttackType) {
                case AIUtil.AttackType.Shooting: {
                        attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                        break;
                    }
                case AIUtil.AttackType.Melee: {
                        Mech targetMech = target as Mech;
                        Mech attackingMech = attacker as Mech;
                        if (attackParams.UseRevengeBonus && targetMech != null && attackingMech != null && attackingMech.IsMeleeRevengeTarget(targetMech)) {
                            attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeRevengeBonus).FloatVal;
                        }
                        if (attackingMech != null && cWeapon.First == attackingMech.MeleeWeapon) {
                            attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeDamageMultiplier).FloatVal;
                            if (attackParams.TargetIsUnsteady) {
                                attackTypeWeight += AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeVsUnsteadyTargetDamageMultiplier).FloatVal;
                            }
                        } else {
                            attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                        }
                        break;
                    }
                case AIUtil.AttackType.DeathFromAbove: {
                        Mech attackerMech = attacker as Mech;
                        if (attackerMech != null && cWeapon.First == attackerMech.DFAWeapon) {
                            attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_DFADamageMultiplier).FloatVal;
                        } else {
                            attackTypeWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                        }
                        break;
                    }
                default:
                    Debug.LogError("unknown attack type: " + attackParams.AttackType);
                    break;
            }

            float toHitFromPos = cWeapon.First.GetToHitFromPosition(target, 1, attackerPos, targetPos, true, attackParams.TargetIsEvasive, false);
            if (attackParams.IsBreachingShotAttack) {
                // Breaching shot is assumed to auto-hit... why?
                toHitFromPos = 1f;
            }

            float damagePerShotFromPos = cWeapon.First.DamagePerShotFromPosition(attackParams.MeleeAttackType, attackerPos, target);

            float heatDamPerShotWeight = AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_HeatToDamageRatio).FloatVal * cWeapon.First.HeatDamagePerShot;

            float stabilityDamPerShotWeight = 0f;
            if (attackParams.TargetIsUnsteady) {
                stabilityDamPerShotWeight = cWeapon.First.Instability() * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;
            }

            float meleeStatusWeights = 0f;
            meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsBraced) ?
                0f : (damagePerShotFromPos * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal));
            meleeStatusWeights += ((attackParams.AttackType != AIUtil.AttackType.Melee || !attackParams.TargetIsEvasive) ?
                0f : (damagePerShotFromPos * AIHelper.GetCachedBehaviorVariableValue(bTree, BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal));

            // If this is an aggregate weapon and the toHitFromPos isn't multiplied by count, does it make a diff?
            // i.e. this would be (2 * 0.7 * X) => 1.4x per, w/ 8 weapons -> 8 * 1.4 => 11.2
            //   with (8 * 2 * 0.7 * X) => 11.2
            float weaponDamageEV = (float)shotsWhenFired * toHitFromPos * (damagePerShotFromPos + heatDamPerShotWeight + stabilityDamPerShotWeight + meleeStatusWeights);

            float aggregateDamageEV = weaponDamageEV * cWeapon.weaponsCondensed;

            return aggregateDamageEV;
        }

        public class AggregateWeapon {
            public Weapon weapon;
            public int count;

            public AggregateWeapon(Weapon weapon, int count) {
                this.weapon = weapon;
                this.count = count;
            }
        }

        public static List<AggregateWeapon> AggregateWeaponList(List<Weapon> allWeapons) {
            Dictionary<string, AggregateWeapon> aggregates = new Dictionary<string, AggregateWeapon>();

            foreach(Weapon weapon in allWeapons) {
                if (!aggregates.ContainsKey(weapon.defId)) {
                    AggregateWeapon aw = new AggregateWeapon(weapon, 1);
                    aggregates.Add(weapon.defId, aw);
                } else {
                    aggregates[weapon.defId].count++;
                }
            }

            return aggregates.Select(kvp => kvp.Value).ToList();
        }


        // --- BEHAVIOR VARIABLE BELOW
        private static ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue> behVarValCache = 
            new ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue>();

        public static void ResetBehaviorCache() { behVarValCache.Clear(); }

        public static BehaviorVariableValue GetCachedBehaviorVariableValue(BehaviorTree bTree, BehaviorVariableName name) {
            return behVarValCache.GetOrAdd(name, GetBehaviorVariableValue(bTree, name));
        }

        public static BehaviorVariableValue GetBehaviorVariableValue(BehaviorTree bTree, BehaviorVariableName name) {
            BehaviorVariableValue behaviorVariableValue = bTree.unitBehaviorVariables.GetVariable(name);
            if (behaviorVariableValue != null) {
                return behaviorVariableValue;
            }

            Pilot pilot = bTree.unit.GetPilot();
            if (pilot != null) {
                BehaviorVariableScope scopeForAIPersonality = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAIPersonality(pilot.pilotDef.AIPersonality);
                if (scopeForAIPersonality != null) {
                    behaviorVariableValue = scopeForAIPersonality.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                    if (behaviorVariableValue != null) {
                        return behaviorVariableValue;
                    }
                }
            }

            if (bTree.unit.lance != null) {
                behaviorVariableValue = bTree.unit.lance.BehaviorVariables.GetVariable(name);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            if (bTree.unit.team != null) {
                Traverse bvT = Traverse.Create(bTree.unit.team).Field("BehaviorVariables");
                BehaviorVariableScope bvs = bvT.GetValue<BehaviorVariableScope>();
                behaviorVariableValue = bvs.GetVariable(name);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            UnitRole unitRole = bTree.unit.DynamicUnitRole;
            if (unitRole == UnitRole.Undefined) {
                unitRole = bTree.unit.StaticUnitRole;
            }

            BehaviorVariableScope scopeForRole = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForRole(unitRole);
            if (scopeForRole != null) {
                behaviorVariableValue = scopeForRole.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                if (behaviorVariableValue != null) {
                    return behaviorVariableValue;
                }
            }

            if (bTree.unit.CanMoveAfterShooting) {
                BehaviorVariableScope scopeForAISkill = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAISkill(AISkillID.Reckless);
                if (scopeForAISkill != null) {
                    behaviorVariableValue = scopeForAISkill.GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
                    if (behaviorVariableValue != null) {
                        return behaviorVariableValue;
                    }
                }
            }

            behaviorVariableValue = bTree.unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariableWithMood(name, bTree.unit.BehaviorTree.mood);
            if (behaviorVariableValue != null) {
                return behaviorVariableValue;
            }

            return DefaultBehaviorVariableValue.GetSingleton();
        }
    }
}
