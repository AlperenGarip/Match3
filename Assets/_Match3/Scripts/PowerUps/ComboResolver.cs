using System.Collections.Generic;
using Match3.Grid;
using Match3.PowerUps.Strategies;
using UnityEngine;

namespace Match3.PowerUps
{
    // Resolves which IActivationStrategy to use given two cells being swapped.
    // All combos are symmetric.
    public static class ComboResolver
    {
        public static IActivationStrategy Resolve(Cell cellFrom, Cell cellTo,
                                                  Vector2Int originFrom, Vector2Int originTo,
                                                  GridModel model)
        {
            CellType a = cellFrom.Type;
            CellType b = cellTo.Type;

            // ── LightBall combos ───────────────────────────────────────────
            if (a == CellType.LightBall && b == CellType.LightBall)
                return new LightBallStrategy(-1); // entire board

            if (a == CellType.LightBall || b == CellType.LightBall)
            {
                Cell other = a == CellType.LightBall ? cellTo : cellFrom;

                if (other.IsCube)
                    return new LightBallStrategy(other.ColorIndex);

                if (other.Type is CellType.HorizontalRocket or CellType.VerticalRocket)
                    return new LightBallRocketStrategy(other.ColorIndex, model);

                if (other.Type == CellType.TNT)
                    return new LightBallTNTStrategy(other.ColorIndex, model);

                // LightBall + LightBall already handled above; fallback
                return new LightBallStrategy(-1);
            }

            // ── TNT combos ─────────────────────────────────────────────────
            if (a == CellType.TNT && b == CellType.TNT)
                return new TNTStrategy(4); // Chebyshev ≤ 4 → 9×9

            if ((a == CellType.TNT && IsRocket(b)) || (b == CellType.TNT && IsRocket(a)))
                return new CrossStrategy(3); // 3 rows + 3 columns

            // ── Rocket combos ──────────────────────────────────────────────
            // All rocket+rocket pairs (H+H, V+V, H+V) behave identically:
            // clear 1 full row + 1 full column centred on the swipe destination.
            if (IsRocket(a) && IsRocket(b))
                return new CrossStrategy(1);

            // Single power-up + cube/obstacle: activate only the power-up cell.
            Cell powerCell = cellFrom.IsPowerUp ? cellFrom : cellTo;
            return Single(powerCell, model);
        }

        // Single power-up activation (tap or chain reaction — no combo partner).
        // For LightBall, clears the most common cube color on the board instead of everything.
        public static IActivationStrategy Single(Cell cell, GridModel model)
        {
            return cell.Type switch
            {
                CellType.HorizontalRocket => new HorizontalRocketStrategy(),
                CellType.VerticalRocket   => new VerticalRocketStrategy(),
                CellType.TNT              => new TNTStrategy(2),
                CellType.LightBall        => new LightBallStrategy(MostCommonColorIndex(model)),
                _                         => new HorizontalRocketStrategy(), // fallback
            };
        }

        // Returns the colorIndex (0–3) of the cube type with the highest count on the board.
        // Falls back to 0 (Red) if the board has no cubes.
        public static int MostCommonColorIndex(GridModel model)
        {
            var counts = new int[4]; // 0=Red, 1=Green, 2=Blue, 3=Yellow
            for (int row = 0; row < model.Rows; row++)
            for (int col = 0; col < model.Columns; col++)
            {
                Cell cell = model.GetCell(row, col);
                if (cell.IsCube && cell.ColorIndex >= 0 && cell.ColorIndex < 4)
                    counts[cell.ColorIndex]++;
            }
            int best = 0;
            for (int i = 1; i < 4; i++)
                if (counts[i] > counts[best]) best = i;
            return best;
        }

        static bool IsRocket(CellType t) =>
            t is CellType.HorizontalRocket or CellType.VerticalRocket;

        static IActivationStrategy SingleStrategy(CellType t, Vector2Int from, Vector2Int to) =>
            t switch
            {
                CellType.HorizontalRocket => new HorizontalRocketStrategy(),
                CellType.VerticalRocket   => new VerticalRocketStrategy(),
                CellType.TNT              => new TNTStrategy(2),
                CellType.LightBall        => new HorizontalRocketStrategy(), // never reached via Resolve
                _                         => new HorizontalRocketStrategy(),
            };
    }

    // ── Inline combo strategies ────────────────────────────────────────────

