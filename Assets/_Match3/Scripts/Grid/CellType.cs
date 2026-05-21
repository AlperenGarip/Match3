namespace Match3.Grid
{
    public enum CellType
    {
        Empty,

        // Cubes
        Red,
        Green,
        Blue,
        Yellow,

        // Obstacles
        Box,
        Stone,
        Vase1,   // full vase (2 HP)
        Vase2,   // cracked vase (1 HP)

        // Power-ups
        HorizontalRocket,
        VerticalRocket,
        TNT,
        LightBall,
    }
}
