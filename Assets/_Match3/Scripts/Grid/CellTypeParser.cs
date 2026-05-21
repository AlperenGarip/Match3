using System;

namespace Match3.Grid
{
    // Single source of truth for JSON cell code → Cell mapping.
    // Adding a new tile type = one new case here only.
    public static class CellTypeParser
    {
        static readonly Random _rng = new Random();
        static readonly CellType[] _cubeTypes = { CellType.Red, CellType.Green, CellType.Blue, CellType.Yellow };

        public static Cell Parse(string code)
        {
            return code switch
            {
                "r"    => new Cell(CellType.Red,              colorIndex: 0),
                "g"    => new Cell(CellType.Green,            colorIndex: 1),
                "b"    => new Cell(CellType.Blue,             colorIndex: 2),
                "y"    => new Cell(CellType.Yellow,           colorIndex: 3),
                "rand" => RandomCube(),
                "bo"   => new Cell(CellType.Box),
                "s"    => new Cell(CellType.Stone),
                "v"    => new Cell(CellType.Vase1),
                "hro"  => new Cell(CellType.HorizontalRocket),
                "vro"  => new Cell(CellType.VerticalRocket),
                "tnt"  => new Cell(CellType.TNT),
                "lb"   => new Cell(CellType.LightBall),
                _      => throw new ArgumentException($"Unknown cell code: '{code}'"),
            };
        }

        static Cell RandomCube()
        {
            int index = _rng.Next(0, _cubeTypes.Length);
            return new Cell(_cubeTypes[index], colorIndex: index);
        }
    }
}