    // Row + column cross (width = size rows and size columns centred on each origin).
    public class CrossStrategy : IActivationStrategy
    {
        readonly int _halfWidth; // 1 → single row+col, 3 → 3 rows + 3 cols

        public CrossStrategy(int totalWidth) { _halfWidth = totalWidth / 2; }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            var seen  = new HashSet<Vector2Int>();

            void Add(int col, int row)
            {
                var p = new Vector2Int(col, row);
                if (model.IsInBounds(row, col) && seen.Add(p)) cells.Add(p);
            }

            // rows centred on origin
            for (int dr = -_halfWidth; dr <= _halfWidth; dr++)
                for (int col = 0; col < model.Columns; col++)
                    Add(col, origin.y + dr);

            // columns centred on origin
            for (int dc = -_halfWidth; dc <= _halfWidth; dc++)
                for (int row = 0; row < model.Rows; row++)
                    Add(origin.x + dc, row);

            return cells;
        }
    }

    public class MultiRowStrategy : IActivationStrategy
    {
        readonly int _count;
        public MultiRowStrategy(int count) { _count = count; }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            int half     = _count / 2;
            // Clamp start so we always clear exactly _count rows within board bounds.
            // Without this, rows near the top/bottom edge would be skipped, giving fewer rows.
            int startRow = Mathf.Clamp(origin.y - half, 0, Mathf.Max(0, model.Rows - _count));
            for (int row = startRow; row < startRow + _count; row++)
                for (int col = 0; col < model.Columns; col++)
                    cells.Add(new Vector2Int(col, row));
            return cells;
        }
    }

    public class MultiColumnStrategy : IActivationStrategy
    {
        readonly int _count;
        public MultiColumnStrategy(int count) { _count = count; }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            int half     = _count / 2;
            // Clamp start so we always clear exactly _count columns within board bounds.
            int startCol = Mathf.Clamp(origin.x - half, 0, Mathf.Max(0, model.Columns - _count));
            for (int col = startCol; col < startCol + _count; col++)
                for (int row = 0; row < model.Rows; row++)
                    cells.Add(new Vector2Int(col, row));
            return cells;
        }
    }

    // LightBall + Rocket: clear all cells of the given color, then treat each as a rocket.
    // For simplicity: collect all matching-color cells + their full row or column.
    public class LightBallRocketStrategy : IActivationStrategy
    {
        readonly int _colorIndex;
        readonly GridModel _model;

        public LightBallRocketStrategy(int colorIndex, GridModel model)
        {
            _colorIndex = colorIndex;
            _model      = model;
        }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            var seen  = new HashSet<Vector2Int>();

            void Add(int col, int row)
            {
                var p = new Vector2Int(col, row);
                if (model.IsInBounds(row, col) && seen.Add(p)) cells.Add(p);
            }

            for (int row = 0; row < model.Rows; row++)
            for (int col = 0; col < model.Columns; col++)
            {
                Cell cell = model.GetCell(row, col);
                if (cell.ColorIndex != _colorIndex) continue;
                // Add the whole row (horizontal rocket effect on each match)
                for (int c = 0; c < model.Columns; c++) Add(c, row);
            }
            return cells;
        }
    }

    // LightBall + TNT: clear all cells of the given color, expanding each as a TNT.
    public class LightBallTNTStrategy : IActivationStrategy
    {
        readonly int _colorIndex;
        readonly GridModel _model;

        public LightBallTNTStrategy(int colorIndex, GridModel model)
        {
            _colorIndex = colorIndex;
            _model      = model;
        }

        public List<Vector2Int> GetAffectedCells(Vector2Int origin, GridModel model)
        {
            var cells = new List<Vector2Int>();
            var seen  = new HashSet<Vector2Int>();

            void Add(int col, int row)
            {
                var p = new Vector2Int(col, row);
                if (model.IsInBounds(row, col) && seen.Add(p)) cells.Add(p);
            }

            var tnt = new Strategies.TNTStrategy(2);
            for (int row = 0; row < model.Rows; row++)
            for (int col = 0; col < model.Columns; col++)
            {
                Cell cell = model.GetCell(row, col);
                if (cell.ColorIndex != _colorIndex) continue;
                foreach (var p in tnt.GetAffectedCells(new Vector2Int(col, row), model))
                    Add(p.x, p.y);
            }
            return cells;
        }
    }
}
