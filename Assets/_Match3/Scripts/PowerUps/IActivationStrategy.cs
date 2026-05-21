using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps
{
    public interface IActivationStrategy
    {
        List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model);
    }
}
