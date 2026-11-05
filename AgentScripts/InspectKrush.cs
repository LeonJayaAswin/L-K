using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectKrush
{
    public static string State()
    {
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sb.AppendLine("scene=" + scene.name + " path=" + scene.path + " dirty=" + scene.isDirty + " playing=" + Application.isPlaying);

        var krush = GameObject.Find("Krush");
        if (krush == null)
        {
            sb.AppendLine("Krush: MISSING");
        }
        else
        {
            sb.AppendLine("Krush pos=" + krush.transform.position + " scale=" + krush.transform.localScale);
            foreach (var c in krush.GetComponents<Component>())
            {
                sb.AppendLine("  component " + c.GetType().Name);
            }

            var sr = krush.GetComponent<SpriteRenderer>();
            sb.AppendLine("  sprite=" + (sr != null && sr.sprite != null ? sr.sprite.name : "null"));
        }

        var leon = GameObject.Find("Leon");
        if (leon != null)
        {
            var move = leon.GetComponent<PlayerMovement2D>();
            var atk = leon.GetComponent<PlayerAttack2D>();
            if (move != null) sb.AppendLine("Leon move mode=" + move.InputMode);
            if (atk != null)
            {
                var so = new SerializedObject(atk);
                sb.AppendLine("Leon attack allowGamepad=" + so.FindProperty("allowGamepad").boolValue +
                              " allowPushGamepad=" + so.FindProperty("allowPushGamepad").boolValue);
            }
        }

        var folder = "Assets/Animations/Krush";
        sb.AppendLine("Krush anim folder exists=" + AssetDatabase.IsValidFolder(folder));

        sb.AppendLine("idle sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/k-idle-v1.png"));
        sb.AppendLine("run sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/k-run-v1.png"));
        sb.AppendLine("jump sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/k-jump-v1.png"));
        sb.AppendLine("push sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/mimo-push-v1.png"));
        sb.AppendLine("hurt sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/k-hurt.png"));
        sb.AppendLine("death sprites=" + CountSprites("Assets/Sprites/Krush spritesheets/Kr-death-v1.png"));
        return sb.ToString();
    }

    static int CountSprites(string path)
    {
        int count = 0;
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var list = new List<string>();
        foreach (var a in assets)
        {
            if (a is Sprite s)
            {
                count++;
                list.Add(s.name);
            }
        }
        return count;
    }
}
