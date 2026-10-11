using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data container for enemy-specific dialogue lines.
/// Supports distinguishing between different enemies by ID, allowing multiple enemies in later levels (e.g. Level 03).
/// </summary>
[Serializable]
public class EnemyDialogueEntry
{
    [Tooltip("Unique ID or name of the enemy (e.g., CyclopsNormal, Hector, Paris).")]
    public string enemyId;

    [Tooltip("Line(s) spoken by this enemy before its first attack/throw.")]
    public DialogueLine[] preFirstAttackLines;

    [Tooltip("Pool of taunt line(s) spoken before attacking in subsequent turns (or every turn).")]
    public DialogueLine[] tauntLines;

    public EnemyDialogueEntry() { }

    public EnemyDialogueEntry(string id, DialogueLine[] preAttack, DialogueLine[] taunts = null)
    {
        enemyId = id;
        preFirstAttackLines = preAttack;
        tauntLines = taunts;
    }
}

/// <summary>
/// Per-level ScriptableObject storing fantasy dialogue lines for opening exchanges,
/// enemy pre-attack banter, and victory lines.
/// Supports lines that vary by selected player (Achilles vs Odysseus) and by enemy ID.
/// </summary>
[CreateAssetMenu(fileName = "SO_LevelDialogueData", menuName = "Attack On Troy/Dialogue/Level Dialogue Data")]
public class LevelDialogueData : ScriptableObject
{
    [Header("Opening Exchanges (Hero vs Opponent)")]
    [Tooltip("Opening dialogue lines when starting the battle as Achilles.")]
    [SerializeField] private DialogueLine[] achillesOpeningLines;

    [Tooltip("Opening dialogue lines when starting the battle as Odysseus.")]
    [SerializeField] private DialogueLine[] odysseusOpeningLines;

    [Header("Enemy Banter (Multiple Enemies Supported)")]
    [Tooltip("Enemy banter entries keyed by enemy ID, such as CyclopsNormal.")]
    [SerializeField] private List<EnemyDialogueEntry> enemyEntries = new List<EnemyDialogueEntry>();

    [Header("Victory Lines")]
    [Tooltip("Victory line(s) spoken when Achilles wins.")]
    [SerializeField] private DialogueLine[] achillesVictoryLines;

    [Tooltip("Victory line(s) spoken when Odysseus wins.")]
    [SerializeField] private DialogueLine[] odysseusVictoryLines;

    public DialogueLine[] AchillesOpeningLines => achillesOpeningLines;
    public DialogueLine[] OdysseusOpeningLines => odysseusOpeningLines;
    public List<EnemyDialogueEntry> EnemyEntries => enemyEntries;
    public DialogueLine[] AchillesVictoryLines => achillesVictoryLines;
    public DialogueLine[] OdysseusVictoryLines => odysseusVictoryLines;

    /// <summary>
    /// Returns opening lines for the given hero name.
    /// </summary>
    public DialogueLine[] GetOpeningLines(string heroName)
    {
        if (string.Equals(heroName, BattleSessionData.HeroOdysseus, StringComparison.OrdinalIgnoreCase))
        {
            if (odysseusOpeningLines != null && odysseusOpeningLines.Length > 0)
                return odysseusOpeningLines;
        }

        return achillesOpeningLines;
    }

    /// <summary>
    /// Returns pre-attack lines for an enemy by ID, supporting multiple enemies in later levels.
    /// </summary>
    public DialogueLine[] GetEnemyPreAttackLines(string enemyId)
    {
        if (enemyEntries == null || enemyEntries.Count == 0)
            return null;

        for (int i = 0; i < enemyEntries.Count; i++)
        {
            if (enemyEntries[i] != null && string.Equals(enemyEntries[i].enemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            {
                return enemyEntries[i].preFirstAttackLines;
            }
        }

        // Fallback: if only one entry is configured, return it
        if (enemyEntries.Count == 1 && enemyEntries[0] != null)
        {
            return enemyEntries[0].preFirstAttackLines;
        }

        return null;
    }

    /// <summary>
    /// Returns pre-attack lines or a taunt line for an enemy by ID for the specified enemy turn (0-indexed).
    /// </summary>
    public DialogueLine[] GetEnemyTurnTauntLines(string enemyId, int enemyTurnIndex)
    {
        if (enemyEntries == null || enemyEntries.Count == 0)
            return null;

        EnemyDialogueEntry entry = null;
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            if (enemyEntries[i] != null && string.Equals(enemyEntries[i].enemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            {
                entry = enemyEntries[i];
                break;
            }
        }

        if (entry == null && enemyEntries.Count == 1)
            entry = enemyEntries[0];

        if (entry == null)
            return null;

        // Turn 0: If preFirstAttackLines is defined, use it
        if (enemyTurnIndex == 0 && entry.preFirstAttackLines != null && entry.preFirstAttackLines.Length > 0)
        {
            return entry.preFirstAttackLines;
        }

        // Subsequent turns (or turn 0 if preFirstAttackLines is empty): Pick from tauntLines pool
        if (entry.tauntLines != null && entry.tauntLines.Length > 0)
        {
            int poolIndex = (enemyTurnIndex == 0 ? 0 : enemyTurnIndex - 1) % entry.tauntLines.Length;
            return new DialogueLine[] { entry.tauntLines[poolIndex] };
        }

        // Fallback: If no tauntLines pool, cycle through preFirstAttackLines
        if (entry.preFirstAttackLines != null && entry.preFirstAttackLines.Length > 0)
        {
            int fallbackIndex = enemyTurnIndex % entry.preFirstAttackLines.Length;
            return new DialogueLine[] { entry.preFirstAttackLines[fallbackIndex] };
        }

        return null;
    }

    /// <summary>
    /// Returns victory lines for the winning hero.
    /// </summary>
    public DialogueLine[] GetVictoryLines(string heroName)
    {
        if (string.Equals(heroName, BattleSessionData.HeroOdysseus, StringComparison.OrdinalIgnoreCase))
        {
            if (odysseusVictoryLines != null && odysseusVictoryLines.Length > 0)
                return odysseusVictoryLines;
        }

        return achillesVictoryLines;
    }

    public void SetAchillesOpeningLines(DialogueLine[] lines) => achillesOpeningLines = lines;
    public void SetOdysseusOpeningLines(DialogueLine[] lines) => odysseusOpeningLines = lines;
    public void SetAchillesVictoryLines(DialogueLine[] lines) => achillesVictoryLines = lines;
    public void SetOdysseusVictoryLines(DialogueLine[] lines) => odysseusVictoryLines = lines;
    public void SetEnemyEntries(List<EnemyDialogueEntry> entries) => enemyEntries = entries;
}
