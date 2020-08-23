using BattleTech;
using System.Collections.Generic;
using us.frostraptor.modUtils;

namespace CleverGirl.Calculator
{
    public static class DFACalculator
    {

        public static List<Weapon> OptimizeAttack(List<Weapon> weapons, Mech attacker, ICombatant target)
        {
            List<Weapon> validWeapons = new List<Weapon>();

            AbstractActor targetActor = (AbstractActor)target;
            if (attacker == null || target == null)
            {
                Mod.Log.Warn?.Write($"Passed a null attacker or target, or target is not an AsbtractActor - returning an empty list");
                return validWeapons;
            }

            // Evaluate preconditions and behavior vars
            if (!EvaluateDFAPreconditions(attacker, targetActor))
            {
                Mod.Log.Debug?.Write("Unit cannot DFA target, returning an empty weapons list.");
                return validWeapons;
            }

            validWeapons.AddRange(weapons);

            return validWeapons;
        }

        private static bool EvaluateDFAPreconditions(Mech attacker, AbstractActor target)
        {

            if (!attacker.IsOperational || attacker.IsProne || attacker.HasMovedThisRound)
            {
                Mod.Log.Debug?.Write($" Attacker {CombatantUtils.Label(attacker)} isProne, has already moved, or is not operational. Cannot DFA!");
                return false;
            }

            if (attacker.WorkingJumpjets < 1)
            {
                Mod.Log.Debug?.Write($" Attacker {CombatantUtils.Label(attacker)} has no working jump jets, cannot DFA!");
                return false;
            }

            List<PathNode> dfaDestinations = attacker.JumpPathing.GetDFADestsForTarget(target);
            if (dfaDestinations == null || dfaDestinations.Count == 0 ||
                !attacker.CanDFATargetFromPosition(target, attacker.CurrentPosition))
            {
                Mod.Log.Debug?.Write($" No LOS or destinations for attacker {CombatantUtils.Label(attacker)} to target {CombatantUtils.Label(target)}, cannot DFA!");
                return false;
            }

            // 1.0f for full armor, 0.0f for no armor
            float attackerLegDamage = AttackEvaluator.LegDamageLevel(attacker);
            float attackerMaxLegDamageBVal = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            bool isLegDamageLowEnough = attackerLegDamage >= attackerMaxLegDamageBVal;
            Mod.Log.Debug?.Write($" Checking attackerLegDamage =>  current: {attackerLegDamage} >= behaviorVal: {attackerMaxLegDamageBVal} = {isLegDamageLowEnough}");
            if (!isLegDamageLowEnough) return false;

            // WTF - you only DFA a target if they aren't damaged?
            float targetDamageRatio = AttackEvaluator.MaxDamageLevel(attacker, target);
            float existingTargetDamageBVal = AIHelper.GetBehaviorVariableValue(attacker.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            bool isTargetDamagedEnough = targetDamageRatio >= existingTargetDamageBVal;
            Mod.Log.Debug?.Write($" Checking targetDamage =>  current: {targetDamageRatio} >= behaviorVal: {existingTargetDamageBVal} = {isTargetDamagedEnough}");
            if (!isTargetDamagedEnough) return false;

            // TODO: Check attacker and target stability

            // Check Retaliation
            // TODO: Retaliation should consider all possible attackers, not just the attacker
            // TODO: Retaliation should consider how much damage you do with melee vs. non-melee - i.e. punchbots should probably prefer punching over weak weapons fire

            return true;
        }
 
    }

}
