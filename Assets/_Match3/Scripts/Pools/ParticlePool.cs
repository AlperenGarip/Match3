using System;
using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using Match3.Grid;
using UnityEngine;
using UnityEngine.Pool;

namespace Match3.Pools
{
    // Object pool for particle burst effects.
    // Supports one prefab per CellType — assign entries in the Inspector.
    // Call Play(worldPos, cellType). Falls back to the _defaultPrefab for unmapped types.
    public class ParticlePool : MonoBehaviour
    {
        [Serializable]
        public struct ParticleEntry
        {
            public CellType       Type;
            public ParticleSystem Prefab;
        }

        [SerializeField] ParticleSystem  _defaultPrefab;
        [SerializeField] ParticleEntry[] _entries;

        [SerializeField] int _defaultCapacity = 8;
        [SerializeField] int _maxSize = 32;

        readonly Dictionary<CellType, ObjectPool<ParticleSystem>> _pools = new();
        ObjectPool<ParticleSystem> _fallbackPool;

        void Awake()
        {
            if (_defaultPrefab != null)
                _fallbackPool = MakePool(_defaultPrefab);

            if (_entries != null)
                foreach (var e in _entries)
                    if (e.Prefab != null)
                        _pools[e.Type] = MakePool(e.Prefab);

            ServiceLocator.Register(this);
        }

        public void Play(Vector3 worldPos, CellType type)
        {
            ObjectPool<ParticleSystem> pool = PoolFor(type);
            if (pool == null) return;

            ParticleSystem ps = pool.Get();
            ps.transform.position = worldPos;
            ps.Play();
            StartCoroutine(ReleaseWhenDone(ps, pool));
        }

        ObjectPool<ParticleSystem> PoolFor(CellType type)
        {
            if (_pools.TryGetValue(type, out var pool)) return pool;
            return _fallbackPool;
        }

        IEnumerator ReleaseWhenDone(ParticleSystem ps, ObjectPool<ParticleSystem> pool)
        {
            yield return new WaitUntil(() => !ps.IsAlive(true));
            pool.Release(ps);
        }

        ObjectPool<ParticleSystem> MakePool(ParticleSystem prefab)
        {
            // Capture prefab in a local for the lambda
            ParticleSystem captured = prefab;
            return new ObjectPool<ParticleSystem>(
                createFunc:      () =>
                {
                    var ps = Instantiate(captured, transform);
                    var main = ps.main;
                    main.loop = false;
                    ps.gameObject.SetActive(false);
                    return ps;
                },
                actionOnGet:     ps => ps.gameObject.SetActive(true),
                actionOnRelease: ps =>
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                },
                actionOnDestroy: ps => Destroy(ps.gameObject),
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize:         _maxSize
            );
        }
    }
}
