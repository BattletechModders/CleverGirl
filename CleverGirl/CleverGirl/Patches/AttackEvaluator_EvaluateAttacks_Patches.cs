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

            // If there are no enemies, exit immediately
            if (unit.BehaviorTree.enemyUnits.Count == 0) {
                Mod.Log.Info?.Write("No important enemy units, skipping decision making.");
                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                __runOriginal = false;
                return;
            }

            // Initialize decision data caches
            AEHelper.InitializeAttackOrderDecisionData(unit);
            
            Mod.Log.Debug?.Write($" == Evaluating attack from unit: {unit.DistinctId()} at pos: {unit.CurrentPosition} against {unit.BehaviorTree.enemyUnits.Count} enemies.");

            float bestTargDamage = 0f;
            float bestTargFirepowerReduction = 0f;

            Mod.Log.Debug?.Write(" === BEGIN DESIGNATED TARGET FIRE CHECKS ===");

            BehaviorTreeResults behaviorTreeResults = null;
            AbstractActor designatedTarget = AEHelper.FilterEnemyUnitsToDesignatedTarget(unit.team as AITeam, unit.lance, unit.BehaviorTree.enemyUnits);
            float behavior1 = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetByPercentage).FloatVal;
            float behavior2 = BehaviorHelper.GetBehaviorVariableValue(unit.BehaviorTree, BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetFirepowerTakeawayByPercentage).FloatVal;

            float opportunityFireTakeawayThreshold = 1f + (behavior2 / 100f);
            float opportunityFireThreshold = 1f + (behavior1 / 100f);

            Mod.Log.Info?.Write($"  Opportunity Fire damageThreshold: {opportunityFireThreshold}  takeawayThreshold: {opportunityFireTakeawayThreshold}");

            if (designatedTarget != null) {
                bestTargDamage = AOHelper.MakeAttackOrderForTarget(unit, designatedTarget, isStationary, out behaviorTreeResults) * opportunityFireThreshold;
                bestTargFirepowerReduction = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, designatedTarget, designatedTarget.CurrentPosition, designatedTarget.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet) * opportunityFireTakeawayThreshold;
                Mod.Log.Debug?.Write($"  DesignatedTarget: {designatedTarget.DistinctId()} will suffer: {bestTargDamage} damage and lose: {bestTargFirepowerReduction} firepower from attack.");
            } else {
                Mod.Log.Debug?.Write("  No designated target identified.");
            }

            Mod.Log.Debug?.Write(" === END DESIGNATED TARGET FIRE CHECKS ===");
            Mod.Log.Debug?.Write(" === BEGIN OPPORTUNITY FIRE CHECKS ===");

            // Walk through every alive enemy, and see if a better shot presents itself.
            for (int j = 0; j < unit.BehaviorTree.enemyUnits.Count; j++) {
                ICombatant combatant = unit.BehaviorTree.enemyUnits[j];
                if (combatant == designatedTarget || combatant.IsDead) { continue; }

                Mod.Log.Debug?.Write($"  Checking opportunity fire against target: {combatant.DistinctId()}");

                AbstractActor opportunityFireTarget = combatant as AbstractActor;
                BehaviorTreeResults oppTargAttackOrder;
                // Should MAOFT take a param for opportunity attacks to simplify? 
                float oppTargDamage = AOHelper.MakeAttackOrderForTarget(unit, combatant, isStationary, out oppTargAttackOrder);
                float oppTargFirepowerReduction = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, combatant, combatant.CurrentPosition, combatant.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet);
                Mod.Log.Debug?.Write($"  Target will suffer: {oppTargDamage} with firepower reduction: {oppTargFirepowerReduction}");

                // TODO: Was where opportunity cost from evasion strip was added to target damage.
                //  Reintroduce utility damage to this calculation

                bool isBetterTargetDamage = oppTargDamage > bestTargDamage;
                Mod.Log.Debug?.Write($"  Comparing damage - opportunity: {oppTargDamage} > best: {bestTargDamage}");
                bool isBetterOrEqualFirepowerReduction = oppTargFirepowerReduction >= bestTargFirepowerReduction;
                Mod.Log.Debug?.Write($"  Comparing firepower reduction - opportunity: {oppTargFirepowerReduction} >= best: {bestTargFirepowerReduction}");

                if (oppTargAttackOrder != null && oppTargAttackOrder.orderInfo != null && isBetterTargetDamage && isBetterOrEqualFirepowerReduction) {
                    Mod.Log.Debug?.Write("  Opportunity attack is better than any previous option considered.");
                    Mod.Log.Debug?.Write($"   Attack order type: {oppTargAttackOrder.orderInfo.OrderType} with debug: '{oppTargAttackOrder.debugOrderString}'");

                    bestTargDamage = oppTargDamage;
                    bestTargFirepowerReduction = oppTargFirepowerReduction;
                    __result = oppTargAttackOrder;
                }
            }
            Mod.Log.Debug?.Write(" === END OPPORTUNITY FIRE CHECKS ===");

            if (__result != null && bestTargDamage > 0) {
                Mod.Log.Debug?.Write("Successfuly calculated attack order");
                Mod.Log.Debug?.Write(__result.debugOrderString);
                Mod.Log.Debug?.Write(__result.behaviorTrace);
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "attacking target. Success");
                __runOriginal = false;
                return;

            }

            Mod.Log.Debug?.Write("Could not calculate reasonable attacks. Skipping node.");
            __result = new BehaviorTreeResults(BehaviorNodeState.Failure);

            __runOriginal = false;
            return;

        }
    }

    [HarmonyPatch(typeof(AttackEvaluator), "MakeAttackOrderForTarget")]
    [HarmonyAfter("io.mission.modrepuation")]
    public static class AttackEvaluator_MakeAttackOrderForTarget {

        [HarmonyPrefix]
        public static void Prefix(ref bool __runOriginal, AbstractActor unit, ICombatant target, int enemyUnitIndex, bool isStationary, out BehaviorTreeResults order, ref float __result) {

            try {
                Mod.Log.Trace?.Write("AE:MAOFT entered.");

                //ModState.RangeToTargetsAlliesCache.Clear();
                __result = AOHelper.MakeAttackOrderForTarget(unit, target, isStationary, out BehaviorTreeResults innerBTR);
                order = innerBTR;
            } catch (Exception e) {
                Mod.Log.Error?.Write("Failed to modify AttackOrder evaluation due to error: " + e.Message);
                Mod.Log.Error?.Write($"  Source:{e.Source}  StackTrace:{e.StackTrace}");

                order = null;
                __runOriginal = true;
                return;

            }

            __runOriginal = false;
            return;

        }
    }

}
