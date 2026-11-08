using UnityEngine;

/// <summary>
/// Lets Leon and Krush pass straight through each other.
///
/// Unity's 2D physics would otherwise treat the two player colliders as solid
/// bodies and let them block, bounce and shove each other. This component
/// switches that collision pair off with <see cref="Physics2D.IgnoreCollision"/>,
/// so the players can stand on the same spot and walk through one another
/// without any push-apart correction.
///
/// Only the collision between the two player colliders is disabled. Both
/// players still collide normally with the ground, walls, the Skeleton and
/// pushable props.
/// </summary>
[AddComponentMenu("L&K/Player/Player Separation 2D")]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerSeparation2D : MonoBehaviour
{
    [Header("Pair")]
    [Tooltip("The other player's separation component. Leave empty to find the other player automatically at runtime.")]
    [SerializeField] private PlayerSeparation2D other;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private bool pairReady;

    /// <summary>True once collision with the other player has been switched off.</summary>
    public bool HardCollisionIgnored { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        ResolvePair();
    }

    private void FixedUpdate()
    {
        if (!pairReady)
        {
            ResolvePair();
        }
    }

    private void ResolvePair()
    {
        if (pairReady) return;
        if (body == null || bodyCollider == null) return;

        if (other == null)
        {
            PlayerSeparation2D[] all = FindObjectsByType<PlayerSeparation2D>();
            foreach (PlayerSeparation2D candidate in all)
            {
                if (candidate != null && candidate != this)
                {
                    other = candidate;
                    break;
                }
            }
        }

        if (other == null || other == this)
        {
            return;
        }

        if (other.body == null || other.bodyCollider == null)
        {
            // The other component has not run Awake yet; try again next step.
            return;
        }

        Physics2D.IgnoreCollision(bodyCollider, other.bodyCollider, true);
        HardCollisionIgnored = true;
        pairReady = true;
    }
}
