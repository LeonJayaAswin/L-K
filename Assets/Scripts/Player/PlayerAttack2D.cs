using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Two-hit sword combo.
///
/// Timing rules that matter:
///  - A swing ALWAYS plays to the end. The controller releases Attack1 on exit
///    time with no condition, so it can never be cut off or parked on its last
///    frame.
///  - Attack2 loops, so it would run forever unless the combo is released. It
///    is held for at least one full cycle before the combo is allowed to end,
///    otherwise a short combo window would chop the loop in half.
///  - Movement is locked from the Animator state, not from the combo timer, so
///    Leon is free the instant a swing finishes instead of waiting out a timer.
/// </summary>
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

    [Header("Combo")]
    [Tooltip("Seconds after the last press during which another press chains into the next swing. After this the combo ends.")]
    [SerializeField, Min(0.1f)] private float comboResetTime = 0.6f;

    [Tooltip("Minimum time a swing is held before the combo may end. Set this to the length of the longest swing clip so it always completes.")]
    [SerializeField, Min(0.1f)] private float minimumSwingTime = 1.25f;

    [Tooltip("How early the Attack parameter is cleared before the Animator's exit-time transition fires. Prevents Idle/Run from re-entering the same swing, which is what leaves the character stuck on the final frame.")]
    [SerializeField, Min(0.01f)] private float releaseLeadTime = 0.08f;

    [Tooltip("Animator int parameter: 0 = neutral, 1 = first swing, 2 = combo swing.")]
    [SerializeField] private string attackParameter = "Attack";

    [Header("References")]
    [SerializeField] private Animator animator;

    [Tooltip("Movement script, locked while a swing is playing.")]
    [SerializeField] private PlayerMovement2D movement;

    private int attackParamHash;
    private int comboStep;
    private int queuedStep;
    private float lastAttackTime = -999f;

    private int lastSeenState;
    private float stateEnteredTime = -999f;

    /// <summary>True while a swing is actually on screen.</summary>
    public bool IsSwinging => lastSeenState != 0;

    /// <summary>Which swing is queued: 0 none, 1 first, 2 combo.</summary>
    public int ComboStep => comboStep;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<PlayerMovement2D>();
        attackParamHash = Animator.StringToHash(attackParameter);
    }

    private void Update()
    {
        TrackAnimatorState();

        if (ReadAttackPress()) AdvanceCombo();

        ReleaseFinishedSwing();

        // Lock movement to the animation, not to the combo timer, so Leon is
        // controllable again the instant a swing ends rather than waiting for
        // the combo window to expire.
        if (movement != null) movement.IsAttacking = IsSwinging;
    }

    /// <summary>
    /// Clears the Attack parameter once the swing on screen has played out, so
    /// the controller can return to Idle/Run.
    ///
    /// This has to happen slightly BEFORE the state machine's own exit-time
    /// transition fires. The controller releases on exit time with no condition
    /// (so a swing can never be chopped short or left parked on its last
    /// frame), which also means Idle/Run would immediately re-enter the same
    /// swing while Attack is still set. Clearing first breaks that loop.
    /// </summary>
    private void ReleaseFinishedSwing()
    {
        if (!IsSwinging) return;

        // A press during this swing queued the follow-up, so swap to it now
        // rather than returning to movement. Swapping this early (before the
        // state machine's exit transition) is what keeps the chain seamless.
        if (queuedStep != 0)
        {
            float playedNow = Time.time - stateEnteredTime;
            if (playedNow >= CurrentSwingLength() - releaseLeadTime)
            {
                comboStep = queuedStep;
                queuedStep = 0;
                lastAttackTime = Time.time;

                // Restart the clock. The Animator has not entered the next swing
                // yet, so without this the elapsed time still measures the swing
                // we just finished and the combo would be released on the very
                // next frame instead of playing the queued swing.
                stateEnteredTime = Time.time;

                animator.SetInteger(attackParamHash, comboStep);
            }
            return;
        }

        // Nothing queued. Let the swing run its full length, then clear.
        float played = Time.time - stateEnteredTime;
        float needed = CurrentSwingLength();

        // Hold the swing until it has had its full length, minus a small lead so
        // we are already clear by the time the exit transition fires.
        if (played >= needed - releaseLeadTime)
        {
            ResetCombo();
        }
    }

    private float CurrentSwingLength()
    {
        if (animator == null) return minimumSwingTime;
        float len = animator.GetCurrentAnimatorStateInfo(0).length;
        return len > 0f ? len : minimumSwingTime;
    }

    private void TrackAnimatorState()
    {
        int state = CurrentAttackState();
        if (state != lastSeenState)
        {
            lastSeenState = state;
            stateEnteredTime = Time.time;
        }
    }

    private int CurrentAttackState()
    {
        if (animator == null) return 0;
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName("Attack1")) return 1;
        if (info.IsName("Attack2")) return 2;
        return 0;
    }

    /// <summary>
    /// Steps the combo forward: 0 -> 1 -> 2 -> 1 -> 2 ... so a chain of presses
    /// keeps alternating the two swings.
    /// </summary>
    public void AdvanceCombo()
    {
        lastAttackTime = Time.time;

        // Decide on comboStep, NOT on the Animator state: the state machine
        // needs a frame or two to actually enter the swing, and a press landing
        // in that gap must queue rather than restart the combo at step 1.
        //
        // A press during a swing QUEUES the next one instead of switching
        // immediately. Without this, mashing the button flip-flops between the
        // two swings and each one barely gets started.
        if (comboStep != 0)
        {
            queuedStep = comboStep == 1 ? 2 : 1;
            return;
        }

        comboStep = 1;
        queuedStep = 0;
        animator.SetInteger(attackParamHash, comboStep);
    }

    /// <summary>Drops the combo and hands control back to idle or run.</summary>
    public void ResetCombo()
    {
        comboStep = 0;
        queuedStep = 0;
        lastSeenState = 0;
        stateEnteredTime = -999f;
        animator.SetInteger(attackParamHash, 0);
    }

    private bool ReadAttackPress()
    {
        bool pressed = false;

        if (allowMouse && Mouse.current != null)
            pressed |= Mouse.current.leftButton.wasPressedThisFrame;

        if (allowKeyboard && Keyboard.current != null)
            pressed |= Keyboard.current[keyboardKey].wasPressedThisFrame;

        if (allowGamepad && Gamepad.current != null)
            pressed |= Gamepad.current.buttonWest.wasPressedThisFrame;

        return pressed;
    }
}