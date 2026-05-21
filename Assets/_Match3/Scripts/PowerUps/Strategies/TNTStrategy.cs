using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps.Strategies
{
    // Clears all in-bounds cells within Chebyshev distance ≤ radius (default 2 → 5×5 area).
    public class TNTStrategy : IActivationStrategy
    {
        readonly int _radius;

        public TNTStrategy(int radius = 2) { _radius = radius; }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            for (int dr = -_radius; dr <= _radius; dr++)
            for (int dc = -_radius; dc <= _radius; dc++)
            {
                int row = origin.y + dr;
                int col = origin.x + dc;
                if (model.IsInBounds(row, col))
                    cells.Add(new Vector2Int(col, row));
            }
            return cells;
        }
    }
}
