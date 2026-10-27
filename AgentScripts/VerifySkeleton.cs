using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class VerifySkeleton
{
    public static string All()
    {
        var sb = new StringBuilder();
        string animDir = "Assets/Animations/Enemies/Skeleton";
        string sprDir = "Assets/Sprites/Enemies/Skeleton";

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(animDir + "/Skeleton.controller");
        sb.AppendLine("CONTROLLER: " + (ctrl == null ? "NULL" : ctrl.name));
        if (ctrl != null)
        {
            sb.AppendLine("  layers=" + ctrl.layers.Length + " params=" + ctrl.parameters.Length);
            var states = ctrl.layers[0].stateMachine.states;
            sb.AppendLine("  states=" + states.Length + " default=" + ctrl.layers[0].stateMachine.defaultState.name);
            foreach (var cs in states)
            {
                var clip = cs.state.motion as AnimationClip;
                sb.AppendLine("   - " + cs.state.name + " motion=" + (clip == null ? "NULL" : clip.name));
            }
        }

        string[] clips = { "Idle", "Walk", "Attack1", "Attack2", "TakeHit", "Death" };
        foreach (var n in clips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animDir + "/Skeleton-" + n + ".anim");
            if (clip == null) { sb.AppendLine(n + ": MISSING"); continue; }
            var bind = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .FirstOrDefault(b => b.propertyName == "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bind);
            sb.AppendLine("CLIP " + n + ": len=" + clip.length.ToString("F3") +
                          " keys=" + (keys == null ? 0 : keys.Length) +
                          " loop=" + clip.isLooping);
        }

        foreach (var sheet in new[] { "Idle", "Walk", "Attack1", "Attack2", "Take Hit", "Death" })
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(sprDir + "/" + sheet + ".png");
            var sprites = assets.OfType<Sprite>().OrderBy(s => s.name).ToArray();
            sb.AppendLine("SHEET " + sheet + ": sprites=" + sprites.Length);
            foreach (var s in sprites.Take(2))
            {
                sb.AppendLine("   " + s.name + " rect=" + s.rect +
                              " bounds.min.y=" + s.bounds.min.y.ToString("F4") +
                              " ppu=" + s.pixelsPerUnit);
            }
        }

        var skeleton = UnityEngine.Object.FindAnyObjectByType<SkeletonAI>();
        if (skeleton == null)
        {
            sb.AppendLine("SCENE: no SkeletonAI found");
        }
        else
        {
            var t = skeleton.transform;
            var sr = skeleton.GetComponent<SpriteRenderer>();
            var col = skeleton.GetComponent<Collider2D>();
            var rb = skeleton.GetComponent<Rigidbody2D>();
            sb.AppendLine("SCENE skeleton: pos=" + t.position + " scale=" + t.lossyScale +
                          " active=" + skeleton.gameObject.activeInHierarchy);
            sb.AppendLine("  controller=" + (sr == null || sr.sprite == null ? "?" : "ok") +
                          " animatorCtrl=" + (skeleton.GetComponent<Animator>().runtimeAnimatorController == null
                              ? "NULL" : skeleton.GetComponent<Animator>().runtimeAnimatorController.name));
            sb.AppendLine("  sprite=" + (sr == null || sr.sprite == null ? "NULL" : sr.sprite.name) +
                          " bounds.min.y=" + (sr == null || sr.sprite == null ? 0f : sr.sprite.bounds.min.y));
            if (col != null) sb.AppendLine("  collider bounds=" + col.bounds);
            if (rb != null) sb.AppendLine("  rb bodyType=" + rb.bodyType + " gravity=" + rb.gravityScale +
                                          " simulated=" + rb.simulated + " constraints=" + rb.constraints);

            var serialized = new SerializedObject(skeleton);
            foreach (var name in new[] { "player", "playerHealth", "playerAttack", "playerMovement",
                                         "animator", "spriteRenderer", "body", "bodyCollider",
                                         "hitsToDie", "deathArtBottomPadding", "patrolDistance" })
            {
                var p = serialized.FindProperty(name);
                sb.AppendLine("  field " + name + " = " +
                              (p == null ? "<missing>" : p.propertyType == SerializedPropertyType.ObjectReference
                                  ? (p.objectReferenceValue == null ? "NULL" : p.objectReferenceValue.name)
                                  : p.ToString()));
            }
        }

        var ground = GameObject.Find("SampleGround");
        if (ground != null && ground.TryGetComponent<Collider2D>(out var gc))
            sb.AppendLine("GROUND top=" + gc.bounds.max.y.ToString("F4"));

        return sb.ToString();
    }
}
