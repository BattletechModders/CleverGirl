
using BattleTech;
using Harmony;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CleverGirl {
    public class AIHelper {

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
