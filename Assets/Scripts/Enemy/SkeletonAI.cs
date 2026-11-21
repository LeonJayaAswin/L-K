using UnityEngine;

/// <summary>
/// Skeleton enemy brain.
///
/// Behaviour:
///  - Idles for <see cref="idleTime"/> seconds, then patrols
///    <see cref="patrolDistance"/> metres left and right with the Walk clip.
///  - Patrols back to an Idle pause at each end, then walks the other way.
///  - When either player (Leon or Krush) comes within
///    <see cref="detectionRange"/> the skeleton chases them with a faster walk,
///    stops at <see cref="attackRange"/> and alternates Attack1/Attack2. Each
///    attack calls PlayerHealthHurt.TakeHit on the focused player when they are
///    in reach, so five skeleton hits kill that player.
///  - Co-op targeting: the first player to enter detection range holds the
///    skeleton's attention, even if the other player walks closer. A sword hit
///    from either player immediately steals that attention, so damage always
///    overrides "who was seen first". A dying or fleeing player is dropped for
///    the other one, and the aggro notes are cleared once both are out of range.
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

    [Tooltip("Animation reference speed for walk animation to match player feel.")]
    [SerializeField, Min(0.1f)] private float walkAnimationReferenceSpeed = 3.878f;

    [Tooltip("Scale walk animation playback to match movement speed.")]
    [SerializeField, Range(0f, 2f)] private float walkAnimationSync = 1.0f;

    [Header("Detection")]
    [Tooltip("The player is noticed inside this radius, measured from the skeleton.")]
    [SerializeField, Min(0.5f)] private float detectionRange = 6f;

    [Tooltip("How far the player can run before the skeleton gives up and goes back to patrolling.")]
    [SerializeField, Min(0.5f)] private float loseTargetRange = 8f;

    [Tooltip("How much closer the other player has to be before the skeleton switches target, so it cannot flip-flop when Leon and Krush stand together.")]
    [SerializeField, Min(0f)] private float targetSwitchDistance = 0.5f;

    [Tooltip("Chase speed. The Walk clip is sped up to match so the legs read as a fast walk. Match to player speed.")]
    [SerializeField, Min(0.1f)] private float chaseSpeed = 3f;

    [Tooltip("Maximum playback multiplier for the chase walk.")]
    [SerializeField, Range(1f, 3f)] private float maxChaseAnimationSpeed = 2.2f;

    [Header("Combat")]
    [Tooltip("Horizontal distance at which the skeleton stops and attacks.")]
    [SerializeField, Min(0.2f)] private float attackRange = 1.6f;

    [Tooltip("The attack only hurts the player when they are still within this horizontal distance at the hit frame.")]
    [SerializeField, Min(0.2f)] private float attackHitReach = 1.9f;

    [Tooltip("Seconds into the Attack1 clip when the blow lands.")]
    [SerializeField, Min(0f)] private float attackHitDelay = 0.32f;

    [Tooltip("Seconds into the Attack2 clip when the blow lands. Its lunge is later than Attack1's.")]
    [SerializeField, Min(0f)] private float attack2HitDelay = 0.5f;

    [Tooltip("Real-time length of the attack, including the slower attackAnimationSpeed. The skeleton returns to chasing after this.")]
    [SerializeField, Min(0.05f)] private float attackDuration = 0.9f;

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

    [Header("Animation Speeds")]
    [Tooltip("Playback speed of the attack clips. Below 1 is slower; 0.75 makes one skeleton swing read at about Leon's swing length.")]
    [SerializeField, Range(0.2f, 2f)] private float attackAnimationSpeed = 0.75f;

    [Tooltip("Playback speed of the Take Hit clip.")]
    [SerializeField, Range(0.2f, 2f)] private float hitAnimationSpeed = 0.8f;

    [Tooltip("Playback speed of the Death clip.")]
    [SerializeField, Range(0.2f, 2f)] private float deathAnimationSpeed = 0.6f;

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
    [Tooltip("Current target, normally Leon or Krush. Left empty, the players are found by their PlayerHealthHurt component.")]
    [SerializeField] private Transform player;

    [Tooltip("Current target's health. Drives which player the skeleton chases and hits.")]
    [SerializeField] private PlayerHealthHurt playerHealth;
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
    private float playerSearchTimer;

    // Co-op aggro. Both players are tracked by their PlayerHealthHurt component,
    // so the skeleton does not need to know their names, tags or input devices.
    // "first seen" is whoever entered detection range first in this encounter.
    // "attacker" is whoever last damaged the skeleton; damage outranks vision.
    private PlayerHealthHurt firstSeenPlayer;
    private PlayerHealthHurt attackerPlayer;
    private bool hasChosenTarget;

    // Every living-and-dead player in the scene. The skeleton keeps the full
    // list so it can fall back to the other player when one of them dies.
    private PlayerHealthHurt[] players = System.Array.Empty<PlayerHealthHurt>();
    private readonly System.Collections.Generic.List<PlayerHealthHurt> playerBuffer = new System.Collections.Generic.List<PlayerHealthHurt>();

    private bool isDead;
    private float deathGroundY;
    private string currentAnimation;

    /// <summary>How many sword hits the skeleton has taken.</summary>
    public int HitsTaken => hitsTaken;

    /// <summary>True once the skeleton has played its Death animation.</summary>
    public bool IsDead => isDead;

    /// <summary>The player currently holding the skeleton's attention, or null when idle.</summary>
    public PlayerHealthHurt FocusedPlayer => playerHealth;

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
        UpdateTarget();
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

        // Players spawned after the scene loaded are still picked up. The list
        // is also refreshed here because a player can be disabled or removed,
        // and the other one has to take over as the target.
        playerSearchTimer -= Time.deltaTime;
        if (playerSearchTimer <= 0f)
        {
            playerSearchTimer = 0.5f;
            ResolvePlayerReferences();
        }

        UpdateTarget();

        if (player == null)
        {
            // Nobody left to fight: stop walking and settle back into Idle
            // without restarting the idle timer every frame.
            desiredVelocityX = 0f;
            if (state != State.Idle && state != State.Hurt) EnterIdle(idleTime);
            return;
        }

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
        attackHitTimer = (useAttack2 ? attack2HitDelay : attackHitDelay) / Mathf.Max(0.05f, attackAnimationSpeed);
        PlayAnimation(useAttack2 ? attack2State : attack1State, attackAnimationSpeed, true);
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
        PlayAnimation(takeHitState, hitAnimationSpeed, true);
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

    private void TryHitPlayer()
    {
        bool facingRight = spriteRenderer == null || !spriteRenderer.flipX;
        float forward = facingRight ? 1f : -1f;

        PlayerHealthHurt nearest = null;
        float bestDistance = float.MaxValue;

        foreach (PlayerHealthHurt candidate in players)
        {
            if (!CanHitPlayer(candidate, forward, out float absDx)) continue;

            // The focused player takes priority: if both co-op players are in
            // the swing at once, the skeleton still fights the one it is
            // chasing instead of randomly hitting whoever stands closer.
            if (candidate == playerHealth)
            {
                candidate.TakeHit();
                return;
            }

            if (absDx < bestDistance)
            {
                bestDistance = absDx;
                nearest = candidate;
            }
        }

        // Focus stepped out of reach during the wind-up: hit whoever is there.
        if (nearest != null) nearest.TakeHit();
    }

    /// <summary>
    /// True when a living player is inside the swing's reach and in front of
    /// the skeleton. Anyone who jumped over it or stepped away is spared.
    /// </summary>
    private bool CanHitPlayer(PlayerHealthHurt candidate, float forward, out float absDx)
    {
        absDx = float.MaxValue;
        if (candidate == null || candidate.IsDead) return false;

        Vector2 target = candidate.transform.position;
        float dx = target.x - transform.position.x;
        absDx = Mathf.Abs(dx);

        if (absDx > attackHitReach) return false;
        if (Mathf.Abs(target.y - transform.position.y) > 2f) return false;

        // The tiny dead zone keeps a player standing right on top hittable.
        if (absDx > 0.2f && Mathf.Sign(dx) != forward) return false;

        return true;
    }

    /// <summary>
    /// One sword hit with no known attacker. Plays Take Hit and knocks the
    /// skeleton back unless this is the killing blow.
    /// </summary>
    public void TakeSwordHit(Vector2 sourcePosition)
    {
        TakeSwordHit(sourcePosition, null);
    }

    /// <summary>
    /// One sword hit from a specific co-op player. The attacker becomes the
    /// focus immediately, so a hit from P2 pulls the skeleton off P1 and vice
    /// versa, regardless of who was seen first or who is standing closer.
    /// </summary>
    public void TakeSwordHit(Vector2 sourcePosition, PlayerHealthHurt attacker)
    {
        if (isDead) return;

        if (attacker != null && !attacker.IsDead)
        {
            attackerPlayer = attacker;
            playerHealth = attacker;
            player = attacker.transform;
            hasChosenTarget = true;
        }

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

        PlayAnimation(deathState, deathAnimationSpeed, true);

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
        playerBuffer.Clear();

        PlayerHealthHurt[] found = FindObjectsByType<PlayerHealthHurt>(FindObjectsInactive.Exclude);
        foreach (PlayerHealthHurt candidate in found)
        {
            if (candidate == null) continue;
            if (!playerBuffer.Contains(candidate)) playerBuffer.Add(candidate);
        }

        // An explicitly assigned player is kept even if it is temporarily
        // missing from the search, so the skeleton still remembers it.
        if (playerHealth != null && !playerBuffer.Contains(playerHealth))
        {
            playerBuffer.Add(playerHealth);
        }

        // Last-resort fallback for a character tagged as the player.
        if (playerBuffer.Count == 0 && player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
            {
                PlayerHealthHurt taggedHealth = foundPlayer.GetComponent<PlayerHealthHurt>();
                if (taggedHealth != null) playerBuffer.Add(taggedHealth);
            }
        }

        players = playerBuffer.ToArray();
    }

    /// <summary>
    /// Co-op target priority: the last player to damage the skeleton wins; if
    /// nobody attacked, the first player that entered detection range keeps
    /// the aggro even when the other player moves closer. Nearest-player with
    /// the old switch hysteresis is only the fallback when neither of those
    /// applies. Dead or out-of-range players are dropped for the other one.
    /// </summary>
    private void UpdateTarget()
    {
        // A dead player cannot hold aggro; the other player takes over.
        if (firstSeenPlayer != null && firstSeenPlayer.IsDead) firstSeenPlayer = null;
        if (attackerPlayer != null && attackerPlayer.IsDead) attackerPlayer = null;

        // Remember who noticed the skeleton first in this encounter. If both
        // step into range on the same frame, the nearer one is marked first.
        if (firstSeenPlayer == null)
        {
            float firstDistance = float.MaxValue;
            foreach (PlayerHealthHurt candidate in players)
            {
                if (candidate == null || candidate.IsDead) continue;

                float distance = Vector2.Distance(transform.position, candidate.transform.position);
                if (distance <= detectionRange && distance < firstDistance)
                {
                    firstDistance = distance;
                    firstSeenPlayer = candidate;
                }
            }
        }

        PlayerHealthHurt desired = null;

        // Damage outranks vision: whoever hit the skeleton is the focus.
        if (IsTargetAvailable(attackerPlayer)) desired = attackerPlayer;

        // Nobody attacked (or the attacker died/fled): keep the first-seen player.
        if (desired == null && IsTargetAvailable(firstSeenPlayer)) desired = firstSeenPlayer;

        if (desired == null)
        {
            // Last resort: the closest available player. The switch distance
            // still stops flip-flopping when both stand together.
            float bestDistance = float.MaxValue;
            foreach (PlayerHealthHurt candidate in players)
            {
                if (!IsTargetAvailable(candidate)) continue;

                float distance = Vector2.Distance(transform.position, candidate.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    desired = candidate;
                }
            }

            if (desired != null && hasChosenTarget && playerHealth != null &&
                playerHealth != desired && IsTargetAvailable(playerHealth))
            {
                float currentDistance = Vector2.Distance(transform.position, playerHealth.transform.position);
                if (bestDistance + targetSwitchDistance >= currentDistance)
                {
                    desired = playerHealth;
                }
            }
        }

        if (desired == null)
        {
            // Nobody left to fight: forget the aggro so the next player to
            // walk into range starts a fresh "who was seen first" contest.
            player = null;
            playerHealth = null;
            playerMovement = null;
            firstSeenPlayer = null;
            attackerPlayer = null;
            hasChosenTarget = false;
            return;
        }

        playerHealth = desired;
        player = desired.transform;
        playerMovement = desired.GetComponent<PlayerMovement2D>();
        hasChosenTarget = true;
    }

    /// <summary>
    /// A player can hold aggro while alive and inside the give-up radius.
    /// Beyond that the skeleton falls back to the other player.
    /// </summary>
    private bool IsTargetAvailable(PlayerHealthHurt candidate)
    {
        if (candidate == null || candidate.IsDead) return false;
        return Vector2.Distance(transform.position, candidate.transform.position) <= loseTargetRange;
    }
}
