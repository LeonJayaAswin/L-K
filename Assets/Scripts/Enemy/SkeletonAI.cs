using UnityEngine;

/// <summary>
/// Skeleton enemy brain.
///
/// Behaviour:
///  - Idles for <see cref="idleTime"/> seconds, then patrols
///    <see cref="patrolDistance"/> metres left and right with the Walk clip.
///  - Patrols back to an Idle pause at each end, then walks the other way.
///  - When the player comes within <see cref="detectionRange"/> the skeleton
///    chases with a faster walk, stops at <see cref="attackRange"/> and
///    alternates Attack1/Attack2. Each attack calls PlayerHealthHurt.TakeHit,
///    so five skeleton hits kill Leon.
///  - Every swing of Leon's sword (Attack1 or the Attack2 combo) can hit the
///    skeleton once. A hit plays TakeHit, knocks the skeleton back a few steps
///    and interrupts whatever it was doing. Five sword hits kill it.
///  - Death freezes the body and pins the visible bottom of the death sprite
///    to the ground line, so the corpse cannot float or sink as the clip plays.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("L&K/Enemy/Skeleton AI")]
public class SkeletonAI : MonoBehaviour
{
    [Header("Patrol")]
    [Tooltip("How long the skeleton stands still at each end of its route.")]
    [SerializeField, Min(0f)] private float idleTime = 2f;

    [Tooltip("How far the skeleton walks from its spawn point in each direction, in metres.")]
    [SerializeField, Min(0.1f)] private float patrolDistance = 1f;

