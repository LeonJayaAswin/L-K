using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

/// <summary>
/// One-time authoring pass: makes each health-bar trail a real, edit-mode
/// visible copy of the fill (same sprite, same rect) with a subtle tint.
/// Run from the Editor: unity command run_script --file AgentScripts/SetupHealthTrail.cs
/// </summary>
public static class SetupHealthTrail
{
    public static string Main()
    {
        var report = new StringBuilder();

        string[] bars = { "Leon's Healthbar", "Krush Healthbar" };
        foreach (string barName in bars)
        {
            GameObject bar = GameObject.Find(barName);
            if (bar == null) { report.AppendLine("MISSING bar: " + barName); continue; }

            Transform fillArea = bar.transform.Find("Fill Area");
            if (fillArea == null) { report.AppendLine("MISSING Fill Area: " + barName); continue; }

            Image fill = null;
            Image trail = null;
            foreach (Transform child in fillArea)
            {
                if (child.name == "Fill") fill = child.GetComponent<Image>();
                else if (child.name.ToLowerInvariant().Contains("trail")) trail = child.GetComponent<Image>();
            }

            if (fill == null || trail == null)
            {
                report.AppendLine("MISSING images on " + barName + " (fill=" + (fill != null) + ", trail=" + (trail != null) + ")");
                continue;
            }

            RectTransform fillRect = fill.rectTransform;
            RectTransform trailRect = trail.rectTransform;

            // Mirror the fill's rect so the afterimage is pixel-aligned with
            // the bar in Edit Mode as well as at runtime.
            trailRect.anchorMin = fillRect.anchorMin;
            trailRect.anchorMax = fillRect.anchorMax;
            trailRect.pivot = fillRect.pivot;
            trailRect.anchoredPosition = fillRect.anchoredPosition;
            trailRect.sizeDelta = fillRect.sizeDelta;
            trailRect.localScale = fillRect.localScale;
            trailRect.localRotation = fillRect.localRotation;

            // Borrow the exact fill art for an identical silhouette.
            trail.sprite = fill.sprite;
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            trail.fillOrigin = fill.fillOrigin;
            trail.fillClockwise = fill.fillClockwise;
            trail.fillCenter = fill.fillCenter;
            trail.preserveAspect = false;
            trail.useSpriteMesh = false;
            trail.pixelsPerUnitMultiplier = fill.pixelsPerUnitMultiplier;
            trail.material = null;
            trail.raycastTarget = false;
            trail.color = new Color(1f, 1f, 1f, 0.25f);
            trail.fillAmount = 1f;

            // The fill must paint on top of the trail.
            if (trailRect.GetSiblingIndex() > fillRect.GetSiblingIndex())
                trailRect.SetSiblingIndex(fillRect.GetSiblingIndex());

            report.AppendLine(barName + ": sprite=" + (fill.sprite != null ? fill.sprite.name : "none")
                + " pos=" + fillRect.anchoredPosition
                + " scale=" + fillRect.localScale
                + " trailColor=" + trail.color
                + " sibling=" + trailRect.GetSiblingIndex() + "/" + fillRect.GetSiblingIndex());
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return report.ToString();
    }
}
