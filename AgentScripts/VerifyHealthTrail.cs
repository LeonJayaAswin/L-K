using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Reflection;

/// <summary>
/// Runtime regression check for the health-bar trail.
///   unity command run_script --file AgentScripts/VerifyHealthTrail.cs --entry Apply
///   (wait ~2s of Play Mode)
///   unity command run_script --file AgentScripts/VerifyHealthTrail.cs --entry Verify
/// </summary>
public static class VerifyHealthTrail
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Apply()
    {
        var report = new StringBuilder();

        // Stop the skeleton from adding extra hits while the test is running.
        SkeletonAI skeleton = Object.FindAnyObjectByType<SkeletonAI>();
        if (skeleton != null) skeleton.enabled = false;

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            int before = health.HurtCount;
            health.TakeHit();
            report.AppendLine(health.name + ": hit " + before + " -> " + health.HurtCount);
        }
        return report.ToString();
    }

    public static string Verify()
    {
        var results = new StringBuilder();

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)typeof(PlayerHealthHurt)
                .GetField("healthTrailImage", Flags).GetValue(health);
            if (trail == null) { results.AppendLine("FAIL: " + health.name + " has no trail image"); continue; }

            RectTransform trailRect = trail.rectTransform;
            Slider bar = GetHealthBar(health);
            if (bar == null) { results.AppendLine("FAIL: " + health.name + " has no health bar"); continue; }
            RectTransform fillRect = bar.fillRect;

            FillState state = ReadFill(health);
            float sliderValue = bar.value;

            Check(results, health.name + " trail tint is white",
                Mathf.Approximately(trail.color.r, 1f) &&
                Mathf.Approximately(trail.color.g, 1f) &&
                Mathf.Approximately(trail.color.b, 1f) &&
                Mathf.Approximately(trail.color.a, 0.75f));
            Check(results, health.name + " trail RGB not overbright",
                trail.color.r <= 1f && trail.color.g <= 1f && trail.color.b <= 1f);
            Check(results, health.name + " no overbright silhouette material",
                trail.material == null || trail.material.name != "Health Trail Silhouette");
            Check(results, health.name + " trail uses borderless white sprite",
                trail.sprite != null && trail.sprite.name == "HealthTrailWhite" && IsAllWhite(trail.sprite));
            Check(results, health.name + " fill keeps its own bar art",
                GetFillImage(health).sprite != trail.sprite);
            float fillLeft = fillRect.anchoredPosition.x - fillRect.pivot.x * fillRect.rect.width;
            float trailLeft = trailRect.anchoredPosition.x - trailRect.pivot.x * trailRect.rect.width;
            Check(results, health.name + " trail left-aligned and inset inside fill",
                trailRect.anchorMin == fillRect.anchorMin &&
                trailRect.anchorMax == fillRect.anchorMax &&
                Mathf.Approximately(trailRect.pivot.x, 0f) &&
                Mathf.Approximately(trailRect.pivot.y, fillRect.pivot.y) &&
                Mathf.Abs(trailLeft - fillLeft) < 0.01f &&
                trailRect.rect.width < fillRect.rect.width &&
                trailRect.rect.height < fillRect.rect.height &&
                trailRect.localScale == fillRect.localScale);
            Check(results, health.name + " trail is behind fill",
                trailRect.GetSiblingIndex() < fillRect.GetSiblingIndex());
            Check(results, health.name + " health fill drained", sliderValue < 0.99f);

            results.AppendLine("  " + health.name + " slider=" + sliderValue.ToString("F3")
                + " healthTarget=" + state.Health.ToString("F3")
                + " trailTarget=" + state.Trail.ToString("F3")
                + " trailFillAmount=" + trail.fillAmount.ToString("F3")
                + " tint=" + trail.color);
        }
        return results.ToString();
    }

    /// <summary>
    /// Measures the drawn left/right edges of fill and trail at the fill
    /// amounts reached by 1-4 hits. Both use a left-origin Filled image, so
    /// rightEdge = leftEdge + fillAmount * width. Anchors and parent match,
    /// comparing anchoredPosition - pivot*width puts both in parent space.
    /// </summary>
    public static string Geometry()
    {
        var results = new StringBuilder();

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)typeof(PlayerHealthHurt)
                .GetField("healthTrailImage", Flags).GetValue(health);
            Image fillImage = GetFillImage(health);
            if (trail == null || fillImage == null) continue;

            RectTransform f = fillImage.rectTransform;
            RectTransform t = trail.rectTransform;

            float fLeft = f.anchoredPosition.x - f.pivot.x * f.rect.width;
            float tLeft = t.anchoredPosition.x - t.pivot.x * t.rect.width;

            results.AppendLine(health.name
                + " fill(width=" + f.rect.width.ToString("F2")
                + " left=" + fLeft.ToString("F2")
                + " pivot=" + f.pivot.x.ToString("F2") + ")"
                + " trail(width=" + t.rect.width.ToString("F2")
                + " left=" + tLeft.ToString("F2")
                + " pivot=" + t.pivot.x.ToString("F2") + ")");

            foreach (float amount in new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f })
            {
                float fRight = fLeft + amount * f.rect.width;
                float tRight = tLeft + amount * t.rect.width;
                results.AppendLine("  a=" + amount.ToString("F1")
                    + " fillRight=" + fRight.ToString("F2")
                    + " trailRight=" + tRight.ToString("F2")
                    + " overhang=" + (tRight - fRight).ToString("F2"));
            }
        }
        return results.ToString();
    }

    /// <summary>
    /// Settled-state check: when the trail has caught up with the fill, its
    /// right edge must never extend past the fill's right edge at any health
    /// level. This is the sliver seen after 3-4 hits.
    /// </summary>
    public static string Edges()
    {
        var results = new StringBuilder();

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)typeof(PlayerHealthHurt)
                .GetField("healthTrailImage", Flags).GetValue(health);
            Image fillImage = GetFillImage(health);
            if (trail == null || fillImage == null) continue;

            RectTransform f = fillImage.rectTransform;
            RectTransform t = trail.rectTransform;
            float fLeft = f.anchoredPosition.x - f.pivot.x * f.rect.width;
            float tLeft = t.anchoredPosition.x - t.pivot.x * t.rect.width;

            foreach (float amount in new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f, 0f })
            {
                float fRight = fLeft + amount * f.rect.width;
                float tRight = tLeft + amount * t.rect.width;
                Check(results, health.name + " no trail overhang at " + amount.ToString("F1"),
                    tRight <= fRight + 0.01f);
            }
        }
        return results.ToString();
    }

    /// <summary>
    /// Dynamic check for the 4-hit case: drive the real Update until the trail
    /// has drained, then assert it settles at exactly the health fill level
    /// and still sits inside the fill.
    /// </summary>
    public static string Converge()
    {
        var results = new StringBuilder();
        SkeletonAI skeleton = Object.FindAnyObjectByType<SkeletonAI>();
        if (skeleton != null) skeleton.enabled = false;

        System.Type type = typeof(PlayerHealthHurt);
        MethodInfo update = type.GetMethod("Update", Flags);
        FieldInfo healthField = type.GetField("targetHealthFill", Flags);
        FieldInfo trailField = type.GetField("targetTrailFill", Flags);
        FieldInfo timerField = type.GetField("trailDelayTimer", Flags);

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)type.GetField("healthTrailImage", Flags).GetValue(health);
            Slider bar = GetHealthBar(health);
            if (trail == null || bar == null) continue;

            // Four hits out of five: health at 0.2, trail still full.
            healthField.SetValue(health, 0.2f);
            trailField.SetValue(health, 1f);
            timerField.SetValue(health, 0.35f);

            for (int i = 0; i < 600; i++) update.Invoke(health, null);

            float healthFill = bar.value;
            float trailFill = trail.fillAmount;
            Check(results, health.name + " trail settles with health fill",
                Mathf.Abs(trailFill - healthFill) < 0.01f);
            Check(results, health.name + " trail settles at 4-hit level",
                Mathf.Abs(trailFill - 0.2f) < 0.01f);

            RectTransform f = bar.fillRect;
            RectTransform t = trail.rectTransform;
            float fLeft = f.anchoredPosition.x - f.pivot.x * f.rect.width;
            float tLeft = t.anchoredPosition.x - t.pivot.x * t.rect.width;
            float fRight = fLeft + healthFill * f.rect.width;
            float tRight = tLeft + trailFill * t.rect.width;
            Check(results, health.name + " settled trail has no overhang",
                tRight <= fRight + 0.01f);

            results.AppendLine("  " + health.name + " healthFill=" + healthFill.ToString("F3")
                + " trailFill=" + trailFill.ToString("F3")
                + " fillRight=" + fRight.ToString("F2")
                + " trailRight=" + tRight.ToString("F2"));
        }
        return results.ToString();
    }

    /// <summary>
    /// Proves Inspector edits are authoritative: change the trail Image's
    /// colour and size, run the component's runtime setup, and confirm they
    /// were NOT overwritten.
    /// </summary>
    public static string Editable()
    {
        var results = new StringBuilder();
        MethodInfo setUp = typeof(PlayerHealthHurt).GetMethod("SetUpTrail", Flags);

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)typeof(PlayerHealthHurt)
                .GetField("healthTrailImage", Flags).GetValue(health);

            var testColor = new Color(1f, 1f, 1f, 0.82f);
            var testSize = new Vector2(1234f, 99f);
            trail.color = testColor;
            trail.rectTransform.sizeDelta = testSize;

            setUp.Invoke(health, null);

            Check(results, health.name + " authored opacity kept at runtime",
                trail.color == testColor);
            Check(results, health.name + " authored size kept at runtime",
                trail.rectTransform.sizeDelta == testSize);
            Check(results, health.name + " drain still uses fillAmount",
                Mathf.Approximately(trail.fillAmount, 1f));
        }
        return results.ToString();
    }

    /// <summary>
    /// Deterministic lag check: drives the component's own Update with known
    /// field values instead of relying on wall-clock timing between commands.
    /// </summary>
    public static string Lag()
    {
        var results = new StringBuilder();
        SkeletonAI skeleton = Object.FindAnyObjectByType<SkeletonAI>();
        if (skeleton != null) skeleton.enabled = false;

        System.Type type = typeof(PlayerHealthHurt);
        FieldInfo trailField = type.GetField("targetTrailFill", Flags);
        FieldInfo timerField = type.GetField("trailDelayTimer", Flags);
        MethodInfo update = type.GetMethod("Update", Flags);

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            type.GetField("targetHealthFill", Flags).SetValue(health, 0.5f);
            trailField.SetValue(health, 1f);
            timerField.SetValue(health, 0.35f);

            update.Invoke(health, null);
            float duringDelay = (float)trailField.GetValue(health);
            float timer = (float)timerField.GetValue(health);
            Check(results, health.name + " trail holds during delay",
                Mathf.Approximately(duringDelay, 1f) && timer < 0.35f);

            timerField.SetValue(health, 0f);
            update.Invoke(health, null);
            float afterDelay = (float)trailField.GetValue(health);
            Check(results, health.name + " trail drains after delay",
                afterDelay < 1f && afterDelay > 0.5f);
        }
        return results.ToString();
    }

    private static Slider GetHealthBar(PlayerHealthHurt health)
    {
        return (Slider)typeof(PlayerHealthHurt)
            .GetField("healthBar", Flags).GetValue(health);
    }

    private static Image GetFillImage(PlayerHealthHurt health)
    {
        RectTransform fill = GetHealthBar(health).fillRect;
        return fill != null ? fill.GetComponent<Image>() : null;
    }

    private static FillState ReadFill(PlayerHealthHurt health)
    {
        return new FillState
        {
            Health = (float)typeof(PlayerHealthHurt).GetField("targetHealthFill", Flags).GetValue(health),
            Trail = (float)typeof(PlayerHealthHurt).GetField("targetTrailFill", Flags).GetValue(health)
        };
    }

    private struct FillState { public float Health; public float Trail; }

    private static bool IsAllWhite(Sprite sprite)
    {
        if (sprite.texture == null) return false;
        Color[] pixels = sprite.texture.GetPixels();
        foreach (Color pixel in pixels)
        {
            if (pixel.r < 0.99f || pixel.g < 0.99f || pixel.b < 0.99f) return false;
        }
        return true;
    }

    private static void Check(StringBuilder results, string label, bool ok)
    {
        results.AppendLine((ok ? "PASS: " : "FAIL: ") + label);
    }
}
