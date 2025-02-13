using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CleverGirl.Objects;
using CleverGirlAIDamagePrediction;
using CustAmmoCategories;
using UnityEngine;
using static System.Math;
using static AttackEvaluator;

namespace CleverGirl.Helper {

    public static class AOHelper {

        // Evaluate all possible attacks for the attacker and target based upon their current position. Returns the total damage the target will take,
        //   which will be compared against all other targets to determine the optimal attack to make
        public static float MakeAttackOrderForTarget(AbstractActor attackerAA, ICombatant target, 
            bool isStationary, out AttackOrder order)
        {
            Mod.Log.Debug?.Write("");
            Mod.Log.Debug?.Write($"Evaluating AttackOrder isStationary: {isStationary} " +
                $"from {attackerAA.DistinctId()} at pos: {attackerAA.CurrentPosition} rot: {attackerAA.CurrentRotation} " +
                $"against {target.DistinctId()} at pos: {target.CurrentPosition} rot: {target.CurrentRotation}");

            // If the unit has no visibility to the target from the current position, they can't attack. Return immediately.
            if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(attackerAA, target))
            {
                order = new AttackOrder
                {
                    Order = BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false)
                };
                return 0f;
            }

            Mech attackerMech = attackerAA as Mech;
            float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
            float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech);
            float behaviorAcceptableHeatLevel = attackerMech == null ? 0f : BehaviorHelper.GetBehaviorVariableValue(attackerMech.BehaviorTree, BehaviorVariableName.Float_AcceptableHeatLevel).FloatVal;
            if (behaviorAcceptableHeatLevel > 1)
            {
                acceptableHeat *= behaviorAcceptableHeatLevel;
                Mod.Log.Debug?.Write($" heat: current: {currentHeat} behaviorAcceptableHeatLevel: {behaviorAcceptableHeatLevel} acceptable: {acceptableHeat}");
            }
            else
            {
                Mod.Log.Debug?.Write($" heat: current: {currentHeat} acceptable: {acceptableHeat}");
            }
            // float weaponToHitThreshold = attackerAA.BehaviorTree.weaponToHitThreshold;

            // Filter weapons that cannot contribute to the battle
            CandidateWeapons candidateWeapons = new CandidateWeapons(attackerAA, target);

            Mech targetMech = target as Mech;
            bool targetIsEvasive = targetMech != null && targetMech.IsEvasive;
            List<List<CondensedWeaponAmmoMode>>[] weaponSetsByAttackType = {
                new List<List<CondensedWeaponAmmoMode>>() { },
                new List<List<CondensedWeaponAmmoMode>>() { },
                new List<List<CondensedWeaponAmmoMode>>() { }
            };

            // Note: Disabled the evasion fractional checking that Vanilla uses. Should make units more free with ammunition against evasive foes
            //float evasiveToHitFraction = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100f;

            // Evaluate ranged attacks 
            //if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
            //    Mod.Log.Debug?.Write($"Checking evasive shots against target, needs {evasiveToHitFraction} or higher to be included.");
            //    weaponSetsByAttackType[0] = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.RangedWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
            //} else {
            //    Mod.Log.Debug?.Write($"Checking non-evasive target.");
            //    weaponSetsByAttackType[0] = AEHelper.MakeWeaponSets(candidateWeapons.RangedWeapons);
            //}
            AbstractActor targetActor = target as AbstractActor;

            weaponSetsByAttackType[0] = AEHelper.MakeRangedWeaponSets(candidateWeapons.RangedWeapons, target, attackerAA.CurrentPosition);
            Mod.Log.Debug?.Write($"Ranged attack weaponSets:{weaponSetsByAttackType[0].Count}");

            Vector3 bestMeleePosition = Vector3.zero;
            if (attackerMech != null && targetActor != null)
            {
                List<PathNode> meleeDestsForTarget = attackerMech.Pathing.GetMeleeDestsForTarget(targetActor);
                bestMeleePosition = meleeDestsForTarget.Count > 0 ?
                    attackerMech.FindBestPositionToMeleeFrom(targetActor, meleeDestsForTarget) : Vector3.zero;
                weaponSetsByAttackType[1] = MakeMeleeWeaponSets(attackerMech, targetActor, bestMeleePosition, candidateWeapons); 
            }
            Mod.Log.Debug?.Write($"BestMeleePosition: {bestMeleePosition} has weaponSets:{weaponSetsByAttackType[1].Count}");

            Vector3 bestDFAPosition = Vector3.zero;
            if (attackerMech != null && targetActor != null)
            {
                List<PathNode> dfaDestsForTarget = attackerMech != null && targetActor != null ?
                    attackerMech.JumpPathing.GetDFADestsForTarget(targetActor) : new List<PathNode>();
                bestDFAPosition = dfaDestsForTarget.Count > 0 ?
                    attackerMech.FindBestPositionToMeleeFrom(targetActor, dfaDestsForTarget) : Vector3.zero;
                weaponSetsByAttackType[2] = MakeDFAWeaponSets(attackerMech, targetActor, bestDFAPosition, candidateWeapons);
            }
            Mod.Log.Debug?.Write($"BestDFAPosition: {bestDFAPosition} has weaponSets:{weaponSetsByAttackType[2].Count}");

            List<AmmoModeAttackEvaluation> list = AEHelper.EvaluateAttacks(attackerAA, target, weaponSetsByAttackType, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug?.Write($"AEHelper found {list.Count} different attack solutions after evaluating attacks");

            // This code does nothing except spam a lot of info in the logs, the predicted values aren't even correct for later in the decision tree.
            if (Mod.Log.IsTrace)
            {
                float bestRangedEDam = 0f;
                float bestMeleeEDam = 0f;
                float bestDFAEDam = 0f;
                for (int m = 0; m < list.Count; m++)
                {
                    AmmoModeAttackEvaluation attackEvaluation = list[m];
                    Mod.Log.Trace?.Write(
                        $"Attack Evaluation result {m} of type {attackEvaluation.AttackType} with {attackEvaluation.WeaponAmmoModes.Count} weapons, " +
                        $"damage EV of {attackEvaluation.ExpectedDamage}, heat {attackEvaluation.HeatGenerated}");
                    switch (attackEvaluation.AttackType)
                    {
                        case AIUtil.AttackType.Shooting:
                            bestRangedEDam = Mathf.Max(bestRangedEDam, attackEvaluation.ExpectedDamage);
                            break;
                        case AIUtil.AttackType.Melee:
                            bestMeleeEDam = Mathf.Max(bestMeleeEDam, attackEvaluation.ExpectedDamage);
                            break;
                        case AIUtil.AttackType.DeathFromAbove:
                            bestDFAEDam = Mathf.Max(bestDFAEDam, attackEvaluation.ExpectedDamage);
                            break;
                        default:
                            Debug.Log("unknown attack type: " + attackEvaluation.AttackType);
                            break;
                    }
                }

                Mod.Log.Trace?.Write($"Best values prior to pruning - shooting: {bestRangedEDam}  melee: {bestMeleeEDam}  dfa: {bestDFAEDam}");
            }

            float targetMaxArmorFractionFromHittableLocations = AttackEvaluator.MaxDamageLevel(attackerAA, target);
            float attackerLegDamage = attackerMech == null ? 0f : AttackEvaluator.LegDamageLevel(attackerMech);

            //float existingTargetDamageForOverheat = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
            float existingTargetDamageForDFA = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            float maxAllowedLegDamageForDFA = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            Mod.Log.Debug?.Write($"  BehVars => ExistingTargetDamageForDFAAttack: {existingTargetDamageForDFA}  OwnMaxLegDamageForDFAAttack: {maxAllowedLegDamageForDFA}");

             
            List<AmmoModeAttackEvaluation> rejectedDueToOverheat = new List<AmmoModeAttackEvaluation>();
            if (FindFirstValidAttackEvaluation(list, out float expectedDamage, out order))
            {
                return expectedDamage;
            }

            // For attacks which caused overheat, can we use less weapons?
            if (Mod.Config.AttemptReducingOverheatSolutions && rejectedDueToOverheat.Any()) 
            {
                Mod.Log.Debug?.Write($"No solution found, but {rejectedDueToOverheat.Count} overheating solutions exist.");
                // Loop until we find solution or 100 attempts or list has no AttackEvaluation with weapons left.
                // Every loop remove one weapon from the weapons list, and if 0 remove the evaluation from the list.
                int removeLoops = 0;
                int totalRemoveAttempts = 0;
                ConcurrentQueue<AmmoModeAttackEvaluation> overheatSolutions = new ConcurrentQueue<AmmoModeAttackEvaluation>(rejectedDueToOverheat);
                while (overheatSolutions.TryDequeue(out var attackEvaluation))
                {
                    Mod.Log.Debug?.Write($"Iteration #{removeLoops} to find valid solution with no overheating. {overheatSolutions.Count + 1} candidates remain.");
                    List<Weapon> weapons = attackEvaluation.WeaponAmmoModes.Keys.ToList();
                    Weapon randomWeapon = weapons.GetRandomElement();
                    Mod.Log.Trace?.Write($"Removing random weapon {randomWeapon.UIName} with AmmoMode {attackEvaluation.WeaponAmmoModes[randomWeapon]}");
                    attackEvaluation.WeaponAmmoModes.Remove(randomWeapon);
                    
                    // Rebuild new list of CondensedWeaponAmmoModes
                    Dictionary<string, CondensedWeaponAmmoMode> cwams = new();
                    foreach (Weapon weapon in attackEvaluation.WeaponAmmoModes.Keys)
                    {
                        if (!cwams.TryGetValue(weapon.defId, out CondensedWeaponAmmoMode cwam))
                        {
                            AmmoModePair ammoModePair = attackEvaluation.WeaponAmmoModes[weapon];
                            cwams[weapon.defId] = new CondensedWeaponAmmoMode(new CondensedWeapon(weapon), ammoModePair);
                        }
                        else
                        {
                            cwam.condensedWeapon.AddWeapon(weapon);
                        }
                    }

                    AmmoModeAttackEvaluation evaluation = AEHelper.EvaluateAttack(attackerAA, target, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive, cwams.Values.ToList(), attackEvaluation.AttackType);
                    if (FindFirstValidAttackEvaluation(new List<AmmoModeAttackEvaluation>() { evaluation }, out expectedDamage, out order))
                    {
                        Mod.Log.Debug?.Write($"Found valid AttackEvaluation after {totalRemoveAttempts} attempts in {removeLoops} loops with expectedDamage {expectedDamage}.");
                        return expectedDamage;
                    }
                    
                    // If there are two or more weapons remaining, enqueue for another iteration
                    if (attackEvaluation.WeaponAmmoModes.Count > 1)
                    {
                        overheatSolutions.Enqueue(attackEvaluation);
                    }

                    if (++totalRemoveAttempts > 100)
                    {
                        Mod.Log.Debug?.Write($"Aborting overheat weapons retry loop after {removeLoops} loops with {totalRemoveAttempts} total attempts.");
                        goto failedRemoveLoop;
                    }
                    removeLoops++;
                }
            }

            failedRemoveLoop:
            
            Mod.Log.Debug?.Write($"Unable to find an AttackOrder with damage for target {target.DistinctId()}.");
            return 0f;

            bool FindFirstValidAttackEvaluation(List<AmmoModeAttackEvaluation> attackEvaluations, out float makeAttackOrderForTarget, out AttackOrder order)
            {
                Mod.Log.Debug?.Write($"Attempting to find first valid attack evaluation out of {attackEvaluations.Count}.");
                // LOGIC: Now, evaluate every set of attacks in the list
                for (int n = 0; n < attackEvaluations.Count; n++)
                {
                    AmmoModeAttackEvaluation currentAttackEvaluation = attackEvaluations[n];
                    Mod.Log.Debug?.Write($" ==== Evaluating attack solution #{n} vs target: {targetActor.DistinctId()}");
                    Mod.Log.Trace?.Write($"  with weapons {GetWeaponsListString(currentAttackEvaluation)}");
 
                    if (currentAttackEvaluation.WeaponAmmoModes.Count == 0)
                    {
                        Mod.Log.Debug?.Write("SOLUTION REJECTED - no weapons!");
                        continue;
                    }

                    // TODO: Does heatGenerated account for jump heat?
                    // TODO: Does not rollup heat!
                    bool willCauseOverheat = currentAttackEvaluation.HeatGenerated + currentHeat > acceptableHeat;
                    Mod.Log.Debug?.Write($"heat generated: {currentAttackEvaluation.HeatGenerated}  current: {currentHeat}  acceptable: {acceptableHeat}  willOverheat: {willCauseOverheat}");
                    //if (willCauseOverheat && attackerMech.OverheatWillCauseDeath())
                    //{
                    //    Mod.Log.Debug?.Write("SOLUTION REJECTED - overheat would cause own death");
                    //    continue;
                    //}
                    if (willCauseOverheat)
                    {
                        Mod.Log.Info?.Write("SOLUTION REJECTED - would cause overheat.");
                        rejectedDueToOverheat.Add(currentAttackEvaluation);
                        continue;
                    }

                    // TODO: Check for acceptable damage from overheat - as per below
                    //bool flag6 = num4 >= existingTargetDamageForOverheat;
                    //Mod.Log.Debug?.Write("but enough damage for overheat attack? " + flag6);
                    //bool flag7 = attackEvaluation2.lowestHitChance >= weaponToHitThreshold;
                    //Mod.Log.Debug?.Write("but enough accuracy for overheat attack? " + flag7);
                    //if (willCauseOverheat && (!flag6 || !flag7)) {
                    //    Mod.Log.Debug?.Write("SOLUTION REJECTED - not enough damage or accuracy on an attack that will overheat");
                    //    continue;
                    //}

                    if (currentAttackEvaluation.AttackType == AIUtil.AttackType.Melee)
                    {

                        if (targetActor == null)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - target is a building, we can't melee buildings!");
                            continue;
                        }

                        //if (!attackerMech.Pathing.CanMeleeMoveTo(targetActor))
                        //{
                        //    Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker cannot make a melee move to target!");
                        //    continue;
                        //}

                        if (attackerMech.HasMovedThisRound)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker has already moved!");
                            continue;
                        }

                        if (bestMeleePosition == Vector3.zero)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - cannot build path to target!");
                            continue;
                        }

                        // Note this seems weird, but it's an artifact of the behavior tree. If we've gotten a stationary node, don't move.
                        if (!isStationary)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker did not choose a stationary move node, should not attack");
                            continue;
                        }

                    }

                    // Check for DFA auto-failures
                    if (currentAttackEvaluation.AttackType == AIUtil.AttackType.DeathFromAbove)
                    {
                        if (targetActor == null)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - target is a building, we can't DFA buildings!");
                            continue;
                        }

                        if (attackerMech.HasMovedThisRound)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker has already moved!");
                            continue;
                        }

                        if (bestDFAPosition == Vector3.zero)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - cannot build path to target!");
                            continue;
                        }

                        if (targetMaxArmorFractionFromHittableLocations < existingTargetDamageForDFA)
                        {
                            Mod.Log.Debug?.Write($"SOLUTION REJECTED - armor fraction: {targetMaxArmorFractionFromHittableLocations} < behVar(Float_ExistingTargetDamageForDFAAttack): {existingTargetDamageForDFA}!");
                            continue;
                        }

                        if (attackerLegDamage > maxAllowedLegDamageForDFA)
                        {
                            Mod.Log.Debug?.Write($"SOLUTION REJECTED - leg damage: {attackerLegDamage} < behVar(Float_OwnMaxLegDamageForDFAAttack): {maxAllowedLegDamageForDFA}!");
                            continue;
                        }

                        // Note this seems weird, but it's an artifact of the behavior tree. If we've gotten a stationary node, don't move.
                        if (!isStationary)
                        {
                            Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker did not choose a stationary move node, should not attack");
                            continue;
                        }


                    }

                    // LOGIC: If we have some damage from an attack, can we improve upon it as a morale / called shot / multi-attack?
                    if (currentAttackEvaluation.ExpectedDamage > 0f)
                    {
                        // TODO: Do we really need this spam?
                        var weaponsListString = GetWeaponsListString(currentAttackEvaluation);
                        Mod.Log.Debug?.Write($"Chosen AttackEvaluation has weapons: {weaponsListString}");
                    
                        BehaviorTreeResults behaviorTreeResults = new BehaviorTreeResults(BehaviorNodeState.Success);

                        AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target)
                        {
                            Weapons = new List<Weapon>(currentAttackEvaluation.WeaponAmmoModes.Keys),
                            TargetUnit = target,
                            IsMelee = false,
                            IsDeathFromAbove = false
                        };
                    
                        //Now that ammo and mode has been decided, we can convert back to the vanilla AttackEvulation
                        AttackEvaluation attackEvaluation = currentAttackEvaluation.ToSimpleAttackEvaluation();
                        
                        AIUtil.AttackType attackType = attackEvaluation.AttackType;
                        Mod.Log.Debug?.Write($"Created attackOrderInfo with attackType: {attackType} vs. target: {target.DistinctId()}.  " +
                                             $"WeaponCount: {attackOrderInfo?.Weapons?.Count} IsMelee: {attackOrderInfo.IsMelee}  IsDeathFromAbove: {attackOrderInfo.IsDeathFromAbove}");

                        string attackOrderString = null;
                        if (attackType == AIUtil.AttackType.DeathFromAbove)
                        {
                            attackOrderInfo.IsDeathFromAbove = true;
                            attackOrderInfo.AttackFromLocation = bestDFAPosition;

                            attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                            attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);

                        }
                        else if (attackType == AIUtil.AttackType.Melee)
                        {
                            attackOrderInfo.IsMelee = true;
                            attackOrderInfo.AttackFromLocation = bestMeleePosition;

                            attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                            attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                        }
                        else
                        {
                            int? targetIndex = ConvertTargetToUnitIndex(attackerAA, target);
                            if (targetIndex != null)
                            {
                                // LOGIC: Check for a morale attack (based on available morale) - target must be shutdown or knocked down
                                CalledShotAttackOrderInfo offensivePushAttackOrderInfo = AttackEvaluator.MakeOffensivePushOrder(attackerAA, attackEvaluation, targetIndex.Value);
                                if (offensivePushAttackOrderInfo != null)
                                {
                                    Mod.Log.Debug?.Write($"-- Converting to an offensive push order");
                                    attackOrderInfo = offensivePushAttackOrderInfo;
                                    attackOrderString = $" using offensive push against: {target.DisplayName}";
                                }
                                else
                                {
                                     // LOGIC: Check for a called shot - target must be shutdown or knocked down
                                    CalledShotAttackOrderInfo calledShotAttackOrderInfo = AttackEvaluator.MakeCalledShotOrder(attackerAA, attackEvaluation, targetIndex.Value, false);
                                    if (calledShotAttackOrderInfo != null) {
                                        Mod.Log.Debug?.Write($"-- Converting to called shot order");
                                        attackOrderInfo  = calledShotAttackOrderInfo;
                                        attackOrderString = $" using called shot against: {target.DisplayName}";
                                    }

                                    // LOGIC: Check for multi-attack that will fit within our heat boundaries
                                    //MultiTargetAttackOrderInfo multiAttackOrderInfo = MultiAttack.MakeMultiAttackOrder(attackerAA, attackEvaluation2, enemyUnitIndex);
                                    //if (!willCauseOverheat && multiAttackOrderInfo != null) {
                                    //     Multi-attack in RT / BTA only makes sense to:
                                    //      1. maximize breaching shot (which ignores cover/etc) if you a single weapon
                                    //      2. spread status effects around while firing on a single target
                                    //      3. maximizing total damage across N targets, while sacrificing potential damage at a specific target
                                    //        3a. Especially with set sof weapons across range brackets, where you can split short-range weapons and long-range weapons                                
                                    //    behaviorTreeResults.orderInfo = multiAttackOrderInfo;
                                    //    behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using multi attack";
                                    //} 
                                }
                            }
                        }

                        behaviorTreeResults.orderInfo = attackOrderInfo;
                        behaviorTreeResults.debugOrderString = attackOrderString ?? $" using attack type: {attackEvaluation.AttackType} against: {target.DisplayName}";

                        Mod.Log.Debug?.Write("Returning attack order " + behaviorTreeResults.debugOrderString);
                        order = new AttackOrder
                        {
                          Order = behaviorTreeResults,
                          AttackEvaluation = currentAttackEvaluation
                        };
                        Mod.Log.Debug?.Write($" ==== DONE Evaluating attack solution #{n} vs target: {targetActor.DistinctId()}");
                        {
                            makeAttackOrderForTarget = attackEvaluation.ExpectedDamage;
                            return true;
                        }
                    }
                    Mod.Log.Debug?.Write("Rejecting attack with no expected damage");
                    Mod.Log.Debug?.Write($" ==== DONE Evaluating attack solution #{n} vs target: {targetActor.DistinctId()}");
                }

                makeAttackOrderForTarget = 0f;
                order = null;
                return false;
            }
        }

        private static int? ConvertTargetToUnitIndex(AbstractActor attacker, ICombatant target)
        {
            for (var index = 0; index < attacker.BehaviorTree.enemyUnits.Count; index++)
            {
                if (attacker.BehaviorTree.enemyUnits[index] == target)
                {
                    return index;
                }
            }

            Mod.Log.Error?.Write($"Unable to find target index in enemy units!");
            return null;
        }

        private static string GetWeaponsListString(AmmoModeAttackEvaluation currentAttackEvaluation)
        {
            StringBuilder weaponListSB = new StringBuilder();
            weaponListSB.Append("(");

            foreach (KeyValuePair<Weapon, AmmoModePair> wamp in currentAttackEvaluation.WeaponAmmoModes)
            {
                weaponListSB.Append("'");
                weaponListSB.Append(wamp.Key?.UIName);
                weaponListSB.Append("'");
                if (!wamp.Value.modeId.Equals(WeaponMode.BASE_MODE_NAME) && !wamp.Value.modeId.Equals(WeaponMode.NONE_MODE_NAME))
                {
                    weaponListSB.Append(":").Append(wamp.Value.modeId);
                    if (wamp.Value.ammoId.Length != 0)
                    {
                        weaponListSB.Append("/").Append(wamp.Value.ammoId);
                    }
                } 
                else if (wamp.Value.ammoId.Length != 0)
                {
                    weaponListSB.Append(":").Append(wamp.Value.ammoId);
                }
                weaponListSB.Append(", ");
            }
            weaponListSB.Append(")");
            string weaponsListString = weaponListSB.ToString();
            return weaponsListString;
        }

        private static List<List<CondensedWeaponAmmoMode>> MakeDFAWeaponSets(Mech attacker, AbstractActor target, Vector3 attackPos, CandidateWeapons candidateWeapons)
        {
            List<List<CondensedWeaponAmmoMode>> dfaWeaponSets = new List<List<CondensedWeaponAmmoMode>>();

            if (attacker == null || !AIHelper.IsDFAAcceptable(attacker, target))
            {
                Mod.Log.Debug?.Write(" - Attacker cannot DFA, or DFA is not acceptable.");
            }
            else
            {

                // TODO: Check Retaliation
                //List<List<CondensedWeapon>> dfaWeaponSets = null;
                //if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                //    dfaWeaponSets = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.DFAWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                //} else {
                //    dfaWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);
                //}
                List<List<CondensedWeaponAmmoMode>> weaponSets = AEHelper.MakeWeaponAmmoModeSets(candidateWeapons.DFAWeapons);

                // Add DFA weapons to each set
                CondensedWeaponAmmoMode cDFAWeapon = new CondensedWeaponAmmoMode(new CondensedWeapon(attacker.DFAWeapon), attacker.DFAWeapon.getCurrentAmmoMode());
                // DFA weapon, use base mode
                for (int i = 0; i < weaponSets.Count; i++)
                {
                    weaponSets[i].Add(cDFAWeapon);
                }

                dfaWeaponSets = weaponSets;
            }

            return dfaWeaponSets;
        }

        private static List<List<CondensedWeaponAmmoMode>> MakeMeleeWeaponSets(Mech attacker, AbstractActor target, Vector3 attackPos, CandidateWeapons candidateWeapons)
        {
            Mod.Log.Info?.Write($"== Creating melee weaponSets for attacker: {attacker.DistinctId()} versus target: {target.DistinctId()}");

            List<List<CondensedWeaponAmmoMode>> meleeWeaponSets = new List<List<CondensedWeaponAmmoMode>>();

            // REVERSING THIS TO CHECK - Check for HasMoved, because the behavior tree will evaluate a stationary node and generate a melee attack order for it
            if (attacker == null)
            {
                Mod.Log.Info?.Write($" - Attacker is null or has already moved, cannot melee attack");
                return meleeWeaponSets;
            }

            if (!attacker.CanEngageTarget(target, out string cannotEngageInMeleeMsg))
            {
                Mod.Log.Info?.Write($" - Attacker cannot melee, or cannot engage due to: '{cannotEngageInMeleeMsg}'");
                return meleeWeaponSets;
            }

            // Determine the best possible melee attack. 
            // 1. Usable weapons will include a MeleeWeapon/DFAWeapon with the damage set to the expected virtual damage BEFORE toHit is applied
            // 2. SelectedState will need to go out to the MakeAO so it can be set
            MeleeHelper.OptimizeMelee(attacker, target, attackPos, candidateWeapons.MeleeWeapons,
                out List<CondensedWeapon> usableWeapons, out float virtualMeleeDamage, out float totalStateDamage);

            // Determine if we're a punchbot - defined by melee damage 2x or greater than raw ranged damage
            bool mechFavorsMelee = DoesMechFavorMelee(attacker);

            // Check Retaliation
            // TODO: Retaliation should consider all possible attackers, not just the attacker
            bool damageOutweighsRetaliation = AEHelper.MeleeDamageOutweighsRisk(totalStateDamage, attacker, target);

            // TODO: Should consider if heat would be reduced by melee attack
            if (mechFavorsMelee || damageOutweighsRetaliation)
            {
                Mod.Log.Debug?.Write($"There are {usableWeapons.Count} usable condensed weapons.");

                List<List<CondensedWeaponAmmoMode>> weaponSets = AEHelper.MakeWeaponAmmoModeSets(usableWeapons);

                // Add melee weapon to each set. Increase it's damage to deal with virtual damage
                CondensedWeapon cMeleeWeapon = new CondensedWeapon(attacker.MeleeWeapon);
                cMeleeWeapon.First.StatCollection.Set(ModStats.HBS_Weapon_DamagePerShot, virtualMeleeDamage);
                for (int i = 0; i < weaponSets.Count; i++)
                {
                    foreach (AmmoModePair ammoModePair in cMeleeWeapon.First.getAvaibleFiringMethods())
                    {
                        weaponSets[i].Add(new CondensedWeaponAmmoMode(cMeleeWeapon, ammoModePair));
                    }
                }

                meleeWeaponSets = weaponSets;
            }
            else
            {
                Mod.Log.Debug?.Write($" potential melee retaliation too high, skipping melee.");
            }

            return meleeWeaponSets;
        }

        private static bool DoesMechFavorMelee(Mech attacker)
        {
            bool isMeleeMech = false;
            // if (Mod.Config.UseCBTBEMelee && attacker.StatCollection.GetValue<bool>(ModStats.CBTBE_HasPhysicalWeapon))
            if (Mod.Config.UseCBTBEMelee)
            {
                if (attacker.StatCollection.GetValue<bool>(ModStats.CBTBE_HasPhysicalWeapon))
                {
                    Mod.Log.Debug?.Write(" Unit has CBTBE physical weapon, marking isMeleeMech.");
                    isMeleeMech = true;
                }
                else
                {
                    // Weapons that have no minimal range are fine to use with a melee attack
                    // Weapons with a minimal range but no forbidden range will be evaluated as 50% efficient
                    float amountWeaponsFireMeleeWithoutPenalty = 0f;
                    foreach (Weapon weapon in attacker.Weapons)
                    {
                        if (!weapon.CanFireInMelee())
                        {
                            continue;
                        }
                        
                        AmmoModePair currentAmmoMode = weapon.getCurrentAmmoMode();
                        bool onlyModesWithMinRange = false;
                        foreach (AmmoModePair ammoModePair in weapon.getAvaibleFiringMethods())
                        {
                            weapon.ApplyAmmoMode(ammoModePair);
                            if (weapon.MinRange == 0)
                            {
                                amountWeaponsFireMeleeWithoutPenalty++;
                                onlyModesWithMinRange = false;
                                break;
                            } 
                            if (weapon.ForbiddenRange() == 0)
                            {
                                onlyModesWithMinRange = true;
                            }
                        }

                        if (onlyModesWithMinRange)
                        {
                            amountWeaponsFireMeleeWithoutPenalty += 0.5f;
                        }

                        weapon.ApplyAmmoMode(currentAmmoMode);
                        weapon.ResetTempAmmo();
                    }

                    if (amountWeaponsFireMeleeWithoutPenalty * Mod.Config.Weights.PunchbotDamageMulti >= attacker.Weapons.Count)
                    {
                        isMeleeMech = true;
                    }
                }
            }
            else
            {
                //Due to how weapon categories are set up with CBTBE this will count practically all weapons as melee. So this check makes no sense if CBTBE is enabled.
                int rawRangedDam = 0, rawMeleeDam = 0;
                foreach (Weapon weapon in attacker.Weapons)
                {
                    if (weapon.CanFireInMelee())
                    {
                        rawMeleeDam += (int)(weapon.DamagePerShot * weapon.ShotsWhenFired);
                    }
                    else
                    {
                        int optimalDamage = 0;
                        AmmoModePair currentAmmoMode = weapon.getCurrentAmmoMode();
                        foreach (AmmoModePair ammoModePair in weapon.getAvaibleFiringMethods())
                        {
                            weapon.ApplyAmmoMode(ammoModePair);
                            optimalDamage = Max(optimalDamage, (int)(weapon.DamagePerShot * weapon.ShotsWhenFired));
                        }
                        weapon.ApplyAmmoMode(currentAmmoMode);
                        weapon.ResetTempAmmo();
                        rawRangedDam += optimalDamage;
                    }
                }

                if (rawMeleeDam >= Mod.Config.Weights.PunchbotDamageMulti * rawRangedDam)
                {
                    Mod.Log.Debug?.Write($" Unit isMeleeMech due to rawMelee: {rawMeleeDam} >= rawRanged: {rawRangedDam} x {Mod.Config.Weights.PunchbotDamageMulti}");
                    isMeleeMech = true;
                }
            }

            return isMeleeMech;
        }
    }
}
