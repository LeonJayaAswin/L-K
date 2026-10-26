using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hit reactions for Leon.
///
/// Pressing the hurt key takes one hit. Every hit plays the Hurt trigger, and
/// the hit that reaches <see cref="hitsBeforeDeath"/> plays Death instead and
/// takes control away for good. Both triggers are wired to Any State
/// transitions in Leon.controller, so they interrupt whatever he is doing.
///
/// The death clips are centre-pivoted sprite swaps, so their visible bottom
/// rises as the frames get shorter. While Death plays, the sprite bottom is
/// pinned to the ground line captured when Leon died, which keeps the corpse
/// on the floor instead of floating.
/// </summary>
[RequireComponent(typeof(Animator))]
[AddComponentMenu("L&K/Player/Player Health Hurt")]
public class PlayerHealthHurt : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Debug key that takes a hit.")]
    [SerializeField] private Key hurtKey = Key.H;

    [Header("Hits")]
    [Tooltip("How many hits Leon can take. The hit that reaches this number plays Death.")]
    [SerializeField, Min(1)] private int hitsBeforeDeath = 10;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Trigger that plays the Hurt reaction.")]
    [SerializeField] private string hurtTrigger = "Hurt";

    [Tooltip("Trigger that plays the Death reaction.")]
    [SerializeField] private string deathTrigger = "Death";

    [Tooltip("Animator state that the Hurt trigger moves into. Used to keep Leon staggered for as long as it is on screen.")]
    [SerializeField] private string hurtStateName = "Hurt";

    [Header("Death Grounding")]
    [Tooltip("Pins the bottom of the death sprite to the ground so the corpse cannot float up as its frames get shorter.")]
    [SerializeField] private bool pinDeathToGround = true;

    [Tooltip("Tag of the objects that count as ground when the ground below a mid-air death is looked up.")]
    [SerializeField] private string groundTag = "Ground";

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Rigidbody2D body;

    [Header("References")]
    [Tooltip("Movement script, locked while the Hurt animation plays.")]
    [SerializeField] private PlayerMovement2D movement;

    [Tooltip("Attack script, disabled once Leon dies.")]
    [SerializeField] private PlayerAttack2D attack;

    private int hurtCount;
    private bool isDead;
    private int hurtTriggerHash;
    private int deathTriggerHash;
    private float deathGroundY;
    private float hurtPendingTimer;

    private const float HurtLockGraceTime = 0.3f;

    /// <summary>How many hits Leon has taken so far.</summary>
    public int HurtCount => hurtCount;

    /// <summary>True once the Death animation has been triggered.</summary>
    public bool IsDead => isDead;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<PlayerMovement2D>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (body == null) body = GetComponent<Rigidbody2D>();

        hurtTriggerHash = Animator.StringToHash(hurtTrigger);
        deathTriggerHash = Animator.StringToHash(deathTrigger);
    }

    private void Update()
    {
        // A dead Leon ignores further hits.
        if (isDead) return;

        if (Keyboard.current != null && Keyboard.current[hurtKey].wasPressedThisFrame)
        {
            TakeHit();

            // The killing hit already disabled everything below us.
            if (isDead) return;
        }

        // Hold Leon in place only while the Hurt state is actually playing.
        // The short grace timer also covers the frame between setting the
        // trigger and the Animator entering the state; if the Animator never
        // enters it, the timer expires and control is handed back.
        if (movement != null)
        {
            if (hurtPendingTimer > 0f) hurtPendingTimer -= Time.deltaTime;

            bool hurtPlaying = IsHurtPlaying();
            if (hurtPlaying) hurtPendingTimer = 0f;

            movement.IsHurt = hurtPlaying || hurtPendingTimer > 0f;
        }
    }

    /// <summary>
    /// Runs after the Animator has swapped the sprite, so the visible bottom of
    /// the current frame is known when the corpse is repositioned.
    /// </summary>
    private void LateUpdate()
    {
        if (!isDead || !pinDeathToGround) return;
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        // Centre-pivoted death frames shrink as Leon collapses, which would
        // lift their bottom off the floor. Shift the whole corpse by the
        // difference so the sprite bottom stays on the ground line.
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null) return;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float spriteBottom = transform.position.y + sprite.bounds.min.y * scaleY;
        float delta = deathGroundY - spriteBottom;

        if (Mathf.Abs(delta) > 0.0001f)
        {
            Vector3 position = transform.position;
            position.y += delta;
            transform.position = position;
        }
    }

    /// <summary>
    /// Registers one hit. The hit that reaches the death threshold kills Leon
    /// instead of staggering him, so ten presses of H kill him on the tenth.
    /// </summary>
    public void TakeHit()
    {
        if (isDead) return;

        hurtCount++;

        if (hurtCount >= hitsBeforeDeath)
        {
            Die();
            return;
        }

        // Lock movement on the same frame the key goes down rather than
        // waiting for the Animator to finish entering the Hurt state.
        hurtPendingTimer = HurtLockGraceTime;

        if (animator != null)
        {
            // A stray Death trigger must never fire while Leon is still alive.
            animator.ResetTrigger(deathTriggerHash);
            animator.SetTrigger(hurtTriggerHash);
        }
    }

    private void Die()
    {
        isDead = true;

        // Remember where the floor is before the body is taken out of physics.
        deathGroundY = ResolveGroundY();

        if (animator != null)
        {
            // The Death transition is evaluated first, but a Hurt trigger that
            // is still set would fire straight after and interrupt it, so clear
            // it before asking for Death.
            animator.ResetTrigger(hurtTriggerHash);
            animator.speed = 1f;
            animator.SetTrigger(deathTriggerHash);
        }

        if (movement != null)
        {
            movement.IsHurt = false;
            movement.enabled = false;
        }

        hurtPendingTimer = 0f;

        if (attack != null) attack.enabled = false;

        // The corpse no longer needs physics. Freezing the body lets LateUpdate
        // slide the sprite onto the floor without the collider fighting it.
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
    }

    /// <summary>
    /// The Y of the ground Leon falls onto. While he is standing this is simply
    /// the bottom of his collider; when he dies in mid-air the first walkable
    /// collider below him is used so the corpse does not hang in the air.
    /// </summary>
    private float ResolveGroundY()
    {
        if (bodyCollider == null) return transform.position.y;

        float feetY = bodyCollider.bounds.min.y;

        if (movement != null && movement.IsGrounded) return feetY;

        Vector2 origin = new Vector2(bodyCollider.bounds.center.x, feetY + 0.05f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 100f);

        float best = float.NegativeInfinity;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
            if (!hit.collider.CompareTag(groundTag)) continue;

            if (hit.point.y > best) best = hit.point.y;
        }

        return float.IsNegativeInfinity(best) ? feetY : best;
    }

    private bool IsHurtPlaying()
    {
        if (animator == null) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName(hurtStateName);
    }
}
