using BattleTech.UI;
using System.Collections.Generic;

namespace CleverGirl {

    public static class State {

        public static bool WithdrawalTriggered = false;
        public static int RoundsUntilWithdrawal = 0;
        public static HBSDOTweenButton RetreatButton = null;

        // Diagnostic Tools
        public static int StackDepth = 0;
        // Needs compound key of invoke count, total runtime?
        public static Dictionary<string, List<long>> InvokeCounts = new Dictionary<string, List<long>>();

    }
}
