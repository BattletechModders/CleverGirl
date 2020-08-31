
using Org.BouncyCastle.Security;

namespace CleverGirl
{

    public class ModConfig
    {

        public bool Debug = false;
        public bool Trace = false;
        public bool Profile = false;

        public class BreachingShot
        {
            // Assuming breaching shot is against target w/ 0.6 damage (20% cover, 20% guarded)
            public float Multi = 2.0f;
        }

        public class DamageMulti
        {
            public float Friendly = 2.0f;
            public float Neutral = 0.5f;
            public float Self = 5.0f;
        }

        public class AttackerAmmo
        {
            // If the toHit is below this chance, don't waste the ammo
            public float MinHitChance = 0.25f;
        }

        public class TargetHeat
        {
            // A point where a target is considered impacted by heat, and how 
            //  much to weight expected damage for this scenario
            public float OverheatImpactedLevel = 70f;
            public float OverheatImpactedMulti = 2.0f;

            // A point where a target is considered critically impacted by heat, 
            //  and how much to weight expected damage for this scenario
            public float OverheatCriticalLevel = 130f;
            public float OverheatCriticalMulti = 3.5f;

            // How much to multiply non-mech heat by when it becomes damage
            public float NonMechHeatToDamageMulti = 1.5f;
        }

        public class TargetInstability
        {
            // Virtual damage to apply per evasion pip that would be lost by the target becoming unsteady
            public float DamagePerPipLost = 30f;
        }

        public class DecisionWeights
        {
            public AttackerAmmo AttackerAmmo = new AttackerAmmo();
            public BreachingShot BreachingShot = new BreachingShot();
            public DamageMulti DamageMultis = new DamageMulti();
            public TargetHeat TargetOverheat = new TargetHeat();
            public TargetInstability TargetInstability = new TargetInstability();
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
