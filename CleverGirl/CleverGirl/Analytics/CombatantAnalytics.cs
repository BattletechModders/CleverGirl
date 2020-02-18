using BattleTech;

namespace CleverGirl.Analytics {
    public class CombatantAnalytics {

        public CombatantAnalytics(ICombatant combatant) {
            
            if (combatant is Building) { isBuilding = true; } 
            else if (combatant is Mech) { isMech = true; } 
            else if (combatant is Turret) { isTurret = true; } 
            else if (combatant is Vehicle) { isVehicle = true; }

        }

        public ICombatant Combatant;
        public bool isBuilding = false;
        public bool isTurret = false;
        public bool isVehicle = false;
        public bool isMech = false;

        public int EvasionPips = 0;

        public int StabDamageToKnockdown = 0;
        public int StabDamageToUnsteady = 0;

        public float TotalDamageReduction = 0f;
        public float DamageReductionAtUnsteady = 0f;
        
        public float TotalFirepower = 0f;
        public float LongRangedFirepower = 0f;
        public float MediumRangeFirepower = 0f;
        public float ShortRangeFirepower = 0f;
        public float MeleeFirepower = 0f;

        public float EWDanger = 0f;

        public int HeatToLethalAmmoExplosion = 0;
        public int HeatToShutdown = 150;

        public float Armor = 0f;
        public float TotalArmorRatio = 0f;
        public float CriticalArmorRatio = 0f;

        public float EvasionAsDefenseRatio = 0f;
    }
}
