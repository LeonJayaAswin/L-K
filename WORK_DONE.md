# L&K — Work Done So Far

> Current state of the project as of **8 November 2026**.
> This is the up-to-date overview; the older `README.md`, `CODE_EXPLANATION.md`,
> `JUMP_IMPLEMENTATION_GUIDE.md`, `ANIMATOR_SETUP_QUICK.md`, `FIXES_SUMMARY.md`
> and `TEST_REPORT.md` describe the earlier Leon jump work.

---

## 1. Project overview

| Item | Value |
|---|---|
| Engine | Unity **6000.6.0f1** |
| Template / pipeline | 2D + URP |
| Input | Unity Input System (`com.unity.inputsystem`) |
| Main scene | `Assets/Scenes/SampleScene.unity` |
| Game style | Local co-op 2D side-scrolling platformer |
| Players | **Leon** (player 1) and **Krush** (player 2) |
| Enemy | Skeleton |
| Editor automation | `com.unity.pipeline` + helper scripts in `AgentScripts/` |

---

## 2. Scene layout

`SampleScene` contains:

| Object | Role |
|---|---|
| `Main Camera` | Orthographic 2D camera |
| `Global Light 2D` | URP 2D lighting |
| `SampleGround` | Walkable ground (tag `Ground`) |
| `Leon` | Player 1 |
| `Krush` | Player 2 |
| `Skeleton` | Patrol / chase / attack enemy |
| `Push_object` | Pushable prop (starts inactive) |

Approximate transforms:

| Object | Position | Scale |
|---|---|---|
| Leon | `(-7.52, -3.37, 0)` | `1.4879` |
| Krush | `(-6.7063, -3.44, 0)` | `1.4190717` |

---

## 3. Player 1 — Leon

**Scripts on the object**

| Component | Purpose |
|---|---|
| `PlayerMovement2D` | Run, jump, grounding, animation sync, input routing |
| `PlayerAttack2D` | Sword combo + push |
| `PlayerHealthHurt` | Hurt reactions and death |
| `PlayerSeparation2D` | Disables player-vs-player collision (players pass through each other) |
| `Animator` | `Assets/Animations/Leon/Leon.controller` |
| `Rigidbody2D` + `BoxCollider2D` | Dynamic body, rotation frozen, collider `0.37 × 1.78` |

**Movement (`PlayerMovement2D`)**

- `inputMode = KeyboardOnly` — one controller cannot drive both players.
- Move speed `3`, acceleration `55`, deceleration `40`, turn speed `90`.
- Jump force `8`, fall gravity multiplier `1.5`, coyote time `0.1 s`.
- Ground probe is a box that straddles the bottom of the collider and looks for
  objects tagged `Ground`, so grounding works regardless of sprite scale/frame.
- Run animation playback is scaled by real speed against
  `animationReferenceSpeed = 3.878` to keep the stride length constant.
- Movement is locked during attack, push, and hurt.

**Attack (`PlayerAttack2D`)**

- Mouse and keyboard (`F`) attack; gamepad attack is **disabled** so the
  controller belongs to Krush.
- Two-swing combo with a `0.35 s` input buffer.
- Hit detection is an overlap test in front of Leon; each swing can hit the
  Skeleton once.
- Push half: hold `E` to shove a `PushObject`; forward-only, direction locked
  at push start, cancelled by steering backwards. Gamepad push is disabled.

**Controls**

| Action | Input |
|---|---|
| Move | `A` / `D` or `←` / `→` |
| Jump | `Space` |
| Attack | Mouse button / `F` |
| Push | `E` |

---

## 4. Player 2 — Krush

Krush was added as a full second character by copying Leon's movement, push,
hurt/death and animation setup — **without attack**.

**Scripts on the object**

| Component | Purpose |
|---|---|
| `PlayerMovement2D` | Same movement/jump system as Leon |
| `PlayerPush2D` | Push only (no sword combo) |
| `PlayerHealthHurt` | Hurt reactions and death |
| `PlayerSeparation2D` | Disables player-vs-player collision (players pass through each other) |
| `Animator` | `Assets/Animations/Krush/Krush.controller` |
| `Rigidbody2D` + `BoxCollider2D` | Dynamic body, rotation frozen, collider `0.37 × 1.70` |

**Movement**

- `inputMode = GamepadOnly`, `gamepadIndex = 0` (the active pad).
- Move speed `4`, otherwise the same acceleration/deceleration/turn values.
- Same jump force, fall gravity, coyote time and ground probe as Leon.

**Push (`PlayerPush2D`)**

A copy of Leon's push behaviour with the sword combo removed:

