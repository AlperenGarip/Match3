using UnityEngine.SceneManagement;

namespace Match3.Core
{
    // Thin wrapper around SceneManager for scene navigation.
    // Phase 6: add DOTween fade overlay here.
    public static class TransitionManager
    {
        public static void LoadLevelScene() => SceneManager.LoadScene("LevelScene");
        public static void LoadMainScene()  => SceneManager.LoadScene("MainScene");
    }
}
