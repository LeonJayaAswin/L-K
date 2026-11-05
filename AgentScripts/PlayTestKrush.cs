using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Drives Krush with a synthetic gamepad in Play Mode and reports what the
/// movement, jump, animation and push systems actually did.
/// </summary>
public static class PlayTestKrush
{
    private static Gamepad pad;

    public static async Task<string> Run()
    {
        var log = new StringBuilder();

        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var krush = GameObject.Find("Krush");
        var leon = GameObject.Find("Leon");
        var pushable = Object.FindAnyObjectByType<PushableObject2D>(FindObjectsInactive.Include);
        var pushObject = pushable != null ? pushable.gameObject : null;
        if (krush == null || leon == null) return "missing Krush or Leon";

        var animator = krush.GetComponent<Animator>();
        var movement = krush.GetComponent<PlayerMovement2D>();
        var push = krush.GetComponent<PlayerPush2D>();
        var body = krush.GetComponent<Rigidbody2D>();

        pad = Gamepad.current;
        bool addedPad = false;
        if (pad == null)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            addedPad = true;
        }

        // Let everything settle on the ground.
        await Task.Delay(700);
        log.AppendLine(Initial("start"));

        Vector3 leonStart = leon.transform.position;
        Vector3 krushStart = krush.transform.position;

        // ---------------------------------------------------------- run right
        Set(new Vector2(1f, 0f), false, false);
        await Task.Delay(700);
        log.AppendLine(Initial("run-right"));
        log.AppendLine("  movedRight=" + (krush.transform.position.x > krushStart.x + 0.3f) +
                       " speed(vx)=" + body.linearVelocity.x.ToString("0.00") +
                       " animatorSpeed=" + animator.speed.ToString("0.00"));

        // -------------------------------------------------------------- jump
        Set(new Vector2(0f, 0f), true, false);
        await Task.Delay(120);
        Set(new Vector2(0f, 0f), false, false);
        await Task.Delay(220);
        log.AppendLine(Initial("jump-air"));
        log.AppendLine("  vy=" + body.linearVelocity.y.ToString("0.00") +
                       " grounded=" + movement.IsGrounded +
                       " animatorSpeed=" + animator.speed.ToString("0.00"));

        await Task.Delay(1400);
        log.AppendLine(Initial("landed"));

        // -------------------------------------------------------------- push
        if (pushObject != null)
        {
            pushObject.SetActive(true);
            var objectBody = pushObject.GetComponent<Rigidbody2D>();
            if (objectBody != null)
            {
                objectBody.position = new Vector2(krush.transform.position.x + 0.9f, -2.9f);
                objectBody.linearVelocity = Vector2.zero;
            }
            else
            {
                pushObject.transform.position = new Vector3(krush.transform.position.x + 0.9f, -2.9f, 0f);
            }

            // Face right by tapping right, then settle.
            Set(new Vector2(1f, 0f), false, false);
            await Task.Delay(200);
            Set(Vector2.zero, false, false);
            await Task.Delay(500);

            float objectStartX = pushObject.transform.position.x;

            Set(Vector2.zero, false, true);
            await Task.Delay(700);
            log.AppendLine(Initial("push-held"));
            log.AppendLine("  isPushing=" + push.IsPushing +
                           " objectMovedX=" + (pushObject.transform.position.x - objectStartX).ToString("0.00") +
                           " animatorState=" + CurrentState(animator));

            await Task.Delay(1100);
            log.AppendLine("  push-continues pushed=" + push.IsPushing +
                           " objectX=" + pushObject.transform.position.x.ToString("0.00"));

            Set(Vector2.zero, false, false);
            await Task.Delay(350);
            log.AppendLine(Initial("push-released"));
        }
        else
        {
            log.AppendLine("Push_object missing");
        }

        // --------------------------------------------------- Leon isolation
        log.AppendLine("Leon moved=" + Vector3.Distance(leon.transform.position, leonStart).ToString("0.000") +
                       " (expected ~0, keyboard-only)");

