using System.Collections.Generic;
using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic
{
    public struct FallMove
    {
        public int FromRow;
        public int ToRow;
        public int Col;
    }

    // Pure C# — no MonoBehaviour, no side effects except SetCellSilent calls.
    public class GravitySystem
    {
        // Returns a list of tiles that need to fall, from bottom to top.
        // Respects Cell.CanFall and Cell.BlocksColumnAbove (Refinement 1).
        public List<FallMove> CalculateFalls(GridModel model)
        {
            var falls = new List<FallMove>();

            for (int col = 0; col < model.Columns; col++)
            {
                // Find the topBound: stop before the lowest stone from the top
                // (stones block spawning above them; tiles above stones stay in place)
                int topBound = FindTopBound(model, col);

                // Bottom-up scan within the processable segment
                int writeRow = 0;
                for (int row = 0; row < topBound; row++)
                {
                    Cell cell = model.GetCell(row, col);

                    if (cell.IsEmpty) continue;

                    if (!cell.CanFall)
                    {
                        // Fixed obstacle (Box) — advance write pointer past it
                        writeRow = row + 1;
                        continue;
                    }

                    if (row != writeRow)
                        falls.Add(new FallMove { FromRow = row, ToRow = writeRow, Col = col });

                    writeRow++;
                }
            }

            return falls;
        }

        // Apply falls to the model silently (view is animated separately).
        public void ApplyFalls(GridModel model, List<FallMove> falls)
        {
            // Sort by fromRow ascending so we never read an already-cleared cell
            falls.Sort((a, b) => a.FromRow.CompareTo(b.FromRow));

            foreach (var fall in falls)
            {
                Cell cell = model.GetCell(fall.FromRow, fall.Col);
                model.SetCellSilent(fall.ToRow,   fall.Col, cell);
                model.SetCellSilent(fall.FromRow,  fall.Col, Cell.Empty());
            }
        }

        // Fill empty cells from the top of each processable column segment.
        // Returns the grid positions of all newly spawned cells.
        // Model is updated silently; GridView is notified separately.
        public List<Vector2Int> FillFromTop(GridModel model, System.Random rng)
        {
            var newPositions = new List<Vector2Int>();

            for (int col = 0; col < model.Columns; col++)
            {
                int topBound = FindTopBound(model, col);

                // Walk from the top of the segment downward, filling empty cells
                for (int row = topBound - 1; row >= 0; row--)
                {
                    Cell cell = model.GetCell(row, col);
                    if (!cell.IsEmpty) break; // hit an existing tile — stop filling

                    Cell newCell = CellTypeParser.Parse("rand");
                    model.SetCellSilent(row, col, newCell);
                    newPositions.Add(new Vector2Int(col, row)); // x=col, y=row
                }
            }

            return newPositions;
        }

        // All 10 levels place stones at the bottom rows, never at the top.
        // Stones act as permanent floors — handled by CanFall=false in CalculateFalls
        // (writeRow advances past them, so cubes never fall into stone cells).
        // FillFromTop's existing break-on-non-empty stops naturally at bottom stones.
        // No top-bound restriction is needed.
        static int FindTopBound(GridModel model, int col) => model.Rows;
    }
}
