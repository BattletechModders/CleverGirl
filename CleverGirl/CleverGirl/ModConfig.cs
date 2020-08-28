
namespace CleverGirl
{

    public class ModConfig
    {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public class DecisionWeights
        {
            public float FriendlyDamageMulti = 2.0f;

        }
        public DecisionWeights Weights = new DecisionWeights();

        public class DamageConversion
        {

            public float StructDamToRawDamMulti = 5.0f;

            public float TurretHeatDamToRawDamMulti = 2.0f;
            public float TurretStabDamToRawDamMulti = 2.0f;

            public float VehicleHeatDamToRawDamMulti = 2.0f;
            public float VehicleStabDamToRawDamMulti = 2.0f;
        }

        public void LogConfig()
        {
            Mod.Log.Info?.Write("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info?.Write($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");
            Mod.Log.Info?.Write("=== MOD CONFIG END ===");
        }

        public override string ToString()
        {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