        // Show the run cycle timing that was copied from Leon.
        var runClip = FindClip(animator, "Krush-Run");
        if (runClip != null)
        {
            log.AppendLine("run clip length=" + runClip.length.ToString("0.000") +
                           " sampleRate=" + runClip.frameRate +
                           " (Leon run = 2.083s @ 12fps)");
        }

        if (addedPad)
        {
            InputSystem.RemoveDevice(pad);
            pad = null;
        }

        return log.ToString();
    }

    /// <summary>
    /// Verifies the copied Hurt/Death reactions on Krush: the hit staggers her,
    /// movement is locked while Hurt plays, and the killing hit leaves a Death
    /// state on the ground instead of a floating corpse.
    /// </summary>
    public static async Task<string> HurtDeath()
    {
        var log = new StringBuilder();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var krush = GameObject.Find("Krush");
        if (krush == null) return "missing Krush";

        var animator = krush.GetComponent<Animator>();
        var movement = krush.GetComponent<PlayerMovement2D>();
        var health = krush.GetComponent<PlayerHealthHurt>();

        await Task.Delay(300);
        health.TakeHit();
        await Task.Delay(250);
        log.AppendLine("[hurt] state=" + CurrentState(animator) +
                       " isHurtLock=" + movement.IsHurt +
                       " hits=" + health.HurtCount);

        await Task.Delay(1600);
        log.AppendLine("[hurt-end] state=" + CurrentState(animator) +
                       " isHurtLock=" + movement.IsHurt +
                       " hits=" + health.HurtCount);

        for (int i = 0; i < 4; i++)
        {
            health.TakeHit();
            await Task.Delay(150);
        }

        await Task.Delay(400);
        log.AppendLine("[death] state=" + CurrentState(animator) +
                       " isDead=" + health.IsDead +
                       " hits=" + health.HurtCount +
                       " bodySimulated=" + krush.GetComponent<Rigidbody2D>().simulated);

        float spriteBottom = krush.transform.position.y +
                             krush.GetComponent<SpriteRenderer>().sprite.bounds.min.y *
                             Mathf.Abs(krush.transform.lossyScale.y);
        log.AppendLine("  spriteBottom=" + spriteBottom.ToString("0.000") +
                       " (ground top ~ -4.500)");
        return log.ToString();
    }

    private static string Initial(string tag)
    {
        var krush = GameObject.Find("Krush");
        var animator = krush.GetComponent<Animator>();
        var movement = krush.GetComponent<PlayerMovement2D>();
        var body = krush.GetComponent<Rigidbody2D>();
        return "[" + tag + "] pos=" + krush.transform.position.ToString("0.00") +
               " vx=" + body.linearVelocity.x.ToString("0.00") +
               " grounded=" + movement.IsGrounded +
               " state=" + CurrentState(animator) +
               " animSpeed=" + animator.speed.ToString("0.00");
    }

    private static string CurrentState(Animator animator)
    {
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName("Idle")) return "Idle";
        if (info.IsName("Run")) return "Run";
        if (info.IsName("Jump")) return "Jump";
        if (info.IsName("Push")) return "Push";
        if (info.IsName("Hurt")) return "Hurt";
        if (info.IsName("Death")) return "Death";
        return "?";
    }

    private static AnimationClip FindClip(Animator animator, string name)
    {
        var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (controller == null) return null;
        foreach (var state in controller.layers[0].stateMachine.states)
        {
            if (state.state.motion is AnimationClip clip && clip.name == name) return clip;
        }
        return null;
    }

    private static void Set(Vector2 stick, bool jump, bool rightShoulder)
    {
        var state = new GamepadState { leftStick = stick };
        if (jump) state = state.WithButton(GamepadButton.South);
        if (rightShoulder) state = state.WithButton(GamepadButton.RightShoulder);
        InputSystem.QueueStateEvent(pad, state);
    }
}
