# Animator Controller Setup - Quick Reference

## Open: Assets/Animations/Leon/Leon.controller

---

## STEP 1: Add IsJumping Parameter

In the **Parameters** panel (left side):
1. Click **+** button
2. Select **Bool**
3. Name it: `IsJumping`
4. Default value: **OFF** (unchecked)

**Result:**
```
Parameters:
├─ Speed (Float) ✓ Exists
├─ Push (Int) ✓ Exists
├─ Attack (Int) ✓ Exists
└─ IsJumping (Bool) ← NEW
```

---

## STEP 2: Create Jump State

In the **Animator Graph** (center):

1. Right-click empty space → **Create State** → **Empty**
2. Name it: `Jump`
3. Select the new Jump state
4. In Inspector:
   - **Motion:** Drag `Leon-Jump.anim` here
   - **Speed:** 1.0
   - **Loop Time:** OFF (unchecked) ← IMPORTANT!

---

## STEP 3: Add Transitions

### Transition A: Idle/Run → Jump
**From:** Any state that isn't jumping (Idle, Run)
**To:** Jump

1. Right-click on **Idle** state → **Make Transition** → Click **Jump**
2. Click on the arrow (transition)
3. In Inspector, set:
   - **Conditions:** `IsJumping` **true**
   - **Exit Time:** 0
   - **Transition Duration:** 0.1
   - **Interruption:** None

**Repeat for Run → Jump**

### Transition B: Jump → Idle/Run
**From:** Jump
**To:** Any state (create two transitions, one to Idle, one to Run)

1. Right-click on **Jump** state → **Make Transition** → Click **Idle**
2. Click on the arrow
3. In Inspector, set:
   - **Conditions:** `IsJumping` **false**
   - **Exit Time:** 0
   - **Transition Duration:** 0.05
   - **Interruption:** None

**Repeat for Jump → Run**

---

## STEP 4: Update Existing Transitions (Important!)

### Attack1 State
- Keep existing transitions
- Make sure Attack1 **CANNOT** transition to Jump
- The code blocks jumping during attacks

### Attack2 State
- Keep existing transitions
- Make sure Attack2 **CANNOT** transition to Jump
- The code blocks jumping during attacks

### Push State
- Keep existing transitions
- Make sure Push **CANNOT** transition to Jump
- The code blocks jumping during pushes

---

## STEP 5: Final Animator Structure

Your animator should look like:

```
                    ┌──────────────┐
                    │    Idle      │
                    └──────────────┘
                         ↕ (Speed)
                    ┌──────────────┐
                    │     Run      │
                    └──────────────┘
                         ↕ IsJumping
                    ┌──────────────┐
                    │     Jump     │
                    └──────────────┘

Additional States (unchanged):
- Attack1
- Attack2
- Push
```

---

## STEP 6: Inspector Settings for PlayerMovement2D

On your player object, make sure `PlayerMovement2D` has:

```
Jump (header)
├─ Jump Force:           12
├─ Fall Gravity Scale:   1.5
├─ Jump Parameter:       IsJumping
└─ Coyote Time:          0.1
```

---

## Testing Sequence

1. **Save** animator (Ctrl+S)
2. **Go to scene** with Leon
3. **Press Play**
4. **Press Space** - Leon should jump!
5. **In mid-air** - Jump animation plays, Run animation gone
6. **Landing** - Transitions smoothly back to Run/Idle
7. **During attack** - Try jumping = blocked ✓
8. **During push** - Try jumping = blocked ✓

---

## Common Issues & Fixes

### Issue: Jump doesn't trigger
**Fix:** Check IsJumping parameter exists and transitions have `IsJumping == true` condition

### Issue: Run animation plays mid-air
**Fix:** Check `animator.SetFloat(speedParamHash, IsGrounded ? normalizedSpeed : 0f);` in Update()
This line sets Speed to 0 when airborne

### Issue: Jump animation loops
**Fix:** Make sure **Loop Time is OFF** in Leon-Jump animation settings

### Issue: Can jump during attack
**Fix:** Check code has `if (IsAttacking || IsPushing) return;` in ExecuteJump()

### Issue: Movement feels floaty
**Fix:** Increase fallGravityScale from 1.5 to 1.8-2.0

---

## Asset Files Ready

✅ **Leon-Jump.anim** - Created and ready
✅ **Leon-Push.anim** - Fixed (no more freeze frame)
✅ **PlayerMovement2D.cs** - Updated with jump code

**All scripts are ready to go!**
