using System.Collections.Generic;
using Match3.Core;
using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic
{
    public struct TileClearedEvent
    {
        // Normalized obstacle type (Vase2 is reported as Vase1 to match GoalCountsEvent keys).
        public CellType    ObstacleType;
        public Vector2Int  GridPos;      // grid position (x=col, y=row) for VFX
    }

    // Pure C# — no MonoBehaviour. Instantiated by BoardController.
    // Handles obstacle hit logic: Box (1-hit clear), Vase1→Vase2→clear, Stone (1-hit clear by power-up AoE only).
    // Adjacent match splash still skips Stone (ProcessAdjacentObstacles guard).
    public class ObstacleSystem
    {
        static readonly Vector2Int[] Cardinals =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        // Apply one hit to the obstacle at pos.
        // Updates model + view. Publishes TileClearedEvent if the obstacle is removed.
        public void HitObstacle(Vector2Int pos, GridModel model, GridView view)
        {
            Cell cell = model.GetCell(pos.y, pos.x);

            switch (cell.Type)
            {
                case CellType.Box:
                    view.ClearCell(pos.y, pos.x);
                    model.SetCellSilent(pos.y, pos.x, Cell.Empty());
                    EventBus.Publish(new TileClearedEvent { ObstacleType = CellType.Box, GridPos = pos });
                    Debug.Log($"[ObstacleSystem] Box cleared at ({pos.x},{pos.y}).");
                    break;

                case CellType.Vase1:
                    // Downgrade to Vase2 — tile stays, sprite changes
                    var vase2Cell = new Cell(CellType.Vase2);
                    model.SetCellSilent(pos.y, pos.x, vase2Cell);
                    view.GetTileView(pos.y, pos.x)?.UpdateVisual(vase2Cell);
                    Debug.Log($"[ObstacleSystem] Vase cracked at ({pos.x},{pos.y}).");
                    break;

                case CellType.Vase2:
                    view.ClearCell(pos.y, pos.x);
                    model.SetCellSilent(pos.y, pos.x, Cell.Empty());
                    // Normalized key: Vase1 (matches GoalCountsEvent / GoalTracker)
                    EventBus.Publish(new TileClearedEvent { ObstacleType = CellType.Vase1, GridPos = pos });
                    Debug.Log($"[ObstacleSystem] Vase cleared at ({pos.x},{pos.y}).");
                    break;

                case CellType.Stone:
                    view.ClearCell(pos.y, pos.x);
                    model.SetCellSilent(pos.y, pos.x, Cell.Empty());
                    EventBus.Publish(new TileClearedEvent { ObstacleType = CellType.Stone, GridPos = pos });
                    Debug.Log($"[ObstacleSystem] Stone cleared at ({pos.x},{pos.y}).");
                    break;

                default:
                    break;
            }
        }

        // For each cleared cube position, check all 4 cardinal neighbors for non-Stone obstacles.
        // Each obstacle is hit at most once per call (deduped via HashSet).
        public void ProcessAdjacentObstacles(IEnumerable<Vector2Int> clearedCubePositions,
                                             GridModel model, GridView view)
        {
            var alreadyHit = new HashSet<Vector2Int>();

            foreach (var cubePos in clearedCubePositions)
            {
                foreach (var dir in Cardinals)
                {
                    var neighbor = cubePos + dir;
                    if (!model.IsInBounds(neighbor.y, neighbor.x)) continue;
                    if (alreadyHit.Contains(neighbor)) continue;

                    Cell neighborCell = model.GetCell(neighbor.y, neighbor.x);
                    if (!neighborCell.IsObstacle || neighborCell.Type == CellType.Stone) continue;

                    alreadyHit.Add(neighbor);
                    HitObstacle(neighbor, model, view);
                }
            }
        }
    }
}
