# CleverGirl

This mod for the [HBS BattleTech](http://battletechgame.com/) attempts to make the AI a bit smarter.

![Jurassic Part Clever Girl](clever-girl-5b1b38.jpg)

## Changes

Jumping Heat change



## Configurable Options

The following values can be tweaked in `mod.json` to customize your experience:
* **Debug:** If true, detailed information will be printed in the logs.

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