- Hold **RB / Right Shoulder** to push.
- Only starts when a `PushObject` is within `pushRange = 1` and in front.
- Direction is locked when the push starts, so Krush only ever pushes forward.
- `OnPushStart` / `OnPushEnd` animation events gate when the object moves.
- Short end hold (`0.2 s`) keeps the authored push rhythm.
- Steering against the push direction cancels it; the cancel latches until the
  button is released.
- Push strength `2`, search radius `6`.

**Controls**

| Action | Input |
|---|---|
| Move | Left stick |
| Jump | `A` / South |
| Push | `RB` / Right Shoulder |
| Attack | — (not implemented by design) |

---

## 5. Co-op input split

`PlayerMovement2D` has a `PlayerInputMode` enum:

```csharp
public enum PlayerInputMode
{
    KeyboardAndGamepad, // single-player default
    KeyboardOnly,       // player 1
    GamepadOnly         // player 2
}
```

- Leon = `KeyboardOnly`
- Krush = `GamepadOnly`
- `gamepadIndex` selects `Gamepad.all[index]`; `0` means “active gamepad”.
  A second controller can be assigned with `gamepadIndex = 1`.

This prevents one controller from moving both characters. Leon's gamepad attack
and push are also switched off in `PlayerAttack2D`.

---

## 6. Player-vs-player collision (latest change)

**Problem:** both players were on the `Default` layer, so their colliders blocked
and shoved each other.

**Solution:** `Assets/Scripts/Player/PlayerSeparation2D.cs`, added to both
players.

How it behaves:

1. **No collision between the two players.** At startup each player calls
   `Physics2D.IgnoreCollision` on the other player's collider.
2. **Players pass through each other.** There is no positional correction and no
   push-apart, so they can stand on the same spot, cross through one another, or
   jump through one another without being nudged.
3. **The rest of the world still collides.** Both players still collide normally
   with the ground, walls, the Skeleton and pushable props.
4. **Stable sprite draw order.** Leon uses sorting order `4` and Krush uses `3`
   (Skeleton `2`, ground/props `0`), so when the sprites overlap one is always
   drawn in front. They previously shared order `3`, which made the renderer
   flicker between them.

**Inspector field**

| Field | Meaning |
|---|---|
| `other` | The other player's separation component (wired in the scene) |

**Test results (play mode)**

| Check | Result |
|---|---|
| Collision ignored Leon ↔ Krush | `true` both directions |
| Krush walks through Leon | Krush crossed to the other side (`16.966` units), maximum overlap `0.507`, Leon did not move (`0.000`) |
| Both standing on the same spot | stayed stacked with a `0.538` overlap, no push-apart |
| Sprite draw order while overlapping | Leon `4`, Krush `3`, distinct and unchanged after the pass-through |

---

## 7. Push system

- `PushableObject2D` is the prop component. It queues a shove and applies it with
  `Rigidbody2D.MovePosition` in `FixedUpdate`, so it slides smoothly and pushes
  dynamic bodies out of the way.
- The prop only moves along the push direction.
- Leon pushes through `PlayerAttack2D`; Krush pushes through `PlayerPush2D`.
- Both use the `PushObject` tag, `pushRange = 1`, `pushStrength = 2` and the
  `Push` animator integer.
- `Push_object` starts inactive in the scene; activate it to test pushing.

---

## 8. Health, hurt and death

`PlayerHealthHurt` is shared by both players.

- `hitsBeforeDeath = 5`; the fifth hit plays Death instead of Hurt.
- Hurt locks movement while the Hurt state is on screen.
- Death disables movement and attack, stops the body and sets
  `Rigidbody2D.simulated = false`.
- While Death plays, the visible bottom of the sprite is pinned to the ground
  line captured at death, so the corpse does not float as the frames shrink.
- The Skeleton calls `TakeHit()` on the player when its attack lands.

Verified on Krush: Hurt locks movement then returns to Idle; the fifth hit plays
Death and pins the sprite bottom to `-4.485` against a ground top of about
`-4.500`.

---

## 9. Animations

### Leon — `Assets/Animations/Leon/`

| Clip | FPS | Length |
|---|---|---|
| `Leon-Idle` | 12 | 2.083 s |
| `Leon-Run` | 12 | 2.083 s |
| `Leon-Jump` | 20 | 0.25 s |
| `Leon-Push` | 20 | 1.2 s |
| `Leon-Attack1` | 24 | 0.917 s |
| `Leon-Attack2` | 20 | 1.25 s |
| `Leon-Hurt` | 20 | 1.25 s |
| `Leon-Death` | 20 | 1.25 s |