    [Tooltip("Normal patrol walking speed. Match to player speed.")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 3f;

    [Tooltip("Animation reference speed for walk animation to match player feel (slower).")]
    [SerializeField, Min(0.1f)] private float walkAnimationReferenceSpeed = 3.8f;

    [Tooltip("Scale walk animation playback to match movement speed (slower).")]
    [SerializeField, Range(0f, 2f)] private float walkAnimationSync = 0.8f;

    [Header("Detection")]
    [Tooltip("The player is noticed inside this radius, measured from the skeleton.")]
    [SerializeField, Min(0.5f)] private float detectionRange = 4f;

    [Tooltip("How far the player can run before the skeleton gives up and goes back to patrolling.")]
    [SerializeField, Min(0.5f)] private float loseTargetRange = 6f;

    [Tooltip("Chase speed. The Walk clip is sped up to match so the legs read as a fast walk. Match to player speed.")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3f;

    [Tooltip("Maximum playback multiplier for the chase walk.")]
    [SerializeField, Range(1f, 3f)] private float maxChaseAnimationSpeed = 2.2f;

    [Header("Combat")]
    [Tooltip("Horizontal distance at which the skeleton stops and attacks.")]
    [SerializeField, Min(0.2f)] private float attackRange = 1.15f;

    [Tooltip("The attack only hurts the player when they are still within this horizontal distance at the hit frame.")]
    [SerializeField, Min(0.2f)] private float attackHitReach = 1.5f;

    [Tooltip("Seconds into the Attack1 clip when the blow lands.")]
    [SerializeField, Min(0f)] private float attackHitDelay = 0.32f;

    [Tooltip("Seconds into the Attack2 clip when the blow lands. Its lunge is later than Attack1's.")]
    [SerializeField, Min(0f)] private float attack2HitDelay = 0.5f;

    [Tooltip("Length of the attack clip. The skeleton returns to chasing after this.")]
    [SerializeField, Min(0.05f)] private float attackDuration = 0.67f;

    [Tooltip("Pause between attacks while the player stays in range. Rest after one hit - make it 0.7 sec.")]
    [SerializeField, Min(0f)] private float attackCooldown = 0.7f;

    [Tooltip("How many sword hits the skeleton survives. The hit that reaches this number plays Death.")]
    [SerializeField, Min(1)] private int hitsToDie = 5;

    [Header("Hit Reaction")]
    [Tooltip("How far the knockback carries the skeleton, in metres.")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.6f;

    [Tooltip("How long the knockback lasts.")]
    [SerializeField, Min(0.05f)] private float knockbackTime = 0.3f;

    [Tooltip("How long the skeleton stays staggered after being hit.")]
    [SerializeField, Min(0f)] private float hitStunTime = 0.35f;

    [Tooltip("Horizontal reach of Leon's sword. The skeleton can only be hit inside this distance.")]
    [SerializeField, Min(0.2f)] private float playerHitReach = 2.0f;

    [Tooltip("Vertical tolerance for Leon's sword, so a jump cannot hit the skeleton's feet from far above.")]
    [SerializeField, Min(0.2f)] private float playerHitHeight = 2.2f;

    [Header("Death Grounding")]
    [Tooltip("Pins the visible bottom of the death sprite to the ground so the corpse cannot float or sink.")]
    [SerializeField] private bool pinDeathToGround = true;

    [Tooltip("Tag of the objects that count as ground when a ground line is looked up.")]
    [SerializeField] private string groundTag = "Ground";

    [Tooltip("Transparent pixels between the bottom of the Death sprite frames and the skeleton's feet, in sprite units. The Skeleton sheets keep 49 px (0.49 at 100 PPU) of empty space below the art.")]
    [SerializeField, Min(0f)] private float deathArtBottomPadding = 0.49f;

    [Header("Animation States")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string walkState = "Walk";
    [SerializeField] private string attack1State = "Attack1";
    [SerializeField] private string attack2State = "Attack2";
    [SerializeField] private string takeHitState = "TakeHit";
    [SerializeField] private string deathState = "Death";

    [Header("References")]
    [Tooltip("Leon's transform. Left empty, the PlayerHealthHurt component is found automatically.")]
    [SerializeField] private Transform player;

    [SerializeField] private PlayerHealthHurt playerHealth;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;

    private enum State { Idle, Patrol, Chase, Attack, Hurt, Dead }

    private State state;
    private Vector3 startPosition;
    private float leftPatrolX;
    private float rightPatrolX;
    private int patrolDirection = -1;
    private float stateTimer;

    private float desiredVelocityX;
    private float nextAttackTime;
    private float attackTimer;
    private float attackHitTimer;
    private bool attackHasHit;
    private bool useAttack2;

    private float knockbackTimer;
    private float knockbackDirection;
    private float hitStunTimer;

    private int hitsTaken;
    private int lastPlayerSwingSequence;
    private float playerSearchTimer;

    private bool isDead;
    private float deathGroundY;
    private string currentAnimation;

    /// <summary>How many sword hits the skeleton has taken.</summary>
    public int HitsTaken => hitsTaken;

    /// <summary>True once the skeleton has played its Death animation.</summary>
    public bool IsDead => isDead;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();

        // The velocity is assigned every physics step, and a sleeping body
        // would ignore it and freeze in place.
        if (body != null) body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        ResolvePlayerReferences();
    }

    private void Start()
    {
        startPosition = transform.position;
        leftPatrolX = startPosition.x - patrolDistance;
        rightPatrolX = startPosition.x + patrolDistance;

        currentAnimation = null;
        EnterIdle(idleTime);
    }

    private void Update()
    {
        if (isDead) return;

        // A player spawned after the scene loaded is still picked up.
        if (player == null)
        {
            playerSearchTimer -= Time.deltaTime;
            if (playerSearchTimer <= 0f)
            {
                playerSearchTimer = 0.5f;
                ResolvePlayerReferences();
            }

            if (player == null)
            {
                desiredVelocityX = 0f;
                return;
            }
        }

        // Between the Animator updating the swing state and this script
        // running, one new swing gets one hit check.
        RegisterPlayerSwing();

        bool playerAlive = playerHealth == null || !playerHealth.IsDead;
        float dx = player.position.x - transform.position.x;
        float distance = Vector2.Distance(transform.position, player.position);

        switch (state)
        {
            case State.Idle: TickIdle(playerAlive, distance); break;
            case State.Patrol: TickPatrol(playerAlive, distance); break;
            case State.Chase: TickChase(playerAlive, dx, distance); break;
            case State.Attack: TickAttack(dx, distance); break;
            case State.Hurt: TickHurt(playerAlive, distance); break;
        }
    }

    private void FixedUpdate()
    {
        if (isDead || body == null) return;

        Vector2 velocity = body.linearVelocity;

        if (state == State.Hurt && knockbackTimer > 0f && knockbackTime > 0f)
        {
            // The shove is integrated here, not in Update, so it is not tied to
            // the render frame rate. A triangular speed curve covers exactly
            // knockbackDistance over knockbackTime; sampling it at the middle
            // of each physics step removes the half-step error.
            float dt = Time.fixedDeltaTime;
            knockbackTimer = Mathf.Max(0f, knockbackTimer - dt);
            float t = Mathf.Clamp01((knockbackTimer + dt * 0.5f) / knockbackTime);
            velocity.x = knockbackDirection * (2f * knockbackDistance / knockbackTime) * t;
        }
        else
        {
            velocity.x = desiredVelocityX;
        }

        body.linearVelocity = velocity;
    }

    /// <summary>
    /// Runs after the Animator has swapped in the current sprite. The death
    /// frames of the sheet are centre-pivoted, so their visible bottom is not
    /// the bottom of the sprite bounds. It is offset by the transparent margin
    /// below the art, which is measured from the sheet and pinned here.
    /// </summary>
    private void LateUpdate()
    {
        if (!isDead || !pinDeathToGround) return;
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        float delta = deathGroundY - GetVisibleSpriteBottom();
        if (Mathf.Abs(delta) > 0.0001f)
        {
            Vector3 position = transform.position;
            position.y += delta;
            transform.position = position;
        }
    }

    // ---------------------------------------------------------------- states

    private void EnterIdle(float duration)
    {
        state = State.Idle;
        stateTimer = Mathf.Max(0f, duration);
        desiredVelocityX = 0f;
        PlayAnimation(idleState);
    }

    private void EnterPatrol()
    {
        state = State.Patrol;
        desiredVelocityX = 0f;
        PlayAnimation(walkState, GetWalkAnimationSpeed());
    }

    private void EnterChase()
    {
        state = State.Chase;
        PlayAnimation(walkState, ChaseAnimationSpeed());
    }

    private void EnterAttack(float dx)
    {
        state = State.Attack;
        desiredVelocityX = 0f;
        attackTimer = attackDuration;
        attackHasHit = false;

        // Face the player before the swing so the hit reads correctly.
        SetFacing(dx);

        useAttack2 = !useAttack2;
        attackHitTimer = useAttack2 ? attack2HitDelay : attackHitDelay;
        PlayAnimation(useAttack2 ? attack2State : attack1State, 1f, true);
    }

    private void EnterHurt(float dx)
    {
        state = State.Hurt;
        desiredVelocityX = 0f;

        // Knock away from the player and turn back to face the attacker.
        knockbackDirection = dx >= 0f ? 1f : -1f;
        knockbackTimer = knockbackTime;
        hitStunTimer = Mathf.Max(hitStunTime, knockbackTime);

        SetFacing(-knockbackDirection);
        PlayAnimation(takeHitState, 1f, true);
    }

    private void TickIdle(bool playerAlive, float distance)
    {
        desiredVelocityX = 0f;
        stateTimer -= Time.deltaTime;

        if (playerAlive && distance <= detectionRange)
        {
            EnterChase();
            return;
        }

        if (stateTimer <= 0f)
        {
            EnterPatrol();
        }
    }

    private void TickPatrol(bool playerAlive, float distance)
    {
        if (playerAlive && distance <= detectionRange)
        {
            EnterChase();
            return;
        }

        float targetX = patrolDirection > 0 ? rightPatrolX : leftPatrolX;
        float toTarget = targetX - transform.position.x;

        if (Mathf.Abs(toTarget) <= 0.05f)
        {
            patrolDirection *= -1;
            EnterIdle(idleTime);
            return;
        }

        desiredVelocityX = Mathf.Sign(toTarget) * walkSpeed;
        SetFacing(desiredVelocityX);
        PlayAnimation(walkState, GetWalkAnimationSpeed());
    }

    private void TickChase(bool playerAlive, float dx, float distance)
    {
        if (!playerAlive || distance > loseTargetRange)
        {
            EnterIdle(idleTime);
            return;
        }

        float absDx = Mathf.Abs(dx);

        // Close enough to strike: plant the feet and wait out the cooldown.
        if (absDx <= attackRange)
        {
            desiredVelocityX = 0f;
            SetFacing(dx);
            PlayAnimation(idleState);

            if (Time.time >= nextAttackTime)
            {
                EnterAttack(dx);
            }
            return;
        }

        desiredVelocityX = Mathf.Sign(dx) * chaseSpeed;
        SetFacing(desiredVelocityX);
        PlayAnimation(walkState, ChaseAnimationSpeed());
    }

    private void TickAttack(float dx, float distance)
    {
        desiredVelocityX = 0f;
        SetFacing(dx);

        if (!attackHasHit)
        {
            attackHitTimer -= Time.deltaTime;
            if (attackHitTimer <= 0f)
            {
                attackHasHit = true;
                TryHitPlayer();
            }
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            nextAttackTime = Time.time + attackCooldown;
            EnterChase();
        }
    }

    private void TickHurt(bool playerAlive, float distance)
    {
        // Knockback itself is integrated in FixedUpdate so it always covers
        // exactly knockbackDistance. Here we only hold the stun and keep the
        // fallback speed at zero for the first physics step after the shove.
        desiredVelocityX = 0f;
        hitStunTimer -= Time.deltaTime;

        if (hitStunTimer <= 0f)
        {
            desiredVelocityX = 0f;

            if (playerAlive && distance <= detectionRange)
            {
                EnterChase();
            }
            else
            {
                EnterIdle(idleTime);
            }
        }
    }

    // --------------------------------------------------------------- combat

    /// <summary>
    /// Watches Leon's swing counter. Every swing (Attack1, or the Attack2
    /// combo follow-up) gets exactly one hit check, so holding F cannot chew
    /// through the skeleton in a handful of physics frames.
    /// </summary>
    private void RegisterPlayerSwing()
    {
        if (playerAttack == null || player == null) return;

        int sequence = playerAttack.SwingSequence;
        if (sequence == lastPlayerSwingSequence) return;

        lastPlayerSwingSequence = sequence;

        if (isDead) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerAttack.IsPushing) return;

        float dx = transform.position.x - player.position.x;
        if (Mathf.Abs(dx) > playerHitReach) return;

        Vector2 enemyCenter = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center
            : (Vector2)transform.position;

        if (Mathf.Abs(enemyCenter.y - player.position.y) > playerHitHeight) return;

        // Leon only hits what is in front of him.
        bool facingRight = playerMovement == null || playerMovement.FacingRight;
        if (facingRight && dx < 0f) return;
        if (!facingRight && dx > 0f) return;

        TakeSwordHit(player.position);
    }

    private void TryHitPlayer()
    {
        if (playerHealth == null || playerHealth.IsDead || player == null) return;

        float dx = Mathf.Abs(player.position.x - transform.position.x);
        if (dx > attackHitReach) return;

        // Keep the blow believable: the player can jump over the skeleton.
        if (Mathf.Abs(player.position.y - transform.position.y) > 2f) return;

        playerHealth.TakeHit();
    }

    /// <summary>
    /// One sword hit. Plays Take Hit and knocks the skeleton back unless this
    /// is the killing blow, which plays Death instead.
    /// </summary>
    public void TakeSwordHit(Vector2 sourcePosition)
    {
        if (isDead) return;

        hitsTaken++;

        if (hitsTaken >= hitsToDie)
        {
            Die();
            return;
        }

        EnterHurt(transform.position.x - sourcePosition.x);
    }

    private void Die()
    {
        isDead = true;
        state = State.Dead;
        desiredVelocityX = 0f;

        // Capture the ground before physics is switched off. If the skeleton
        // was somehow airborne, look the floor up below it so the corpse still
        // lands on the ground instead of freezing in mid-air.
        deathGroundY = ResolveDeathGroundY();

        PlayAnimation(deathState, 1f, true);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (bodyCollider != null) bodyCollider.enabled = false;
    }

    /// <summary>
    /// World Y line that the visible bottom of the corpse should sit on.
    /// Grounded deaths keep the exact current feet line; airborne deaths find
    /// the first ground collider below.
    /// </summary>
    private float ResolveDeathGroundY()
    {
        float feetY = GetVisibleSpriteBottom();

        Vector2 origin = new Vector2(transform.position.x, feetY + 0.1f);
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

    /// <summary>
    /// Y of the bottom of the visible artwork, not the bottom of the sprite
    /// frame. The skeleton sheets keep 49 px of transparent space under the
    /// feet, which deathArtBottomPadding accounts for.
    /// </summary>
    private float GetVisibleSpriteBottom()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return transform.position.y;
        }

        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float localBottom = spriteRenderer.sprite.bounds.min.y + deathArtBottomPadding;
        return transform.position.y + localBottom * scaleY;
    }

    // ------------------------------------------------------------ presentation

    private float ChaseAnimationSpeed()
    {
        if (walkSpeed <= 0.001f) return 1f;
        float speed = chaseSpeed / walkSpeed;
        if (walkAnimationSync <= 0f) return Mathf.Clamp(speed, 0.5f, maxChaseAnimationSpeed);
        float synced = (walkAnimationReferenceSpeed > 0.001f) ? (chaseSpeed / walkAnimationReferenceSpeed) * walkAnimationSync : speed * walkAnimationSync;
        return Mathf.Clamp(synced, 0.4f, maxChaseAnimationSpeed);
    }

    private float GetWalkAnimationSpeed()
    {
        if (walkSpeed <= 0.001f) return 1f;
        if (walkAnimationSync <= 0f) return 1f;
        if (walkAnimationReferenceSpeed <= 0.001f) return Mathf.Clamp(walkSpeed / walkSpeed, 0.5f, 2f);
        return Mathf.Clamp((walkSpeed / walkAnimationReferenceSpeed) * walkAnimationSync, 0.4f, 2f);
    }

    private void SetFacing(float dx)
    {
        if (spriteRenderer == null) return;
        if (Mathf.Abs(dx) < 0.0001f) return;

        spriteRenderer.flipX = dx < 0f;
    }

    private void PlayAnimation(string stateName, float speed = 1f, bool restart = false)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        bool sameState = currentAnimation == stateName;
        if (sameState && !restart && Mathf.Approximately(animator.speed, speed)) return;

        currentAnimation = stateName;
        animator.speed = speed;
        animator.Play(stateName, 0, 0f);
    }

    private void ResolvePlayerReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealthHurt>();
        }

        if (playerHealth != null)
        {
            if (player == null) player = playerHealth.transform;
            if (playerAttack == null) playerAttack = playerHealth.GetComponent<PlayerAttack2D>();
            if (playerMovement == null) playerMovement = playerHealth.GetComponent<PlayerMovement2D>();
        }

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (player != null)
        {
            if (playerHealth == null) playerHealth = player.GetComponent<PlayerHealthHurt>();
            if (playerAttack == null) playerAttack = player.GetComponent<PlayerAttack2D>();
            if (playerMovement == null) playerMovement = player.GetComponent<PlayerMovement2D>();
        }
    }
}
