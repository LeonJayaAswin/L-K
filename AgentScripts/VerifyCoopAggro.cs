using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Runtime check for the co-op aggro rules in SkeletonAI.
/// Run in Play Mode: unity command run_script --file AgentScripts/VerifyCoopAggro.cs
/// </summary>
public static class VerifyCoopAggro
{
    public static string Main()
    {
        var results = new List<string>();

        SkeletonAI skeleton = Object.FindFirstObjectByType<SkeletonAI>();
        if (skeleton == null) return "FAIL: no SkeletonAI in scene";

        PlayerHealthHurt leon = null;
        PlayerHealthHurt krush = null;
        foreach (PlayerHealthHurt p in Object.FindObjectsByType<PlayerHealthHurt>(FindObjectsSortMode.None))
        {
            if (p.GetComponent<PlayerAttack2D>() != null) leon = p;
            else krush = p;
        }
        if (leon == null || krush == null) return "FAIL: expected two players (one with PlayerAttack2D)";

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo updateTarget = typeof(SkeletonAI).GetMethod("UpdateTarget", flags);
        MethodInfo tryHitPlayer = typeof(SkeletonAI).GetMethod("TryHitPlayer", flags);
        FieldInfo firstSeen = typeof(SkeletonAI).GetField("firstSeenPlayer", flags);
        FieldInfo attacker = typeof(SkeletonAI).GetField("attackerPlayer", flags);
        FieldInfo chosen = typeof(SkeletonAI).GetField("hasChosenTarget", flags);

        Vector3 s = skeleton.transform.position;
        Vector3 leonStart = leon.transform.position;
        Vector3 krushStart = krush.transform.position;

        try
        {
            firstSeen.SetValue(skeleton, null);
            attacker.SetValue(skeleton, null);
            chosen.SetValue(skeleton, false);

            // 1) Krush enters vision first, Leon is far away.
            krush.transform.position = s + new Vector3(2f, 0f, 0f);
            leon.transform.position = s + new Vector3(-10f, 0f, 0f);
            updateTarget.Invoke(skeleton, null);
            Check(results, "first seen wins", skeleton.FocusedPlayer == krush);

            // 2) Leon walks in closer without attacking; first-seen must hold.
            leon.transform.position = s + new Vector3(-1.2f, 0f, 0f);
            updateTarget.Invoke(skeleton, null);
            Check(results, "first seen holds vs closer player", skeleton.FocusedPlayer == krush);

            // 3) Leon attacks: damage steals aggro from the first-seen player.
            skeleton.TakeSwordHit(s, leon);
            Check(results, "attacker steals aggro", skeleton.FocusedPlayer == leon);

            // 4) Both in swing reach: the focused player takes the skeleton's hit.
            SpriteRenderer sr = skeleton.GetComponent<SpriteRenderer>();
            sr.flipX = false;
            leon.transform.position = s + new Vector3(0.8f, 0f, 0f);
            krush.transform.position = s + new Vector3(1.2f, 0f, 0f);
            int leonHits = leon.HurtCount;
            int krushHits = krush.HurtCount;
            tryHitPlayer.Invoke(skeleton, null);
            Check(results, "swing hits focused player", leon.HurtCount == leonHits + 1 && krush.HurtCount == krushHits);

            // 5) Both flee: aggro notes are cleared for the next encounter.
            leon.transform.position = s + new Vector3(-20f, 0f, 0f);
            krush.transform.position = s + new Vector3(20f, 0f, 0f);
            updateTarget.Invoke(skeleton, null);
            Check(results, "target dropped when both flee", skeleton.FocusedPlayer == null);
            Check(results, "aggro notes cleared", firstSeen.GetValue(skeleton) == null && attacker.GetValue(skeleton) == null);
        }
        finally
        {
            leon.transform.position = leonStart;
            krush.transform.position = krushStart;
        }

        return string.Join("\n", results);
    }

    private static void Check(List<string> results, string label, bool ok)
    {
        results.Add((ok ? "PASS: " : "FAIL: ") + label);
    }
}
