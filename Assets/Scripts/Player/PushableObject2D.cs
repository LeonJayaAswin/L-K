using UnityEngine;

/// <summary>
/// A prop that Leon can shove around.
///
/// The push is accumulated during Update and applied in FixedUpdate through
/// MovePosition, so the prop moves smoothly with the physics step and still
/// pushes dynamic bodies (like Leon) out of the way.
///
/// The prop only ever moves along the direction it is pushed in, which is
/// what keeps a push forward only.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("L&K/Player/Pushable Object 2D")]
public class PushableObject2D : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 pendingDelta;

    /// <summary>True while a push is being applied to this object.</summary>
    public bool IsBeingPushed { get; private set; }

    /// <summary>The direction the object was last pushed in.</summary>
    public Vector2 LastPushDirection { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        // The prop must never fall asleep, otherwise a later push would be
        // swallowed by a sleeping body.
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    /// <summary>
    /// Queues a shove. The object is moved by <paramref name="distance"/>
    /// units along <paramref name="direction"/> on the next physics step.
    /// </summary>
    public void Push(Vector2 direction, float distance)
    {
        Vector2 flat = new Vector2(direction.x, 0f);

        if (flat.sqrMagnitude <= 0.000001f) return;

        flat.Normalize();

        pendingDelta += flat * distance;
        LastPushDirection = flat;
        IsBeingPushed = true;

        if (body != null) body.WakeUp();
    }

    /// <summary>Stops any queued movement.</summary>
    public void StopPush()
    {
        pendingDelta = Vector2.zero;
        IsBeingPushed = false;
    }

    private void FixedUpdate()
    {
        if (pendingDelta.sqrMagnitude <= 0f)
        {
            IsBeingPushed = false;
            return;
        }

        Vector2 delta = pendingDelta;
        pendingDelta = Vector2.zero;

        if (body == null) return;

        // MovePosition works for both body types: the prop slides to the new
        // spot instead of teleporting, so the solver stays happy and Leon is
        // pushed out of the way instead of being stuck inside the prop.
        body.MovePosition(body.position + delta);
    }
}
