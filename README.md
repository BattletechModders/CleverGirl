# CleverGirl

This mod for the [HBS BattleTech](http://battletechgame.com/) attempts to make the AI a bit smarter, faster, and overall more capable. Note that capable does not necessarily mean 'deadly'.

A brief summary of the changes made in this mod include:

* Correct AI calculation of Jump Heat
* Simplify AI weapon-set selection 

:warning: This mod requires the latest release of [https://github.com/iceraptor/IRBTModUtils/]. 

# Changes

Details on each change is listed below.

### GenerateJumpMoveCandidatesNode Heat Correction

The vanilla implementation of the `GenerateJumpMoveCandidatesNode` calculates jump heat using a 2-dimensional model. This may contribute to the cases where a jump-capable 'mech suddenly overheats and damages themselves, even on poor accuracy shots. Actual heat gain is based upon the 3-dimensional distance travelled. This patch corrects the logic inside the BehaviorNode to compare the 3D heat generation versus the acceptable heat for the unit.

### MakeAttackOrderForTarget Simplifications

`MakeAttackOrderForTarget` is invoked by the AI when it's decided it wants to attack a particular unit. The vanilla implementation calculates the expected damage to that target unit by shooting, melee, and DFA damage. But it does this by evaluating every *combination* of weapons available to the attacker, to maximize damage and minimize utility (such as heat). For a unit with the following weapons: [ LRM5, LRM5, PPC, SLAS, SLAS ] it will calculate 31 different sets of weapons (i.e. a combination without substitution - `sum r=1->n n! / (r! ( n-r )!)`)  for the ranged attack, and 7 sets for melee and DFA (SLAS, SLAS, MELEE). See `AttackEvaluator.MakeAttackOrderForTarget` for the vanilla logic.

This is fairly inefficient (close to O(n^2) complexity) but it gets worse. Inside of `AttackEvaluator.MakeAttackOrder` it executes `MakeAttackOrderForTarget` not only for the lance's designated target, but also **for every enemy target**. This pushes the evaluation complexity to O(n^n) because we're evaluating every possibly weapon set, against every possible target, every time. It does this to find 'better' targets than the lance-designed target, which may be too far away.

CleverGirl subverts this logic by calculating the expected damage for all weapons, then filtering the full set based upon criteria. The criteria changes based upon the attack type. Ranged attacks will filter weapons based upon how much heat they can do, or try to account for breaching shot's ability to punch through cover. Melee and DFA attacks will ignore any weapons that cannot attack. This drops the process back to O(n^2) and results in a 'faster' AI think time.

# Assumptions and Warnings

* This mode assumes the following **CombatGameConstants** values are set to 1.0 (vanilla values). Expected damage predictions will be less accurate the further from 1.0 you have set these values:
  * `ToHit.DamageResistanceIndirectFire`
  * `ToHit.DamageResistanceObstructed` 
  * `CombatValueMultipliers.GlobalDamageMultiplier`
* May have an issue with `DamagePerShotPredicted`, since **CustomUnits** overrides it as well.

# Configurable Options

The following values can be tweaked in `mod.json` to customize your experience:

* **Debug:** If true, detailed information will be printed in the logs.

## DEV NOTES

### General
Part of the problem is that BT has too many options. These options have to be encoded into decision tree calculations, which is a large series of branching yes/no questions. What's your role (sniper, brawler, escort, etc), what movement best helps that purpose (from a choice of 20-60 hexes and 2-3 movement types (move, sprint, jump)). Balance that against which position makes the most sense when you have 4 weapon bands, and heat to balance as well.

There are many things that could help that outcome - running different movement calculations in parallel, making a stronger determination of unit purpose before going through the tree logic to determine specific actions (which is present but not heavily used), pre-calculating weapon fire from each position independently, etc. Short-cutting the weapon calculations by applying a mean value to the shots * projectiles * damage calcuation, instead of doing each one one by one, etc.

I've been trying for a while to get a profiler hooked up to confirm my suspicions, but I think three changes would make a major impact on performance:

* Start running the decision tree in the previous actor's turn. A suboptimal decision that doesn't take into account the last action but executed immediately probably increases player satisfaction immensely.
* Normalize weapons and cluster them from a single emitter, instead of resolving the damage weapon-by-shot-by-projectile
* Implement stronger 'lance commander' logic (already in AI) that defines what the lance wants to do, instead of letting it waffle actor by actor. Pick a model on the player's side to just punish, instead of letting each actor do their own thing.

#### MISC

* Add crit-seeking potential to weapons
* Aggregate weapons into clusters
  * Clusters should reduce their count to work with overheat calculations effectively
* Calculate firepower loss from friendly unit shutdown
* Calculate melee retaliation properly 
* Prevent melee from light units that won't benefit
* Incorporate CAC AoE and inferno effects