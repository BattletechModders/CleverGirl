using BattleTech;
using IRBTModUtils;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using us.frostraptor.modUtils;
using static AttackEvaluator;

namespace CleverGirl.Helper {

    public static class AOHelper {

        // Evaluate all possible attacks for the attacker and target based upon their current position. Returns the total damage the target will take,
        //   which will be compared against all other targets to determine the optimal attack to make
        public static float MakeAttackOrderForTarget(AbstractActor attackerAA, ICombatant target, bool isStationary, out BehaviorTreeResults order) {
            Mod.Log.Debug?.Write($"Evaluating AttackOrder from ({CombatantUtils.Label(attackerAA)}) against ({CombatantUtils.Label(target)} at position: ({target.CurrentPosition})");

            // If the unit has no visibility to the target from the current position, they can't attack. Return immediately.
            if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(attackerAA, target)) {
                order = BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
                return 0f;
            }

            Mech attackerMech = attackerAA as Mech;
            float currentHeat = attackerMech == null ? 0f : (float)attackerMech.CurrentHeat;
            float acceptableHeat = attackerMech == null ? float.MaxValue : AIUtil.GetAcceptableHeatLevelForMech(attackerMech); ;
            Mod.Log.Debug?.Write($" heat: current: {currentHeat} acceptable: {acceptableHeat}");

            //float weaponToHitThreshold = attackerAA.BehaviorTree.weaponToHitThreshold;

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
            //float evasiveToHitFraction = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100f;

            // Evaluate ranged attacks 
            //if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
            //    Mod.Log.Debug?.Write($"Checking evasive shots against target, needs {evasiveToHitFraction} or higher to be included.");
            //    weaponSetsByAttackType[0] = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.RangedWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
            //} else {
            //    Mod.Log.Debug?.Write($"Checking non-evasive target.");
            //    weaponSetsByAttackType[0] = AEHelper.MakeWeaponSets(candidateWeapons.RangedWeapons);
            //}
            weaponSetsByAttackType[0] = AEHelper.MakeWeaponSets(candidateWeapons.RangedWeapons);

            // Evaluate melee attacks
            string cannotEngageInMeleeMsg = "";
            if (attackerMech == null || !attackerMech.CanEngageTarget(target, out cannotEngageInMeleeMsg)) {
                Mod.Log.Debug?.Write($" attacker cannot melee, or cannot engage due to: '{cannotEngageInMeleeMsg}'");
            } else {


                // Determine if we're a punchbot - defined by melee damage 2x or greater than raw ranged damage
                bool isPunchbot = false;
                if (Mod.Config.CBTBEMelee && attackerMech.StatCollection.GetValue<bool>(ModStats.CBTBE_HasPhysicalWeapon))
                {
                    Mod.Log.Debug?.Write(" Unit has CBTBE physical weapon, marking as punchbot.");
                    isPunchbot = true;
                }
                else
                {
                    int rawRangedDam = 0, rawMeleeDam = 0;
                    foreach (Weapon weapon in attackerMech.Weapons)
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
                        Mod.Log.Debug?.Write($" Unit isPunchbot due to rawMelee: {rawMeleeDam} >= rawRanged: {rawRangedDam} x {Mod.Config.Weights.PunchbotDamageMulti}");
                        isPunchbot = true;
                    }
                }
                
                // Check Retaliation
                // TODO: Retaliation should consider all possible attackers, not just the attacker
                // TODO: Retaliation should consider how much damage you do with melee vs. non-melee - i.e. punchbots should probably prefer punching over weak weapons fire
                // TODO: Should consider if heat would be reduced by melee attack
                if (isPunchbot || AEHelper.MeleeDamageOutweighsRisk(attackerMech, target)) {

                    // Generate base list
                    //List<List<CondensedWeapon>> meleeWeaponSets = null;
                    //if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                    //    meleeWeaponSets = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.MeleeWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                    //} else {
                    //    meleeWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.MeleeWeapons);
                    //}
                    List<List<CondensedWeapon>> meleeWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.MeleeWeapons);

