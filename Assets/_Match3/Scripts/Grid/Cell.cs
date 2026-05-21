namespace Match3.Grid
{
    public class Cell
    {
        public CellType Type;

        // 0=Red, 1=Green, 2=Blue, 3=Yellow; -1 for non-cubes
        public int ColorIndex;

        public bool IsEmpty      => Type == CellType.Empty;
        public bool IsCube       => Type is CellType.Red or CellType.Green or CellType.Blue or CellType.Yellow;
        public bool IsObstacle   => Type is CellType.Box or CellType.Stone or CellType.Vase1 or CellType.Vase2;
        public bool IsPowerUp    => Type is CellType.HorizontalRocket or CellType.VerticalRocket
                                         or CellType.TNT or CellType.LightBall;

        // Refinement 1: centralised gravity rules used by GravitySystem
        // Cubes, vases, and power-ups fall with gravity; boxes and stones are fixed.
        public bool CanFall      => IsCube || Type is CellType.Vase1 or CellType.Vase2 || IsPowerUp;

        // Only Stone blocks spawning above it; everything else allows cubes to fall past.
        public bool BlocksColumnAbove => Type == CellType.Stone;

        // Stone and Box cannot be moved by the player; Vase can be swapped.
        public bool CanBeSwapped => !IsEmpty && Type != CellType.Stone && Type != CellType.Box;

        public Cell(CellType type, int colorIndex = -1)
        {
            Type = type;
            ColorIndex = colorIndex;
        }

        public static Cell Empty() => new Cell(CellType.Empty);
    }
}
