using System.Collections.Generic;
using Match3.Core;
using Match3.Data;
using UnityEngine;

namespace Match3.Grid
{
    public struct GoalCountsEvent
    {
        // Obstacle type → count at level start (the level goals)
        public Dictionary<CellType, int> Counts;
    }

    public class LevelBuilder
    {
        LevelData _data;
        GridModel _model;
        GridView  _view;

        public LevelBuilder WithLevelData(LevelData data)  { _data  = data;  return this; }
        public LevelBuilder WithGridModel(GridModel model)  { _model = model; return this; }
        public LevelBuilder WithGridView(GridView view)     { _view  = view;  return this; }

        public void Build()
        {
            if (_data == null)  throw new System.InvalidOperationException("LevelData not set.");
            if (_model == null) throw new System.InvalidOperationException("GridModel not set.");
            if (_view == null)  throw new System.InvalidOperationException("GridView not set.");

            _model.Initialize(_data);
            _view.BuildView(_model);

            var goalCounts = InferGoals();
            EventBus.Publish(new GoalCountsEvent { Counts = goalCounts });

            Debug.Log($"[LevelBuilder] Level {_data.level_number} loaded — " +
                      $"{_data.grid_width}×{_data.grid_height} grid, {_data.move_count} moves. " +
                      $"Goals: {GoalSummary(goalCounts)}");
        }

        // Count all obstacle instances → these become the level goals
        Dictionary<CellType, int> InferGoals()
        {
            var counts = new Dictionary<CellType, int>();

            for (int r = 0; r < _model.Rows; r++)
                for (int c = 0; c < _model.Columns; c++)
                {
                    Cell cell = _model.GetCell(r, c);
                    if (!cell.IsObstacle) continue;

                    // Vase1 and Vase2 both count toward the "Vase" goal
                    CellType key = (cell.Type == CellType.Vase2) ? CellType.Vase1 : cell.Type;

                    if (!counts.ContainsKey(key))
                        counts[key] = 0;
                    counts[key]++;
                }

            return counts;
        }

        static string GoalSummary(Dictionary<CellType, int> counts)
        {
            var parts = new List<string>();
            foreach (var kv in counts)
                parts.Add($"{kv.Key}×{kv.Value}");
            return parts.Count > 0 ? string.Join(", ", parts) : "none";
        }
    }
}
