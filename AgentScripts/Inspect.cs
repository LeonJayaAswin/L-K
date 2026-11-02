using System.Reflection;
using System.Text;
using UnityEngine;

public static class Inspect
{
    static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    public static string Weapons()
    {
        var sb = new StringBuilder();
        var all = UnityEngine.Object.FindObjectsByType<SkeletonAI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("skeletonCount=" + all.Length);
        foreach (var sk in all)
        {
            sb.AppendLine(sk.name + " path=" + PathOf(sk.transform) +
                          " active=" + sk.gameObject.activeInHierarchy +
                          " enabled=" + sk.enabled +
                          " cooldown=" + Field(sk, "attackCooldown") +
                          " duration=" + Field(sk, "attackDuration") +
                          " hitsToDie=" + Field(sk, "hitsToDie") +
                          " idleTime=" + Field(sk, "idleTime") +
                          " attackSpeed=" + Field(sk, "attackAnimationSpeed"));
        }

        var pl = UnityEngine.Object.FindAnyObjectByType<PlayerAttack2D>();
        if (pl != null)
        {
            sb.AppendLine("playerAttack comboBuffer=" + Field(pl, "comboBufferTime") +
                          " startLock=" + Field(pl, "swingStartLockTime") +
                          " releaseLead=" + Field(pl, "releaseLeadTime") +
                          " hitRange=" + Field(pl, "attackHitRange"));
        }

        sb.AppendLine("playMode=" + Application.isPlaying);
        return sb.ToString();
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    public static string SceneInfo()
    {
        var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var sb = new StringBuilder();
        sb.AppendLine("scene=" + s.name + " path=" + s.path + " dirty=" + s.isDirty);
        var sk = UnityEngine.Object.FindAnyObjectByType<SkeletonAI>();
        if (sk != null)
        {
            sb.AppendLine("cooldown=" + Field(sk, "attackCooldown") +
                          " duration=" + Field(sk, "attackDuration") +
                          " active=" + sk.gameObject.activeInHierarchy);
        }
        return sb.ToString();
    }

    public static string SetCooldown(float value)
    {
        var sk = UnityEngine.Object.FindAnyObjectByType<SkeletonAI>();
        if (sk == null) return "no skeleton";
        typeof(SkeletonAI).GetField("attackCooldown", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(sk, value);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(sk);
#endif
        return "cooldown set to " + Field(sk, "attackCooldown");
    }

    public static string SetCooldownDefault()
    {
        return SetCooldown(0.7f);
    }
}
