
using System.Collections.Generic;

namespace CleverGirl {

    public class ModConfig {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public bool UseCBTBEMelee = false;

        public class DecisionWeights {
            public float FriendlyDamageMulti = 2.0f;

            public float PunchbotDamageMulti = 2.0f;

            public float OneShotMinimumToHit = 0.4f;
        }
        public DecisionWeights Weights = new DecisionWeights();
        public List<string> BlockedDlls = new List<string>();
        public void LogConfig() {
            Mod.Log.Info?.Write("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info?.Write($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");
            Mod.Log.Info?.Write($" CBTBEMelee: {this.UseCBTBEMelee}");
            Mod.Log.Info?.Write("");
            Mod.Log.Info?.Write("--- Decision Weights ---");
            Mod.Log.Info?.Write($" FriendlyDamageMulti: {this.Weights.FriendlyDamageMulti}");
            Mod.Log.Info?.Write($" PunchbotDamageMulti: {this.Weights.PunchbotDamageMulti}");
            Mod.Log.Info?.Write("=== MOD CONFIG END ===");
        }

        public override string ToString() {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
