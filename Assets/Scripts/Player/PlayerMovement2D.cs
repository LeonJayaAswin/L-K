using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("L&K/Player/Player Movement 2D")]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top horizontal speed in units/second.")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.6f;

    [Tooltip("How fast we reach moveSpeed while a direction is held. Kept low on purpose so the speed ramp is long enough for the run animation to visibly track it.")]
    [SerializeField, Min(0f)] private float acceleration = 25f;

    [Tooltip("How fast we come to a stop after input is released.")]
    [SerializeField, Min(0f)] private float deceleration = 30f;

    [Tooltip("How fast Leon changes direction. Deliberately high so he pivots instantly like a platformer character instead of sliding backwards.")]
    [SerializeField, Min(0f)] private float turnSpeed = 90f;

    [Tooltip("How hard a swing stops Leon. High on purpose: the swing should read as a committed, locked move.")]
    [SerializeField, Min(0f)] private float attackLockDeceleration = 250f;

    [Tooltip("Input below this magnitude counts as no input (kills controller drift).")]
    [SerializeField, Range(0f, 0.5f)] private float inputDeadzone = 0.15f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Float parameter on the Animator, expected to be 0 (idle) to 1 (full run).")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Normalized speed above which the Run animation is used.")]
    [SerializeField, Range(0f, 1f)] private float runThreshold = 0.1f;

    [Tooltip("The ground speed the run clip was authored for. The run cycle is played back faster or slower than 1.0 in proportion to how much faster or slower Leon is actually moving than this, which keeps the stride length (and therefore the foot contact) constant at any moveSpeed.")]
    [SerializeField, Min(0.1f)] private float animationReferenceSpeed = 2.6f;

    [Tooltip("0 disables speed scaling and plays the run clip at a fixed rate; 1 scales fully.")]
    [SerializeField, Range(0f, 1f)] private float syncRunToSpeed = 1f;

    [Tooltip("Floor for run playback speed, so a barely-moving character does not crawl through the run cycle.")]
    [SerializeField, Range(0.1f, 1f)] private float minRunPlaybackSpeed = 0.5f;

    [Tooltip("Ceiling for run playback speed, so a collision overshoot cannot make the legs blur into noise.")]
    [SerializeField, Range(1f, 4f)] private float maxRunPlaybackSpeed = 3f;

    [Header("Facing")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Which way Leon looks at startup. The art faces right, so left uses flipX.")]
    [SerializeField] private bool startsFacingRight = true;

    private Rigidbody2D body;
    private int speedParamHash;
    private float moveInput;
    private bool facingRight;

    /// <summary>
    /// Set by PlayerAttack2D while a swing is playing. While true this script
    /// keeps driving movement but stops flipping the sprite and stops scaling
    /// Animator playback, so an attack and a run never fight over them.
    /// </summary>
    public bool IsAttacking { get; set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        speedParamHash = Animator.StringToHash(speedParameter);
        facingRight = startsFacingRight;
        spriteRenderer.flipX = !facingRight;
    }

    private void Update()
    {
        moveInput = ReadHorizontalInput();

        float velocityX = body.linearVelocity.x;

        // While a swing is playing we still report Speed (so Leon can walk out
        // of the attack into Run), but we must not flip him mid-strike and we
        // must not rescale the swing with ground speed.
        if (!IsAttacking)
        {
            if (Mathf.Abs(velocityX) > 0.01f)
            {
                facingRight = velocityX > 0f;
            }

            spriteRenderer.flipX = !facingRight;
        }

        if (animator != null)
        {
            float normalizedSpeed = moveSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(velocityX) / moveSpeed)
                : 0f;

            animator.SetFloat(speedParamHash, normalizedSpeed);

            if (IsAttacking)
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
    }

    private void FixedUpdate()
    {
        Vector2 velocity = body.linearVelocity;

        // A swing is committed: Leon plants his feet and does not steer. The
        // high rate means he stops within a frame or two of the swing starting,
        // so it reads as a hard lock rather than a slow drift.
        if (IsAttacking)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, attackLockDeceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
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
    }

    /// <summary>
    /// Reads left/right from the keyboard first, then lets the gamepad stick
    /// override it, so the two never fight when both are being touched.
    /// </summary>
    private float ReadHorizontalInput()
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