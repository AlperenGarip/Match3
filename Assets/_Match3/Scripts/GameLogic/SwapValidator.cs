using Match3.Grid;
using UnityEngine;

namespace Match3.GameLogic
{
    // Pure C# — no MonoBehaviour, no side effects.
    public class SwapValidator
    {
        public bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        // Checks whether swapping cells at a and b would produce a match of 3+.
        // Read-only — does NOT modify the model.
        public bool WouldProduceMatch(GridModel model, Vector2Int a, Vector2Int b)
        {
            Cell cellA = model.GetCell(a.y, a.x);
            Cell cellB = model.GetCell(b.y, b.x);

            // After swap: cellB hypothetically at a, cellA hypothetically at b
            return HasMatchAt(model, a, cellB, b, cellA)
                || HasMatchAt(model, b, cellA, a, cellB);
        }

        bool HasMatchAt(GridModel model, Vector2Int pos, Cell hypothetical,
                        Vector2Int swapPos, Cell swapPosCell)
        {
            if (!hypothetical.IsCube) return false;
            int color = hypothetical.ColorIndex;

            int h = 1
                + CountSame(model, pos, color, 0,  1, swapPos, swapPosCell)
                + CountSame(model, pos, color, 0, -1, swapPos, swapPosCell);
            if (h >= 3) return true;

            int v = 1
                + CountSame(model, pos, color,  1, 0, swapPos, swapPosCell)
                + CountSame(model, pos, color, -1, 0, swapPos, swapPosCell);
            return v >= 3;
        }

        // Walk in direction (dr, dc) counting cubes of the same color.
        // Treats swapPos as containing swapPosCell (hypothetical post-swap state).
        int CountSame(GridModel model, Vector2Int origin, int colorIndex,
                      int dr, int dc, Vector2Int swapPos, Cell swapPosCell)
        {
            int count = 0;
            int r = origin.y + dr;
            int c = origin.x + dc;

            while (model.IsInBounds(r, c))
            {
                Cell cell = (r == swapPos.y && c == swapPos.x)
                    ? swapPosCell
                    : model.GetCell(r, c);

                if (!cell.IsCube || cell.ColorIndex != colorIndex) break;
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }
    }
}
