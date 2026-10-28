using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sword combo plus push action.
///
/// Combo:
///  - A swing ALWAYS plays to the end. The controller releases Attack1 on exit
///    time with no condition, so it can never be cut off or parked on its last
///    frame.
///  - Attack2 loops, so it would run forever unless the combo is released. It
///    is held for at least one full cycle before the combo is allowed to end,
///    otherwise a short combo window would chop the loop in half.
///  - Movement is locked from the Animator state, not from the combo timer, so
///    Leon is free the instant a swing ends instead of waiting out a timer.
///
/// Push:
///  - E (or gamepad South) starts a push, but only when a pushable object is
///    within range and in front of Leon.
///  - The push direction is locked to the way Leon is facing when the push
///    starts, so the object is only ever shoved FORWARD, never backwards.
///  - The push plays the Push animation for as long as E is held. Releasing E
///    ends it and hands Leon straight back to the normal run/idle animation.
///  - Pressing A (or steering against the push) cancels it. The cancel latches
///    until E is released, so tapping A while holding E really does stop the
///    push instead of restarting it on the next frame.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("L&K/Player/Player Attack 2D")]
public class PlayerAttack2D : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Left mouse button attacks.")]
    [SerializeField] private bool allowMouse = true;

    [Tooltip("Keyboard key attacks.")]
    [SerializeField] private bool allowKeyboard = true;

    [SerializeField] private Key keyboardKey = Key.F;

    [Tooltip("Gamepad X / Square attacks.")]
    [SerializeField] private bool allowGamepad = true;

    [Header("Push")]
    [Tooltip("Keyboard key starts a push.")]
    [SerializeField] private bool allowPush = true;

    [SerializeField] private Key pushKeyboardKey = Key.E;

    [Tooltip("Gamepad button starts a push.")]
    [SerializeField] private bool allowPushGamepad = true;

    [Tooltip("Animator int parameter: 0 = none, 1 = pushing.")]
    [SerializeField] private string pushParameter = "Push";

    [Tooltip("How far away (in world units, gap between the two colliders) a pushable object may be and still be pushable.")]
    [SerializeField, Min(0.1f)] private float pushRange = 1f;

    [Tooltip("How far away the pushable object is looked for before the range test is applied.")]
    [SerializeField, Min(1f)] private float pushSearchRadius = 6f;

    [Tooltip("How fast the pushed object travels while the push animation plays. Units per second, forward only.")]
    [SerializeField, Min(0.1f)] private float pushStrength = 2f;

    [Tooltip("Duration to hold/stop at the end before restarting the push cycle (seconds). Adjustable in editor.")]
    [SerializeField, Range(0f, 2f)] private float pushEndHoldDuration = 0.9f;

    [Tooltip("Tag of the pushable objects that can be pushed.")]
    [SerializeField] private string pushTag = "PushObject";

    [Tooltip("The pushable object Leon pushes. Left null, one is looked up by tag.")]
    [SerializeField] private GameObject pushObject;

    [Tooltip("Key that cancels a push and returns Leon to his normal run/idle animation.")]
    [SerializeField] private Key cancelPushKey = Key.A;

    [Header("Combo")]
    [Tooltip("Minimum time a swing is held before the combo may end. Set this to the length of the longest swing clip so it always completes.")]
    [SerializeField, Min(0.1f)] private float minimumSwingTime = 1.25f;

    [Tooltip("How early the Attack parameter is cleared before the Animator's exit-time transition fires. Prevents Idle/Run from re-entering the same swing, which is what leaves the character stuck on the final frame.")]
    [SerializeField, Min(0.01f)] private float releaseLeadTime = 0.08f;

    [Tooltip("Animator int parameter: 0 = neutral, 1 = first swing, 2 = combo swing.")]
    [SerializeField] private string attackParameter = "Attack";

    [Header("Hit Detection")]
    [Tooltip("Range to detect enemies when attacking.")]
    [SerializeField, Min(0.1f)] private float attackHitRange = 1.8f;

    [Tooltip("Vertical tolerance for hitting enemies.")]
    [SerializeField, Min(0.1f)] private float attackHitHeight = 2.0f;

    [Tooltip("Layer mask for enemies.")]
    [SerializeField] private LayerMask enemyLayerMask = -1;

    [Header("References")]
    [SerializeField] private Animator animator;

    [Tooltip("Movement script, locked while a swing or a push is playing.")]
    [SerializeField] private PlayerMovement2D movement;

    private int attackParamHash;
    private int pushParamHash;
    private int comboStep;
    private int queuedStep;
    private int swingSequence;

    private int lastSeenState;
    private float stateEnteredTime = -999f;

    private bool isPushing;
    private bool pushIsActive;  // Animation event flag: controls if object moves NOW
    private float pushDirection;
    private Transform pushTarget;
    private float pushEndHoldTimer; // timer for end hold
    private bool pushInHold; // true when holding at end frame

    // Set when the push is cancelled with A (or by input against the push
    // direction). Keeps the push cancelled until E is released and pressed
    // again, so holding E and tapping A cannot restart the push every frame.
    private bool pushCancelLatch;

    private bool attackKeyHeld;
    public bool IsSwinging => lastSeenState == 1 || lastSeenState == 2;
    public bool IsPushing => isPushing;
    public int ComboStep => comboStep;

    /// <summary>
    /// Increments once for every swing that starts, including the second half
    /// of a combo. Enemies watch this number so one swing can only ever land
    /// one hit, no matter how many frames it is on screen.
    /// </summary>
    public int SwingSequence => swingSequence;

    /// <summary>The locked push direction: +1 right, -1 left.</summary>
    public float PushDirection => pushDirection;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<PlayerMovement2D>();
        attackParamHash = Animator.StringToHash(attackParameter);
        pushParamHash = Animator.StringToHash(pushParameter);
    }

    private void Update()
    {
        TrackAnimatorState();

        // Attacks are ignored while a push is running: the Push animation owns
        // Leon's body until the push is cancelled.
        bool attackPressed = ReadPressEdge(IsAttackHeld(), ref attackKeyHeld);
        if (!isPushing && attackPressed) AdvanceCombo();

        // Try to hit enemies during active swing frames
        if (IsSwinging && !isPushing) TryHitEnemies();

        ReleaseFinishedSwing();

        if (isPushing)
        {
            // Push requires holding E. Releasing E (or cancelling) returns to movement.
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
                // Latch the cancel so holding E cannot immediately restart it.
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
                        // Restart animation from beginning for seamless loop feel
                        if (animator != null)
                        {
                            animator.Play("Push", 0, 0f);
                        }
                    }
                    // Keep object stopped during hold
                }
                else
                {
                    ApplyPush();
                }
            }
        }
        else
        {
            // Let go of E to clear a cancel, so the next press pushes again.
            if (!IsPushHeld()) pushCancelLatch = false;

            if (allowPush && !pushCancelLatch && !IsSwinging && IsPushHeld())
            {
                StartPush();
            }
        }

        if (movement != null)
        {
            movement.IsAttacking = IsSwinging;
            movement.IsPushing = isPushing;
            movement.PushDirection = isPushing ? pushDirection : 0f;
            movement.PushSpeed = pushStrength;
        }
    }

    /// <summary>
    /// Cancels the push. Called by the cancel key and by anything else that
    /// needs Leon back in his normal run/idle animation.
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
    /// Leon is busy.
    /// </summary>
    public bool StartPush()
    {
        if (isPushing) return false;
        if (IsSwinging) return false;
        if (movement != null && movement.IsLocked) return false;
        if (movement != null && !movement.IsGrounded) return false;

        Collider2D target = FindPushTarget();
        if (target == null) return false;

        // The push always goes the way Leon is looking. Locking the
        // direction here is what makes the push forward only: nothing that
        // happens later can flip it.
        float forward = movement != null && movement.FacingRight ? 1f : -1f;

        // ...and the object has to be in front of Leon, otherwise a push
        // would drag it backwards.
        float side = Mathf.Sign(target.bounds.center.x - transform.position.x);
        if (side != 0f && side != Mathf.Sign(forward)) return false;

        pushTarget = target.transform;
        pushDirection = forward;
        isPushing = true;
        pushInHold = false;
        pushEndHoldTimer = 0f;
        pushIsActive = true;

        // A push and a swing can never be active at the same time, so make
        // sure the attack parameter is neutral before the Push animation
        // takes over.
        ResetCombo();

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
        // Only move if animation event has enabled push movement
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
    /// Gap between Leon and the target. Zero when they are touching.
    /// </summary>
    private float DistanceTo(Collider2D target)
    {
        var leonCollider = GetComponent<Collider2D>();

        if (leonCollider != null)
        {
            return Mathf.Max(0f, leonCollider.Distance(target).distance);
        }

        return Vector2.Distance(transform.position, target.bounds.center);
    }

    /// <summary>
    /// Push is now strictly hold-based. This method is kept for compatibility
    /// but is no longer used; push starts only while the key is held.
    /// </summary>
    private bool ReadPushRequest()
    {
        return false;
    }

    private bool IsPushHeld()
    {
        bool held = false;

        if (allowPush && Keyboard.current != null) held |= Keyboard.current[pushKeyboardKey].isPressed;
        if (allowPushGamepad && Gamepad.current != null) held |= Gamepad.current.buttonSouth.isPressed;

        return held;
    }

    private bool IsAttackHeld()
    {
        bool held = false;

        if (allowMouse && Mouse.current != null) held |= Mouse.current.leftButton.isPressed;
        if (allowKeyboard && Keyboard.current != null) held |= Keyboard.current[keyboardKey].isPressed;
        if (allowGamepad && Gamepad.current != null) held |= Gamepad.current.buttonWest.isPressed;

        return held;
    }

    /// <summary>
    /// True on the frame a control goes down. This is used instead of
    /// ButtonControl.wasPressedThisFrame because that flag is cleared by the
    /// input update that runs before the script update, which swallows any
    /// key press that arrives between two frames.
    /// </summary>
    private static bool ReadPressEdge(bool held, ref bool previousHeld)
    {
        bool edge = held && !previousHeld;
        previousHeld = held;
        return edge;
    }

    /// <summary>
    /// The cancel key always ends a push. So does input that points against
    /// the locked push direction, which is the "never backwards" rule.
    /// </summary>
    private bool ReadCancelInput()
    {
        if (Keyboard.current != null)
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

    private int lastHitSwingSequence = 0;

    private void TrackAnimatorState()
    {
        int state = CurrentAttackState();
        if (state != lastSeenState)
        {
            lastSeenState = state;
            stateEnteredTime = Time.time;
        }
    }

    private void TryHitEnemies()
    {
        if (lastSeenState == 0) return;

        // Only hit once per swing sequence - each swing can only hit once
        if (lastHitSwingSequence == swingSequence) return;

        float forward = movement != null && movement.FacingRight ? 1f : -1f;
        Vector2 origin = (Vector2)transform.position + new Vector2(forward * 0.5f, 0f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackHitRange, enemyLayerMask);

        bool hitAny = false;
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            float dx = hit.transform.position.x - transform.position.x;

            // Check if target is in front of the player
            if (Mathf.Sign(dx) != Mathf.Sign(forward)) continue;

            // Check horizontal reach
            if (Mathf.Abs(dx) > attackHitRange) continue;

            // Check vertical reach
            if (Mathf.Abs(hit.transform.position.y - transform.position.y) > attackHitHeight) continue;

            SkeletonAI skeleton = hit.GetComponent<SkeletonAI>();
            if (skeleton == null) skeleton = hit.GetComponentInParent<SkeletonAI>();
            if (skeleton != null && !skeleton.IsDead)
            {
                skeleton.TakeSwordHit(transform.position);
                hitAny = true;
            }
        }

        // Mark this swing sequence as hit so it only hits once
        if (hitAny)
        {
            lastHitSwingSequence = swingSequence;
        }
    }

    private void ReleaseFinishedSwing()
    {
        if (!IsSwinging) return;
        if (lastSeenState != 1 && lastSeenState != 2) return;

        if (queuedStep != 0)
        {
            float playedNow = Time.time - stateEnteredTime;
            if (playedNow >= CurrentSwingLength() - releaseLeadTime)
            {
                comboStep = queuedStep;
                queuedStep = 0;
                stateEnteredTime = Time.time;
                swingSequence++;
                animator.SetInteger(attackParamHash, comboStep);
            }
            return;
        }

        float played = Time.time - stateEnteredTime;
        float needed = CurrentSwingLength();

        if (played >= needed - releaseLeadTime) ResetCombo();
    }

    private float CurrentSwingLength()
    {
        if (animator == null) return minimumSwingTime;
        float len = animator.GetCurrentAnimatorStateInfo(0).length;
        return len > 0f ? len : minimumSwingTime;
    }

    private int CurrentAttackState()
    {
        if (animator == null) return 0;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName("Attack1")) return 1;
        if (info.IsName("Attack2")) return 2;
        return 0;
    }

    public void AdvanceCombo()
    {
        if (comboStep != 0)
        {
            queuedStep = comboStep == 1 ? 2 : 1;
            return;
        }

        comboStep = 1;
        queuedStep = 0;
        swingSequence++;
        if (animator != null) animator.SetInteger(attackParamHash, comboStep);
    }

    public void ResetCombo()
    {
        comboStep = 0;
        queuedStep = 0;
        lastSeenState = 0;
        stateEnteredTime = -999f;
        if (animator != null) animator.SetInteger(attackParamHash, 0);
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
            // No hold, restart immediately
            pushInHold = false;
            pushIsActive = true;
            if (animator != null)
            {
                animator.Play("Push", 0, 0f);
            }
        }
    }
}
