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
            EdgeGeometry e = ReadEdges(GetFillImage(health), trail);
            Check(results, health.name + " trail inset inside fill",
                e.TrailLeft >= e.FillLeft - 0.01f &&
                e.TrailWidth < e.FillWidth &&
                e.TrailLeft + e.TrailWidth <= e.FillLeft + e.FillWidth + 0.01f);
            Check(results, health.name + " trail is behind fill",
                trailRect.GetSiblingIndex() < fillRect.GetSiblingIndex());
            Check(results, health.name + " health fill drained", sliderValue < 0.99f);
            Check(results, health.name + " trail position and size unchanged",
                Mathf.Approximately(trailRect.sizeDelta.x, 2009.2799f) &&
                Mathf.Approximately(trailRect.sizeDelta.y, 158.47734f));

            results.AppendLine("  " + health.name + " slider=" + sliderValue.ToString("F3")
                + " healthTarget=" + state.Health.ToString("F3")
                + " trailTarget=" + state.Trail.ToString("F3")
                + " trailFillAmount=" + trail.fillAmount.ToString("F3")
                + " tint=" + trail.color);
        }
        return results.ToString();
    }

    /// <summary>
    /// Measures the drawn right edges at the fill amounts reached by 1-4 hits,
    /// using world-space corners and the actual reduction applied to the trail.
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

            EdgeGeometry e = ReadEdges(fillImage, trail);
            float reduce = GetTrailReduce(health);

            results.AppendLine(health.name
                + " fillWorld=[" + e.FillLeft.ToString("F2") + ", " + (e.FillLeft + e.FillWidth).ToString("F2") + "]"
                + " trailWorld=[" + e.TrailLeft.ToString("F2") + ", " + (e.TrailLeft + e.TrailWidth).ToString("F2") + "]"
                + " reduce=" + reduce.ToString("F3")
                + " minReduce=" + (Mathf.Max(0f, (e.TrailLeft - e.FillLeft) / e.TrailWidth)).ToString("F4"));

            foreach (float amount in new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f })
            {
                float fRight = e.FillLeft + amount * e.FillWidth;
                float tRight = e.TrailLeft + Mathf.Clamp01(amount - reduce) * e.TrailWidth;
                results.AppendLine("  a=" + amount.ToString("F1")
                    + " fillRight=" + fRight.ToString("F2")
                    + " trailRight=" + tRight.ToString("F2")
                    + " overhang=" + (tRight - fRight).ToString("F2"));
            }
        }
        return results.ToString();
    }

    /// <summary>
    /// Definitive rendered geometry: world-space corners avoid any ambiguity
    /// from stretched anchors or localScale when comparing fill and trail.
    /// </summary>
    public static string World()
    {
        var results = new StringBuilder();

        foreach (PlayerHealthHurt health in Object.FindObjectsByType<PlayerHealthHurt>())
        {
            Image trail = (Image)typeof(PlayerHealthHurt)
                .GetField("healthTrailImage", Flags).GetValue(health);
            Image fillImage = GetFillImage(health);
            if (trail == null || fillImage == null) continue;

            var fc = new Vector3[4];
            var tc = new Vector3[4];
            fillImage.rectTransform.GetWorldCorners(fc);
            trail.rectTransform.GetWorldCorners(tc);

            // corner 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right.
            float fL = fc[0].x, fR = fc[3].x;
            float tL = tc[0].x, tR = tc[3].x;

            results.AppendLine(health.name
                + " fillWorld=[" + fL.ToString("F2") + ", " + fR.ToString("F2") + "]"
                + " trailWorld=[" + tL.ToString("F2") + ", " + tR.ToString("F2") + "]"
                + " trailOffset=" + (tL - fL).ToString("F2")
                + " trailWorldWidth=" + (tR - tL).ToString("F2"));
            results.AppendLine("  requiredReduction=" + (Mathf.Max(0f, (tL - fL) / (tR - tL))).ToString("F4")
                + " fillOrigin=" + fillImage.fillOrigin + " trailOrigin=" + trail.fillOrigin
                + " fillMethod=" + fillImage.fillMethod + " trailMethod=" + trail.fillMethod);

            Transform area = fillImage.rectTransform.parent;
            results.AppendLine("  parent=" + area.name
                + " mask=" + (area.GetComponent<Mask>() != null)
                + " rectMask=" + (area.GetComponent<RectMask2D>() != null));
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

            EdgeGeometry e = ReadEdges(fillImage, trail);
            float reduce = GetTrailReduce(health);

            foreach (float amount in new[] { 1f, 0.8f, 0.6f, 0.4f, 0.2f, 0f })
            {
                float trailAmount = Mathf.Clamp01(amount - reduce);
                float fRight = e.FillLeft + amount * e.FillWidth;
                float tRight = e.TrailLeft + trailAmount * e.TrailWidth;
                // At fillAmount 0 the Image draws nothing, so its left edge
                // is not an overhang.
                bool noOverhang = trailAmount <= 0f || tRight <= fRight + 0.01f;
                Check(results, health.name + " no trail overhang at " + amount.ToString("F1"),
                    noOverhang);
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
            float reduce = GetTrailReduce(health);
            Check(results, health.name + " trail settles with health fill",
                Mathf.Abs(trailFill - Mathf.Clamp01(healthFill - reduce)) < 0.01f);
            Check(results, health.name + " trail settles at 4-hit level",
                Mathf.Abs(trailFill - Mathf.Clamp01(0.2f - reduce)) < 0.01f);

            EdgeGeometry e = ReadEdges(GetFillImage(health), trail);
            float fRight = e.FillLeft + healthFill * e.FillWidth;
            float tRight = e.TrailLeft + trailFill * e.TrailWidth;
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
                Mathf.Approximately(trail.fillAmount, 1f - GetTrailReduce(health)));
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

    private static float GetTrailReduce(PlayerHealthHurt health)
    {
        return (float)typeof(PlayerHealthHurt)
            .GetField("trailReduce", Flags).GetValue(health);
    }

    private struct EdgeGeometry
    {
        public float FillLeft;
        public float FillWidth;
        public float TrailLeft;
        public float TrailWidth;
    }

    private static EdgeGeometry ReadEdges(Image fill, Image trail)
    {
        var fc = new Vector3[4];
        var tc = new Vector3[4];
        fill.rectTransform.GetWorldCorners(fc);
        trail.rectTransform.GetWorldCorners(tc);
        return new EdgeGeometry
        {
            FillLeft = fc[0].x,
            FillWidth = fc[3].x - fc[0].x,
            TrailLeft = tc[0].x,
            TrailWidth = tc[3].x - tc[0].x
        };
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
