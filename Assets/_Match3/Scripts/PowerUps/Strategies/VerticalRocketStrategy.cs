using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps.Strategies
{
    // Clears the entire column of the origin cell.
    public class VerticalRocketStrategy : IActivationStrategy
    {
        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            for (int row = 0; row < model.Rows; row++)
                cells.Add(new Vector2Int(origin.x, row));
            return cells;
        }
    }
}
