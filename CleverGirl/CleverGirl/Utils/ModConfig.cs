
namespace CleverGirl {

    public class ModConfig {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public bool CustomAmmoCategoriesDetected = false;

        public void LogConfig() {
            Mod.Log.Info("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");
            Mod.Log.Info($" PROFILING -- Enabled:{this.Profile}");
            Mod.Log.Info($" CAC Detected:{this.CustomAmmoCategoriesDetected}");
            Mod.Log.Info("=== MOD CONFIG END ===");
        }

        public override string ToString() {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
