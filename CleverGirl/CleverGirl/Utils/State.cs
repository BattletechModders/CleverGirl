using BattleTech.UI;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CleverGirl {

    public static class State {

        public static bool WithdrawalTriggered = false;
        public static int RoundsUntilWithdrawal = 0;
        public static HBSDOTweenButton RetreatButton = null;

        public static ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue> BehaviorVarValuesCache =
            new ConcurrentDictionary<BehaviorVariableName, BehaviorVariableValue>();

        // Diagnostic Tools
        public static int StackDepth = 0;
        // Needs compound key of invoke count, total runtime?
        public static Dictionary<string, List<long>> InvokeCounts = new Dictionary<string, List<long>>();

        public static void Reset() {
            // Reinitialize state
            WithdrawalTriggered = false;
            RoundsUntilWithdrawal = 0;
            RetreatButton = null;
            BehaviorVarValuesCache.Clear();
        }
    }
}
