using UnityEngine;

/// <summary>
/// Cross-scene session state storing the selected player hero, menu navigation flags,
/// and persistent level unlock progression (Level 01 -> Level 02 -> Level 03 -> Game Completed).
/// Persists across game restarts via PlayerPrefs.
/// </summary>
public static class BattleSessionData
{
    private const string HeroPrefKey = "AttackOnTroy_SelectedHero";
    public const string HighestUnlockedLevelPrefKey = "AttackOnTroy_HighestUnlockedLevel";
    public const string GameCompletedPrefKey = "AttackOnTroy_GameCompleted";
    public const string LevelCompletedPrefKeyPrefix = "AttackOnTroy_LevelCompleted_";

    public const string HeroAchilles = "Achilles";
    public const string HeroOdysseus = "Odysseus";

    private static string selectedHero;
    private static bool returnToCharacterSelect;
    private static bool returnToLevelSelect;

    public static string SelectedHero
    {
        get
        {
            if (string.IsNullOrEmpty(selectedHero))
            {
                selectedHero = PlayerPrefs.GetString(HeroPrefKey, HeroAchilles);
            }
            return selectedHero;
        }
        set
        {
            selectedHero = string.IsNullOrEmpty(value) ? HeroAchilles : value;
            PlayerPrefs.SetString(HeroPrefKey, selectedHero);
            PlayerPrefs.Save();
        }
    }

    public static bool ReturnToCharacterSelect
    {
        get => returnToCharacterSelect;
        set => returnToCharacterSelect = value;
    }

    public static bool ReturnToLevelSelect
    {
        get => returnToLevelSelect;
        set => returnToLevelSelect = value;
    }

    public static bool IsOdysseus => SelectedHero == HeroOdysseus;
    public static bool IsAchilles => !IsOdysseus;

    #region Level Unlock Progression

    /// <summary>
    /// Highest level unlocked so far (1 = Level 01, 2 = Level 02, 3 = Level 03).
    /// Default on fresh install is 1 (Level 01 unlocked, Level 02 & 03 locked).
    /// Setter will only ever increase the value to prevent replaying older levels from locking newer ones.
    /// </summary>
    public static int HighestUnlockedLevel
    {
        get => Mathf.Max(1, PlayerPrefs.GetInt(HighestUnlockedLevelPrefKey, 1));
        set
        {
            int current = HighestUnlockedLevel;
            if (value > current)
            {
                PlayerPrefs.SetInt(HighestUnlockedLevelPrefKey, value);
                PlayerPrefs.Save();
                Debug.Log($"[BattleSessionData] HighestUnlockedLevel updated to {value}");
            }
        }
    }

    /// <summary>
    /// Whether the final level (Level 03) has been conquered and the game completed.
    /// </summary>
    public static bool IsGameCompleted
    {
        get => PlayerPrefs.GetInt(GameCompletedPrefKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(GameCompletedPrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[BattleSessionData] IsGameCompleted set to {value}");
        }
    }

    /// <summary>
    /// Checks whether a specific level (1, 2, or 3) is currently unlocked.
    /// </summary>
    public static bool IsLevelUnlocked(int levelIndex)
    {
        if (levelIndex <= 1) return true;
        return levelIndex <= HighestUnlockedLevel;
    }

    /// <summary>
    /// Checks whether a specific level has been conquered previously.
    /// </summary>
    public static bool IsLevelCompleted(int levelIndex)
    {
        return PlayerPrefs.GetInt(LevelCompletedPrefKeyPrefix + levelIndex, 0) == 1;
    }

    /// <summary>
    /// Records a true Victory in the specified level (1, 2, or 3).
    /// Unlocks the subsequent level and flags completion immediately.
    /// Must only be called upon legitimate Victory.
    /// </summary>
    public static void RecordLevelVictory(int completedLevelIndex)
    {
        if (completedLevelIndex <= 0) return;

        // Mark this level as completed
        PlayerPrefs.SetInt(LevelCompletedPrefKeyPrefix + completedLevelIndex, 1);

        if (completedLevelIndex == 1)
        {
            UnlockLevel(2);
        }
        else if (completedLevelIndex == 2)
        {
            UnlockLevel(3);
        }
        else if (completedLevelIndex >= 3)
        {
            IsGameCompleted = true;
        }

        PlayerPrefs.Save();
        Debug.Log($"[BattleSessionData] Victory recorded for Level {completedLevelIndex}. HighestUnlockedLevel: {HighestUnlockedLevel}, GameCompleted: {IsGameCompleted}");
    }

    /// <summary>
    /// Unlocks a target level index if not already unlocked.
    /// </summary>
    public static void UnlockLevel(int levelToUnlock)
    {
        if (levelToUnlock > HighestUnlockedLevel)
        {
            HighestUnlockedLevel = levelToUnlock;
        }
    }

    #endregion
}
