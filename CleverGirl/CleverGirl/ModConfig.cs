
namespace CleverGirl {

    public class ModConfig {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public class DecisionWeights {
            public float FriendlyDamageMulti = 2.0f;
        }
        public DecisionWeights Weights = new DecisionWeights();

        public void LogConfig() {
            Mod.Log.Info("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");
            Mod.Log.Info("=== MOD CONFIG END ===");
        }

        public override string ToString() {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
