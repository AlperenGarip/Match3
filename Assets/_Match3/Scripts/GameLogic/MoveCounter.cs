using Match3.Core;
using UnityEngine;

namespace Match3.GameLogic
{
    public struct MovesChangedEvent
    {
        public int Remaining;
    }

    // Tracks remaining moves for the current level.
    // Registers itself in ServiceLocator on LevelReadyEvent.
    // BoardController calls ConsumeMove() on each valid swap.
    public class MoveCounter : MonoBehaviour
    {
        public int MovesRemaining { get; private set; }

        void OnEnable()  => EventBus.Subscribe<LevelReadyEvent>(OnLevelReady);
        void OnDisable() => EventBus.Unsubscribe<LevelReadyEvent>(OnLevelReady);

        void OnLevelReady(LevelReadyEvent evt)
        {
            MovesRemaining = evt.Data.move_count;
            ServiceLocator.Register(this);
            EventBus.Publish(new MovesChangedEvent { Remaining = MovesRemaining });
            Debug.Log($"[MoveCounter] Initialised — {MovesRemaining} moves.");
        }

        public void ConsumeMove()
        {
            if (MovesRemaining <= 0) return;
            MovesRemaining--;
            EventBus.Publish(new MovesChangedEvent { Remaining = MovesRemaining });
            Debug.Log($"[MoveCounter] Move consumed — {MovesRemaining} remaining.");
        }
    }
}
