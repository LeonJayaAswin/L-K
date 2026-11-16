using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hit reactions and health for Leon and Krush.
///
/// Enemy attacks call <see cref="TakeHit"/>. Every hit plays the Hurt
/// trigger, and the hit that reaches <see cref="hitsBeforeDeath"/> plays Death
/// instead and takes control away for good. Both triggers are wired to Any
/// State transitions in the character's controller, so they interrupt
/// whatever they are doing. H is only an optional debug key; by default
/// enemies are the damage source and five of their hits kill the character.
///
/// The death clips are centre-pivoted sprite swaps, so their visible bottom
/// rises as the frames get shorter. While Death plays, the sprite bottom is
/// pinned to the ground line captured when the character died, which keeps
/// the corpse on the floor instead of floating.
///
/// The Hurt clips have the same problem, but the body is still simulated while
/// they play, so the transform cannot be moved without fighting physics. The
/// hurt frame is therefore drawn through a small child renderer whose offset
/// keeps the visible bottom on the floor while the collider and rigidbody stay
/// where they are.
/// </summary>
[RequireComponent(typeof(Animator))]
[AddComponentMenu("L&K/Player/Player Health Hurt")]
public class PlayerHealthHurt : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Optional debug key that takes a hit. Off by default: enemy attacks are the real damage source.")]
    [SerializeField] private bool enableDebugHurtKey;

    [Tooltip("Debug key used when Enable Debug Hurt Key is on.")]
    [SerializeField] private Key hurtKey = Key.H;

    [Header("Hits")]
    [Tooltip("How many hits the character can take. The hit that reaches this number plays Death.")]
    [SerializeField, Min(1)] private int hitsBeforeDeath = 5;

    [Header("Health Bar")]
    [SerializeField] private Slider healthBar;
    [SerializeField, Min(0.1f)] private float healthFillSmoothSpeed = 1.5f;
    [SerializeField] private Image healthTrailImage;
    [SerializeField, Min(0.1f)] private float trailReduceSpeed = 0.65f;
    [SerializeField, Min(0f)] private float trailDelay = 0.35f;
    private float targetHealthFill = 1f;
    private float targetTrailFill = 1f;
    private float trailDelayTimer;
    private RectTransform healthTrailRect;
    private Vector3 trailOriginalScale;
    private bool trailUsesFillAmount;
    private Material trailMaterial;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Tooltip("Trigger that plays the Hurt reaction.")]
    [SerializeField] private string hurtTrigger = "Hurt";

    [Tooltip("Trigger that plays the Death reaction.")]
    [SerializeField] private string deathTrigger = "Death";

    [Tooltip("Animator state that the Hurt trigger moves into. Used to keep the character staggered for as long as it is on screen.")]
    [SerializeField] private string hurtStateName = "Hurt";

    [Header("Hurt Grounding")]
    [Tooltip("Pins the bottom of the hurt sprite to the floor so a centre-pivoted frame cannot lift the character off the ground. The rigidbody is left alone.")]
    [SerializeField] private bool pinHurtToGround = true;

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

    [Tooltip("Attack script, disabled once the character dies.")]
    [SerializeField] private PlayerAttack2D attack;

    [Tooltip("Push script, cancelled when the character is hit and disabled once the character dies.")]
    [SerializeField] private PlayerPush2D push;

    private int hurtCount;
    private bool isDead;
    private int hurtTriggerHash;
    private int deathTriggerHash;
    private float deathGroundY;
    private float hurtPendingTimer;
    private SpriteRenderer hurtPinRenderer;
    private bool isHurtPinned;
    private float hurtGroundY;

    private const float HurtLockGraceTime = 0.3f;

    /// <summary>How many hits this player has taken so far.</summary>
    public int HurtCount => hurtCount;

    /// <summary>True once the Death animation has been triggered.</summary>
    public bool IsDead => isDead;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<PlayerMovement2D>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (push == null) push = GetComponent<PlayerPush2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (body == null) body = GetComponent<Rigidbody2D>();

        if (healthBar != null)
        {
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
            healthBar.value = 1f;

            // A Simple Image is squeezed as the Slider resizes its fill rect.
            // Filled mode clips the original bar art from left to right instead.
            Image fillImage = healthBar.fillRect != null
                ? healthBar.fillRect.GetComponent<Image>()
                : null;
            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.preserveAspect = false;
            }

        }

        if (healthTrailImage != null)
        {
            SetUpTrail();
        }

        hurtTriggerHash = Animator.StringToHash(hurtTrigger);
        deathTriggerHash = Animator.StringToHash(deathTrigger);
    }

    private void Update()
    {
        if (healthBar != null)
            healthBar.value = Mathf.MoveTowards(
                healthBar.value,
                targetHealthFill,
                healthFillSmoothSpeed * Time.deltaTime);

        if (healthTrailRect != null)
        {
            if (targetHealthFill < targetTrailFill)
            {
                trailDelayTimer = Mathf.Max(0f, trailDelayTimer - Time.deltaTime);
                if (trailDelayTimer <= 0f)
                    targetTrailFill = Mathf.MoveTowards(
                        targetTrailFill,
                        targetHealthFill,
                        trailReduceSpeed * Time.deltaTime);
            }
            else if (targetHealthFill > targetTrailFill)
            {
                targetTrailFill = targetHealthFill;
                trailDelayTimer = 0f;
            }

            SetTrailFill(targetTrailFill);
        }

        // A dead character ignores further hits.
        if (isDead) return;

        if (enableDebugHurtKey && Keyboard.current != null && Keyboard.current[hurtKey].wasPressedThisFrame)
        {
            TakeHit();

            // The killing hit already disabled everything below us.
            if (isDead) return;
        }

        // Hold the character in place only while the Hurt state is actually playing.
        // The short grace timer also covers the frame between setting the
        // trigger and the Animator entering the state; if the Animator never
        // enters it, the timer expires and control is handed back.
        if (movement != null)
        {
            if (hurtPendingTimer > 0f) hurtPendingTimer -= Time.deltaTime;

            bool hurtPlaying = IsHurtPlaying();
            if (hurtPlaying) hurtPendingTimer = 0f;

            bool hurtLocked = hurtPlaying || hurtPendingTimer > 0f;
            movement.IsHurt = hurtLocked;

            if (pinHurtToGround)
            {
                // Start pinning as soon as the reaction locks the character and
                // the feet are on the floor. A hit taken in mid-air is pinned
                // once the body settles instead of freezing the sprite in air.
                if (hurtLocked && !isHurtPinned && movement.IsGrounded)
                    StartHurtPin();
                else if (!hurtLocked && isHurtPinned)
                    StopHurtPin();
            }
            else if (isHurtPinned)
            {
                StopHurtPin();
            }
        }
        else if (isHurtPinned)
        {
            StopHurtPin();
        }
    }

    /// <summary>
    /// Runs after the Animator has swapped the sprite, so the visible bottom of
    /// the current frame is known when the corpse is repositioned.
    /// </summary>
    private void LateUpdate()
    {
        if (!isDead)
        {
            if (isHurtPinned) UpdateHurtPin();
            return;
        }

        if (!pinDeathToGround) return;
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
    /// Starts drawing the hurt animation through an offset child renderer.
    /// The collider and rigidbody stay exactly where physics put them; only
    /// the sprite is shifted so the visible feet cannot rise with the frame.
    /// </summary>
    private void StartHurtPin()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        if (hurtPinRenderer == null)
        {
            var pinObject = new GameObject("Hurt Sprite");
            pinObject.layer = gameObject.layer;
            pinObject.transform.SetParent(transform, false);
            hurtPinRenderer = pinObject.AddComponent<SpriteRenderer>();
            hurtPinRenderer.enabled = false;
        }

        isHurtPinned = true;

        // Remember the floor line before the first hurt frame is swapped in.
        hurtGroundY = CurrentSpriteBottom();

        CopySpriteToPin();
        spriteRenderer.enabled = false;
        hurtPinRenderer.enabled = true;
        UpdateHurtPin();
    }

    /// <summary>
    /// Stops the pinned draw and hands rendering back to the real renderer.
    /// </summary>
    private void StopHurtPin()
    {
        isHurtPinned = false;

        if (hurtPinRenderer != null) hurtPinRenderer.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
    }

    /// <summary>
    /// Mirrors the Animator's sprite on the pin renderer so the hurt frames can
    /// be drawn at an offset without moving the whole character.
    /// </summary>
    private void UpdateHurtPin()
    {
        if (!isHurtPinned || hurtPinRenderer == null || spriteRenderer == null) return;

        CopySpriteToPin();

        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null) return;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        if (scaleY < 0.0001f) return;

        float spriteBottom = transform.position.y + sprite.bounds.min.y * scaleY;
        float offset = (hurtGroundY - spriteBottom) / scaleY;
        hurtPinRenderer.transform.localPosition = new Vector3(0f, offset, 0f);
    }

    private void CopySpriteToPin()
    {
        if (hurtPinRenderer == null || spriteRenderer == null) return;

        hurtPinRenderer.sprite = spriteRenderer.sprite;
        hurtPinRenderer.flipX = spriteRenderer.flipX;
        hurtPinRenderer.flipY = spriteRenderer.flipY;
        hurtPinRenderer.color = spriteRenderer.color;
        hurtPinRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        hurtPinRenderer.sortingOrder = spriteRenderer.sortingOrder;
        hurtPinRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        hurtPinRenderer.drawMode = spriteRenderer.drawMode;
        hurtPinRenderer.size = spriteRenderer.size;
        hurtPinRenderer.maskInteraction = spriteRenderer.maskInteraction;
        hurtPinRenderer.spriteSortPoint = spriteRenderer.spriteSortPoint;
    }

    /// <summary>World Y of the bottom of the sprite currently on screen.</summary>
    private float CurrentSpriteBottom()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return transform.position.y;

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        return transform.position.y + spriteRenderer.sprite.bounds.min.y * scaleY;
    }

    private void OnDisable()
    {
        StopHurtPin();
    }

    /// <summary>
    /// Registers one hit, called by an enemy attack. The hit that reaches the
    /// death threshold kills the character instead of staggering them, so five
    /// skeleton hits kill on the fifth.
    /// </summary>
    public void TakeHit()
    {
        if (isDead) return;

        hurtCount++;
        targetHealthFill = Mathf.Clamp01(1f - (float)hurtCount / hitsBeforeDeath);
        trailDelayTimer = trailDelay;

        // A hit interrupts a push. Otherwise the hold would keep the Push
        // animation parameter set and fight the Hurt animation.
        if (push != null) push.EndPush();

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

        // The Death pin takes over from the hurt pin and moves the whole
        // corpse, so hand rendering back to the real sprite renderer first.
        StopHurtPin();

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

        if (push != null)
        {
            push.EndPush();
            push.enabled = false;
        }

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
    /// The Y of the ground the character falls onto. While standing this is
    /// simply the bottom of the collider; when death happens in mid-air the
    /// first walkable collider below is used so the corpse does not hang in
    /// the air.
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

    private void SetUpTrail()
    {
        healthTrailRect = healthTrailImage.rectTransform;

        RectTransform fillRect = healthBar != null ? healthBar.fillRect : null;
        Image fillImage = fillRect != null ? fillRect.GetComponent<Image>() : null;

        // The authored trail is a plain rectangle with its own rect and scale,
        // so after a hit its bright edge shows around the bar art. Copy the
        // fill's transform and sprite instead: both graphics then share one
        // silhouette, and the trail stays the first sibling so the fill always
        // paints over the part of it that is still full.
        if (fillRect != null)
        {
            if (healthTrailRect.parent != fillRect.parent)
                healthTrailRect.SetParent(fillRect.parent, false);

            healthTrailRect.anchorMin = fillRect.anchorMin;
            healthTrailRect.anchorMax = fillRect.anchorMax;
            healthTrailRect.pivot = fillRect.pivot;
            healthTrailRect.anchoredPosition = fillRect.anchoredPosition;
            healthTrailRect.sizeDelta = fillRect.sizeDelta;
            healthTrailRect.localScale = fillRect.localScale;
            healthTrailRect.localRotation = fillRect.localRotation;

            if (healthTrailRect.GetSiblingIndex() > fillRect.GetSiblingIndex())
                healthTrailRect.SetSiblingIndex(fillRect.GetSiblingIndex());
        }

        trailOriginalScale = healthTrailRect.localScale;

        trailUsesFillAmount = false;
        if (fillImage != null && fillImage.sprite != null)
        {
            healthTrailImage.sprite = fillImage.sprite;
            healthTrailImage.type = Image.Type.Filled;
            healthTrailImage.fillMethod = Image.FillMethod.Horizontal;
            healthTrailImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthTrailImage.fillClockwise = fillImage.fillClockwise;
            healthTrailImage.preserveAspect = false;
            healthTrailImage.useSpriteMesh = false;
            trailUsesFillAmount = true;
        }
        else
        {
            // No fill art to borrow: keep the old plain rectangle, but shrink
            // it from its left edge so the trail still drains rightwards.
            Vector2 pivot = healthTrailRect.pivot;
            float leftEdge = healthTrailRect.anchoredPosition.x
                - healthTrailRect.rect.width * healthTrailRect.localScale.x * pivot.x;
            healthTrailRect.pivot = new Vector2(0f, pivot.y);
            healthTrailRect.anchoredPosition = new Vector2(leftEdge, healthTrailRect.anchoredPosition.y);
        }

        healthTrailImage.raycastTarget = false;
        healthTrailImage.color = Color.white;

        // The trail borrows the fill's art, so a normal tint would make it the
        // exact same colour as the health and it would vanish behind the fill.
        // UI/Default multiplies its pixels by the material's _Color; pushing
        // that far past white turns the whole silhouette into a flat pale
        // ghost while keeping the sprite's alpha (and therefore its exact
        // shape and rounded ends).
        if (trailMaterial == null)
        {
            trailMaterial = new Material(healthTrailImage.material)
            {
                name = "Health Trail Silhouette"
            };

            if (trailMaterial.HasProperty("_Color"))
                trailMaterial.SetColor("_Color", new Color(255f, 255f, 255f, 1f));

            healthTrailImage.material = trailMaterial;
        }

        SetTrailFill(1f);
    }

    private void OnDestroy()
    {
        if (trailMaterial != null) Destroy(trailMaterial);
    }

    private void SetTrailFill(float fill)
    {
        if (healthTrailImage == null) return;

        fill = Mathf.Clamp01(fill);

        if (trailUsesFillAmount)
        {
            healthTrailImage.fillAmount = fill;
            return;
        }

        if (healthTrailRect == null) return;

        Vector3 scale = trailOriginalScale;
        scale.x *= fill;
        healthTrailRect.localScale = scale;
    }

}
