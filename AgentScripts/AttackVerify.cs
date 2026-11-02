using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode probes for sword hit timing and combo chaining. Every attack is
/// triggered through the real Input System (key F), not by calling the combo
/// method directly, so the whole input -> parameter -> Animator path is tested.
/// </summary>
public static class AttackVerify
{
    static SkeletonAI Sk() => UnityEngine.Object.FindAnyObjectByType<SkeletonAI>();
    static PlayerHealthHurt Pl() => UnityEngine.Object.FindAnyObjectByType<PlayerHealthHurt>();

    static FieldInfo F(Type t, string n) =>
        t.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

    static object Field(object target, string name) => F(target.GetType(), name).GetValue(target);

    static string AnimName(Animator a)
    {
        var st = a.GetCurrentAnimatorStateInfo(0);
        if (st.IsName("Attack1")) return "Attack1";
        if (st.IsName("Attack2")) return "Attack2";
        if (st.IsName("Idle")) return "Idle";
        if (st.IsName("Run")) return "Run";
        if (st.IsName("Hurt")) return "Hurt";
        if (st.IsName("Death")) return "Death";
        if (st.IsName("Push")) return "Push";
        if (st.IsName("Jump")) return "Jump";
        return "other";
    }

    static async Task PressF(int holdMs = 60)
    {
        var kb = Keyboard.current;
        if (kb == null) throw new InvalidOperationException("No keyboard device");
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F));
        InputSystem.Update();
        await Task.Delay(holdMs);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        InputSystem.Update();
    }

    static async Task<int> Prepare(float dx)
    {
        var sk = Sk();
        var pl = Pl();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();

        // Fresh skeleton for every probe, even if an earlier probe killed it.
        F(typeof(SkeletonAI), "hitsTaken").SetValue(sk, 0);
        F(typeof(SkeletonAI), "isDead").SetValue(sk, false);
        var skCol = sk.GetComponent<Collider2D>();
        if (skCol != null) skCol.enabled = true;

        // Keep the skeleton out of the fight so Leon cannot die mid-probe.
        F(typeof(SkeletonAI), "nextAttackTime").SetValue(sk, Time.time + 9999f);

        // Pin it in place: the probe is about the player's hit frame, not the
        // skeleton's knockback. TakeSwordHit still records damage.
        var skRb = sk.GetComponent<Rigidbody2D>();
        skRb.simulated = true;
        skRb.linearVelocity = Vector2.zero;
        skRb.bodyType = RigidbodyType2D.Kinematic;
        sk.enabled = false;

        plRb.position = new Vector2(sk.transform.position.x - dx, -3.37f);
        plRb.linearVelocity = Vector2.zero;
        mv.FaceDirection(1f);
        pl.GetComponent<SpriteRenderer>().flipX = false;

        var atk = pl.GetComponent<PlayerAttack2D>();
        atk.ResetCombo();
        await Task.Delay(250);
        return sk.HitsTaken;
    }

    static void Restore()
    {
        var sk = Sk();
        if (sk == null) return;
        var skRb = sk.GetComponent<Rigidbody2D>();
        if (skRb != null)
        {
            skRb.simulated = true;
            skRb.bodyType = RigidbodyType2D.Dynamic;
        }
        sk.enabled = true;
    }

    static string HitLine(string tag, Animator anim, float t0, int hits)
    {
        var st = anim.GetCurrentAnimatorStateInfo(0);
        float clipTime = st.normalizedTime * st.length;
        return tag + " t=" + (Time.time - t0).ToString("F3") +
               " anim=" + AnimName(anim) +
               " norm=" + st.normalizedTime.ToString("F3") +
               " clipT=" + clipTime.ToString("F3") +
               " hits=" + hits;
    }

    /// <summary>One real F press. Reports exactly when the skeleton loses HP.</summary>
    public static async Task<string> SingleTiming()
    {
        var sk = Sk();
        var pl = Pl();
        var anim = pl.GetComponent<Animator>();
        var sb = new StringBuilder();
        try
        {
            int hits0 = await Prepare(1.1f);
            float t0 = Time.time;
            int lastHits = hits0;
            string lastAnim = "";
            sb.AppendLine("START hits=" + hits0);

            await PressF();

            for (int i = 0; i < 110; i++)
            {
                await Task.Delay(16);
                string now = AnimName(anim);
                if (now != lastAnim)
                {
                    sb.AppendLine("STATE " + now + " t=" + (Time.time - t0).ToString("F3"));
                    lastAnim = now;
                }
                if (sk.HitsTaken != lastHits)
                {
                    lastHits = sk.HitsTaken;
                    sb.AppendLine("HIT " + HitLine("", anim, t0, lastHits));
                }
            }

            sb.AppendLine("FINAL hits=" + sk.HitsTaken + " gained=" + (sk.HitsTaken - hits0) +
                          " anim=" + AnimName(anim) + " dead=" + sk.IsDead);
        }
        finally { Restore(); }
        return sb.ToString();
    }

    static async Task<string> PressTrace(int secondDelayMs, int totalMs)
    {
        var sk = Sk();
        var pl = Pl();
        var anim = pl.GetComponent<Animator>();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var sb = new StringBuilder();
        try
        {
            int hits0 = await Prepare(1.1f);
            float t0 = Time.time;
            int lastHits = hits0;
            string lastAnim = "";
            sb.AppendLine("START hits=" + hits0 + " secondPressAt=" + secondDelayMs + "ms");

            await PressF();

            bool pressed2 = false;
            int loops = Mathf.Max(1, totalMs / 16);

            for (int i = 0; i < loops; i++)
            {
                float elapsedMs = (Time.time - t0) * 1000f;
                if (!pressed2 && elapsedMs >= secondDelayMs)
                {
                    pressed2 = true;
                    sb.AppendLine("SECOND PRESS t=" + (Time.time - t0).ToString("F3") +
                                  " anim=" + AnimName(anim) +
                                  " norm=" + anim.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("F2") +
                                  " step=" + atk.ComboStep + " seq=" + atk.SwingSequence);
                    await PressF();
                }

                await Task.Delay(16);

                string now = AnimName(anim);
                if (now != lastAnim)
                {
                    sb.AppendLine("STATE " + now + " t=" + (Time.time - t0).ToString("F3") +
                                  " seq=" + atk.SwingSequence + " step=" + atk.ComboStep);
                    lastAnim = now;
                }
                if (sk.HitsTaken != lastHits)
                {
                    lastHits = sk.HitsTaken;
                    sb.AppendLine("HIT " + HitLine("", anim, t0, lastHits));
                }
            }

            sb.AppendLine("FINAL hits=" + sk.HitsTaken + " gained=" + (sk.HitsTaken - hits0) +
                          " anim=" + AnimName(anim) + " seq=" + atk.SwingSequence +
                          " dead=" + sk.IsDead);
        }
        finally { Restore(); }
        return sb.ToString();
    }

    /// <summary>Two real presses: first starts the swing, second ~240 ms later queues the combo.</summary>
    public static async Task<string> ComboTiming()
    {
        return await PressTrace(240, 2700);
    }

    /// <summary>Two real presses with the second one late inside Attack1 (~0.70 s).</summary>
    public static async Task<string> LatePressTiming()
    {
        return await PressTrace(690, 2700);
    }

    /// <summary>Second real press arrives after Attack1 has ended but inside the buffer.</summary>
    public static async Task<string> BufferPressTiming()
    {
        return await PressTrace(1120, 2700);
    }

    /// <summary>Second real press arrives after the buffer has closed: must start a fresh Attack1.</summary>
    public static async Task<string> NoBufferTiming()
    {
        return await PressTrace(1700, 3200);
    }

    static string GroundTop()
    {
        var ground = GameObject.Find("SampleGround");
        if (ground != null && ground.TryGetComponent<Collider2D>(out var gc))
            return gc.bounds.max.y.ToString("F4");
        return "no-ground";
    }

    static string ArtBottom(SkeletonAI sk)
    {
        var sr = sk.GetComponent<SpriteRenderer>();
        return (sk.transform.position.y +
                (sr.sprite.bounds.min.y + 0.49f) * Mathf.Abs(sk.transform.lossyScale.y)).ToString("F4");
    }

    /// <summary>Five real F presses must kill the skeleton, one hit each.</summary>
    public static async Task<string> FiveRealPresses()
    {
        var sk = Sk();
        var pl = Pl();
        var anim = pl.GetComponent<Animator>();
        var skAnim = sk.GetComponent<Animator>();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var sb = new StringBuilder();

        F(typeof(SkeletonAI), "hitsTaken").SetValue(sk, 0);
        F(typeof(SkeletonAI), "nextAttackTime").SetValue(sk, Time.time + 9999f);
        sb.AppendLine("START hits=" + sk.HitsTaken + " dead=" + sk.IsDead +
                      " hitsToDie=" + Field(sk, "hitsToDie") +
                      " groundTop=" + GroundTop());

        for (int round = 1; round <= 6 && !sk.IsDead; round++)
        {
            plRb.position = new Vector2(sk.transform.position.x - 1.0f, plRb.position.y);
            plRb.linearVelocity = Vector2.zero;
            mv.FaceDirection(1f);
            pl.GetComponent<SpriteRenderer>().flipX = false;
            atk.ResetCombo();
            await Task.Delay(150);

            await PressF();
            await Task.Delay(1050);

            sb.AppendLine("press " + round +
                          " hits=" + sk.HitsTaken + " dead=" + sk.IsDead +
                          " playerAnim=" + AnimName(anim) +
                          " skAnim=" + AnimName(skAnim) +
                          " artBottom=" + ArtBottom(sk));
        }

        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(250);
            sb.AppendLine("corpse +" + ((i + 1) * 250) + "ms anim=" + AnimName(skAnim) +
                          " artBottom=" + ArtBottom(sk) +
                          " y=" + sk.transform.position.y.ToString("F4"));
        }

        sb.AppendLine("FINAL hits=" + sk.HitsTaken + " dead=" + sk.IsDead +
                      " anim=" + AnimName(skAnim) + " artBottom=" + ArtBottom(sk) +
                      " groundTop=" + GroundTop());
        return sb.ToString();
    }

    /// <summary>
    /// Combo with a fully live skeleton (dynamic body, AI on, knockback on).
    /// The pinned probes cannot show this: the first hit shoves the skeleton
    /// 0.6 m, so the frame-15 event may fire out of reach.
    /// </summary>
    public static async Task<string> RealComboDamage()
    {
        var sk = Sk();
        var pl = Pl();
        var anim = pl.GetComponent<Animator>();
        var skAnim = sk.GetComponent<Animator>();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var skRb = sk.GetComponent<Rigidbody2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var sb = new StringBuilder();

        // Revive and reset the skeleton, keeping it fully simulated.
        F(typeof(SkeletonAI), "hitsTaken").SetValue(sk, 0);
        F(typeof(SkeletonAI), "isDead").SetValue(sk, false);
        var skCol = sk.GetComponent<Collider2D>();
        if (skCol != null) skCol.enabled = true;
        skRb.simulated = true;
        skRb.bodyType = RigidbodyType2D.Dynamic;
        sk.enabled = true;

        // Stop it attacking Leon, but leave chase/knockback untouched.
        F(typeof(SkeletonAI), "nextAttackTime").SetValue(sk, Time.time + 9999f);

        skRb.linearVelocity = Vector2.zero;
        plRb.position = new Vector2(sk.transform.position.x - 1.2f, -3.37f);
        plRb.linearVelocity = Vector2.zero;
        mv.FaceDirection(1f);
        pl.GetComponent<SpriteRenderer>().flipX = false;
        atk.ResetCombo();
        await Task.Delay(300);

        int hits0 = sk.HitsTaken;
        float t0 = Time.time;
        int lastHits = hits0;
        sb.AppendLine("START hits=" + hits0 + " dx=" + Dx(sk, pl).ToString("F3") +
                      " range=" + Field(atk, "attackHitRange") +
                      " bonus=" + Field(atk, "comboHitReachBonus"));

        await PressF();
        await Task.Delay(240);
        sb.AppendLine("SECOND PRESS t=" + (Time.time - t0).ToString("F3"));
        await PressF();

        for (int i = 0; i < 170; i++)
        {
            await Task.Delay(16);
            if (sk.HitsTaken != lastHits)
            {
                lastHits = sk.HitsTaken;
                var st = anim.GetCurrentAnimatorStateInfo(0);
                sb.AppendLine("HIT t=" + (Time.time - t0).ToString("F3") +
                              " hits=" + lastHits +
                              " dx=" + Dx(sk, pl).ToString("F3") +
                              " anim=" + AnimName(anim) +
                              " clipT=" + (st.normalizedTime * st.length).ToString("F3") +
                              " skAnim=" + AnimName(skAnim));
            }
        }

        sb.AppendLine("FINAL hits=" + sk.HitsTaken + " gained=" + (sk.HitsTaken - hits0) +
                      " dx=" + Dx(sk, pl).ToString("F3") + " dead=" + sk.IsDead);
        return sb.ToString();
    }

    static float Dx(SkeletonAI sk, PlayerHealthHurt pl)
    {
        var col = sk.GetComponent<Collider2D>();
        Vector2 target = col != null ? (Vector2)col.bounds.center : (Vector2)sk.transform.position;
        return target.x - pl.transform.position.x;
    }

    /// <summary>Confirms the imported clips actually carry the hit event at the right time.</summary>
    public static string ClipEvents()
    {
        var sb = new StringBuilder();
        var pl = Pl();
        var anim = pl.GetComponent<Animator>();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var clips = anim.runtimeAnimatorController.animationClips;
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            if (clip.name != "Leon-Attack1" && clip.name != "Leon-Attack2") continue;
            sb.AppendLine(clip.name + " length=" + clip.length.ToString("F3") +
                          " loop=" + clip.isLooping);
            var events = clip.events;
            if (events == null || events.Length == 0)
            {
                sb.AppendLine("  NO EVENTS");
                continue;
            }
            foreach (var e in events)
            {
                sb.AppendLine("  event " + e.functionName + " at " + e.time.ToString("F3"));
            }
        }
        return sb.ToString();
    }
}
