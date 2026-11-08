using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode check for PlayerSeparation2D: the two players must have hard
/// physics collision switched off, must be allowed to overlap completely, and
/// must not nudge each other when one walks through the other.
/// </summary>
public static class PlayTestSeparation
{
    private static Gamepad pad;

    public static async Task<string> Run()
    {
        var log = new StringBuilder();
        if (!Application.isPlaying) return "NOT IN PLAY MODE";

        var leon = GameObject.Find("Leon");
        var krush = GameObject.Find("Krush");
        if (leon == null || krush == null) return "missing Leon or Krush";

        var leonBody = leon.GetComponent<Rigidbody2D>();
        var krushBody = krush.GetComponent<Rigidbody2D>();
        var leonCol = leon.GetComponent<Collider2D>();
        var krushCol = krush.GetComponent<Collider2D>();
        var leonSep = leon.GetComponent<PlayerSeparation2D>();
        var krushSep = krush.GetComponent<PlayerSeparation2D>();
        var leonSprite = leon.GetComponent<SpriteRenderer>();
        var krushSprite = krush.GetComponent<SpriteRenderer>();

        // The skeleton would kill Leon during the long test and freeze his
        // body, which makes the pass-through checks meaningless.
        var skeleton = GameObject.Find("Skeleton");
        bool skeletonWasActive = skeleton != null && skeleton.activeSelf;
        if (skeleton != null) skeleton.SetActive(false);

        pad = Gamepad.current;
        bool addedPad = false;
        if (pad == null)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            addedPad = true;
        }

        await Task.Delay(600);

        log.AppendLine("components leon=" + (leonSep != null) + " krush=" + (krushSep != null));
        log.AppendLine("ignoreCollision leon->krush=" + Physics2D.GetIgnoreCollision(leonCol, krushCol) +
                       " krush->leon=" + Physics2D.GetIgnoreCollision(krushCol, leonCol));
        log.AppendLine("hardCollisionIgnored leon=" + (leonSep != null && leonSep.HardCollisionIgnored) +
                       " krush=" + (krushSep != null && krushSep.HardCollisionIgnored));
        log.AppendLine("sortingOrder leon=" + leonSprite.sortingOrder +
                       " krush=" + krushSprite.sortingOrder +
                       " distinct=" + (leonSprite.sortingOrder != krushSprite.sortingOrder));

        // ------------------------------------------------ Krush walks through Leon
        Teleport(leonBody, -7.5f);
        Teleport(krushBody, -9.5f);
        await Task.Delay(500);

        float leonStartX = leon.transform.position.x;
        float krushStartX = krush.transform.position.x;
        float maxOverlap = 0f;

        SetStick(1f);
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(50);
            maxOverlap = Mathf.Max(maxOverlap, -IntersectionDepth(leonCol, krushCol));
        }
        SetStick(0f);
        await Task.Delay(250);

        float leonDx = leon.transform.position.x - leonStartX;
        float krushDx = krush.transform.position.x - krushStartX;
        bool krushCrossed = krush.transform.position.x > leon.transform.position.x + 0.5f;
        bool leonStayed = Mathf.Abs(leonDx) < 0.1f;
        bool passedThrough = maxOverlap > 0.3f;

        log.AppendLine("[walk-through] leonDx=" + leonDx.ToString("0.000") +
                       " krushDx=" + krushDx.ToString("0.000") +
                       " maxOverlap=" + maxOverlap.ToString("0.000"));
        log.AppendLine("  passedThrough=" + passedThrough +
                       " krushCrossed=" + krushCrossed +
                       " leonStayed=" + leonStayed);

        // --------------------------------------------- Both stand on same spot
        Teleport(leonBody, -7.5f);
        Teleport(krushBody, -7.5f);
        await Task.Delay(800);

        float stackedGap = HorizontalGap(leonCol, krushCol);
        float stackedOverlap = -stackedGap;
        float leonStackedDx = Mathf.Abs(leon.transform.position.x - (-7.5f));
        float krushStackedDx = Mathf.Abs(krush.transform.position.x - (-7.5f));
        bool stayedStacked = leonStackedDx < 0.1f && krushStackedDx < 0.1f;

        log.AppendLine("[stacked] gap=" + stackedGap.ToString("0.000") +
                       " overlap=" + stackedOverlap.ToString("0.000") +
                       " leonDx=" + leonStackedDx.ToString("0.000") +
                       " krushDx=" + krushStackedDx.ToString("0.000") +
                       " stayedStacked=" + stayedStacked);
        log.AppendLine("sortingOrderAfter leon=" + leonSprite.sortingOrder +
                       " krush=" + krushSprite.sortingOrder);

        if (skeleton != null) skeleton.SetActive(skeletonWasActive);

        if (addedPad)
        {
            InputSystem.RemoveDevice(pad);
            pad = null;
        }

        return log.ToString();
    }

    private static void Teleport(Rigidbody2D body, float x)
    {
        body.linearVelocity = Vector2.zero;
        body.position = new Vector2(x, body.position.y);
        body.WakeUp();
    }

    /// <summary>
    /// Signed horizontal gap between two colliders. Negative means overlap.
    /// Uses the live transform position for the centre so the value reflects a
    /// correction applied this frame instead of the bounds cached at the last
    /// physics sync.
    /// </summary>
    private static float HorizontalGap(Collider2D a, Collider2D b)
    {
        float ac = a.transform.position.x;
        float bc = b.transform.position.x;
        float ah = a.bounds.size.x * 0.5f;
        float bh = b.bounds.size.x * 0.5f;
        return ac <= bc ? (bc - bh) - (ac + ah) : (ac - ah) - (bc + bh);
    }

    /// <summary>Signed vertical gap between two colliders. Negative means overlap.</summary>
    private static float VerticalGap(Collider2D a, Collider2D b)
    {
        float ac = a.transform.position.y;
        float bc = b.transform.position.y;
        float ah = a.bounds.size.y * 0.5f;
        float bh = b.bounds.size.y * 0.5f;
        return ac <= bc ? (bc - bh) - (ac + ah) : (ac - ah) - (bc + bh);
    }

    /// <summary>
    /// How deeply two colliders actually intersect. Returns 0 when they are
    /// separated on at least one axis, otherwise a negative depth.
    /// </summary>
    private static float IntersectionDepth(Collider2D a, Collider2D b)
    {
        float h = HorizontalGap(a, b);
        float v = VerticalGap(a, b);
        if (h >= 0f || v >= 0f) return 0f;
        return Mathf.Max(h, v);
    }

    private static void SetStick(float x)
    {
        Set(new Vector2(x, 0f), false, false);
    }

    private static void Set(Vector2 stick, bool jump, bool rightShoulder)
    {
        var state = new GamepadState { leftStick = stick };
        if (jump) state = state.WithButton(GamepadButton.South);
        if (rightShoulder) state = state.WithButton(GamepadButton.RightShoulder);
        InputSystem.QueueStateEvent(pad, state);
    }
}
