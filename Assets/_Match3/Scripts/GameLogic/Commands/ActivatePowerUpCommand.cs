using UnityEngine;

namespace Match3.GameLogic.Commands
{
    // Stub — full implementation in Phase 3 when IActivationStrategy is defined.
    public class ActivatePowerUpCommand : ICommand
    {
        readonly Vector2Int _origin;

        public ActivatePowerUpCommand(Vector2Int origin)
        {
            _origin = origin;
        }

        public void Execute()
        {
            // TODO Phase 3: resolve IActivationStrategy for the cell type and invoke it
            Debug.Log($"[Phase 3] ActivatePowerUp at {_origin} — not yet implemented.");
        }

        public void Undo()
        {
            // Power-up activation is not undoable
        }
    }
}
