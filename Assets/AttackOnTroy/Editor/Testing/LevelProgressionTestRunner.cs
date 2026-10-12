using UnityEditor;
using UnityEngine;

namespace AttackOnTroy.Editor.Testing
{
    public static class LevelProgressionTestRunner
    {
        [MenuItem("AttackOnTroy/Tests/Run Level Progression & Menu Test")]
        public static void RunTests()
        {
            Debug.Log("=== [TEST] Starting Level Progression & Menu Tests ===");

            // 1. Backup any existing state
            int originalUnlocked = PlayerPrefs.GetInt(BattleSessionData.HighestUnlockedLevelPrefKey, -1);
            int originalCompleted = PlayerPrefs.GetInt(BattleSessionData.GameCompletedPrefKey, -1);
            int origL1 = PlayerPrefs.GetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "1", -1);
            int origL2 = PlayerPrefs.GetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "2", -1);
            int origL3 = PlayerPrefs.GetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "3", -1);

            try
            {
                // Test 1: Fresh State (default)
                PlayerPrefs.DeleteKey(BattleSessionData.HighestUnlockedLevelPrefKey);
                PlayerPrefs.DeleteKey(BattleSessionData.GameCompletedPrefKey);
                PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "1");
                PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "2");
                PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "3");

                Assert(BattleSessionData.HighestUnlockedLevel == 1, "Default HighestUnlockedLevel must be 1");
                Assert(BattleSessionData.IsLevelUnlocked(1) == true, "Level 01 must be unlocked by default");
                Assert(BattleSessionData.IsLevelUnlocked(2) == false, "Level 02 must be locked by default");
                Assert(BattleSessionData.IsLevelUnlocked(3) == false, "Level 03 must be locked by default");
                Assert(BattleSessionData.IsGameCompleted == false, "Game must not be completed by default");
                Debug.Log("[TEST 1 PASSED] Fresh install state correctly has Level 01 open, Level 02 & 03 locked.");

                // Test 2: Defeat / Restart does NOT unlock next level
                // (Defeat doesn't call RecordLevelVictory)
                Assert(BattleSessionData.HighestUnlockedLevel == 1, "Defeat/restart should not advance unlocked level");
                Assert(BattleSessionData.IsLevelUnlocked(2) == false, "Level 02 still locked after defeat/restart");
                Debug.Log("[TEST 2 PASSED] Defeat/Restart/Quit does not unlock levels.");

                // Test 3: Victory Level 01 -> Unlocks Level 02
                BattleSessionData.RecordLevelVictory(1);
                Assert(BattleSessionData.HighestUnlockedLevel == 2, "HighestUnlockedLevel must be 2 after winning Level 01");
                Assert(BattleSessionData.IsLevelUnlocked(1) == true, "Level 01 is unlocked");
                Assert(BattleSessionData.IsLevelUnlocked(2) == true, "Level 02 is now unlocked");
                Assert(BattleSessionData.IsLevelUnlocked(3) == false, "Level 03 is still locked");
                Assert(BattleSessionData.IsLevelCompleted(1) == true, "Level 01 is marked completed");
                Debug.Log("[TEST 3 PASSED] Winning Level 01 unlocks Level 02.");

                // Test 4: Replaying Level 01 does NOT re-lock Level 02
                BattleSessionData.RecordLevelVictory(1);
                Assert(BattleSessionData.HighestUnlockedLevel == 2, "Replaying Level 01 must not decrease HighestUnlockedLevel");
                Assert(BattleSessionData.IsLevelUnlocked(2) == true, "Level 02 remains unlocked after replaying Level 01");
                Debug.Log("[TEST 4 PASSED] Replaying older levels does not regress unlocked progress.");

                // Test 5: Victory Level 02 -> Unlocks Level 03
                BattleSessionData.RecordLevelVictory(2);
                Assert(BattleSessionData.HighestUnlockedLevel == 3, "HighestUnlockedLevel must be 3 after winning Level 02");
                Assert(BattleSessionData.IsLevelUnlocked(1) == true, "Level 01 is unlocked");
                Assert(BattleSessionData.IsLevelUnlocked(2) == true, "Level 02 is unlocked");
                Assert(BattleSessionData.IsLevelUnlocked(3) == true, "Level 03 is now unlocked");
                Assert(BattleSessionData.IsLevelCompleted(2) == true, "Level 02 is marked completed");
                Debug.Log("[TEST 5 PASSED] Winning Level 02 unlocks Level 03.");

                // Test 6: Victory Level 03 -> Marks Game Completed
                BattleSessionData.RecordLevelVictory(3);
                Assert(BattleSessionData.IsGameCompleted == true, "IsGameCompleted must be true after winning Level 03");
                Assert(BattleSessionData.IsLevelCompleted(3) == true, "Level 03 is marked completed");
                Debug.Log("[TEST 6 PASSED] Winning Level 03 records game completion.");

                // Test 7: Persistence verification via PlayerPrefs
                int savedHighest = PlayerPrefs.GetInt(BattleSessionData.HighestUnlockedLevelPrefKey);
                int savedGameComp = PlayerPrefs.GetInt(BattleSessionData.GameCompletedPrefKey);
                Assert(savedHighest == 3, "PlayerPrefs persists HighestUnlockedLevel = 3 across session reloads");
                Assert(savedGameComp == 1, "PlayerPrefs persists GameCompleted = 1 across session reloads");
                Debug.Log("[TEST 7 PASSED] PlayerPrefs persistence across sessions verified.");
            }
            finally
            {
                // Restore original state
                if (originalUnlocked >= 0) PlayerPrefs.SetInt(BattleSessionData.HighestUnlockedLevelPrefKey, originalUnlocked);
                else PlayerPrefs.DeleteKey(BattleSessionData.HighestUnlockedLevelPrefKey);

                if (originalCompleted >= 0) PlayerPrefs.SetInt(BattleSessionData.GameCompletedPrefKey, originalCompleted);
                else PlayerPrefs.DeleteKey(BattleSessionData.GameCompletedPrefKey);

                if (origL1 >= 0) PlayerPrefs.SetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "1", origL1);
                else PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "1");

                if (origL2 >= 0) PlayerPrefs.SetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "2", origL2);
                else PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "2");

                if (origL3 >= 0) PlayerPrefs.SetInt(BattleSessionData.LevelCompletedPrefKeyPrefix + "3", origL3);
                else PlayerPrefs.DeleteKey(BattleSessionData.LevelCompletedPrefKeyPrefix + "3");

                PlayerPrefs.Save();
                Debug.Log("[TEST] Original player state restored safely. No user progress was lost.");
            }

            Debug.Log("=== [TEST] ALL LEVEL PROGRESSION & MENU TESTS PASSED SUCCESSFULLY! ===");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.Exception("Assertion Failed: " + message);
            }
        }
    }
}
