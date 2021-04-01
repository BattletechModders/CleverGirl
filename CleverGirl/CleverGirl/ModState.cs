using BattleTech;
using BattleTech.UI;
using CleverGirl.Analytics;
using CleverGirl.InfluenceMap;
using IRBTModUtils.CustomInfluenceMap;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CleverGirl {

    public static class ModState {

        public static bool WithdrawalTriggered = false;
        public static int RoundsUntilWithdrawal = 0;
        public static HBSDOTweenButton RetreatButton = null;

        // Cache of AI behavior tree properties
        public static ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue> BehaviorVarValuesCache =
            new ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue>();

        // Cache of allied combatants to current actor
        public static ConcurrentDictionary<string, ICombatant> CurrentActorAllies = new ConcurrentDictionary<string, ICombatant>();
        // Cache of neutral combatants to current actor
        public static ConcurrentDictionary<string, ICombatant> CurrentActorNeutrals = new ConcurrentDictionary<string, ICombatant>();
        // Cache of enemy combatants to current actor
        public static ConcurrentDictionary<string, ICombatant> CurrentActorEnemies = new ConcurrentDictionary<string, ICombatant>();
        
        // Cache of local player objectives that need to be destroyed
        public static ConcurrentDictionary<string, ICombatant> LocalPlayerEnemyObjective = new ConcurrentDictionary<string, ICombatant>();

        // Cache of local player objectives that need to be protected
        public static ConcurrentDictionary<string, ICombatant> LocalPlayerFriendlyObjective = new ConcurrentDictionary<string, ICombatant>();

        public static ConcurrentDictionary<string, CombatantAnalytics> CombatantAnalytics = new ConcurrentDictionary<string, CombatantAnalytics>();
        //public static ConcurrentDictionary<ICombatant, float[]> RangeToTargetsAlliesCache =
        //    new ConcurrentDictionary<ICombatant, float[]>();

        // Diagnostic Tools
        public static int StackDepth = 0;
        // Needs compound key of invoke count, total runtime?
        public static Dictionary<string, List<long>> InvokeCounts = new Dictionary<string, List<long>>();

        // InfluenceMap elements
        public static List<CustomInfluenceMapAllyFactor> CustomAllyFactors = new List<CustomInfluenceMapAllyFactor>();
        public static List<InfluenceMapAllyFactor> AllyFactorsToRemove = new List<InfluenceMapAllyFactor>();

        public static List<CustomInfluenceMapHostileFactor> CustomHostileFactors = new List<CustomInfluenceMapHostileFactor>();
        public static List<InfluenceMapHostileFactor> HostileFactorsToRemove = new List<InfluenceMapHostileFactor>();

        public static List<CustomInfluenceMapPositionFactor> CustomPositionFactors = new List<CustomInfluenceMapPositionFactor>();
        public static List<InfluenceMapPositionFactor> PositionFactorsToRemove = new List<InfluenceMapPositionFactor>();

        public static void Reset() {
            // Reinitialize state
            WithdrawalTriggered = false;
            RoundsUntilWithdrawal = 0;
            RetreatButton = null;
            BehaviorVarValuesCache.Clear();
            //RangeToTargetsAlliesCache.Clear();
        }
    }
}
