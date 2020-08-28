using BattleTech;
using IRBTModUtils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleverGirl.Objects
{
    public class AttackDetails
    {
        public readonly AbstractActor Attacker;
        public readonly Vector3 AttackPosition;
        public readonly ICombatant Target;
        public readonly Vector3 TargetPosition;

        public readonly bool UseRevengeBonus;

        // Precalculated attack values
        public readonly AttackImpactQuality BaseRangedImpactQuality;
        private readonly DesignMaskDef AttackerDesignMask;
        private readonly DesignMaskDef TargetDesignMask;

        // Multipliers for damage done based upon unit stats, designMasks, etc
        private enum DamageMultiType { Ballistic, Energy, Missile, Support, Generic };
        private readonly Dictionary<DamageMultiType, float> DamageMultipliers = new Dictionary<DamageMultiType, float>();

        public bool TargetIsBraced
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.BracedLastRound;
            }
            private set { }
        }
        public bool TargetIsEvasive
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.IsEvasive;
            }
            private set { }
        }
        public bool TargetIsUnsteady
        {
            get
            {
                return Target != null && Target is AbstractActor targetActor && targetActor.IsUnsteady;
            }
            private set { }
        }

        public float BallisticDamageMulti
        {
            get 
            {
                var hasVal = DamageMultipliers.TryGetValue(DamageMultiType.Ballistic, out float multi);
                return hasVal ? multi : 1.0f; 
            } 
            private set { }
        }
        public float EnergyDamageMulti
        {
            get
            {
                var hasVal = DamageMultipliers.TryGetValue(DamageMultiType.Energy, out float multi);
                return hasVal ? multi : 1.0f;
            }
            private set { }
        }
        public float GenericDamageMulti
        {
            get
            {
                var hasVal = DamageMultipliers.TryGetValue(DamageMultiType.Generic, out float multi);
                return hasVal ? multi : 1.0f;
            }
            private set { }
        }
        public float MissileDamageMulti
        {
            get
            {
                var hasVal = DamageMultipliers.TryGetValue(DamageMultiType.Missile, out float multi);
                return hasVal ? multi : 1.0f;
            }
            private set { }
        }
        public float SupportDamageMulti
        {
            get
            {
                var hasVal = DamageMultipliers.TryGetValue(DamageMultiType.Support, out float multi);
                return hasVal ? multi : 1.0f;
            }
            private set { }
        }

        public AttackDetails(AbstractActor attacker, ICombatant target, Vector3 attackPos, Vector3 targetPos, bool useRevengeBonus)
        {
            this.Attacker = attacker;
            this.Target = target;

            this.AttackPosition = attackPos;
            this.TargetPosition = targetPos;

            this.UseRevengeBonus = useRevengeBonus;

            // Precalculate some values heavily used by the prediction engine

            // Impact quality for any melee attack is always solid
            this.BaseRangedImpactQuality = SharedState.Combat.ToHit.GetBlowQuality(attacker, attackPos,
                null, target, MeleeAttackType.Punch, false);

            this.AttackerDesignMask = SharedState.Combat.MapMetaData.GetPriorityDesignMaskAtPos(attackPos);
            if (this.AttackerDesignMask == null) AttackerDesignMask = new DesignMaskDef();
            this.TargetDesignMask = SharedState.Combat.MapMetaData.GetPriorityDesignMaskAtPos(targetPos);
            if (this.TargetDesignMask== null) TargetDesignMask = new DesignMaskDef();

            // Calculate the total damage multiplier for attacks by weaponType
            this.DamageMultipliers.Add(DamageMultiType.Ballistic, CalculateDamageMulti(DamageMultiType.Ballistic, target));
            this.DamageMultipliers.Add(DamageMultiType.Energy, CalculateDamageMulti(DamageMultiType.Energy, target));
            this.DamageMultipliers.Add(DamageMultiType.Missile, CalculateDamageMulti(DamageMultiType.Missile, target));
            this.DamageMultipliers.Add(DamageMultiType.Support, CalculateDamageMulti(DamageMultiType.Support, target));
            this.DamageMultipliers.Add(DamageMultiType.Generic, CalculateDamageMulti(DamageMultiType.Generic, target));
        }

        // CANT CALCULATE:
        //  Weapon.DamagePerShotAdjusted (applies jumping modifier from weapon)
        //  Target weaponType.DamageReductionMultiStat (doesn't follow design mask rules)

        private float CalculateDamageMulti(DamageMultiType type, ICombatant target)
        {
            float totalMulti = 1f;
            
            try
            {
                // Common values that won't change
                float dealtBase = 1.0f * this.AttackerDesignMask.allDamageDealtMultiplier;
                float dealtBiomeBase = 1.0f * SharedState.Combat.MapMetaData.biomeDesignMask.allDamageDealtMultiplier;

                float takenBase = 1.0f * this.TargetDesignMask.allDamageTakenMultiplier;
                float takenBiomeBase = 1.0f * SharedState.Combat.MapMetaData.biomeDesignMask.allDamageTakenMultiplier;

                float targetDamageReduction = target.StatCollection.GetValue<float>(ModStats.Actor_DamageReductionMultipierAll);
                Mod.Log.Debug?.Write($" dealtBase: {dealtBase} dealtBiomeBase: {dealtBiomeBase} " +
                    $"takenbase: {takenBase} takenBiomeBase: {takenBiomeBase}  " +
                    $"targetDamageReduction: {targetDamageReduction}");

                // Values that change by weaponValueType
                float dealtTypeMulti = 1f;
                float dealtBiomeTypeMulti = 1f;
                float takenTypeMulti = 1f;
                float takenBiomeTypeMulti = 1f;
                switch (type)
                {
                    case DamageMultiType.Ballistic:
                        dealtTypeMulti = dealtBase * this.AttackerDesignMask.ballisticDamageDealtMultiplier;
                        dealtBiomeTypeMulti = dealtBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.ballisticDamageDealtMultiplier;
                        takenTypeMulti = takenBase * this.TargetDesignMask.ballisticDamageTakenMultiplier;
                        takenBiomeTypeMulti = takenBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.ballisticDamageTakenMultiplier;
                        break;
                    case DamageMultiType.Energy:
                        dealtTypeMulti = dealtBase * this.AttackerDesignMask.energyDamageDealtMultiplier;
                        dealtBiomeTypeMulti = dealtBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.energyDamageDealtMultiplier;
                        takenTypeMulti = takenBase * this.TargetDesignMask.energyDamageTakenMultiplier;
                        takenBiomeTypeMulti = takenBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.energyDamageTakenMultiplier;
                        break;
                    case DamageMultiType.Missile:
                        dealtTypeMulti = dealtBase * this.AttackerDesignMask.missileDamageDealtMultiplier;
                        dealtBiomeTypeMulti = dealtBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.missileDamageDealtMultiplier;
                        takenTypeMulti = takenBase * this.TargetDesignMask.missileDamageTakenMultiplier;
                        takenBiomeTypeMulti = takenBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.missileDamageTakenMultiplier;
                        break;
                    case DamageMultiType.Support:
                        dealtTypeMulti = dealtBase * this.AttackerDesignMask.antipersonnelDamageDealtMultiplier;
                        dealtBiomeTypeMulti = dealtBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.antipersonnelDamageDealtMultiplier;
                        takenTypeMulti = takenBase * this.TargetDesignMask.antipersonnelDamageTakenMultiplier;
                        takenBiomeTypeMulti = takenBiomeBase * SharedState.Combat.MapMetaData.biomeDesignMask.antipersonnelDamageTakenMultiplier;
                        break;
                    case DamageMultiType.Generic:
                        dealtTypeMulti = dealtBase;
                        dealtBiomeTypeMulti = dealtBiomeBase;
                        takenTypeMulti = takenBase;
                        takenBiomeTypeMulti = takenBiomeBase;
                        break;
                }
                totalMulti = dealtTypeMulti * dealtBiomeTypeMulti * takenTypeMulti * takenBiomeTypeMulti * targetDamageReduction;
                Mod.Log.Debug?.Write($" type: {type} has multi: {totalMulti} from =>" +
                    $"  dealt: {dealtTypeMulti} x dealtBiome: {dealtBiomeTypeMulti} x taken: {takenTypeMulti} x" +
                    $" takenBiome: {takenBiomeTypeMulti} x targetDamReduction: {targetDamageReduction}");
            }
            catch (Exception e)
            {
                Mod.Log.Error?.Write(e, "Failed to calculate attack multipliers!");
            }

            return totalMulti;
        }

        public float DamageMultiForWeapon(Weapon weapon)
        {
            float typeDamageReductionMulti = 1.0f;
            if (!string.IsNullOrEmpty(weapon.WeaponCategoryValue.DamageReductionMultiplierStat))
            {
                // Check for target damage reduction specific to this weapon type
                typeDamageReductionMulti *= this.Target.StatCollection.GetValue<float>(weapon.WeaponCategoryValue.DamageReductionMultiplierStat);
                Mod.Log.Debug?.Write($" -- Target has damage reduction multi: {typeDamageReductionMulti} from stat: {weapon.WeaponCategoryValue.DamageReductionMultiplierStat}");
            }

            float staticMulti;
            if (weapon.WeaponCategoryValue.IsBallistic) staticMulti = this.DamageMultipliers[DamageMultiType.Ballistic];
            else if (weapon.WeaponCategoryValue.IsEnergy) staticMulti = this.DamageMultipliers[DamageMultiType.Energy];
            else if (weapon.WeaponCategoryValue.IsMissile) staticMulti = this.DamageMultipliers[DamageMultiType.Missile];
            else if (weapon.WeaponCategoryValue.IsSupport) staticMulti = this.DamageMultipliers[DamageMultiType.Support];
            else staticMulti = this.DamageMultipliers[DamageMultiType.Generic];

            return staticMulti * typeDamageReductionMulti;
        }
    }
}
