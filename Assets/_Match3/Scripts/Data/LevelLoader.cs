using UnityEngine;

namespace Match3.Data
{
    public static class LevelLoader
    {
        const string ResourcePath = "Levels/level_{0:D2}";

        public static LevelData Load(int levelNumber)
        {
            string path = string.Format(ResourcePath, levelNumber);
            TextAsset asset = Resources.Load<TextAsset>(path);

            if (asset == null)
                throw new System.Exception($"Level file not found at Resources/{path}.json");

            LevelData data = JsonUtility.FromJson<LevelData>(asset.text);

            if (data == null)
                throw new System.Exception($"Failed to parse level JSON at Resources/{path}.json");

            int expectedCells = data.grid_width * data.grid_height;
            if (data.grid == null || data.grid.Length != expectedCells)
                throw new System.Exception(
                    $"Level {levelNumber}: grid array has {data.grid?.Length} cells, expected {expectedCells}");

            return data;
        }
    }
}
