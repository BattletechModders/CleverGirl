
namespace CleverGirl {

    public class ModConfig {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public void LogConfig() {
            Mod.Log.Info("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");

#if USE_CAC
            Mod.Log.Info($" Enabling CustomAmmoCategories tweaks");
#endif
#if USE_CC
            Mod.Log.Info($" Enabling CustomComponents tweaks");
#endif

            Mod.Log.Info("=== MOD CONFIG END ===");
        }

        public override string ToString() {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
