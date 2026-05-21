using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic.Commands
{
    public class SwapCommand : ICommand
    {
        readonly GridModel _model;

        public Vector2Int From { get; }
        public Vector2Int To   { get; }

        public SwapCommand(GridModel model, Vector2Int from, Vector2Int to)
        {
            _model = model;
            From   = from;
            To     = to;
        }

        public void Execute() => Swap(From, To);
        public void Undo()    => Swap(To, From);

        void Swap(Vector2Int a, Vector2Int b)
        {
            Cell cellA = _model.GetCell(a.y, a.x);
            Cell cellB = _model.GetCell(b.y, b.x);
            _model.SetCellSilent(a.y, a.x, cellB);
            _model.SetCellSilent(b.y, b.x, cellA);
        }
    }
}
