using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class PlayTest
{
    static SkeletonAI Sk() { return UnityEngine.Object.FindAnyObjectByType<SkeletonAI>(); }
    static PlayerHealthHurt Pl() { return UnityEngine.Object.FindAnyObjectByType<PlayerHealthHurt>(); }

    static string Anim(Animator a)
    {
        if (a == null) return "no-animator";
        var clips = a.GetCurrentAnimatorClipInfo(0);
        string clip = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "none";
        var st = a.GetCurrentAnimatorStateInfo(0);
        return clip + " t=" + st.normalizedTime.ToString("F2");
    }

    public static string Status()
    {
        var sb = new StringBuilder();
        var sk = Sk();
        var pl = Pl();
        sb.AppendLine("t=" + Time.time.ToString("F2"));
        if (sk != null)
        {
            var sr = sk.GetComponent<SpriteRenderer>();
            var rb = sk.GetComponent<Rigidbody2D>();
            var anim = sk.GetComponent<Animator>();
            float artBottom = sk.transform.position.y +
                              (sr.sprite.bounds.min.y + 0.49f) * Mathf.Abs(sk.transform.lossyScale.y);
            sb.AppendLine("SK pos=" + sk.transform.position.ToString("F3") +
                          " dead=" + sk.IsDead + " hits=" + sk.HitsTaken +
                          " anim=" + Anim(anim) + " flip=" + sr.flipX +
                          " artBottom=" + artBottom.ToString("F4") +
                          " vel=" + rb.linearVelocity.ToString("F2"));
        }
        else sb.AppendLine("SK missing");
        if (pl != null)
        {
            var sr = pl.GetComponent<SpriteRenderer>();
            var anim = pl.GetComponent<Animator>();
            sb.AppendLine("PL pos=" + pl.transform.position.ToString("F3") +
                          " dead=" + pl.IsDead + " hits=" + pl.HurtCount +
                          " anim=" + Anim(anim) +
                          " spriteBottom=" + (pl.transform.position.y + sr.sprite.bounds.min.y * Mathf.Abs(pl.transform.lossyScale.y)).ToString("F4"));
        }
        else sb.AppendLine("PL missing");
        return sb.ToString();
    }

    public static string PlaceLeon(float x)
    {
        var pl = Pl();
        var rb = pl.GetComponent<Rigidbody2D>();
        rb.position = new Vector2(x, -3.37f);
        rb.linearVelocity = Vector2.zero;
        return "placed Leon at " + rb.position;
    }

    public static string PlaceLeonNextToSkeleton(float offset)
    {
        var sk = Sk();
        return PlaceLeon(sk.transform.position.x + offset);
    }

    public static string Swing(int n)
    {
        var pl = Pl();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var f = typeof(PlayerAttack2D).GetField("swingSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        f.SetValue(atk, n);
        return "swingSequence=" + f.GetValue(atk) + "\n" + Status();
    }

    public static string SwingNow()
    {
        var pl = Pl();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var f = typeof(PlayerAttack2D).GetField("swingSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        int cur = (int)f.GetValue(atk);
        f.SetValue(atk, cur + 1);
        return "swingSequence=" + (cur + 1) + "\n" + Status();
    }

    public static string HitSkeleton(float px)
    {
        var sk = Sk();
        sk.TakeSwordHit(new Vector2(px, sk.transform.position.y));
        return Status();
    }

    public static async Task<string> HitAndSample(float px)
    {
        var sk = Sk();
        float before = sk.transform.position.x;
        int hitsBefore = sk.HitsTaken;
        sk.TakeSwordHit(new Vector2(px, sk.transform.position.y));
        string animNow = Anim(sk.GetComponent<Animator>());
        float immediate = sk.transform.position.x;
        await Task.Delay(120);
        float after120 = sk.transform.position.x;
        string anim120 = Anim(sk.GetComponent<Animator>());
        await Task.Delay(380);
        float after500 = sk.transform.position.x;
        string anim500 = Anim(sk.GetComponent<Animator>());
        return "hit " + (hitsBefore + 1) + " before=" + before.ToString("F3") +
               " immediate=" + immediate.ToString("F3") +
               " +120ms=" + after120.ToString("F3") + " (" + anim120 + ")" +
               " +500ms=" + after500.ToString("F3") + " (" + anim500 + ")" +
               " dead=" + sk.IsDead + " hitAnimWas=" + animNow +
               " artBottom=" + ArtBottom(sk);
    }

    static string ArtBottom(SkeletonAI sk)
    {
        var sr = sk.GetComponent<SpriteRenderer>();
        return (sk.transform.position.y + (sr.sprite.bounds.min.y + 0.49f) * Mathf.Abs(sk.transform.lossyScale.y)).ToString("F4");
    }

    static string GroundTop()
    {
        var ground = GameObject.Find("SampleGround");
        if (ground != null && ground.TryGetComponent<Collider2D>(out var gc))
            return gc.bounds.max.y.ToString("F4");
        return "no-ground";
    }

    public static async Task<string> RealSwingTrace()
    {
        var sk = Sk();
        var pl = Pl();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var plSr = pl.GetComponent<SpriteRenderer>();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var fFacing = typeof(PlayerMovement2D).GetField("facingRight", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLast = typeof(SkeletonAI).GetField("lastPlayerSwingSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        var fState = typeof(SkeletonAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var sb = new StringBuilder();

        plRb.position = new Vector2(sk.transform.position.x - 0.8f, plRb.position.y);
        plRb.linearVelocity = Vector2.zero;
        fFacing.SetValue(mv, true);
        plSr.flipX = false;
        await Task.Delay(150);

        sb.AppendLine("before: dx=" + (sk.transform.position.x - pl.transform.position.x).ToString("F2") +
                      " facing=" + mv.FacingRight + " seq=" + atk.SwingSequence +
                      " lastSeen=" + fLast.GetValue(sk) + " hits=" + sk.HitsTaken +
                      " anim=" + Anim(sk.GetComponent<Animator>()) + " state=" + fState.GetValue(sk));

        atk.ResetCombo();
        atk.AdvanceCombo();

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(50);
            sb.AppendLine("+" + ((i + 1) * 50) + "ms dx=" + (sk.transform.position.x - pl.transform.position.x).ToString("F2") +
                          " facing=" + mv.FacingRight +
                          " swinging=" + atk.IsSwinging + " pushing=" + atk.IsPushing +
                          " seq=" + atk.SwingSequence + " lastSeen=" + fLast.GetValue(sk) +
                          " hits=" + sk.HitsTaken + " anim=" + Anim(sk.GetComponent<Animator>()) +
                          " state=" + fState.GetValue(sk));
        }
        return sb.ToString();
    }

    public static async Task<string> RealSwingDiag()
    {
        var sk = Sk();
        var pl = Pl();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var plSr = pl.GetComponent<SpriteRenderer>();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var fFacing = typeof(PlayerMovement2D).GetField("facingRight", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLast = typeof(SkeletonAI).GetField("lastPlayerSwingSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        var sb = new StringBuilder();

        for (int i = 1; i <= 6; i++)
        {
            plRb.position = new Vector2(sk.transform.position.x - 0.8f, plRb.position.y);
            plRb.linearVelocity = Vector2.zero;
            fFacing.SetValue(mv, true);
            plSr.flipX = false;
            await Task.Delay(120);

            float dxBefore = sk.transform.position.x - pl.transform.position.x;
            atk.ResetCombo();
            atk.AdvanceCombo();

            await Task.Delay(300);

            float dxAfter = sk.transform.position.x - pl.transform.position.x;
            sb.AppendLine("swing " + i +
                          " seq=" + atk.SwingSequence +
                          " lastSeen=" + fLast.GetValue(sk) +
                          " dxBefore=" + dxBefore.ToString("F2") +
                          " dxAfter=" + dxAfter.ToString("F2") +
                          " hits=" + sk.HitsTaken +
                          " dead=" + sk.IsDead +
                          " anim=" + Anim(sk.GetComponent<Animator>()) +
                          " playerHits=" + pl.HurtCount);

            if (sk.IsDead) break;
            await Task.Delay(400);
        }

        return sb.ToString();
    }

    public static async Task<string> PatrolTrace()
    {
        var sk = Sk();
        var pl = Pl();
        var sb = new StringBuilder();
        var fState = typeof(SkeletonAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var anim = sk.GetComponent<Animator>();
        var sr = sk.GetComponent<SpriteRenderer>();
        float start = sk.transform.position.x;
        sb.AppendLine("leonX=" + pl.transform.position.x.ToString("F2") +
                      " dist=" + Vector2.Distance(sk.transform.position, pl.transform.position).ToString("F2") +
                      " startX=" + start.ToString("F3"));

        string lastKey = "";
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(250);
            string key = fState.GetValue(sk) + "|" + sr.flipX;
            if (key != lastKey)
            {
                lastKey = key;
                sb.AppendLine("+" + ((i + 1) * 250) + "ms state=" + fState.GetValue(sk) +
                              " x=" + sk.transform.position.x.ToString("F3") +
                              " dxFromStart=" + (sk.transform.position.x - start).ToString("F3") +
                              " anim=" + Anim(anim) + " speed=" + anim.speed.ToString("F2") +
                              " flip=" + sr.flipX);
            }
        }
        return sb.ToString();
    }

    public static async Task<string> ChaseTrace()
    {
        var sk = Sk();
        var pl = Pl();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var sb = new StringBuilder();
        var fState = typeof(SkeletonAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var anim = sk.GetComponent<Animator>();

        plRb.position = new Vector2(sk.transform.position.x + 3.2f, plRb.position.y);
        plRb.linearVelocity = Vector2.zero;
        sb.AppendLine("placed Leon at dx=+3.2");

        string lastKey = "";
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(250);
            float dx = sk.transform.position.x - pl.transform.position.x;
            string key = fState.GetValue(sk) + "|" + (Mathf.Abs(dx) < 1.2f);
            if (key != lastKey)
            {
                lastKey = key;
                sb.AppendLine("+" + ((i + 1) * 250) + "ms state=" + fState.GetValue(sk) +
                              " dx=" + dx.ToString("F3") +
                              " anim=" + Anim(anim) + " speed=" + anim.speed.ToString("F2") +
                              " skX=" + sk.transform.position.x.ToString("F3") +
                              " playerHits=" + pl.HurtCount);
            }
        }
        return sb.ToString();
    }

    public static async Task<string> EnemyKillsLeon()
    {
        var sk = Sk();
        var pl = Pl();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var plSr = pl.GetComponent<SpriteRenderer>();
        var sb = new StringBuilder();

        plRb.position = new Vector2(sk.transform.position.x + (mv.FacingRight ? -0.9f : 0.9f), plRb.position.y);
        plRb.linearVelocity = Vector2.zero;
        sb.AppendLine("start skinX=" + sk.transform.position.x.ToString("F3") +
                      " leonX=" + pl.transform.position.x.ToString("F3") +
                      " playerHits=" + pl.HurtCount + " dead=" + pl.IsDead);

        int lastHits = pl.HurtCount;
        for (int i = 0; i < 80; i++)
        {
            await Task.Delay(500);

            // Keep Leon in the skeleton's reach; a real player can also stand still.
            if (!pl.IsDead)
            {
                bool faceRight = mv.FacingRight;
                plRb.position = new Vector2(sk.transform.position.x + (faceRight ? -0.9f : 0.9f), plRb.position.y);
                plRb.linearVelocity = Vector2.zero;
            }

            if (pl.HurtCount != lastHits)
            {
                lastHits = pl.HurtCount;
                sb.AppendLine("+" + ((i + 1) * 500) + "ms leonHit=" + lastHits +
                              " dead=" + pl.IsDead + " anim=" + Anim(pl.GetComponent<Animator>()) +
                              " skAnim=" + Anim(sk.GetComponent<Animator>()) +
                              " skState=" + typeof(SkeletonAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sk));
            }

            if (pl.IsDead)
            {
                sb.AppendLine("leon died at ~" + ((i + 1) * 500) + "ms");
                break;
            }
        }

        var plAnim = pl.GetComponent<Animator>();
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(250);
            var srr = pl.GetComponent<SpriteRenderer>();
            float bottom = pl.transform.position.y + srr.sprite.bounds.min.y * Mathf.Abs(pl.transform.lossyScale.y);
            sb.AppendLine("post-death +" + ((i + 1) * 250) + "ms anim=" + Anim(plAnim) +
                          " artBottom=" + bottom.ToString("F4") +
                          " y=" + pl.transform.position.y.ToString("F4") +
                          " skAnim=" + Anim(sk.GetComponent<Animator>()) +
                          " skX=" + sk.transform.position.x.ToString("F3"));
        }

        sb.AppendLine("FINAL playerHits=" + pl.HurtCount + " dead=" + pl.IsDead +
                      " anim=" + Anim(plAnim) + " groundTop=" + GroundTop() +
                      " skAlive=" + !sk.IsDead);
        return sb.ToString();
    }

    public static async Task<string> RealSwingDeath()
    {
        var sk = Sk();
        var pl = Pl();
        var atk = pl.GetComponent<PlayerAttack2D>();
        var mv = pl.GetComponent<PlayerMovement2D>();
        var plRb = pl.GetComponent<Rigidbody2D>();
        var sb = new StringBuilder();
        sb.AppendLine("groundTop=" + GroundTop() + " skStart=" + sk.transform.position.ToString("F3") +
                      " facingRight=" + mv.FacingRight);

        for (int i = 1; i <= 5; i++)
        {
            // Put Leon on the side he is actually looking at, like a player
            // stepping up to the enemy, instead of fighting the facing logic.
            bool faceRight = mv.FacingRight;
            plRb.position = new Vector2(sk.transform.position.x + (faceRight ? -0.8f : 0.8f), plRb.position.y);
            plRb.linearVelocity = Vector2.zero;
            await Task.Delay(150);

            if (mv.FacingRight != faceRight)
            {
                faceRight = mv.FacingRight;
                plRb.position = new Vector2(sk.transform.position.x + (faceRight ? -0.8f : 0.8f), plRb.position.y);
                await Task.Delay(120);
            }

            atk.ResetCombo();
            atk.AdvanceCombo();
            await Task.Delay(300);

            sb.AppendLine("swing " + i + " seq=" + atk.SwingSequence +
                          " facing=" + mv.FacingRight +
                          " dx=" + (sk.transform.position.x - pl.transform.position.x).ToString("F2") +
                          " hits=" + sk.HitsTaken + " dead=" + sk.IsDead +
                          " anim=" + Anim(sk.GetComponent<Animator>()) +
                          " artBottom=" + ArtBottom(sk));
            if (sk.IsDead) break;
            await Task.Delay(400);
        }

        // Watch the corpse for a while: the pin must hold it on the ground.
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(250);
            sb.AppendLine("post-death +" + ((i + 1) * 250) + "ms anim=" + Anim(sk.GetComponent<Animator>()) +
                          " artBottom=" + ArtBottom(sk) +
                          " y=" + sk.transform.position.y.ToString("F4"));
        }

        sb.AppendLine("FINAL hits=" + sk.HitsTaken + " dead=" + sk.IsDead +
                      " anim=" + Anim(sk.GetComponent<Animator>()) +
                      " artBottom=" + ArtBottom(sk) + " groundTop=" + GroundTop() +
                      " playerDead=" + pl.IsDead + " playerHits=" + pl.HurtCount);
        return sb.ToString();
    }

    public static async Task<string> FrictionProbe(float px)
    {
        var sk = Sk();
        var col = sk.GetComponent<Collider2D>();
        col.sharedMaterial = new PhysicsMaterial2D("FrictionlessProbe") { friction = 0f, bounciness = 0f };
        return "assigned frictionless material\n" + await KnockbackProbe(px);
    }

    public static async Task<string> KnockbackProbe(float px)
    {
        var sk = Sk();
        var rb = sk.GetComponent<Rigidbody2D>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var fTimer = typeof(SkeletonAI).GetField("knockbackTimer", flags);
        var fState = typeof(SkeletonAI).GetField("state", flags);
        var fDesired = typeof(SkeletonAI).GetField("desiredVelocityX", flags);
        var sb = new StringBuilder();
        float startRb = rb.position.x;
        sb.AppendLine("before transform=" + sk.transform.position.x.ToString("F4") + " rb=" + startRb.ToString("F4"));
        sk.TakeSwordHit(new Vector2(px, sk.transform.position.y));
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(50);
            sb.AppendLine((i * 50 + 50) + "ms tr=" + sk.transform.position.x.ToString("F4") +
                          " rb=" + rb.position.x.ToString("F4") +
                          " vx=" + rb.linearVelocity.x.ToString("F2") +
                          " timer=" + ((float)fTimer.GetValue(sk)).ToString("F3") +
                          " desired=" + ((float)fDesired.GetValue(sk)).ToString("F2") +
                          " state=" + fState.GetValue(sk) +
                          " fixedT=" + Time.fixedTime.ToString("F3") +
                          " time=" + Time.time.ToString("F3") +
                          " dt=" + Time.deltaTime.ToString("F3") +
                          " fdt=" + Time.fixedDeltaTime.ToString("F3") +
                          " frame=" + Time.frameCount);
        }
        sb.AppendLine("DELTA rb=" + (rb.position.x - startRb).ToString("F4") +
                      " transform=" + (sk.transform.position.x - startRb).ToString("F4"));
        return sb.ToString();
    }

    public static string HitPlayer(int n)
    {
        var pl = Pl();
        for (int i = 0; i < n; i++) pl.TakeHit();
        return Status();
    }

    public static string Facing()
    {
        var pl = Pl();
        var mv = pl.GetComponent<PlayerMovement2D>();
        return "facingRight=" + mv.FacingRight + " grounded=" + mv.IsGrounded;
    }
}
