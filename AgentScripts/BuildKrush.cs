using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Krush as a second, fully playable character:
///  - slices the six Krush sprite sheets into SpriteRenderer clips with the
///    same frame counts / sample rates as Leon (no attack clips),
///  - builds a Krush AnimatorController (Idle, Run, Jump, Push, Hurt, Death),
///  - adds the physics + movement + push + health components to the Krush
///    GameObject and wires every reference,
///  - binds Leon to the keyboard and Krush to the gamepad so the single
///    controller does not drive both characters at once.
///
/// Run with:
///   unity command run_script --file AgentScripts/BuildKrush.cs --entry BuildKrush.All
/// </summary>
public static class BuildKrush
{
    private const string AnimFolder = "Assets/Animations/Krush";
    private const string ControllerPath = AnimFolder + "/Krush.controller";
    private const string SpriteFolder = "Assets/Sprites/Krush spritesheets";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly Dictionary<string, string> Sheets = new Dictionary<string, string>
    {
        { "Idle", SpriteFolder + "/k-idle-v1.png" },
        { "Run", SpriteFolder + "/k-run-v1.png" },
        { "Jump", SpriteFolder + "/k-jump-v1.png" },
        { "Push", SpriteFolder + "/mimo-push-v1.png" },
        { "Hurt", SpriteFolder + "/k-hurt.png" },
        { "Death", SpriteFolder + "/Kr-death-v1.png" },
    };

    public static string All()
    {
        var log = new System.Text.StringBuilder();

        var clips = BuildClips(log);
        var controller = BuildController(clips, log);
        WireScene(controller, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine("Done.");
        return log.ToString();
    }

    // ------------------------------------------------------------------ clips

    private static Dictionary<string, AnimationClip> BuildClips(System.Text.StringBuilder log)
    {
        EnsureFolder();

        // Rebuild from scratch so the run is repeatable.
        foreach (var key in Sheets.Keys)
        {
            AssetDatabase.DeleteAsset(AnimFolder + "/Krush-" + key + ".anim");
        }

        var result = new Dictionary<string, AnimationClip>();

        result["Idle"] = CreateClip("Krush-Idle", Sheets["Idle"], 12f, true);
        result["Run"] = CreateClip("Krush-Run", Sheets["Run"], 12f, true);
        result["Jump"] = CreateClip("Krush-Jump", Sheets["Jump"], 20f, false);
        result["Push"] = CreateClip("Krush-Push", Sheets["Push"], 20f, true, new[]
        {
            new AnimationEvent { functionName = "OnPushStart", time = 0f },
            new AnimationEvent { functionName = "OnPushEnd", time = 1.15f },
        });
        result["Hurt"] = CreateClip("Krush-Hurt", Sheets["Hurt"], 20f, false);
        result["Death"] = CreateClip("Krush-Death", Sheets["Death"], 20f, false);

        foreach (var pair in result)
        {
            log.AppendLine("clip " + pair.Key + " frames=" + CountFrames(pair.Value) +
                           " length=" + pair.Value.length.ToString("0.###") +
                           " loop=" + IsLooping(pair.Value));
        }

        return result;
    }

    private static AnimationClip CreateClip(string name, string sheetPath, float fps, bool loop,
        AnimationEvent[] events = null)
    {
        var frames = LoadFrames(sheetPath);
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("No sprites found in " + sheetPath);
        }

        var clip = new AnimationClip { name = name, frameRate = fps, wrapMode = loop ? WrapMode.Loop : WrapMode.Once };

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite",
        };

        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = frames[i],
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f;
        settings.stopTime = frames.Count / fps;
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (events != null && events.Length > 0)
        {
            AnimationUtility.SetAnimationEvents(clip, events);
        }

