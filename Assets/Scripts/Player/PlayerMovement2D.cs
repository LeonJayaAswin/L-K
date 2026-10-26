using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Horizontal platformer movement for Leon.
///
/// Grounding:
///  - Leon only ever moves while he is standing on a collider tagged with
///    "Ground". The probe is a small box that straddles the bottom of Leon's
///    own collider, so it keeps working no matter how the sprite is scaled or
///    which frame of the animation is on screen.
///
/// Movement:
///  - A/D (or the left stick) accelerate Leon up to moveSpeed.
///  - While an attack is playing Leon is locked in place.
///  - While a hit reaction is playing Leon is locked in place.
///  - While a push is playing Leon walks the object forward, never back.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("L&K/Player/Player Movement 2D")]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top horizontal speed in units/second.")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;

    [Tooltip("How fast we reach moveSpeed while a direction is held.")]
    [SerializeField, Min(0f)] private float acceleration = 55f;

    [Tooltip("How fast we come to a stop after input is released.")]
    [SerializeField, Min(0f)] private float deceleration = 40f;

    [Tooltip("How fast Leon changes direction. Deliberately high so he pivots instantly like a platformer character instead of sliding backwards.")]
    [SerializeField, Min(0f)] private float turnSpeed = 90f;

    [Tooltip("How hard a swing stops Leon. High on purpose: the swing should read as a committed, locked move.")]
    [SerializeField, Min(0f)] private float attackLockDeceleration = 250f;

    [Tooltip("Input below this magnitude counts as no input (kills controller drift).")]
    [SerializeField, Range(0f, 0.5f)] private float inputDeadzone = 0.15f;

    [Header("Grounding")]
    [Tooltip("Tag of the objects Leon may walk on. Movement and the run animation only happen while standing on one.")]
    [SerializeField] private string groundTag = "Ground";

    [Tooltip("How far below Leon's feet the ground probe reaches.")]
    [SerializeField, Min(0.02f)] private float groundProbeDepth = 0.25f;

    [Tooltip("How far above Leon's feet the ground probe reaches. This is the tolerance that keeps Leon grounded while he rests on the floor.")]
    [SerializeField, Min(0f)] private float groundProbeLift = 0.06f;

    [Tooltip("Width of the ground probe. Zero derives it from Leon's own collider.")]
    [SerializeField, Min(0f)] private float groundProbeWidth = 0f;

    [Tooltip("Only used when Leon has no 2D collider: offset below the transform that is treated as his feet.")]
    [SerializeField] private float groundCheckOffset = -0.89f;

    [Tooltip("Only used when Leon has no 2D collider: radius of the fallback ground probe.")]
    [SerializeField, Min(0.05f)] private float groundCheckRadius = 0.15f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Float parameter on the Animator, expected to be 0 (idle) to 1 (full run).")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Normalized speed above which the Run animation is used.")]
    [SerializeField, Range(0f, 1f)] private float runThreshold = 0.1f;

    [Tooltip("The ground speed the run clip was authored for. The run cycle is played back faster or slower than 1.0 in proportion to how much faster or slower Leon is actually moving than this, which keeps the stride length (and therefore the foot contact) constant at any moveSpeed.")]
    [SerializeField, Min(0.1f)] private float animationReferenceSpeed = 3.878f;

    [Tooltip("0 disables speed scaling and plays the run clip at a fixed rate; 1 scales fully.")]
    [SerializeField, Range(0f, 1f)] private float syncRunToSpeed = 1f;

    [Tooltip("Floor for run playback speed, so a barely-moving character does not crawl through the run cycle.")]
    [SerializeField, Range(0.1f, 1f)] private float minRunPlaybackSpeed = 0.5f;

    [Tooltip("Ceiling for run playback speed, so a collision overshoot cannot make the legs blur into noise.")]
    [SerializeField, Range(1f, 4f)] private float maxRunPlaybackSpeed = 3f;

    [Header("Jump")]
    [Tooltip("Jump force applied when jumping. Higher = higher jump.")]
    [SerializeField, Min(0f)] private float jumpForce = 12f;

    [Tooltip("Gravity multiplier applied while falling. Higher = faster fall.")]
    [SerializeField, Min(0.1f)] private float fallGravityScale = 1.5f;

    [Tooltip("Bool parameter on the Animator for jump state.")]
    [SerializeField] private string jumpParameter = "IsJumping";

    [Tooltip("Coyote time: frames after leaving ground that jump is still allowed. Feels more forgiving.")]
    [SerializeField, Range(0f, 0.2f)] private float coyoteTime = 0.1f;

    [Header("Facing")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Which way Leon looks at startup. The art faces right, so left uses flipX.")]
    [SerializeField] private bool startsFacingRight = true;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private int speedParamHash;
    private int jumpParamHash;
    private float moveInput;
    private bool facingRight;

    private float coyoteCounter;
    private bool jumpInputQueued;
    private float jumpInputCooldown;

    /// <summary>
    /// Set by PlayerAttack2D while a swing is playing. While true this script
    /// keeps driving movement but stops flipping the sprite and stops scaling
    /// Animator playback, so an attack and a run never fight over them.
    /// </summary>
    public bool IsAttacking { get; set; }

    /// <summary>
    /// Set by PlayerAttack2D while Leon is pushing an object. A push is a
    /// committed action: Leon holds the object and only ever pushes forward.
    /// </summary>
    public bool IsPushing { get; set; }

    /// <summary>
    /// Signed direction (+1 right, -1 left) Leon is committed to while pushing.
    /// Zero means "not pushing". The direction is locked when the push starts
    /// so a push can never turn into a backwards shove.
    /// </summary>
    public float PushDirection { get; set; }

    /// <summary>
    /// How fast Leon travels while he is pushing, in units per second.
    /// </summary>
    public float PushSpeed { get; set; } = 2f;

    /// <summary>
    /// Set by PlayerHealthHurt for as long as the Hurt animation is on screen.
    /// A hit staggers Leon, so he cannot steer or flip while it plays.
    /// </summary>
    public bool IsHurt { get; set; }

    private bool movementLocked => IsAttacking || IsPushing || IsHurt;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        // Leon must never fall asleep. A sleeping Rigidbody2D ignores a
        // linearVelocity assignment, so the character would stand still
        // forever after the first moment of standing still.
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        speedParamHash = Animator.StringToHash(speedParameter);
        jumpParamHash = Animator.StringToHash(jumpParameter);
        facingRight = startsFacingRight;
        spriteRenderer.flipX = !facingRight;
    }

    private void Update()
    {
        moveInput = ReadHorizontalInput();

        // Refresh the ground state here as well as in FixedUpdate: the
        // animator needs an up to date value even on frames where the
        // physics step did not run.
        IsGrounded = CheckGrounded();

        // Coyote time: allows jump shortly after leaving ground for forgiving feel
        if (IsGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // Read jump input (Space or Gamepad A)
        HandleJumpInput();

        float velocityX = body.linearVelocity.x;

        // While a swing or a push is playing we still report Speed (so Leon can
        // walk out of the action into Run), but we must not flip him mid-action
        // and we must not rescale the action with ground speed.
        if (!movementLocked)
        {
            // Input wins over velocity: the sprite turns the instant the
            // player presses a direction, which is what makes the push
            // direction predictable.
            if (!Mathf.Approximately(moveInput, 0f))
            {
                facingRight = moveInput > 0f;
            }
            else if (Mathf.Abs(velocityX) > 0.01f)
            {
                facingRight = velocityX > 0f;
            }

            spriteRenderer.flipX = !facingRight;
        }

        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = moveSpeed > 0f
            ? Mathf.Clamp01(Mathf.Abs(velocityX) / moveSpeed)
            : 0f;

        // Only walk when standing on something that can be walked on.
        // Do NOT show run animation mid-air.
        animator.SetFloat(speedParamHash, IsGrounded ? normalizedSpeed : 0f);

        // Update jump animation state
        animator.SetBool(jumpParamHash, !IsGrounded);

        if (movementLocked)
        {
            animator.speed = 1f;
            return;
        }

        // Play the run cycle at the rate Leon is actually travelling.
        // The reference speed is what the clip was authored for, so the
        // stride length stays constant no matter how fast he moves -
        // which is what stops the feet sliding at high speed.
        // Values above 1 mean "legs move faster", which is exactly what a
        // fast platformer run should look like.
        float targetPlayback = Mathf.Abs(velocityX) / animationReferenceSpeed;
        float runPlayback = Mathf.Lerp(1f, targetPlayback, syncRunToSpeed);
        animator.speed = normalizedSpeed > runThreshold
            ? Mathf.Clamp(runPlayback, minRunPlaybackSpeed, maxRunPlaybackSpeed)
            : 1f;
    }

    private void FixedUpdate()
    {
        // The physics step is authoritative for grounding: movement is
        // decided here, so the check has to be re-run here.
        IsGrounded = CheckGrounded();

        // Execute queued jump if valid
        if (jumpInputQueued)
        {
            ExecuteJump();
        }

        Vector2 velocity = body.linearVelocity;

        // Apply enhanced gravity while falling for snappier platformer feel
        if (!IsGrounded && velocity.y < 0f)
        {
            velocity.y += Physics2D.gravity.y * (fallGravityScale - 1f) * Time.fixedDeltaTime;
        }

        // A hit is committed too: the Hurt animation owns Leon's body, so he
        // plants his feet instantly and cannot drift (or keep air control)
        // until the reaction ends. This takes priority over a push or a swing.
        if (IsHurt)
        {
            velocity.x = 0f;
            body.linearVelocity = velocity;
            return;
        }

        // A push is committed: Leon walks the object forward and never back.
        if (IsPushing && !Mathf.Approximately(PushDirection, 0f))
        {
            velocity.x = PushDirection * PushSpeed;
            body.linearVelocity = velocity;
            body.WakeUp();
            return;
        }

        // A swing is committed: Leon plants his feet and does not steer. The
        // high rate means he stops within a frame or two of the swing
        // starting, so it reads as a hard lock rather than a drift.
        if (IsAttacking)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, attackLockDeceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
            return;
        }

        // Allow movement in air for better feel
        if (!IsGrounded)
        {
            float airTargetSpeed = moveInput * moveSpeed;
            bool airReversing = !Mathf.Approximately(moveInput, 0f)
                && Mathf.Abs(velocity.x) > 0.01f
                && Mathf.Sign(airTargetSpeed) != Mathf.Sign(velocity.x);
            float airRate = airReversing ? turnSpeed : (Mathf.Approximately(moveInput, 0f) ? (deceleration * 0.5f) : (acceleration * 0.7f));
            velocity.x = Mathf.MoveTowards(velocity.x, airTargetSpeed, airRate * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
            if (!Mathf.Approximately(moveInput, 0f)) body.WakeUp();
            return;
        }

        float targetSpeed = moveInput * moveSpeed;

        // Turning around uses a much higher rate than accelerating from rest.
        // Without this, reversing direction leaves Leon briefly sliding
        // backwards at full speed, which reads as a moonwalk.
        bool reversing = !Mathf.Approximately(moveInput, 0f)
            && Mathf.Abs(velocity.x) > 0.01f
            && Mathf.Sign(targetSpeed) != Mathf.Sign(velocity.x);

        float rate;
        if (reversing)
        {
            rate = turnSpeed;
        }
        else
        {
            rate = Mathf.Approximately(moveInput, 0f) ? deceleration : acceleration;
        }

        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * Time.fixedDeltaTime);

        body.linearVelocity = velocity;

        // A sleeping body would ignore the velocity above.
        if (!Mathf.Approximately(moveInput, 0f)) body.WakeUp();
    }

    /// <summary>
    /// True while Leon is standing on something tagged as walkable ground.
    /// </summary>
    public bool IsGrounded { get; private set; }

    /// <summary>True while a swing or a push is holding Leon in place.</summary>
    public bool IsLocked => movementLocked;

    /// <summary>True while Leon faces right. Drives the push direction (forward = right).</summary>
    public bool FacingRight => facingRight;

    /// <summary>World space position of Leon's feet (the bottom of his collider).</summary>
    public Vector2 FeetPosition
    {
        get
        {
            if (bodyCollider != null && bodyCollider.enabled)
            {
                return bodyCollider.bounds.min;
            }

            return (Vector2)transform.position + Vector2.up * groundCheckOffset;
        }
    }

    /// <summary>
    /// Checks whether Leon is standing on something tagged as ground.
    /// The probe is a box that straddles his feet, so it still finds the
    /// floor while he is resting on it or while he is settling onto it.
    /// </summary>
    private bool CheckGrounded()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
        {
            return CheckGroundedFallback();
        }

        Vector2 feet = bodyCollider.bounds.min;

        float width = groundProbeWidth > 0.01f
            ? groundProbeWidth
            : Mathf.Max(0.1f, bodyCollider.bounds.size.x * 0.75f);

        float depth = Mathf.Max(0.02f, groundProbeDepth);
        float lift = Mathf.Clamp(groundProbeLift, 0f, depth);

        Vector2 size = new Vector2(width, depth);
        Vector2 center = new Vector2(feet.x, feet.y + lift - depth * 0.5f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

        foreach (var c in hits)
        {
            if (c == null) continue;

            // Never let Leon's own collider count as ground.
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;

            if (c.gameObject.CompareTag(groundTag)) return true;
        }

        return false;
    }

    /// <summary>
    /// Used only when Leon has no 2D collider at all.
    /// </summary>
    private bool CheckGroundedFallback()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * groundCheckOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, groundCheckRadius);

        foreach (var c in hits)
        {
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.gameObject.CompareTag(groundTag)) return true;
        }

        return false;
    }

    /// <summary>
    /// Reads jump input (Space or Gamepad A) and queues the jump if valid.
    /// </summary>
    private void HandleJumpInput()
    {
        bool jumpPressed = false;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpPressed = true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            jumpPressed = true;
        }

        if (jumpPressed)
        {
            jumpInputCooldown = 0.1f; // Small cooldown to prevent double-taps
            jumpInputQueued = true;
        }

        jumpInputCooldown -= Time.deltaTime;
    }

    /// <summary>
    /// Attempts to execute a jump if conditions are met:
    /// - Not attacking
    /// - Not pushing
    /// - Have coyote time or are grounded
    /// </summary>
    private void ExecuteJump()
    {
        // Cannot jump while attacking or pushing
        if (IsAttacking || IsPushing) return;

        // Must have coyote time remaining or be grounded
        if (coyoteCounter <= 0f) return;

        Vector2 velocity = body.linearVelocity;
        velocity.y = jumpForce;
        body.linearVelocity = velocity;
        body.WakeUp();

        // Consume jump input and coyote time
        jumpInputQueued = false;
        coyoteCounter = 0f;
    }

    /// <summary>
    /// Reads left/right from the keyboard first, then lets the gamepad stick
    /// override it, so the two never fight when both are being touched.
    /// </summary>
    public float ReadHorizontalInput()
    {
        float value = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                value -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                value += 1f;
            }
        }

        if (Gamepad.current != null)
        {
            float stick = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stick) > inputDeadzone)
            {
                value = stick;
            }
        }

        return Mathf.Clamp(value, -1f, 1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Run should not be reachable while standing still.
        runThreshold = Mathf.Clamp(runThreshold, 0.01f, 1f);
    }
#endif
}