Animator parameters: `Speed` (float), `Attack` (int), `Push` (int),
`IsJumping` (bool), `Hurt` (trigger), `Death` (trigger).

### Krush — `Assets/Animations/Krush/`

| Clip | FPS | Length | Notes |
|---|---|---|---|
| `Krush-Idle` | 12 | 2.083 s | loops |
| `Krush-Run` | 12 | 2.083 s | loops, same timing as Leon |
| `Krush-Jump` | 20 | 1.25 s | |
| `Krush-Push` | 20 | 1.25 s | loops, events at `0` and `1.15` |
| `Krush-Hurt` | 20 | 1.25 s | |
| `Krush-Death` | 20 | 1.25 s | |

Animator parameters: `Speed` (float), `Push` (int), `IsJumping` (bool),
`Hurt` (trigger), `Death` (trigger).

States: Idle, Run, Jump, Push, Hurt, Death. The run timing was matched to Leon
exactly. Unused Krush bow/shoot sprites remain in
`Assets/Sprites/Krush spritesheets/`.

---

## 10. Enemy — Skeleton

`Assets/Scripts/Enemy/SkeletonAI.cs`

- Idles, then patrols left and right.
- Detects the player within `6` units, chases, stops at `1.6` units and
  alternates `Attack1` / `Attack2`.
- Each landed attack calls `PlayerHealthHurt.TakeHit()`.
- Five sword hits kill it; hits play `TakeHit`, knock it back and interrupt it.
- Death freezes the body and pins the corpse to the ground.

---

## 11. Script map

```
Assets/Scripts/
├── Player/
│   ├── PlayerMovement2D.cs      run, jump, grounding, input modes, animation sync
│   ├── PlayerAttack2D.cs        Leon sword combo + push
│   ├── PlayerPush2D.cs          Krush push only (no attack)
│   ├── PlayerHealthHurt.cs      hurt / death for both players
│   ├── PlayerSeparation2D.cs    disables player-vs-player collision (pass-through)
│   └── PushableObject2D.cs      pushable prop
└── Enemy/
    └── SkeletonAI.cs            patrol / chase / attack / death
```

Editor-only helpers (not part of the game build):

```
AgentScripts/
├── BuildKrush.cs            idempotent builder: Krush clips, controller, scene wiring
├── PlayTestKrush.cs         gamepad play-mode tests: run, jump, push, hurt/death
├── PlayTestSeparation.cs    play-mode tests: collision off, walk-through, stacking
├── InspectKrush.cs          scene/asset inspection
├── Inspect.cs               general scene inspection
├── PlayTest.cs              original Leon play-mode tests
├── AttackVerify.cs          attack/combo verification
└── VerifySkeleton.cs        skeleton verification
```

---

## 12. Verification summary

All of the following were checked in play mode:

- Krush runs, jumps with `A`, and pushes with `RB`.
- Pushed object moved about `1.26` units during the push test.
- Krush's Hurt locks movement and returns to Idle.
- The fifth hit plays Death and grounds the sprite correctly.
- Leon does not respond to the gamepad.
- Leon and Krush pass through each other with no collision and no push-apart.
- Leon (sorting order `4`) and Krush (`3`) have a fixed draw order, so
  overlapping sprites no longer flicker front and back.
- No console errors or warnings after the final pass.

Tests were driven through the Unity Pipeline package:

```powershell
unity command editor_play
unity command run_script --file AgentScripts/PlayTestSeparation.cs --entry PlayTestSeparation.Run --timeout_ms 120000
unity command editor_stop
```

---

## 13. Known limitations / possible next steps

- **One physical controller** drives Krush; Leon is keyboard-only. Two-controller
  play needs `gamepadIndex = 1` on Krush (or a second `GamepadOnly` player).
- Krush has **no attack by design**; the bow/shoot sprites and the uncommitted
  `Krush-Shot` animator state are not used by gameplay.
- `PlayerMovement2D` still contains Leon-specific comments/tooltips and an unused
  `attackLockDeceleration` path; harmless for Krush but could be renamed later.

---

## 14. Working-tree state at the time of writing

Committed: Leon jump/attack work and the initial Krush character.

Uncommitted:

```
 M Assets/Animations/Krush/Krush.controller      (unrelated Krush-Shot state)
 M Assets/Scenes/SampleScene.unity               (PlayerSeparation2D on both players)
?? Assets/Scripts/Player/PlayerSeparation2D.cs
?? Assets/Scripts/Player/PlayerSeparation2D.cs.meta
?? AgentScripts/PlayTestSeparation.cs
```

No changes were committed as part of this work.