        AssetDatabase.CreateAsset(clip, AnimFolder + "/" + name + ".anim");
        return clip;
    }

    private static List<Sprite> LoadFrames(string sheetPath)
    {
        var sprites = new List<Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
        {
            if (asset is Sprite sprite) sprites.Add(sprite);
        }

        return sprites
            .OrderBy(s => FrameNumber(s.name))
            .ToList();
    }

    private static int FrameNumber(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');
        if (underscore < 0) return 0;
        return int.TryParse(spriteName.Substring(underscore + 1), out int index) ? index : 0;
    }

    private static int CountFrames(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings.Length == 0) return 0;
        var curve = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        return curve != null ? curve.Length : 0;
    }

    private static bool IsLooping(AnimationClip clip)
    {
        return AnimationUtility.GetAnimationClipSettings(clip).loopTime;
    }

    // ------------------------------------------------------------- controller

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips,
        System.Text.StringBuilder log)
    {
        AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Push", AnimatorControllerParameterType.Int);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        var machine = controller.layers[0].stateMachine;

        var idle = machine.AddState("Idle", new Vector3(220, 120, 0));
        var run = machine.AddState("Run", new Vector3(480, 220, 0));
        var jump = machine.AddState("Jump", new Vector3(750, 120, 0));
        var push = machine.AddState("Push", new Vector3(820, -180, 0));
        var hurt = machine.AddState("Hurt", new Vector3(220, 300, 0));
        var death = machine.AddState("Death", new Vector3(480, 300, 0));

        idle.motion = clips["Idle"];
        run.motion = clips["Run"];
        jump.motion = clips["Jump"];
        push.motion = clips["Push"];
        hurt.motion = clips["Hurt"];
        death.motion = clips["Death"];

        machine.defaultState = idle;

        // Idle / Run -> Jump (same timings as Leon).
        AddTransition(idle, run, 0.06f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        AddTransition(idle, push, 0.06f).AddCondition(AnimatorConditionMode.Equals, 1f, "Push");
        AddTransition(idle, jump, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "IsJumping");

        AddTransition(run, idle, 0.12f).AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
        AddTransition(run, push, 0.06f).AddCondition(AnimatorConditionMode.Equals, 1f, "Push");
        AddTransition(run, jump, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "IsJumping");

        // Jump is a one-shot; landing drops straight back to Idle/Run.
        AddTransition(jump, idle, 0.05f, exitTime: 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsJumping");
        AddTransition(jump, run, 0.05f, exitTime: 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsJumping");

        // Releasing the push button returns to Run or Idle depending on speed.
        var pushToRun = AddTransition(push, run, 0.06f);
        pushToRun.AddCondition(AnimatorConditionMode.Equals, 0f, "Push");
        pushToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        AddTransition(push, idle, 0.06f).AddCondition(AnimatorConditionMode.Equals, 0f, "Push");

        // Hurt is a one-shot reaction, then back to normal.
        AddTransition(hurt, idle, 0.1f, hasExitTime: true, exitTime: 0.9f);

        // Triggers interrupt anything, exactly like Leon.
        var anyToHurt = machine.AddAnyStateTransition(hurt);
        anyToHurt.hasExitTime = false;
        anyToHurt.duration = 0.05f;
        anyToHurt.hasFixedDuration = true;
        anyToHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");

        var anyToDeath = machine.AddAnyStateTransition(death);
        anyToDeath.hasExitTime = false;
        anyToDeath.duration = 0.05f;
        anyToDeath.hasFixedDuration = true;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        EditorUtility.SetDirty(controller);
        log.AppendLine("controller states: Idle, Run, Jump, Push, Hurt, Death");
        return controller;
    }

    private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to,
        float duration, bool hasExitTime = false, float exitTime = 0.9f)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
        return transition;
    }

    // ------------------------------------------------------------------ scene

    private static void WireScene(AnimatorController controller, System.Text.StringBuilder log)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var krush = GameObject.Find("Krush");
        if (krush == null) throw new InvalidOperationException("Krush GameObject not found in " + ScenePath);

        var spriteRenderer = krush.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = krush.AddComponent<SpriteRenderer>();

        // Animator ---------------------------------------------------------
        var animator = krush.GetComponent<Animator>();
        if (animator == null) animator = krush.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Physics ----------------------------------------------------------
        var body = krush.GetComponent<Rigidbody2D>();
        if (body == null) body = krush.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        body.mass = 1f;
        body.gravityScale = 1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        var bodyCollider = krush.GetComponent<BoxCollider2D>();
        if (bodyCollider == null) bodyCollider = krush.AddComponent<BoxCollider2D>();
        bodyCollider.isTrigger = false;
        bodyCollider.offset = Vector2.zero;
        bodyCollider.size = new Vector2(0.37f, 1.70f);

        // Movement ---------------------------------------------------------
        var movement = krush.GetComponent<PlayerMovement2D>();
        if (movement == null) movement = krush.AddComponent<PlayerMovement2D>();
        var moveSo = new SerializedObject(movement);
        SetFloat(moveSo, "moveSpeed", 3f);
        SetFloat(moveSo, "acceleration", 55f);
        SetFloat(moveSo, "deceleration", 40f);
        SetFloat(moveSo, "turnSpeed", 90f);
        SetFloat(moveSo, "attackLockDeceleration", 250f);
        SetFloat(moveSo, "inputDeadzone", 0.15f);
        SetString(moveSo, "groundTag", "Ground");
        SetFloat(moveSo, "groundProbeDepth", 0.25f);
        SetFloat(moveSo, "groundProbeLift", 0.06f);
        SetFloat(moveSo, "groundProbeWidth", 0f);
        SetFloat(moveSo, "groundCheckOffset", -0.89f);
        SetFloat(moveSo, "groundCheckRadius", 0.15f);
        SetObject(moveSo, "animator", animator);
        SetString(moveSo, "speedParameter", "Speed");
        SetFloat(moveSo, "runThreshold", 0.1f);
        SetFloat(moveSo, "animationReferenceSpeed", 3.878f);
        SetFloat(moveSo, "syncRunToSpeed", 1f);
        SetFloat(moveSo, "minRunPlaybackSpeed", 0.5f);
        SetFloat(moveSo, "maxRunPlaybackSpeed", 3f);
        SetFloat(moveSo, "jumpForce", 8f);
        SetFloat(moveSo, "fallGravityScale", 1.5f);
        SetString(moveSo, "jumpParameter", "IsJumping");
        SetFloat(moveSo, "coyoteTime", 0.1f);
        SetObject(moveSo, "spriteRenderer", spriteRenderer);
        SetBool(moveSo, "startsFacingRight", true);
        SetEnum(moveSo, "inputMode", (int)PlayerInputMode.GamepadOnly);
        SetInt(moveSo, "gamepadIndex", 0);
        moveSo.ApplyModifiedPropertiesWithoutUndo();

        // Push -------------------------------------------------------------
        var push = krush.GetComponent<PlayerPush2D>();
        if (push == null) push = krush.AddComponent<PlayerPush2D>();
        var pushSo = new SerializedObject(push);
        SetBool(pushSo, "allowKeyboard", false);
        SetInt(pushSo, "pushKeyboardKey", (int)Key.E);
        SetBool(pushSo, "allowGamepad", true);
        SetEnum(pushSo, "pushGamepadButton", (int)PushGamepadButton.RightShoulder);
        SetInt(pushSo, "cancelPushKey", (int)Key.A);
        SetString(pushSo, "pushParameter", "Push");
        SetFloat(pushSo, "pushRange", 1f);
        SetFloat(pushSo, "pushSearchRadius", 6f);
        SetFloat(pushSo, "pushStrength", 2f);
        SetFloat(pushSo, "pushEndHoldDuration", 0.2f);
        SetString(pushSo, "pushTag", "PushObject");
        SetObject(pushSo, "pushObject", null);
        SetObject(pushSo, "animator", animator);
        SetObject(pushSo, "movement", movement);
        pushSo.ApplyModifiedPropertiesWithoutUndo();

        // Health / hurt / death -------------------------------------------
        var health = krush.GetComponent<PlayerHealthHurt>();
        if (health == null) health = krush.AddComponent<PlayerHealthHurt>();
        var healthSo = new SerializedObject(health);
        SetBool(healthSo, "enableDebugHurtKey", false);
        SetInt(healthSo, "hurtKey", (int)Key.H);
        SetInt(healthSo, "hitsBeforeDeath", 5);
        SetObject(healthSo, "animator", animator);
        SetString(healthSo, "hurtTrigger", "Hurt");
        SetString(healthSo, "deathTrigger", "Death");
        SetString(healthSo, "hurtStateName", "Hurt");
        SetBool(healthSo, "pinDeathToGround", true);
        SetString(healthSo, "groundTag", "Ground");
        SetObject(healthSo, "spriteRenderer", spriteRenderer);
        SetObject(healthSo, "bodyCollider", bodyCollider);
        SetObject(healthSo, "body", body);
        SetObject(healthSo, "movement", movement);
        SetObject(healthSo, "attack", null);
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        // Input split: player 1 on the keyboard, player 2 on the gamepad.
        var leon = GameObject.Find("Leon");
        if (leon != null)
        {
            var leonMove = leon.GetComponent<PlayerMovement2D>();
            if (leonMove != null)
            {
                var leonMoveSo = new SerializedObject(leonMove);
                SetEnum(leonMoveSo, "inputMode", (int)PlayerInputMode.KeyboardOnly);
                SetInt(leonMoveSo, "gamepadIndex", 0);
                leonMoveSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var leonAttack = leon.GetComponent<PlayerAttack2D>();
            if (leonAttack != null)
            {
                var leonAttackSo = new SerializedObject(leonAttack);
                SetBool(leonAttackSo, "allowGamepad", false);
                SetBool(leonAttackSo, "allowPushGamepad", false);
                leonAttackSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorUtility.SetDirty(krush);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        log.AppendLine("Krush wired: Animator, Rigidbody2D, BoxCollider2D, PlayerMovement2D (gamepad), " +
                       "PlayerPush2D (RB), PlayerHealthHurt");
        log.AppendLine("Leon rebound: movement keyboard-only, attack/push gamepad off");
    }

    // ---------------------------------------------------------------- helpers

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
        {
            AssetDatabase.CreateFolder("Assets", "Animations");
        }

        if (!AssetDatabase.IsValidFolder(AnimFolder))
        {
            AssetDatabase.CreateFolder("Assets/Animations", "Krush");
        }
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.stringValue = value;
    }

    private static void SetEnum(SerializedObject so, string name, int value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.enumValueIndex = value;
    }

    private static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
    }
}
