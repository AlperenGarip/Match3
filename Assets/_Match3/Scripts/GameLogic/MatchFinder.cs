using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic
{
    public enum MatchShape
    {
        Line3,      // 3-in-a-line — no power-up
        HLine4,     // 4 horizontal — Horizontal Rocket
        VLine4,     // 4 vertical   — Vertical Rocket
        LOrTShape,  // L or T shape — TNT
        Line5,      // 5 straight   — Light Ball
    }

    public class MatchGroup
    {
        public List<Vector2Int> Cells        = new();
        public MatchShape       Shape;
        public Vector2Int       PowerUpSpawnCell;
        public CellType?        PowerUpToSpawn;  // null for Line3
    }

    // Pure C# — no MonoBehaviour, no side effects.
    public class MatchFinder
    {
        // swapTo:   the cell the player's finger ended on (primary spawn hint).
        // swapFrom: the cell the player's finger started on (secondary spawn hint).
        // A match can form around either swapped cell; we try swapTo first then swapFrom.
        // Pass nulls for cascade checks.
        public List<MatchGroup> FindAllMatches(GridModel model,
                                               Vector2Int? swapTo   = null,
                                               Vector2Int? swapFrom = null)
        {
            int rows = model.Rows, cols = model.Columns;

            var inHRun  = new bool[rows, cols];
            var inVRun  = new bool[rows, cols];
            var matched = new bool[rows, cols];

            // ── Horizontal runs ──────────────────────────────────────────
            for (int r = 0; r < rows; r++)
            {
                int c = 0;
                while (c < cols)
                {
                    Cell cell = model.GetCell(r, c);
                    if (!cell.IsCube) { c++; continue; }

                    int end = c + 1;
                    while (end < cols && IsSameColorCube(model.GetCell(r, end), cell))
                        end++;

                    if (end - c >= 3)
                        for (int i = c; i < end; i++)
                        { matched[r, i] = true; inHRun[r, i] = true; }

                    c = end;
                }
            }

            // ── Vertical runs ────────────────────────────────────────────
            for (int c = 0; c < cols; c++)
            {
                int r = 0;
                while (r < rows)
                {
                    Cell cell = model.GetCell(r, c);
                    if (!cell.IsCube) { r++; continue; }

                    int end = r + 1;
                    while (end < rows && IsSameColorCube(model.GetCell(end, c), cell))
                        end++;

                    if (end - r >= 3)
                        for (int i = r; i < end; i++)
                        { matched[i, c] = true; inVRun[i, c] = true; }

                    r = end;
                }
            }

            // ── Flood-fill connected matched cells into groups ────────────
            var visited = new bool[rows, cols];
            var groups  = new List<MatchGroup>();

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    if (!matched[r, c] || visited[r, c]) continue;
                    groups.Add(FloodFill(r, c, rows, cols, matched, visited, model));
                }

            // ── Determine shape and power-up for each group ───────────────
            foreach (var group in groups)
                DetermineShapeAndPowerUp(group, inHRun, inVRun, swapTo, swapFrom);

            return groups;
        }

        // ── Private helpers ───────────────────────────────────────────────

        static MatchGroup FloodFill(int startR, int startC, int rows, int cols,
                                    bool[,] matched, bool[,] visited, GridModel model)
        {
            var group      = new MatchGroup();
            var stack      = new Stack<(int r, int c)>();
            int startColor = model.GetCell(startR, startC).ColorIndex;
            stack.Push((startR, startC));

            while (stack.Count > 0)
            {
                var (r, c) = stack.Pop();
                if (r < 0 || r >= rows || c < 0 || c >= cols) continue;
                if (!matched[r, c] || visited[r, c]) continue;
                // Only group cells of the same color — two adjacent same-direction matches
                // of different colors must not be merged into an L/T shape.
                if (model.GetCell(r, c).ColorIndex != startColor) continue;

                visited[r, c] = true;
                group.Cells.Add(new Vector2Int(c, r)); // x=col, y=row

                stack.Push((r + 1, c));
                stack.Push((r - 1, c));
                stack.Push((r, c + 1));
                stack.Push((r, c - 1));
            }
            return group;
        }

        static void DetermineShapeAndPowerUp(MatchGroup group, bool[,] inHRun, bool[,] inVRun,
                                             Vector2Int? swapTo, Vector2Int? swapFrom)
        {
            int minRow = int.MaxValue, maxRow = int.MinValue;
            int minCol = int.MaxValue, maxCol = int.MinValue;

            foreach (var pos in group.Cells)
            {
                if (pos.y < minRow) minRow = pos.y;
                if (pos.y > maxRow) maxRow = pos.y;
                if (pos.x < minCol) minCol = pos.x;
                if (pos.x > maxCol) maxCol = pos.x;
            }

            int rowSpan = maxRow - minRow + 1;
            int colSpan = maxCol - minCol + 1;
            int count   = group.Cells.Count;
            bool isLine = rowSpan == 1 || colSpan == 1;

            // Priority: Line5 > LOrTShape > Line4 > Line3 (CLAUDE.md).
            // Count cells per row and per column — if any reaches ≥5 this is a Line5
            // even when extra cells branch off the arm (e.g. a T with a 5-long arm).
            var rowCounts = new Dictionary<int, int>();
            var colCounts = new Dictionary<int, int>();
            foreach (var pos in group.Cells)
            {
                rowCounts.TryGetValue(pos.y, out int rc); rowCounts[pos.y] = rc + 1;
                colCounts.TryGetValue(pos.x, out int cc); colCounts[pos.x] = cc + 1;
            }
            bool hasLine5 = false;
            foreach (var v in rowCounts.Values) if (v >= 5) { hasLine5 = true; break; }
            if (!hasLine5)
                foreach (var v in colCounts.Values) if (v >= 5) { hasLine5 = true; break; }

            if (hasLine5)
            {
                group.Shape = MatchShape.Line5;
            }
            else if (isLine)
            {
                bool isHorizontal = rowSpan == 1;
                if (count >= 5)       group.Shape = MatchShape.Line5;
                else if (count == 4)  group.Shape = isHorizontal ? MatchShape.HLine4 : MatchShape.VLine4;
                else                  group.Shape = MatchShape.Line3;
            }
            else
            {
                group.Shape = MatchShape.LOrTShape;
            }

            group.PowerUpToSpawn = group.Shape switch
            {
                MatchShape.HLine4    => (CellType?)CellType.HorizontalRocket,
                MatchShape.VLine4    => (CellType?)CellType.VerticalRocket,
                MatchShape.LOrTShape => (CellType?)CellType.TNT,
                MatchShape.Line5     => (CellType?)CellType.LightBall,
                _                    => null,
            };

            // Refinement 4: spawn at the swapped cell that is inside this group.
            // Try swapTo first (the tile the player moved), then swapFrom (the displaced tile).
            // Both can create matches — e.g. a vertical swipe that forms a match at the FROM
            // position means swapTo is not in the group but swapFrom is.
            // Fallback: nearest group cell to the centroid — raw integer average can land
            // outside the group (L/T centre), causing ProcessMatchGroup to clear all tiles.
            if (swapTo.HasValue && group.Cells.Contains(swapTo.Value))
            {
                group.PowerUpSpawnCell = swapTo.Value;
            }
            else if (swapFrom.HasValue && group.Cells.Contains(swapFrom.Value))
            {
                group.PowerUpSpawnCell = swapFrom.Value;
            }
            else
            {
                int sumR = 0, sumC = 0;
                foreach (var pos in group.Cells) { sumR += pos.y; sumC += pos.x; }
                int avgC = sumC / group.Cells.Count;
                int avgR = sumR / group.Cells.Count;

                // Find the actual cell in the group closest to the average position.
                group.PowerUpSpawnCell = group.Cells[0];
                int minDist = int.MaxValue;
                foreach (var pos in group.Cells)
                {
                    int dc = pos.x - avgC, dr = pos.y - avgR;
                    int dist = dc * dc + dr * dr;
                    if (dist < minDist) { minDist = dist; group.PowerUpSpawnCell = pos; }
                }
            }
        }

        static bool IsSameColorCube(Cell a, Cell b) =>
            a.IsCube && b.IsCube && a.ColorIndex == b.ColorIndex;
    }
}
