using UnityEngine;

namespace Match3.Data
{
    // PlayerPrefs-based save system. Tracks the current level (1–MAX_LEVEL).
    // Static — no MonoBehaviour needed.
    public static class SaveSystem
    {
        public const int MAX_LEVEL = 20;

        const string KEY_LEVEL = "current_level";

        // Returns the saved level (defaults to 1 on first run).
        public static int GetCurrentLevel() => PlayerPrefs.GetInt(KEY_LEVEL, 1);

        public static void SaveCurrentLevel(int level)
        {
            PlayerPrefs.SetInt(KEY_LEVEL, Mathf.Clamp(level, 1, MAX_LEVEL));
            PlayerPrefs.Save();
        }

        // Called on win: advances saved level by 1, capped at MAX_LEVEL.
        public static void UnlockNextLevel() =>
            SaveCurrentLevel(Mathf.Min(GetCurrentLevel() + 1, MAX_LEVEL));
    }
}
