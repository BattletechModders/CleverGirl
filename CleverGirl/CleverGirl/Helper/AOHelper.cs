using BattleTech;
using CBTBehaviorsEnhanced;
using CBTBehaviorsEnhanced.Helper;
using CleverGirlAIDamagePrediction;
using IRBTModUtils.Extension;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static AttackEvaluator;

namespace CleverGirl.Helper {

    public static class AOHelper {

        // Evaluate all possible attacks for the attacker and target based upon their current position. Returns the total damage the target will take,
        //   which will be compared against all other targets to determine the optimal attack to make
        public static float MakeAttackOrderForTarget(AbstractActor attackerAA, ICombatant target, 
            bool isStationary, out BehaviorTreeResults order)
        {
            Mod.Log.Debug?.Write("");
            Mod.Log.Debug?.Write($"Evaluating AttackOrder from ({attackerAA.DistinctId()}) against ({target.DistinctId()} at position: ({target.CurrentPosition})");

            // If the unit has no visibility to the target from the current position, they can't attack. Return immediately.
            if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(attackerAA, target))
            {
                order = BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
                return 0f;
            }

            Mech attackerMech = attackerAA as Mech;
            float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
            float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech); ;
            Mod.Log.Debug?.Write($" heat: current: {currentHeat} acceptable: {acceptableHeat}");

            // float weaponToHitThreshold = attackerAA.BehaviorTree.weaponToHitThreshold;

            // Filter weapons that cannot contribute to the battle
            CandidateWeapons candidateWeapons = new CandidateWeapons(attackerAA, target);

