
# CleverGirl

This mod for the [HBS BattleTech](http://battletechgame.com/) attempts to make the AI a bit smarter.

![Jurassic Part Clever Girl](clever-girl-5b1b38.jpg)



This mod requires [https://github.com/iceraptor/IRBTModUtils/]. Grab the latest release of __IRBTModUtils__ and extract it in your Mods/ directory alongside of this mod.

## Changes

Jumping Heat change



## Configurable Options

The following values can be tweaked in `mod.json` to customize your experience:
* **Debug:** If true, detailed information will be printed in the logs.
* **Trace:** If true, even more detailed information will be printed in the logs. Should always be enabled together with *Debug*.
* **UseCBTBEMelee:** If true enables integration with [CBTBE](https://github.com/BattletechModders/CBTBehaviorsEnhanced) to decide if a unit favors melee attacks.
* **SimplifiedAmmoModeOperationThreshold:** Integer value, used to configure [Simplified Selection Mode](#simplified-selection-mode). If the configured value is less than the total amount of firing modes over all grouped weapons, simplified operation mode will be used..
* **AttemptReducingOverheatSolutions:** If true, enables [Reduction of weapons on Overheat](#reduction-of-weapons-on-overheat).
* **RestrictFiringModeToFlyingTargets:** List of *Id*s for firing modes that should only be used against flying targets. 
* **NoMeleeWeaponCategory:** Category ID used to mark a weapon as never consider for melee attacks.
* **Weights:** Three sub-configurations to control the decision weights for the AI:
	* **FriendlyDamageMulti:** Weight multiplier for friendly fire damage. Higher value means AI will further down-prioritize firing solutions resulting in friendly fire damage.
	* **PunchbotDamageMulti:** Weight multiplier for melee damage when considering melee attack vs ranged attack.
	* **OneShotMinimumToHit:** Minimum hit chance for one-shot weapons to be part of a potential solution.
* **PrioritizeArtilleryDamageRatio:** The ratio artillery damage is multplied by when deciding if to use artillery or standard damage.	
	
#### Standard selection of firing solution
The standard case of selection of which weapons a unit should fire is done using the following base steps:
* Group all weapons of same type (i.e. same *Id*) together, creating a *Condensed Weapon* group.
* For each such Condensed Weapon, find all available firing modes and ammunition types.
* Create all combinations of the above factors, creating all potential firing solutions.
	* Also create separate firing solution including Melee and DFA pseudo-weapons.
* Iterate over all potential firing solutions to find the optimal damage prediction.
* Configure all weapons to use the selected firing mode and ammunition, where all weapons part of the same *Condensed Weapon* share the same configuration.
* Execute the attack order

#### Simplified Selection Mode
In a situation where the AI unit has weapons of many different types and with many firing modes, the amount of potential firing solutions grows exponentially. As a result the selection algorithm can take a very long time, often minutes.

The Simplified selection mode is a way to optimize the AI in such cases, limiting it to only use each *Condensed Weapon*'s base firing mode and the firing mode firing the most shots (if there is one).

#### Reduction of weapons on Overheat
When all potential firing solutions are rejected, and some of them due to projected overheating, it is possible to still find a valid firing solution to fire *some* weapons.

This is done by reducing the amount of weapons part of each *Condensed Weapon* group, checking if this subset of weapons creates an acceptable heat level.

## WIP

### General
Part of the problem is that BT has too many options. These options have to be encoded into decision tree calculations, which is a large series of branching yes/no questions. What's your role (sniper, brawler, escort, etc), what movement best helps that purpose (from a choice of 20-60 hexes and 2-3 movement types (move, sprint, jump)). Balance that against which position makes the most sense when you have 4 weapon bands, and heat to balance as well.

There are many things that could help that outcome - running different movement calculations in parallel, making a stronger determination of unit purpose before going through the tree logic to determine specific actions (which is present but not heavily used), pre-calculating weapon fire from each position independently, etc. Short-cutting the weapon calculations by applying a mean value to the shots * projectiles * damage calcuation, instead of doing each one one by one, etc.

I've been trying for a while to get a profiler hooked up to confirm my suspicions, but I think three changes would make a major impact on performance:

* Start running the decision tree in the previous actor's turn. A suboptimal decision that doesn't take into account the last action but executed immediately probably increases player satisfaction immensely.
* Normalize weapons and cluster them from a single emitter, instead of resolving the damage weapon-by-shot-by-projectile
* Implement stronger 'lance commander' logic (already in AI) that defines what the lance wants to do, instead of letting it waffle actor by actor. Pick a model on the player's side to just punish, instead of letting each actor do their own thing.

### Jumping
AIUtil uses: `if (AIUtil.Get2DDistanceBetweenVector3s(sampledPathNodes[i].Position, this.unit.CurrentPosition) >= 1f)`

But add Mech uses: 

```
        public void OnJumpComplete(Vector3 finalPosition, Quaternion finalHeading, int sequenceUID)
        {
            float num = Vector3.Distance(base.PreviousPosition, finalPosition);
            this.AddJumpHeat(num);
```

`I wonder if it's a difference in 2D vs. 3D vector calculation
That actually would explain it fairly neatly
I bet they don't overheat on a flat plain
But only when they are jumping vertical distances
I bet that's it, yeah.`

## Possible Improvements

* Add crit-seeking potential to weapons
* Aggregate weapons into clusters
  * Clusters should reduce their count to work with overheat calculations effectively
* Calculate firepower loss from friendly unit shutdown
* Calculate melee retaliation properly 
* Prevent melee from light units that won't benefit
* Incorporate CAC AoE and inferno effects

## Dev Notes

AI logic invoked by AITeam.TurnActorProcessActivation

* Checks for opfor reservation
* Checks for reservation pre-reqs
* Assignes role to unit

AITeam.OnUpdate() -> AITeam.think()

Sequence starts at ShootAtHighestPriorityEnemyNode:Tick() ExecuteStationaryAttackNode.Tick(). Both invoke AttackEvaluator.MakeAttackOrder() 

* Iterates over designed targets for lance, making an attack order for the target. Evaluates the firepower reduction from the attack
* Then iterates over every enemy unit that's not dead, and is an abstract actor. MakesAnAttack order, and evals firepower reduction from attack.
** For each target, evaluates additional damage from friends attacking an evasive target (because of evasion strip)
** If damage is greater than BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetFirepowerTakeawayByPercentage, chooses opportunity fire
*** If opportunity fire, tries to create a MultiTargetAttackOrder if possible.

MakeAttackOrder invokes MakeAttackOrderForTarget, and evaluates FirepowerReduction from attack. MakeAttackOrderForTarget is only ever invoked by AttackEvalutor.MakeAttackOder
