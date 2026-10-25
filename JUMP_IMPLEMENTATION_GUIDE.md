# Jump Animation & Movement Implementation Guide

## What Was Done

### 1. ✅ Fixed Push Animation Loop Freeze
**Problem:** Push animation had a freeze frame at the end (frame 24), causing it to pause on the last frame before looping back to frame 0.

**Solution:** 
- Removed the extra keyframe at time 1.2
- Changed stop time from 1.25 to 1.2
- Animation now loops smoothly without any visible pause

**File Modified:** `Assets/Animations/Leon/Leon-Push.anim`

---

### 2. ✅ Created Jump Animation
**New File:** `Assets/Animations/Leon/Leon-Jump.anim`

**Animation Details:**
- **Duration:** 0.6 seconds (no loop - single play animation)
- **Sample Rate:** 10 FPS
- **Frames:** 5 frames from Leon-jump-v1.png sprite sheet
  - Frame 0-1: Ascent phase
  - Frame 2-3: Peak of jump
  - Frame 4-5: Hold at peak (smooth fall transition)

**Purpose:** Shows Leon mid-air during the jump, with hold frame for smooth landing

---

### 3. ✅ Enhanced PlayerMovement2D Script
**File:** `Assets/Scripts/Player/PlayerMovement2D.cs`

#### New Parameters Added:

```csharp
// Jump mechanics
[SerializeField, Min(0f)] private float jumpForce = 12f;           // Jump height
[SerializeField, Min(0.1f)] private float fallGravityScale = 1.5f; // Fall speed
[SerializeField] private string jumpParameter = "IsJumping";      // Animator param
[SerializeField, Range(0f, 0.2f)] private float coyoteTime = 0.1f; // Air forgiveness
```

#### Key Features:

**A. Jump Input Handling**
- Space bar or Gamepad A button to jump
- Queue-based system prevents input loss between frames
- Small cooldown prevents double-taps

**B. Coyote Time**
- Allows jump up to 0.1 seconds after leaving ground
- Makes platformer feel more forgiving and responsive
- Player doesn't need pixel-perfect timing

**C. State Management**
- Jump disabled while attacking (IsAttacking flag)
- Jump disabled while pushing (IsPushing flag)
- Run animation doesn't play mid-air (Speed param = 0 when airborne)

**D. Physics Enhancement**
- Enhanced fall gravity (1.5x default) = snappier platformer feel
- Allows quick descent instead of floaty jumps
- Prevents moon-walking on fall

**E. Animation State**
- `IsJumping` bool parameter controls jump animation
- Set to true when NOT grounded
- Set to false when landing

---

## Setup Instructions for Unity

### Step 1: Animator Parameters
Open `Assets/Animations/Leon/Leon.controller` and add these parameters:
- **Name:** `IsJumping` | **Type:** Bool | **Default:** false

### Step 2: Animator States
Create these states in the controller:

#### A. Jump State
- **Animation:** Leon-Jump
- **Speed:** 1.0
- **Loop Time:** OFF (unchecked)

#### B. Transitions

**From Idle/Run → Jump**
- **Condition:** `IsJumping == true`
- **Exit Time:** 0 (immediate)
- **Transition Duration:** 0.1s

**From Jump → Idle/Run**
- **Condition:** `IsJumping == false` 
- **Exit Time:** 0 (immediate)
- **Transition Duration:** 0.05s

**From Attack → Jump**
- **Condition:** `IsJumping == true`
- **Note:** Blocked by `!IsAttacking` check in code

**From Push → Jump**
- **Condition:** `IsJumping == true`
- **Note:** Blocked by `!IsPushing` check in code

### Step 3: Inspector Setup (PlayerMovement2D)
Set these values in the Inspector:

```
Jump section:
  Jump Force:        12        (adjust for jump height)
  Fall Gravity Scale: 1.5      (higher = faster fall)
  Jump Parameter:    IsJumping (auto-filled)
  Coyote Time:       0.1       (10ms window after leaving ground)
```

---

## Animation Flow Chart

```
                    ┌─── IsGrounded ───┐
                    │                  │
              [Idle/Run]          [Jump]
                    │                  │
                    └─── IsJumping ───┘
                    
State Logic:
- Grounded + No Input    → Idle
- Grounded + Input       → Run (speed based)
- Airborne              → Jump (IsJumping = true)
- During Attack         → Attack (Jump blocked)
- During Push           → Push (Jump blocked)
```

---

## Movement Flow Control

### Input Hierarchy:
1. **Attack** (highest priority - locks movement)
2. **Push** (medium priority - locked direction)
3. **Jump** (during air phase)
4. **Run** (ground movement only)
5. **Idle** (default)

### Jump Blocked When:
- ❌ `IsAttacking == true`
- ❌ `IsPushing == true`
- ❌ `coyoteCounter <= 0` (and not grounded)

### Jump Allowed When:
- ✅ Not attacking
- ✅ Not pushing
- ✅ Grounded OR in coyote time window

---

## Tweaking Guide (Senior Dev Tips)

### For Higher Jumps:
```
Increase jumpForce: 12 → 15-18
```

### For Lower Jumps:
```
Decrease jumpForce: 12 → 8-10
```

### For Floatier Feel:
```
Decrease fallGravityScale: 1.5 → 1.2-1.3
```

### For Snappier Feel:
```
Increase fallGravityScale: 1.5 → 1.8-2.0
```

### For More Forgiveness:
```
Increase coyoteTime: 0.1 → 0.15-0.2
```

### For Tighter Controls:
```
Decrease coyoteTime: 0.1 → 0.05
```

---

## Smooth Animation Transitions

The animator transitions are tuned for smooth feel:
- **Jump Entry:** 0.1s blend (quick response)
- **Jump Exit:** 0.05s blend (snappy landing)
- **No Exit Time:** Immediate transitions (responsive)

This creates:
✓ Snappy jump start
✓ Smooth mid-air hold
✓ Crisp landing into run/idle

---

## Testing Checklist

- [ ] Jump animation plays when Space/A pressed
- [ ] Jump animation stops when landing
- [ ] No run animation visible mid-air
- [ ] Can't jump during attack
- [ ] Can't jump during push
- [ ] Jump feels responsive (not floaty)
- [ ] Can jump shortly after walking off ledge (coyote time)
- [ ] Push animation loops smoothly with no freeze
- [ ] Landing transitions smoothly to run/idle

---

## Files Modified/Created

✅ **Modified:**
- `Assets/Animations/Leon/Leon-Push.anim` (fixed loop)
- `Assets/Scripts/Player/PlayerMovement2D.cs` (added jump system)

✅ **Created:**
- `Assets/Animations/Leon/Leon-Jump.anim` (new jump animation)

---

## Next Steps

1. Open the animator controller
2. Add `IsJumping` parameter
3. Create Jump state and transitions
4. Tweak jump values in Inspector
5. Test in-game

Enjoy your smooth jumping! 🎮
