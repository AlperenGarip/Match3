using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps.Strategies
{
    // Clears all cubes matching the given colorIndex (-1 = clear entire board).
    public class LightBallStrategy : IActivationStrategy
    {
        readonly int _colorIndex; // -1 means clear everything

        public LightBallStrategy(int colorIndex) { _colorIndex = colorIndex; }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            for (int row = 0; row < model.Rows; row++)
            for (int col = 0; col < model.Columns; col++)
            {
                Cell cell = model.GetCell(row, col);
                if (cell.IsEmpty) continue;
                if (_colorIndex == -1 || cell.ColorIndex == _colorIndex)
                    cells.Add(new Vector2Int(col, row));
            }
            return cells;
        }
    }
}
