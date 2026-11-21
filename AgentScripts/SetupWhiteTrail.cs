using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// Authors a borderless white trail sprite and rewires both health bars to use
/// it. Run: unity command run_script --file AgentScripts/SetupWhiteTrail.cs
/// </summary>
public static class SetupWhiteTrail
{
    private const string Folder = "Assets/Sprites/UI";
    private const string SpritePath = Folder + "/HealthTrailWhite.png";
    private static readonly Color Tint = new Color(1f, 1f, 1f, 0.75f);
    private const float Inset = 0.04f;

    public static string Main()
    {
        var report = new StringBuilder();

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Sprites", "UI");

        if (AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) == null)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Sprites/UI/HealthTrailWhite.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        report.AppendLine("White sprite: " + (whiteSprite != null ? AssetDatabase.GetAssetPath(whiteSprite) : "FAILED"));

        string[] bars = { "Leon's Healthbar", "Krush Healthbar" };
        foreach (string barName in bars)
        {
            GameObject bar = GameObject.Find(barName);
            if (bar == null) { report.AppendLine("MISSING bar: " + barName); continue; }

            Transform fillArea = bar.transform.Find("Fill Area");
            Image fill = null;
            Image trail = null;
            foreach (Transform child in fillArea)
            {
                if (child.name == "Fill") fill = child.GetComponent<Image>();
                else if (child.name.ToLowerInvariant().Contains("trail")) trail = child.GetComponent<Image>();
            }
            if (fill == null || trail == null) { report.AppendLine("MISSING images: " + barName); continue; }

            RectTransform fillRect = fill.rectTransform;
            RectTransform trailRect = trail.rectTransform;
            trailRect.anchorMin = fillRect.anchorMin;
            trailRect.anchorMax = fillRect.anchorMax;
            trailRect.pivot = fillRect.pivot;
            trailRect.anchoredPosition = fillRect.anchoredPosition;
            trailRect.sizeDelta = new Vector2(fillRect.sizeDelta.x * (1f - Inset), fillRect.sizeDelta.y * (1f - Inset));
            trailRect.localScale = fillRect.localScale;
            trailRect.localRotation = fillRect.localRotation;
            if (trailRect.GetSiblingIndex() > fillRect.GetSiblingIndex())
                trailRect.SetSiblingIndex(fillRect.GetSiblingIndex());

            trail.sprite = whiteSprite;
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            trail.fillOrigin = (int)Image.OriginHorizontal.Left;
            trail.fillClockwise = true;
            trail.fillCenter = true;
            trail.preserveAspect = false;
            trail.useSpriteMesh = false;
            trail.material = null;
            trail.raycastTarget = false;
            trail.color = Tint;
            trail.fillAmount = 1f;

            report.AppendLine(barName + ": sprite=" + whiteSprite.name
                + " tint=" + trail.color + " trailSize=" + trailRect.sizeDelta
                + " fillSize=" + fillRect.sizeDelta
                + " sibling=" + trailRect.GetSiblingIndex() + "/" + fillRect.GetSiblingIndex());
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return report.ToString();
    }
}
