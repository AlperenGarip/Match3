using System.Collections.Generic;
using DG.Tweening;
using Match3.Core;
using Match3.Data;
using Match3.GameLogic;
using Match3.Grid;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.UI
{
    // Drives the in-game HUD and win/lose popups.
    // Attach to the Canvas GameObject in LevelScene.
    // Wire all serialized fields in the Inspector.
    public class LevelUIController : MonoBehaviour
    {
        [Header("HUD — Moves")]
        [SerializeField] TextMeshProUGUI _movesText;

        [Header("HUD — Goal Panel")]
        [SerializeField] GoalEntryUI _goalEntryPrefab;  // prefab: Icon + CountText + Checkmark
        [SerializeField] Transform   _goalContainer;     // GridLayoutGroup parent

        [Header("Obstacle Icon Sprites")]
        [SerializeField] Sprite _boxIcon;
        [SerializeField] Sprite _stoneIcon;
        [SerializeField] Sprite _vaseIcon;

        [Header("Win Popup")]
        [SerializeField] GameObject      _winPopup;
        [SerializeField] RectTransform[] _winStars;  // assign 3 star RectTransforms
        [SerializeField] Button          _nextLevelButton;

        [Header("Lose Popup")]
        [SerializeField] GameObject _losePopup;
        [SerializeField] Button     _retryButton;

        readonly Dictionary<CellType, GoalEntryUI> _goalEntries = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────
        void OnEnable()
        {
            EventBus.Subscribe<GoalCountsEvent>(OnGoalCounts);
            EventBus.Subscribe<MovesChangedEvent>(OnMovesChanged);
            EventBus.Subscribe<GoalUpdatedEvent>(OnGoalUpdated);
            EventBus.Subscribe<GameWonEvent>(OnGameWon);
            EventBus.Subscribe<GameLostEvent>(OnGameLost);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GoalCountsEvent>(OnGoalCounts);
            EventBus.Unsubscribe<MovesChangedEvent>(OnMovesChanged);
            EventBus.Unsubscribe<GoalUpdatedEvent>(OnGoalUpdated);
            EventBus.Unsubscribe<GameWonEvent>(OnGameWon);
            EventBus.Unsubscribe<GameLostEvent>(OnGameLost);
        }

        void Start()
        {
            if (_winPopup  != null) _winPopup .SetActive(false);
            if (_losePopup != null) _losePopup.SetActive(false);
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        // Called at level load — builds one GoalEntryUI per obstacle type.
        void OnGoalCounts(GoalCountsEvent evt)
        {
            if (_goalContainer == null || _goalEntryPrefab == null) return;

            // Clear previous entries (handles level reload)
            foreach (Transform child in _goalContainer)
                Destroy(child.gameObject);
            _goalEntries.Clear();

            foreach (var kv in evt.Counts)
            {
                Sprite icon = kv.Key switch
                {
                    CellType.Box   => _boxIcon,
                    CellType.Stone => _stoneIcon,
                    _              => _vaseIcon  // Vase1
                };

                GoalEntryUI entry = Instantiate(_goalEntryPrefab, _goalContainer);
                entry.Setup(icon, kv.Value);
                _goalEntries[kv.Key] = entry;
            }
        }

        void OnMovesChanged(MovesChangedEvent evt)
        {
            if (_movesText != null)
                _movesText.text = evt.Remaining.ToString();
        }

        void OnGoalUpdated(GoalUpdatedEvent evt)
        {
            foreach (var kv in evt.Remaining)
            {
                if (_goalEntries.TryGetValue(kv.Key, out var entry))
                    entry.UpdateCount(kv.Value);
            }
        }

        void OnGameWon(GameWonEvent evt)
        {
            SaveSystem.UnlockNextLevel();

            if (_winPopup == null) return;
            _winPopup.SetActive(true);
            _winPopup.transform.localScale = Vector3.zero;
            _winPopup.transform
                .DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(AnimateWinStars);
        }

        void AnimateWinStars()
        {
            if (_winStars == null) return;
            for (int i = 0; i < _winStars.Length; i++)
            {
                if (_winStars[i] == null) continue;
                _winStars[i].localScale = Vector3.zero;
                int idx = i;
                DOVirtual.DelayedCall(idx * 0.25f, () =>
                    _winStars[idx].DOScale(1f, 0.8f).SetEase(Ease.OutBack));
            }
        }

        void OnGameLost(GameLostEvent evt)
        {
            if (_losePopup == null) return;
            _losePopup.SetActive(true);
            _losePopup.transform.localScale = Vector3.zero;
            _losePopup.transform
                .DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack);
        }

        // ── Button callbacks (wire in Inspector via onClick) ──────────────────
        public void OnNextLevelClicked() => TransitionManager.LoadLevelScene();
        public void OnRetryClicked()     => TransitionManager.LoadLevelScene();
        public void OnHomeClicked()      => TransitionManager.LoadMainScene();
    }
}
