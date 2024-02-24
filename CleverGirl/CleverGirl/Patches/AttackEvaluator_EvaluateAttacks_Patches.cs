using CleverGirl.Helper;

namespace CleverGirl.Patches
{

    [HarmonyPatch(typeof(AttackEvaluator), "MakeAttackOrder")]
    [HarmonyAfter("io.mission.modrepuation")]
    public static class AttackEvaluator_MakeAttackOrder {
        // WARNING: Replaces the existing logic 
        // isStationary here represents the attacker, not the target

        [HarmonyPrefix]
        public static void Prefix(ref bool __runOriginal, AbstractActor unit, bool isStationary, ref BehaviorTreeResults __result) {
            if (!__runOriginal) return;

            // If there is no unit, exit immediately
            if (unit == null) {
                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                __runOriginal = false;
                return;
            }

            Mod.AttackEvalLog.Info?.Write($"=== START EVALUATING ATTACK from unit: {unit.DistinctId()} with isStationary: {isStationary}");

            // If there are no enemies, exit immediately
            if (unit.BehaviorTree.enemyUnits.Count == 0) {
                Mod.AttackEvalLog.Info?.Write("No important enemy units, skipping decision making.");
                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                __runOriginal = false;
                return;
            }

            // Initialize decision data caches
            AEHelper.InitializeAttackOrderDecisionData(unit);
            
            Mod.AttackEvalLog.Debug?.Write($" == Evaluating attack from unit: {unit.DistinctId()} at pos: {unit.CurrentPosition} against {unit.BehaviorTree.enemyUnits.Count} enemies.");

            float bestTargDamage = 0f;
            float bestTargFirepowerReduction = 0f;

            Mod.AttackEvalLog.Debug?.Write(" === BEGIN DESIGNATED TARGET FIRE CHECKS ===");

            BehaviorTreeResults behaviorTreeResults = null;
            AbstractActor designatedTarget = AEHelper.FilterEnemyUnitsToDesignatedTarget(unit.team as AITeam, unit.lance, unit.BehaviorTree.enemyUnits);
            float behavior1 = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetByPercentage).FloatVal;
            float behavior2 = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetFirepowerTakeawayByPercentage).FloatVal;

            float opportunityFireTakeawayThreshold = 1f + (behavior2 / 100f);
            float opportunityFireThreshold = 1f + (behavior1 / 100f);

            Mod.AttackEvalLog.Info?.Write($"  Opportunity Fire damageThreshold: {opportunityFireThreshold}  takeawayThreshold: {opportunityFireTakeawayThreshold}");

            if (designatedTarget != null) {
                bestTargDamage = AOHelper.MakeAttackOrderForTarget(unit, designatedTarget, isStationary, out behaviorTreeResults) * opportunityFireThreshold;
                bestTargFirepowerReduction = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, designatedTarget, designatedTarget.CurrentPosition, designatedTarget.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet) * opportunityFireTakeawayThreshold;
                Mod.AttackEvalLog.Debug?.Write($"  DesignatedTarget: {designatedTarget.DistinctId()} will suffer: {bestTargDamage} damage and lose: {bestTargFirepowerReduction} firepower from attack.");
            } else {
                Mod.AttackEvalLog.Debug?.Write("  No designated target identified.");
            }

            Mod.AttackEvalLog.Debug?.Write(" === END DESIGNATED TARGET FIRE CHECKS ===");
            Mod.AttackEvalLog.Debug?.Write(" === BEGIN OPPORTUNITY FIRE CHECKS ===");

            // Walk through every alive enemy, and see if a better shot presents itself.
            for (int j = 0; j < unit.BehaviorTree.enemyUnits.Count; j++) {
                ICombatant combatant = unit.BehaviorTree.enemyUnits[j];
                if (combatant == designatedTarget || combatant.IsDead) { continue; }

                Mod.AttackEvalLog.Debug?.Write($"  Checking opportunity fire against target: {combatant.DistinctId()}");

                AbstractActor opportunityFireTarget = combatant as AbstractActor;
                BehaviorTreeResults oppTargAttackOrder;
                // Should MAOFT take a param for opportunity attacks to simplify? 
                float oppTargDamage = AOHelper.MakeAttackOrderForTarget(unit, combatant, isStationary, out oppTargAttackOrder);
                float oppTargFirepowerReduction = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, combatant, combatant.CurrentPosition, combatant.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet);
                Mod.AttackEvalLog.Debug?.Write($"  Target will suffer: {oppTargDamage} with firepower reduction: {oppTargFirepowerReduction}");

                // TODO: Was where opportunity cost from evasion strip was added to target damage.
                //  Reintroduce utility damage to this calculation

                bool isBetterTargetDamage = oppTargDamage > bestTargDamage;
                Mod.AttackEvalLog.Debug?.Write($"  Comparing damage - opportunity: {oppTargDamage} > best: {bestTargDamage}");
                bool isBetterOrEqualFirepowerReduction = oppTargFirepowerReduction >= bestTargFirepowerReduction;
                Mod.AttackEvalLog.Debug?.Write($"  Comparing firepower reduction - opportunity: {oppTargFirepowerReduction} >= best: {bestTargFirepowerReduction}");

                if (oppTargAttackOrder != null && oppTargAttackOrder.orderInfo != null && isBetterTargetDamage && isBetterOrEqualFirepowerReduction) {
                    Mod.AttackEvalLog.Debug?.Write("  Opportunity attack is better than any previous option considered.");
                    Mod.AttackEvalLog.Debug?.Write($"   Attack order type: {oppTargAttackOrder.orderInfo.OrderType} with debug: '{oppTargAttackOrder.debugOrderString}'");

                    bestTargDamage = oppTargDamage;
                    bestTargFirepowerReduction = oppTargFirepowerReduction;
                    __result = oppTargAttackOrder;
                }
            }
            Mod.AttackEvalLog.Debug?.Write(" === END OPPORTUNITY FIRE CHECKS ===");

            if (__result != null && bestTargDamage > 0) {
                Mod.AttackEvalLog.Info?.Write($"Successfully calculated attack order vs. target: {((AttackOrderInfo)__result.orderInfo).TargetUnit.DistinctId()}");
                Mod.AttackEvalLog.Debug?.Write("  debugOrderString: " + __result.debugOrderString);
                Mod.AttackEvalLog.Debug?.Write("  behaviorTrace: " + __result.behaviorTrace);
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "attacking target. Success");
                __runOriginal = false;
                return;
            }
            else
            {
                Mod.AttackEvalLog.Info?.Write("Could not calculate reasonable attacks. Skipping node.");
                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
            }

            Mod.AttackEvalLog.Info?.Write($"=== DONE EVALUATING ATTACK  from unit: {unit.DistinctId()} with isStationary: {isStationary}");
            Mod.AttackEvalLog.Info?.Write("");

            __runOriginal = false;
            return;

        }
    }

}
