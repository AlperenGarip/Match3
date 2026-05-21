using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps.Strategies
{
    // Clears the entire row of the origin cell.
    public class HorizontalRocketStrategy : IActivationStrategy
    {
        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            for (int col = 0; col < model.Columns; col++)
                cells.Add(new Vector2Int(col, origin.y));
            return cells;
        }
    }
}