                    // Add melee weapons to each set
                    CondensedWeapon cMeleeWeapon = new CondensedWeapon(attackerMech.MeleeWeapon);
                    for (int i = 0; i < meleeWeaponSets.Count; i++) {
                        meleeWeaponSets[i].Add(cMeleeWeapon);
                    }

                    weaponSetsByAttackType[1] = meleeWeaponSets;
                } else {
                    Mod.Log.Debug?.Write($" potential melee retaliation too high, skipping melee.");
                }
            }

            // Evaluate DFA attacks
            if (attackerMech == null || !AIHelper.IsDFAAcceptable(attackerMech, target)) {
                Mod.Log.Debug?.Write("this unit cannot dfa");
            } else {

                // TODO: Check Retaliation
                //List<List<CondensedWeapon>> dfaWeaponSets = null;
                //if (targetIsEvasive && attackerAA.UnitType == UnitType.Mech) {
                //    dfaWeaponSets = AEHelper.MakeWeaponSetsForEvasive(candidateWeapons.DFAWeapons, evasiveToHitFraction, target, attackerAA.CurrentPosition);
                //} else {
                //    dfaWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);
                //}
                List<List<CondensedWeapon>> dfaWeaponSets = AEHelper.MakeWeaponSets(candidateWeapons.DFAWeapons);

                // Add DFA weapons to each set
                CondensedWeapon cDFAWeapon = new CondensedWeapon(attackerMech.DFAWeapon);
                for (int i = 0; i < dfaWeaponSets.Count; i++) {
                    dfaWeaponSets[i].Add(cDFAWeapon);
                }

                weaponSetsByAttackType[2] = dfaWeaponSets;
            }

            List<AttackEvaluation> list = AEHelper.EvaluateAttacks(attackerAA, target, weaponSetsByAttackType, attackerAA.CurrentPosition, target.CurrentPosition, targetIsEvasive);
            Mod.Log.Debug?.Write(string.Format("found {0} different attack solutions", list.Count));
            float bestRangedEDam = 0f;
            float bestMeleeEDam = 0f;
            float bestDFAEDam = 0f;
            for (int m = 0; m < list.Count; m++) {
                AttackEvaluation attackEvaluation = list[m];
                Mod.Log.Debug?.Write($"evaluated attack of type {attackEvaluation.AttackType} with {attackEvaluation.WeaponList.Count} weapons, " +
                    $"damage EV of {attackEvaluation.ExpectedDamage}, heat {attackEvaluation.HeatGenerated}");
                switch (attackEvaluation.AttackType) {
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
            float existingTargetDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
            //float existingTargetDamageForOverheat = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
            float attackerLegDamage = attackerMech == null ? 0f : AttackEvaluator.LegDamageLevel(attackerMech);
            float maxAllowedLegDamageForDFA = AIHelper.GetBehaviorVariableValue(attackerAA.BehaviorTree, BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;

            AbstractActor targetActor = target as AbstractActor;
            List<PathNode> dfadestsForTarget = attackerMech.JumpPathing.GetDFADestsForTarget(targetActor);
            List<PathNode> meleeDestsForTarget = attackerMech.Pathing.GetMeleeDestsForTarget(targetActor);

            // LOGIC: Now, evaluate every set of attacks in the list
            for (int n = 0; n < list.Count; n++) {
                AttackEvaluator.AttackEvaluation attackEvaluation2 = list[n];
                Mod.Log.Debug?.Write("------");
                Mod.Log.Debug?.Write($"Evaluating attack solution #{n} vs target: {CombatantUtils.Label(targetActor)}");
                
                // TODO: Do we really need this spam?
                StringBuilder weaponListSB = new StringBuilder();
                weaponListSB.Append(" Weapons: (");
                foreach (Weapon weapon3 in attackEvaluation2.WeaponList) {
                    weaponListSB.Append("'");
                    weaponListSB.Append(weapon3.Name);
                    weaponListSB.Append("', ");
                }
                weaponListSB.Append(")");
                Mod.Log.Debug?.Write(weaponListSB.ToString());

                if (attackEvaluation2.WeaponList.Count == 0) {
                    Mod.Log.Debug?.Write("SOLUTION REJECTED - no weapons!");
                }

                // TODO: Does heatGenerated account for jump heat?
                // TODO: Does not rollup heat!
                bool willCauseOverheat = attackEvaluation2.HeatGenerated + currentHeat > acceptableHeat;
                Mod.Log.Debug?.Write($"heat generated: {attackEvaluation2.HeatGenerated}  current: {currentHeat}  acceptable: {acceptableHeat}  willOverheat: {willCauseOverheat}");
                if (willCauseOverheat && attackerMech.OverheatWillCauseDeath()) {
                    Mod.Log.Debug?.Write("SOLUTION REJECTED - overheat would cause own death");
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

                if (attackEvaluation2.AttackType == AIUtil.AttackType.Melee) {
                    if (!attackerAA.CanEngageTarget(target)) {
                        Mod.Log.Debug?.Write("SOLUTION REJECTED - can't engage target!");
                        continue;
                    }
                    if (meleeDestsForTarget.Count == 0) {
                        Mod.Log.Debug?.Write("SOLUTION REJECTED - can't build path to target!");
                        continue;
                    }
                    if (targetActor == null) {
                        Mod.Log.Debug?.Write("SOLUTION REJECTED - target is a building, we can't melee buildings!");
                        continue;
                    }
                    // TODO: This seems wrong... why can't you melee if the target is already engaged with you?
                    if (isStationary) {
                        Mod.Log.Debug?.Write("SOLUTION REJECTED - attacker was stationary, can't melee");
                        continue;
                    } 
                }

                // Check for DFA auto-failures
                if (attackEvaluation2.AttackType == AIUtil.AttackType.DeathFromAbove) {

                    if (!attackerAA.CanDFATargetFromPosition(target, attackerAA.CurrentPosition)) {
                        Mod.Log.Debug?.Write($"SOLUTION REJECTED - Cannot DFA target from pos: {attackerAA.CurrentPosition}!");
                        continue;
                    }

                    if (dfadestsForTarget.Count == 0) {
                        Mod.Log.Debug?.Write($"SOLUTION REJECTED - no valid DFA destination pathNodes!");
                        continue;
                    }

                    if (targetMaxArmorFractionFromHittableLocations < existingTargetDamageForDFA) {
                        Mod.Log.Debug?.Write($"SOLUTION REJECTED - armor fraction: {targetMaxArmorFractionFromHittableLocations} < behVar(Float_ExistingTargetDamageForDFAAttack): {existingTargetDamageForDFA}!");
                        continue;
                    }

                    if (attackerLegDamage > maxAllowedLegDamageForDFA) {
                        Mod.Log.Debug?.Write($"SOLUTION REJECTED - leg damage: {attackerLegDamage} < behVar(Float_OwnMaxLegDamageForDFAAttack): {maxAllowedLegDamageForDFA}!");
                        continue;
                    }
                }

                // LOGIC: If we have some damage from an attack, can we improve upon it as a morale / called shot / multi-attack?
                if (attackEvaluation2.ExpectedDamage > 0f) {
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
                        attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(targetActor, dfadestsForTarget);
                    } 
                    else if (attackType == AIUtil.AttackType.Melee) 
                    {
                        attackOrderInfo.IsMelee = true;
                        attackOrderInfo.Weapons.Remove(attackerMech.MeleeWeapon);
                        attackOrderInfo.Weapons.Remove(attackerMech.DFAWeapon);

                        attackOrderInfo.AttackFromLocation = attackerMech.FindBestPositionToMeleeFrom(targetActor, meleeDestsForTarget);
                    }

                    behaviorTreeResults.orderInfo = attackOrderInfo;
                    behaviorTreeResults.debugOrderString = $" using attack type: {attackEvaluation2.AttackType} against: {target.DisplayName}";

                    Mod.Log.Debug?.Write("attack order: " + behaviorTreeResults.debugOrderString);
                    order = behaviorTreeResults;
                    return attackEvaluation2.ExpectedDamage;
                }
                Mod.Log.Debug?.Write("Rejecting attack for not having any expected damage");
            }

            Mod.Log.Debug?.Write("There are no targets I can shoot at without overheating.");
            order = null;
            return 0f;
        }

    }
}
