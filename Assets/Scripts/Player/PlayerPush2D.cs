using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Which gamepad button starts a push.
///
/// Kept as an enum instead of a fixed button so a second player can be moved
/// onto another shoulder button without touching code.
/// </summary>
public enum PushGamepadButton
{
    South,
    East,
    West,
    North,
    LeftShoulder,
    RightShoulder,
    LeftStick,
    RightStick
}

/// <summary>
/// Push action for Krush. This is the push half of <see cref="PlayerAttack2D"/>
/// without the sword combo: Krush never attacks, she only shoves props.
///
/// Behaviour is copied one-for-one from Leon's push:
///  - Holding RB (or the configured key) starts a push, but only when a
///    pushable object is within range and in front of Krush.
///  - The push direction is locked to the way Krush is facing when the push
///    starts, so the object is only ever shoved FORWARD, never backwards.
///  - The push plays the Push animation for as long as the button is held.
///    Releasing it hands Krush straight back to the normal run/idle animation.
///  - The animation events OnPushStart/OnPushEnd gate when the object actually
///    moves, and the short end hold gives the push its authored rhythm.
///  - Steering against the push direction cancels it. The cancel latches until
///    the button is released, so holding RB cannot restart the push every frame.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("L&K/Player/Player Push 2D")]
public class PlayerPush2D : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Keyboard key starts a push. Off for Krush, who is controller-only.")]
    [SerializeField] private bool allowKeyboard;

    [SerializeField] private Key pushKeyboardKey = Key.E;

    [Tooltip("Gamepad button starts a push.")]
    [SerializeField] private bool allowGamepad = true;

    [Tooltip("Krush pushes with RB / right shoulder by default.")]
    [SerializeField] private PushGamepadButton pushGamepadButton = PushGamepadButton.RightShoulder;

    [Tooltip("Key that cancels a push and returns Krush to her normal run/idle animation.")]
    [SerializeField] private Key cancelPushKey = Key.A;

    [Header("Push")]
    [Tooltip("Animator int parameter: 0 = none, 1 = pushing.")]
    [SerializeField] private string pushParameter = "Push";

    [Tooltip("How far away (in world units, gap between the two colliders) a pushable object may be and still be pushable.")]
    [SerializeField, Min(0.1f)] private float pushRange = 1f;

    [Tooltip("How far away the pushable object is looked for before the range test is applied.")]
    [SerializeField, Min(1f)] private float pushSearchRadius = 6f;

    [Tooltip("How fast the pushed object travels while the push animation plays. Units per second, forward only.")]
    [SerializeField, Min(0.1f)] private float pushStrength = 2f;

    [Tooltip("Duration to hold/stop at the end before restarting the push cycle (seconds).")]
    [SerializeField, Range(0f, 2f)] private float pushEndHoldDuration = 0.2f;

    [Tooltip("Tag of the pushable objects that can be pushed.")]
    [SerializeField] private string pushTag = "PushObject";

    [Tooltip("The pushable object Krush pushes. Left null, one is looked up by tag.")]
    [SerializeField] private GameObject pushObject;

    [Header("References")]
    [SerializeField] private Animator animator;

    [Tooltip("Movement script, locked while a push is playing.")]
    [SerializeField] private PlayerMovement2D movement;

    private int pushParamHash;
    private bool isPushing;
    private bool pushIsActive;  // Animation event flag: controls if object moves NOW
    private float pushDirection;
    private Transform pushTarget;
    private float pushEndHoldTimer; // timer for end hold
    private bool pushInHold; // true when holding at end frame

    // Set when the push is cancelled with the cancel key (or by steering
    // against it). Keeps the push cancelled until the button is released and
    // pressed again, so holding RB cannot restart the push every frame.
    private bool pushCancelLatch;

    public bool IsPushing => isPushing;

    /// <summary>The locked push direction: +1 right, -1 left.</summary>
    public float PushDirection => pushDirection;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<PlayerMovement2D>();
        pushParamHash = Animator.StringToHash(pushParameter);
    }

    private void Update()
    {
        if (isPushing)
        {
            // Push requires holding the button. Releasing it (or cancelling)
            // returns Krush to normal movement.
            if (!IsPushHeld())
            {
                EndPush();
            }
            else if (movement != null && !movement.IsGrounded)
            {
                EndPush();
            }
            else if (ReadCancelInput())
            {
                // Latch the cancel so holding the button cannot immediately
                // restart the push.
                pushCancelLatch = true;
                EndPush();
            }
            else
            {
                if (pushInHold)
                {
                    pushEndHoldTimer -= Time.deltaTime;
                    if (pushEndHoldTimer <= 0f)
                    {
                        pushInHold = false;
                        pushIsActive = true;
                        // Restart the animation from the beginning for a
                        // seamless loop, exactly like Leon's push.
                        if (animator != null)
                        {
                            animator.Play("Push", 0, 0f);
                        }
                    }
                    // Keep the object stopped during the hold.
                }
                else
                {
                    ApplyPush();
                }
            }
        }
        else
        {
            // Let go of the button to clear a cancel, so the next press pushes.
            if (!IsPushHeld()) pushCancelLatch = false;

            if (!pushCancelLatch && IsPushHeld())
            {
                StartPush();
            }
        }

        if (movement != null)
        {
            movement.IsPushing = isPushing;
            movement.PushDirection = isPushing ? pushDirection : 0f;
            movement.PushSpeed = pushStrength;
        }
    }

    /// <summary>
    /// Cancels the push. Called by the cancel key and by anything else that
    /// needs Krush back in her normal run/idle animation.
    /// </summary>
    public void EndPush()
    {
        if (!isPushing) return;

        isPushing = false;
        pushIsActive = false;  // Stop movement when push ends
        pushTarget = null;
        pushInHold = false;
        pushEndHoldTimer = 0f;

        if (animator != null) animator.SetInteger(pushParamHash, 0);

        if (movement != null)
        {
            movement.IsPushing = false;
            movement.PushDirection = 0f;
        }
    }

    /// <summary>
    /// Starts a push. Returns false when there is nothing to push or when
    /// Krush is busy.
    /// </summary>
    public bool StartPush()
    {
        if (isPushing) return false;
        if (movement != null && movement.IsLocked) return false;
        if (movement != null && !movement.IsGrounded) return false;

        Collider2D target = FindPushTarget();
        if (target == null) return false;

        // The push always goes the way Krush is looking. Locking the
        // direction here is what makes the push forward only: nothing that
        // happens later can flip it.
        float forward = movement != null && movement.FacingRight ? 1f : -1f;

        // ...and the object has to be in front of Krush, otherwise a push
        // would drag it backwards.
        float side = Mathf.Sign(target.bounds.center.x - transform.position.x);
        if (side != 0f && side != Mathf.Sign(forward)) return false;

        pushTarget = target.transform;
        pushDirection = forward;
        isPushing = true;
        pushInHold = false;
        pushEndHoldTimer = 0f;
        pushIsActive = true;

        if (animator != null) animator.SetInteger(pushParamHash, 1);

        return true;
    }

    /// <summary>
    /// Moves the pushed object. The object only ever travels along the
    /// locked push direction, so it can never be shoved backwards.
    /// Only executes when pushIsActive is true (set by animation events).
    /// </summary>
    private void ApplyPush()
    {
        if (!pushIsActive) return;

        if (pushTarget == null)
        {
            EndPush();
            return;
        }

        Vector3 direction = new Vector3(pushDirection, 0f, 0f);
        float distance = pushStrength * Time.deltaTime;

        var pushable = pushTarget.GetComponent<PushableObject2D>();
        if (pushable != null)
        {
            pushable.Push(direction, distance);
        }
        else
        {
            pushTarget.position += direction * distance;
        }
    }

    /// <summary>
    /// Finds the closest pushable object that is within push range.
    /// </summary>
    private Collider2D FindPushTarget()
    {
        if (pushObject != null)
        {
            var explicitCollider = pushObject.GetComponent<Collider2D>();
            if (explicitCollider != null) return explicitCollider;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll((Vector2)transform.position, pushSearchRadius);

        Collider2D best = null;
        float bestDistance = float.MaxValue;

        foreach (var c in hits)
        {
            if (c == null || c.gameObject == null) continue;
            if (!c.gameObject.CompareTag(pushTag)) continue;

            float distance = DistanceTo(c);
            if (distance > pushRange) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }

        return best;
    }

    /// <summary>
    /// Gap between Krush and the target. Zero when they are touching.
    /// </summary>
    private float DistanceTo(Collider2D target)
    {
        var bodyCollider = GetComponent<Collider2D>();

        if (bodyCollider != null)
        {
            return Mathf.Max(0f, bodyCollider.Distance(target).distance);
        }

        return Vector2.Distance(transform.position, target.bounds.center);
    }

    /// <summary>
    /// True while the push button is held on the device Krush is bound to.
    /// </summary>
    private bool IsPushHeld()
    {
        bool held = false;

        if (allowKeyboard
            && movement != null
            && movement.InputMode != PlayerInputMode.GamepadOnly
            && Keyboard.current != null)
        {
            held |= Keyboard.current[pushKeyboardKey].isPressed;
        }

        if (allowGamepad)
        {
            Gamepad gamepad = movement != null ? movement.ActiveGamepad() : Gamepad.current;
            if (gamepad != null)
            {
                held |= ReadButton(gamepad, pushGamepadButton).isPressed;
            }
        }

        return held;
    }

    /// <summary>
    /// The cancel key always ends a push. So does input that points against
    /// the locked push direction, which is the "never backwards" rule.
    /// </summary>
    private bool ReadCancelInput()
    {
        if (allowKeyboard
            && movement != null
            && movement.InputMode != PlayerInputMode.GamepadOnly
            && Keyboard.current != null)
        {
            if (Keyboard.current[cancelPushKey].isPressed) return true;
            if (Keyboard.current.leftArrowKey.isPressed) return true;
        }

        if (movement != null)
        {
            float input = movement.ReadHorizontalInput();
            if (Mathf.Abs(input) > 0.1f && Mathf.Sign(input) != Mathf.Sign(pushDirection)) return true;
        }

        return false;
    }

    private static ButtonControl ReadButton(Gamepad gamepad, PushGamepadButton button)
    {
        switch (button)
        {
            case PushGamepadButton.East: return gamepad.buttonEast;
            case PushGamepadButton.West: return gamepad.buttonWest;
            case PushGamepadButton.North: return gamepad.buttonNorth;
            case PushGamepadButton.LeftShoulder: return gamepad.leftShoulder;
            case PushGamepadButton.RightShoulder: return gamepad.rightShoulder;
            case PushGamepadButton.LeftStick: return gamepad.leftStickButton;
            case PushGamepadButton.RightStick: return gamepad.rightStickButton;
            default: return gamepad.buttonSouth;
        }
    }

    /// <summary>
    /// Called by animation event at frame 0 of push animation.
    /// Enables object movement during active pushing frames.
    /// </summary>
    public void OnPushStart()
    {
        pushIsActive = true;
    }

    /// <summary>
    /// Called by animation event at end of push animation.
    /// Disables object movement and starts hold timer before restarting.
    /// </summary>
    public void OnPushEnd()
    {
        pushIsActive = false;
        if (isPushing && pushEndHoldDuration > 0f)
        {
            pushInHold = true;
            pushEndHoldTimer = pushEndHoldDuration;
        }
        else if (isPushing)
        {
            // No hold, restart immediately.
            pushInHold = false;
            pushIsActive = true;
            if (animator != null)
            {
                animator.Play("Push", 0, 0f);
            }
        }
    }
}
