using Match3.Core;
using Match3.Data;

namespace Match3.Grid
{
    public struct CellChangedEvent
    {
        public int Row;
        public int Col;
        public Cell NewCell;
    }

    // Pure data container — zero Unity dependency. Testable without Unity runtime.
    public class GridModel
    {
        public int Rows    { get; private set; }
        public int Columns { get; private set; }

        Cell[,] _cells;

        public void Initialize(LevelData data)
        {
            Rows    = data.grid_height;
            Columns = data.grid_width;
            _cells  = new Cell[Rows, Columns];

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    int index = r * Columns + c;
                    _cells[r, c] = CellTypeParser.Parse(data.grid[index]);
                }
        }

        public bool IsInBounds(int row, int col) =>
            row >= 0 && row < Rows && col >= 0 && col < Columns;

        public Cell GetCell(int row, int col) => _cells[row, col];

        public void SetCell(int row, int col, Cell cell)
        {
            _cells[row, col] = cell;
            EventBus.Publish(new CellChangedEvent { Row = row, Col = col, NewCell = cell });
        }

        // Silently update a cell without firing an event.
        // Used by gameplay systems (GravitySystem, SwapCommand) when the view
        // is updated explicitly via GridView methods rather than reactively.
        public void SetCellSilent(int row, int col, Cell cell)
        {
            _cells[row, col] = cell;
        }
    }
}
