using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic
{
    public struct GoalUpdatedEvent
    {
        public Dictionary<CellType, int> Remaining;
    }

    // Subscribes to GoalCountsEvent (level start) and TileClearedEvent (obstacle removed).
    // Registers itself in ServiceLocator so BoardController can query IsGoalMet.
    public class GoalTracker : MonoBehaviour
    {
        Dictionary<CellType, int> _remaining = new();

        public bool IsGoalMet { get; private set; }

        void OnEnable()
        {
            EventBus.Subscribe<GoalCountsEvent>(OnGoalCounts);
            EventBus.Subscribe<TileClearedEvent>(OnTileCleared);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GoalCountsEvent>(OnGoalCounts);
            EventBus.Unsubscribe<TileClearedEvent>(OnTileCleared);
        }

        void OnGoalCounts(GoalCountsEvent evt)
        {
            _remaining = new Dictionary<CellType, int>(evt.Counts);
            IsGoalMet  = _remaining.Count == 0;

            ServiceLocator.Register(this);
            EventBus.Publish(new GoalUpdatedEvent { Remaining = _remaining });

            Debug.Log($"[GoalTracker] Goals initialised. IsGoalMet={IsGoalMet}. " +
                      $"Counts: {string.Join(", ", _remaining.Select(kv => $"{kv.Key}×{kv.Value}"))}");
        }

        void OnTileCleared(TileClearedEvent evt)
        {
            // Vase2 is already normalised to Vase1 by ObstacleSystem
            CellType key = evt.ObstacleType;
            if (!_remaining.ContainsKey(key)) return;

            _remaining[key] = Mathf.Max(0, _remaining[key] - 1);
            EventBus.Publish(new GoalUpdatedEvent { Remaining = _remaining });

            Debug.Log($"[GoalTracker] {key} cleared. Remaining: {_remaining[key]}");

            if (_remaining.Values.All(v => v == 0))
            {
                IsGoalMet = true;
                Debug.Log("[GoalTracker] All goals met!");
            }
        }
    }
}
