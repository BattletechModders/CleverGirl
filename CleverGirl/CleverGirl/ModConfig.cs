
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

        public int SimplifiedAmmoModeSelectionThreshold = 50;
        public bool AttemptReducingOverheatSolutions = false;
        public DecisionWeights Weights = new DecisionWeights();
        public List<string> BlockedDlls = new List<string>();
        public List<string> RestrictFiringModeToFlyingTargets = new List<string>();
        public string NoMeleeWeaponCategory = "NeverMelee";
        public float PrioritizeArtilleryDamageRatio = 0.3f;
        public void LogConfig() {
            Mod.Log.Info?.Write("=== MOD CONFIG BEGIN ===");
            Mod.Log.Info?.Write($" LOGGING -- Debug:{this.Debug} Trace:{this.Trace}");
            Mod.Log.Info?.Write($" CBTBEMelee: {this.UseCBTBEMelee}");
            Mod.Log.Info?.Write($" SimplifiedAmmoModeSelectionThreshold: {this.SimplifiedAmmoModeSelectionThreshold}");
            Mod.Log.Info?.Write($" AttemptReducingOverheatSolutions: {this.AttemptReducingOverheatSolutions}");
            Mod.Log.Info?.Write($" RestrictToFlyingTargetsFiringModes: {string.Join(", ", RestrictFiringModeToFlyingTargets)}");
            Mod.Log.Info?.Write($" NoMeleeWeaponCategory: {NoMeleeWeaponCategory}");
            Mod.Log.Info?.Write($" PrioritizeArtilleryDamageRatio: {PrioritizeArtilleryDamageRatio}");
            Mod.Log.Info?.Write("");
            Mod.Log.Info?.Write("--- Decision Weights ---");
            Mod.Log.Info?.Write($" FriendlyDamageMulti: {this.Weights.FriendlyDamageMulti}");
            Mod.Log.Info?.Write($" PunchbotDamageMulti: {this.Weights.PunchbotDamageMulti}");
            Mod.Log.Info?.Write($" OneShotMinimumToHit: {this.Weights.OneShotMinimumToHit}");
            Mod.Log.Info?.Write("=== MOD CONFIG END ===");
        }

        public override string ToString() {
            return $"Logging - Debug:{Debug}  Trace:{Trace}";
        }
    }
}
