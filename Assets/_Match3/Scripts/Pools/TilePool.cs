using Match3.Core;
using Match3.Grid;
using UnityEngine;
using UnityEngine.Pool;

namespace Match3.Pools
{
    // Object pool for TileView instances.
    // Attach to a dedicated GameObject in LevelScene.
    // Registers itself in ServiceLocator so GridView can fetch it without a hard reference.
    public class TilePool : MonoBehaviour
    {
        [SerializeField] TileView _prefab;
        [SerializeField] int _defaultCapacity = 64;
        [SerializeField] int _maxSize = 256;

        ObjectPool<TileView> _pool;

        void Awake()
        {
            _pool = new ObjectPool<TileView>(
                createFunc:      CreateTile,
                actionOnGet:     tile => tile.gameObject.SetActive(true),
                actionOnRelease: tile => tile.gameObject.SetActive(false),
                actionOnDestroy: tile => Destroy(tile.gameObject),
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize:         _maxSize
            );
            ServiceLocator.Register(this);
        }

        // Fetch a tile, position it, and make it active.
        public TileView Get(Vector3 worldPos)
        {
            TileView tile = _pool.Get();
            tile.transform.position = worldPos;
            return tile;
        }

        // Kill tweens, reset visuals, return to pool. (Refinement 2)
        public void Release(TileView tile)
        {
            tile.KillAllTweens();
            tile.ResetToDefaults();
            _pool.Release(tile);
        }

        TileView CreateTile()
        {
            TileView tile = Instantiate(_prefab, transform);
            tile.gameObject.SetActive(false);
            return tile;
        }
    }
}
