using System;
using System.Collections.Generic;
using DG.Tweening;
using Match3.Core;
using Match3.GameLogic;
using Match3.Grid;
using UnityEngine;

namespace Match3.VFX
{
    // Plays sequenced highlight pulses across a list of board cells.
    // BoardController calls PlayHighlightSequence to animate LightBall combos:
    //   - LightBall + Cube: pulse each matching cube, then pop all together
    //   - LightBall + Rocket/TNT: pulse each cube, transform it on each pulse via callback
    // Registers in ServiceLocator on LevelReadyEvent so BoardController can fetch it.
    public class LightBallAnimator : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] float _delayBetweenCubes  = 0.05f;
        [SerializeField] float _highlightDuration  = 0.15f;
        [SerializeField] float _postSelectionPause = 0.2f;

        [Header("Highlight Effect")]
        [SerializeField] Vector3 _scalePunch = new Vector3(0.3f, 0.3f, 0f);
        [SerializeField] Color   _flashTint  = Color.white;

        GridView _view;

        void OnEnable()  => EventBus.Subscribe<LevelReadyEvent>(OnLevelReady);
        void OnDisable() => EventBus.Unsubscribe<LevelReadyEvent>(OnLevelReady);

        void OnLevelReady(LevelReadyEvent evt)
        {
            _view = evt.View;
            ServiceLocator.Register(this);
        }

        // Plays a highlight pulse on each cell in order, separated by _delayBetweenCubes.
        // onEachHighlight: fires once at the start of each pulse — lets the caller mutate
        // model+view (e.g. transform cube → rocket) while the punch animation plays.
        // Returns a Sequence that completes after the final post-selection pause.
        public Sequence PlayHighlightSequence(List<Vector2Int> cells,
                                              Action<Vector2Int> onEachHighlight = null)
        {
            var seq = DOTween.Sequence();

            if (_view == null || cells == null || cells.Count == 0)
            {
                seq.AppendInterval(_postSelectionPause);
                return seq;
            }

            foreach (var pos in cells)
            {
                Vector2Int captured = pos;
                seq.AppendCallback(() =>
                {
                    onEachHighlight?.Invoke(captured);

                    TileView tile = _view.GetTileView(captured.y, captured.x);
                    if (tile == null) return;

                    tile.transform.DOPunchScale(_scalePunch, _highlightDuration, 1, 0.5f);
                    tile.SpriteRenderer
                        .DOColor(_flashTint, _highlightDuration * 0.5f)
                        .SetLoops(2, LoopType.Yoyo);
                });
                seq.AppendInterval(_delayBetweenCubes);
            }

            seq.AppendInterval(_postSelectionPause);
            return seq;
        }
    }
}
