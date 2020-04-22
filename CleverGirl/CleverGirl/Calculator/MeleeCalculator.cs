using BattleTech;
using System.Collections.Generic;
using us.frostraptor.modUtils;

namespace CleverGirl.Calculator
{
    public static class MeleeCalculator
    {

        public static List<Weapon> OptimizeAttack(List<Weapon> weapons, Mech attacker, ICombatant target)
        {
            List<Weapon> validWeapons = new List<Weapon>();

            AbstractActor targetActor = (AbstractActor)target;
            if (attacker == null || target == null)
            {
                Mod.Log.Warn($"Passed a null attacker or target, or target is not an AsbtractActor - returning an empty list");
                return validWeapons;
            }

            // TODO: FIXME
            // Evaluate preconditions and behavior vars
            if (!EvaluateDFAPreconditions(attacker, targetActor))
            {
                Mod.Log.Debug("Unit cannot DFA target, returning an empty weapons list.");
                return validWeapons;
            }

            validWeapons = weapons.DFAWeapons;

            return validWeapons;
        }

        private static bool EvaluateDFAPreconditions(Mech attacker, AbstractActor target)
        {

            if (!attacker.IsOperational || attacker.IsProne || attacker.HasMovedThisRound)
            {
                Mod.Log.Debug($" Attacker {CombatantUtils.Label(attacker)} isProne, has already moved, or is not operational. Cannot DFA!");
                return false;
            }

            if (attacker.WorkingJumpjets < 1)
            {
                Mod.Log.Debug($" Attacker {CombatantUtils.Label(attacker)} has no working jump jets, cannot DFA!");
                return false;
            }

            List<PathNode> dfaDestinations = attacker.JumpPathing.GetDFADestsForTarget(target);
            if (dfaDestinations == null || dfaDestinations.Count == 0 ||
                !attacker.CanDFATargetFromPosition(target, attacker.CurrentPosition))
            {
                Mod.Log.Debug($" No LOS or destinations for attacker {CombatantUtils.Label(attacker)} to target {CombatantUtils.Label(target)}, cannot DFA!");
                return false;
            }

            // 1.0f for full armor, 0.0f for no armor
            float attackerLegDamage = AttackEvaluator.LegDamageLevel(attacker);
            float attackerMaxLegDamageBVal = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            bool isLegDamageLowEnough = attackerLegDamage >= attackerMaxLegDamageBVal;
            Mod.Log.Debug($" Checking attackerLegDamage =>  current: {attackerLegDamage} >= behaviorVal: {attackerMaxLegDamageBVal} = {isLegDamageLowEnough}");
            if (!isLegDamageLowEnough) return false;

            // WTF - you only DFA a target if they aren't damaged?
            float targetDamageRatio = AttackEvaluator.MaxDamageLevel(attacker, target);
            float existingTargetDamageBVal = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            bool isTargetDamagedEnough = targetDamageRatio >= existingTargetDamageBVal;
            Mod.Log.Debug($" Checking targetDamage =>  current: {targetDamageRatio} >= behaviorVal: {existingTargetDamageBVal} = {isTargetDamagedEnough}");
            if (!isTargetDamagedEnough) return false;

            // TODO: Check attacker and target stability

            // Check Retaliation
            // TODO: Retaliation should consider all possible attackers, not just the attacker
            // TODO: Retaliation should consider how much damage you do with melee vs. non-melee - i.e. punchbots should probably prefer punching over weak weapons fire


            if (attackEvaluation2.AttackType == AIUtil.AttackType.Melee)
            {
                if (!attackerAA.CanEngageTarget(target))
                {
                    Mod.Log.Debug("SOLUTION REJECTED - can't engage target!");
                    continue;
                }
                if (meleeDestsForTarget.Count == 0)
                {
                    Mod.Log.Debug("SOLUTION REJECTED - can't build path to target!");
                    continue;
                }
                if (targetActor == null)
                {
                    Mod.Log.Debug("SOLUTION REJECTED - target is a building, we can't melee buildings!");
                    continue;
                }
                // TODO: This seems wrong... why can't you melee if the target is already engaged with you?
                if (isStationary)
                {
                    Mod.Log.Debug("SOLUTION REJECTED - attacker was stationary, can't melee");
                    continue;
                }
            }

            return true;
        }
 
    }

}