            Mech targetMech = target as Mech;
            bool targetIsEvasive = targetMech != null && targetMech.IsEvasive;
            List<List<CondensedWeapon>>[] weaponSetsByAttackType = {
                new List<List<CondensedWeapon>>() { },
                new List<List<CondensedWeapon>>() { },
                new List<List<CondensedWeapon>>() { }
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

            List<AttackEvaluation> list = AEHelper.EvaluateAttacks(attackerAA, target, weaponSetsByAttackType, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug?.Write(string.Format("AEHelper found {0} different attack solutions after evaluating attacks", list.Count));
            float bestRangedEDam = 0f;
            float bestMeleeEDam = 0f;
            float bestDFAEDam = 0f;
            for (int m = 0; m < list.Count; m++)
            {
                AttackEvaluation attackEvaluation = list[m];
                Mod.Log.Debug?.Write($"evaluated attack of type {attackEvaluation.AttackType} with {attackEvaluation.WeaponList.Count} weapons, " +
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
            Mod.Log.Debug?.Write($"best shooting: {bestRangedEDam}  melee: {bestMeleeEDam}  dfa: {bestDFAEDam}");

            float targetMaxArmorFractionFromHittableLocations = AttackEvaluator.MaxDamageLevel(attackerAA, target);
            float attackerLegDamage = attackerMech == null ? 0f : AttackEvaluator.LegDamageLevel(attackerMech);

            //float existingTargetDamageForOverheat = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
            float existingTargetDamageForDFA = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            float maxAllowedLegDamageForDFA = BehaviorHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
            Mod.Log.Debug?.Write($"  BehVars => ExistingTargetDamageForDFAAttack: {existingTargetDamageForDFA}  OwnMaxLegDamageForDFAAttack: {maxAllowedLegDamageForDFA}");

            // LOGIC: Now, evaluate every set of attacks in the list
            for (int n = 0; n < list.Count; n++)
            {
                AttackEvaluator.AttackEvaluation attackEvaluation2 = list[n];
                Mod.Log.Debug?.Write($" ==== Evaluating attack solution #{n} vs target: {targetActor.DistinctId()}");

                // TODO: Do we really need this spam?
                StringBuilder weaponListSB = new StringBuilder();
                weaponListSB.Append(" Weapons: (");
                foreach (Weapon weapon3 in attackEvaluation2.WeaponList)
                {
                    weaponListSB.Append("'");
                    weaponListSB.Append(weapon3.Name);
                    weaponListSB.Append("', ");
                }
                weaponListSB.Append(")");
                Mod.Log.Debug?.Write(weaponListSB.ToString());

                if (attackEvaluation2.WeaponList.Count == 0)
                {
                    Mod.Log.Debug?.Write("SOLUTION REJECTED - no weapons!");
                    continue;
                }

                // TODO: Does heatGenerated account for jump heat?
                // TODO: Does not rollup heat!
                bool willCauseOverheat = attackEvaluation2.HeatGenerated + currentHeat > acceptableHeat;
                Mod.Log.Debug?.Write($"heat generated: {attackEvaluation2.HeatGenerated}  current: {currentHeat}  acceptable: {acceptableHeat}  willOverheat: {willCauseOverheat}");
                //if (willCauseOverheat && attackerMech.OverheatWillCauseDeath())
                //{
                //    Mod.Log.Debug?.Write("SOLUTION REJECTED - overheat would cause own death");
                //    continue;
                //}
                if (willCauseOverheat)
                {
                    Mod.Log.Info?.Write("SOLUTION REJECTED - would cause overheat.");
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

                if (attackEvaluation2.AttackType == AIUtil.AttackType.Melee)
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
                if (attackEvaluation2.AttackType == AIUtil.AttackType.DeathFromAbove)
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
                if (attackEvaluation2.ExpectedDamage > 0f)
                {
                    BehaviorTreeResults behaviorTreeResults = new BehaviorTreeResults(BehaviorNodeState.Success);

                    // LOGIC: Check for a morale attack (based on available morale) - target must be shutdown or knocked down
                    //CalledShotAttackOrderInfo offensivePushAttackOrderInfo = AEHelper.MakeOffensivePushOrder(attackerAA, attackEvaluation2, target);
                    //if (offensivePushAttackOrderInfo != null) {
                    //    behaviorTreeResults.orderInfo = offensivePushAttackOrderInfo;
                    //    behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using offensive push";
                    //}

                    // LOGIC: Check for a called shot - target must be shutdown or knocked down
                    //CalledShotAttackOrderInfo calledShotAttackOrderInfo = AEHelper.MakeCalledShotOrder(attackerAA, attackEvaluation2, target, false);
                    //if (calledShotAttackOrderInfo != null) {
                    //    behaviorTreeResults.orderInfo = calledShotAttackOrderInfo;
                    //    behaviorTreeResults.debugOrderString = attackerAA.DisplayName + " using called shot";
                    //}

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

                    AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target)
                    {
                        Weapons = attackEvaluation2.WeaponList,
                        TargetUnit = target
                    };
                    AIUtil.AttackType attackType = attackEvaluation2.AttackType;

                    if (attackType == AIUtil.AttackType.DeathFromAbove)
                    {
                        attackOrderInfo.IsDeathFromAbove = true;
                        attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                        attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);
                        attackOrderInfo.AttackFromLocation = bestDFAPosition;
                    }
                    else if (attackType == AIUtil.AttackType.Melee)
                    {
                        attackOrderInfo.IsMelee = true;
                        attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                        attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);

                        attackOrderInfo.AttackFromLocation = bestMeleePosition;
                    }

                    behaviorTreeResults.orderInfo = attackOrderInfo;
                    behaviorTreeResults.debugOrderString = $" using attack type: {attackEvaluation2.AttackType} against: {target.DisplayName}";

                    Mod.Log.Debug?.Write("attack order: " + behaviorTreeResults.debugOrderString);
                    order = behaviorTreeResults;
                    return attackEvaluation2.ExpectedDamage;
                }
                Mod.Log.Debug?.Write("Rejecting attack with no expected damage");
            }

            Mod.Log.Debug?.Write("Could not build an AttackOrder with damage, returning a null order. Unit will likely brace.");
            order = null;
            return 0f;
        }

        private static List<List<CondensedWeapon>> MakeDFAWeaponSets(Mech attacker, AbstractActor target, Vector3 attackPos, CandidateWeapons candidateWeapons)
        {
            List<List<CondensedWeapon>> dfaWeaponSets = new List<List<CondensedWeapon>>();

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
                List<List<CondensedWeapon>> weaponSets = AEHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);

                // Add DFA weapons to each set
                CondensedWeapon cDFAWeapon = new CondensedWeapon(attacker.DFAWeapon);
                for (int i = 0; i < weaponSets.Count; i++)
                {
                    weaponSets[i].Add(cDFAWeapon);
                }

                dfaWeaponSets = weaponSets;
            }

