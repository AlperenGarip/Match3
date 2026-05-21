using System.Collections.Generic;
using DG.Tweening;
using Match3.Core;
using Match3.GameLogic;
using Match3.Grid;
using UnityEngine;

namespace Match3.PowerUps
{
    // Processes a MatchGroup: clears non-spawn cells and places the power-up at the spawn cell.
    public static class PowerUpSpawner
    {
        // Returns the list of positions that were cleared (excluding spawn cell).
        // BoardController passes this to ObstacleSystem.ProcessAdjacentObstacles.
        public static List<Vector2Int> ProcessMatchGroup(MatchGroup group, GridModel model, GridView view)
        {
            bool hasSpawn = group.PowerUpToSpawn.HasValue;
            var cleared = new List<Vector2Int>();

            foreach (var pos in group.Cells)
            {
                // Skip the spawn cell — it keeps its tile; we'll update its visual below.
                if (hasSpawn && pos == group.PowerUpSpawnCell)
                    continue;

                // Capture type before the cell is wiped so particles know the color.
                CellType clearedType = model.GetCell(pos.y, pos.x).Type;

                view.ClearCell(pos.y, pos.x);
                model.SetCellSilent(pos.y, pos.x, Cell.Empty());
                cleared.Add(pos);

                EventBus.Publish(new TilePoppedEvent { Type = clearedType, GridPos = pos });
            }

            if (!hasSpawn) return cleared;

            // Place power-up in model
            var spawnPos  = group.PowerUpSpawnCell;
            var powerCell = new Cell(group.PowerUpToSpawn.Value);
            model.SetCellSilent(spawnPos.y, spawnPos.x, powerCell);

            // Update visual (tile already exists at that position)
            var tile = view.GetTileView(spawnPos.y, spawnPos.x);
            if (tile != null)
            {
                tile.UpdateVisual(powerCell);
                // Small punch animation to signal power-up creation
                tile.transform.DOPunchScale(new Vector3(0.4f, 0.4f, 0f), 0.3f, 1, 0.5f);
            }

            return cleared;
        }
    }
}
