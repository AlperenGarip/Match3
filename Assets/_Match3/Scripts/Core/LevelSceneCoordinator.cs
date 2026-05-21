using Match3.Data;
using Match3.Grid;
using UnityEngine;


namespace Match3.Core
{
    public struct LevelReadyEvent
    {
        public GridModel Model;
        public GridView  View;
        public LevelData Data;
    }

    // Wires together level loading for LevelScene.
    // Attach to the GameBootstrapper GameObject in LevelScene.
    // Phase 5: replace hardcoded level with SaveSystem.GetCurrentLevel().
    public class LevelSceneCoordinator : MonoBehaviour
    {
        [SerializeField] GridView _gridView;

        // 0 = use SaveSystem (production). 1–10 = force that level (debug override).
        [SerializeField] int _debugLevelOverride = 0;

        void Start()
        {
            int level = (_debugLevelOverride > 0) ? _debugLevelOverride : SaveSystem.GetCurrentLevel();
            LevelData data = LevelLoader.Load(level);
            var model = new GridModel();

            new LevelBuilder()
                .WithLevelData(data)
                .WithGridModel(model)
                .WithGridView(_gridView)
                .Build();

            ServiceLocator.Register(model);
            ServiceLocator.Register(_gridView);

            // Notify all systems that the level is ready
            EventBus.Publish(new LevelReadyEvent { Model = model, View = _gridView, Data = data });
        }
    }
}