            return dfaWeaponSets;
        }

        private static List<List<CondensedWeapon>> MakeMeleeWeaponSets(Mech attacker, AbstractActor target, Vector3 attackPos, CandidateWeapons candidateWeapons)
        {
            Mod.Log.Info?.Write($"== Creating melee weaponSets for attacker: {attacker.DistinctId()} versus target: {target.DistinctId()}");

            List<List<CondensedWeapon>> meleeWeaponSets = new List<List<CondensedWeapon>>();

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

            // Expand candidate weapons to a full weapon list for CBTBE so we don't have a cyclical dependency here.
            List<Weapon> availableWeapons = new List<Weapon>();
            candidateWeapons.MeleeWeapons.ForEach(x => availableWeapons.AddRange(x.condensedWeapons));

            // Determine the best possible melee attack. 
            // 1. Usable weapons will include a MeleeWeapon/DFAWeapon with the damage set to the expected virtual damage BEFORE toHit is applied
            // 2. SelectedState will need to go out to the MakeAO so it can be set
            CleverGirlCalculator.OptimizeMelee(attacker, target, attackPos, availableWeapons,
                out List<Weapon> usableWeapons, out MeleeState selectedState,
                out float virtualMeleeDamage, out float totalStateDamage);

            // Determine if we're a punchbot - defined by melee damage 2x or greater than raw ranged damage
            bool mechFavorsMelee = DoesMechFavorMelee(attacker);

            // Check Retaliation
            // TODO: Retaliation should consider all possible attackers, not just the attacker
            bool damageOutweighsRetaliation = AEHelper.MeleeDamageOutweighsRisk(totalStateDamage, attacker, target);

            // TODO: Should consider if heat would be reduced by melee attack
            if (mechFavorsMelee || damageOutweighsRetaliation)
            {
                // Convert usableWeapons back to condensedWeapons for evaluation. They should be a subset of 
                //   meleeWeapons, so we've already checked them for canFire, etc
                Dictionary<string, CondensedWeapon> condensed = new Dictionary<string, CondensedWeapon>();
                foreach (Weapon weapon in usableWeapons)
                {
                    CondensedWeapon cWeapon = new CondensedWeapon(weapon);
                    Mod.Log.Debug?.Write($" -- '{weapon.defId}' included");
                    string cWepKey = weapon.weaponDef.Description.Id;
                    if (condensed.ContainsKey(cWepKey))
                    {
                        condensed[cWepKey].AddWeapon(weapon);
                    }
                    else
                    {
                        condensed[cWepKey] = cWeapon;
                    }
                }
                List<CondensedWeapon> condensedUsableWeps = condensed.Values.ToList();
                Mod.Log.Debug?.Write($"There are {condensedUsableWeps.Count} usable condensed weapons.");

                List<List<CondensedWeapon>> weaponSets = AEHelper.MakeWeaponSets(condensedUsableWeps);

                // Add melee weapon to each set. Increase it's damage to deal with virtual damage
                CondensedWeapon cMeleeWeapon = new CondensedWeapon(attacker.MeleeWeapon);
                cMeleeWeapon.First.StatCollection.Set(ModStats.HBS_Weapon_DamagePerShot, virtualMeleeDamage);
                for (int i = 0; i < weaponSets.Count; i++)
                {
                    weaponSets[i].Add(cMeleeWeapon);
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
            if (Mod.Config.UseCBTBEMelee && attacker.StatCollection.GetValue<bool>(ModStats.CBTBE_HasPhysicalWeapon))
            {
                Mod.Log.Debug?.Write(" Unit has CBTBE physical weapon, marking isMeleeMech.");
                isMeleeMech = true;
            }
            else
            {
                int rawRangedDam = 0, rawMeleeDam = 0;
                foreach (Weapon weapon in attacker.Weapons)
                {
                    if (weapon.WeaponCategoryValue.CanUseInMelee)
                    {
                        rawMeleeDam += (int)(weapon.DamagePerShot * weapon.ShotsWhenFired);
                    }
                    else
                    {
                        rawRangedDam += (int)(weapon.DamagePerShot * weapon.ShotsWhenFired);
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
