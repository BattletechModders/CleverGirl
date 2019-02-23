# CleverGirl

This mod for the [HBS BattleTech](http://battletechgame.com/) attempts to make the AI a bit smarter.

![Jurassic Part Clever Girl](clever-girl-5b1b38.jpg)

## Changes

Jumping Heat change



## Configurable Options

The following values can be tweaked in `mod.json` to customize your experience:
* **Debug:** If true, detailed information will be printed in the logs.

### WIP

AIUtil uses: `if (AIUtil.Get2DDistanceBetweenVector3s(sampledPathNodes[i].Position, this.unit.CurrentPosition) >= 1f)`

But add Mech uses: 

```
        public void OnJumpComplete(Vector3 finalPosition, Quaternion finalHeading, int sequenceUID)
        {
            float num = Vector3.Distance(base.PreviousPosition, finalPosition);
            this.AddJumpHeat(num);
```

I wonder if it's a difference in 2D vs. 3D vector calculation

That actually would explain it fairly neatly

I bet they don't overheat on a flat plain

But only when they are jumping vertical distances

I bet that's it, yeah.