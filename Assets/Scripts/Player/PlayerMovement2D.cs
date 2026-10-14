using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("L&K/Player/Player Movement 2D")]
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top horizontal speed in units/second.")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Tooltip("How fast we reach moveSpeed while a direction is held.")]
    [SerializeField, Min(0f)] private float acceleration = 60f;

    [Tooltip("How fast we come to a stop after input is released.")]
    [SerializeField, Min(0f)] private float deceleration = 80f;

    [Tooltip("Input below this magnitude counts as no input (kills controller drift).")]
    [SerializeField, Range(0f, 0.5f)] private float inputDeadzone = 0.15f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Float parameter on the Animator, expected to be 0 (idle) to 1 (full run).")]
    [SerializeField] private string speedParameter = "Speed";

    [Tooltip("Normalized speed above which the Run animation is used.")]
    [SerializeField, Range(0f, 1f)] private float runThreshold = 0.1f;

    [Header("Facing")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Which way Leon looks at startup. The art faces right, so left uses flipX.")]
    [SerializeField] private bool startsFacingRight = true;

    private Rigidbody2D body;
    private int speedParamHash;
    private float moveInput;
    private bool facingRight;

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

        if (Mathf.Abs(velocityX) > 0.01f)
        {
            facingRight = velocityX > 0f;
        }

        spriteRenderer.flipX = !facingRight;

        if (animator != null)
        {
            float normalizedSpeed = moveSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(velocityX) / moveSpeed)
                : 0f;

            animator.SetFloat(speedParamHash, normalizedSpeed);
        }
    }

    private void FixedUpdate()
    {
        Vector2 velocity = body.linearVelocity;

        float targetSpeed = moveInput * moveSpeed;
        float rate = Mathf.Approximately(moveInput, 0f) ? deceleration : acceleration;

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