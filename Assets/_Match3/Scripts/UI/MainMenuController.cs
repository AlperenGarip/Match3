using Match3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    // Drives the main menu screen.
    // Attach to the Canvas GameObject in MainScene.
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button _playButton;

        void Start()
        {
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);
        }

        void OnPlayClicked() => TransitionManager.LoadLevelScene();
    }
}
