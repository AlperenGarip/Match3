using System.Collections.Generic;
using DG.Tweening;
using Match3.Core;
using Match3.GameLogic;
using Match3.Grid;
using Match3.Pools;
using UnityEngine;

namespace Match3.VFX
{
    // Subscribes to game events and fires per-type particle bursts and rocket beams.
    // Attach to any persistent GameObject in LevelScene.
    // Registers itself in ServiceLocator so BoardController can call PlayRocketBeams directly.
    public class JuiceController : MonoBehaviour
    {
        [SerializeField] RocketBeamVFX _rocketBeamPrefab;
        [SerializeField] TNTRingVFX    _tntRingPrefab;

        GridView     _gridView;
        GridModel    _model;
        ParticlePool _particles;

        readonly List<RocketBeamVFX> _beamPool = new();
        readonly List<TNTRingVFX>    _ringPool = new();

        void OnEnable()
        {
            EventBus.Subscribe<LevelReadyEvent>(OnLevelReady);
            EventBus.Subscribe<TilePoppedEvent>(OnTilePopped);
            EventBus.Subscribe<TileClearedEvent>(OnTileCleared);
            EventBus.Subscribe<PowerUpActivatedEvent>(OnPowerUpActivated);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<LevelReadyEvent>(OnLevelReady);
            EventBus.Unsubscribe<TilePoppedEvent>(OnTilePopped);
            EventBus.Unsubscribe<TileClearedEvent>(OnTileCleared);
            EventBus.Unsubscribe<PowerUpActivatedEvent>(OnPowerUpActivated);
        }

        void OnLevelReady(LevelReadyEvent evt)
        {
            _gridView = evt.View;
            _model    = evt.Model;
            ServiceLocator.TryGet(out _particles);
            ServiceLocator.Register(this);
        }

        // ── Particle events ────────────────────────────────────────────────

        void OnTilePopped(TilePoppedEvent evt)
        {
            if (_particles == null || _gridView == null) return;
            Vector3 worldPos = _gridView.GridToWorld(evt.GridPos.y, evt.GridPos.x);
            _particles.Play(worldPos, evt.Type);
        }

        void OnTileCleared(TileClearedEvent evt)
        {
            if (_particles == null || _gridView == null) return;
            Vector3 worldPos = _gridView.GridToWorld(evt.GridPos.y, evt.GridPos.x);
            _particles.Play(worldPos, evt.ObstacleType);
        }

        void OnPowerUpActivated(PowerUpActivatedEvent evt)
        {
            if (_particles == null) return;
            _particles.Play(evt.WorldPos, evt.PowerUpType);
        }

        // ── Rocket beam VFX ────────────────────────────────────────────────
        // Called directly by BoardController (via ServiceLocator) to sequence gravity after beams.
        // Returns a Sequence that completes when all beams have finished animating.
        // Always returns a valid Sequence — falls back to zero-duration (instant complete)
        // if the prefab isn't set up, so BoardController's OnComplete chain never stalls.
        public Sequence PlayRocketBeams(List<RocketFiredEvent> events)
        {
            var seq = DOTween.Sequence();

            if (_gridView == null || _model == null || _rocketBeamPrefab == null)
            {
                Debug.LogWarning("[JuiceController] PlayRocketBeams: missing references — skipping beam VFX.");
                return seq; // empty sequence auto-completes → gravity runs immediately
            }

            foreach (var evt in events)
            {
                bool    isH    = evt.RocketType == CellType.HorizontalRocket;
                Vector3 origin = _gridView.GridToWorld(evt.GridPos.y, evt.GridPos.x);
                Vector3 endA, endB;

                // Use the max of the two side-distances so both projectile halves travel
                // the same distance at the same speed — the near-edge half overshoots the
                // board instead of crawling slowly to a nearby edge.
                if (isH)
                {
                    int leftDist  = evt.GridPos.x;
                    int rightDist = _model.Columns - 1 - evt.GridPos.x;
                    int maxDist   = Mathf.Max(leftDist, rightDist);
                    endA = _gridView.GridToWorld(evt.GridPos.y, evt.GridPos.x - maxDist);
                    endB = _gridView.GridToWorld(evt.GridPos.y, evt.GridPos.x + maxDist);
                }
                else
                {
                    int downDist = evt.GridPos.y;
                    int upDist   = _model.Rows - 1 - evt.GridPos.y;
                    int maxDist  = Mathf.Max(downDist, upDist);
                    endA = _gridView.GridToWorld(evt.GridPos.y - maxDist, evt.GridPos.x);
                    endB = _gridView.GridToWorld(evt.GridPos.y + maxDist, evt.GridPos.x);
                }

                RocketBeamVFX beam    = GetOrCreateBeam();
                Sequence      beamSeq = beam.Play(origin, endA, endB, isH, evt.Scale);
                if (beamSeq == null) continue; // beam not wired — skip, gravity still runs
                beamSeq.OnComplete(() => beam.Hide());
                seq.Join(beamSeq);
            }

            return seq;
        }

        RocketBeamVFX GetOrCreateBeam()
        {
            foreach (var b in _beamPool)
                if (!b.gameObject.activeInHierarchy) return b;

            RocketBeamVFX nb = Instantiate(_rocketBeamPrefab, transform);
            _beamPool.Add(nb);
            return nb;
        }

        // ── TNT ring VFX ───────────────────────────────────────────────────
        // Plays an expanding shockwave at each TNT grid position. Returns a Sequence
        // that completes when every ring finishes — BoardController joins this with
        // rocket beams before running gravity.
        public Sequence PlayTNTRings(List<Vector2Int> tntGridPositions)
        {
            var seq = DOTween.Sequence();

            if (_gridView == null || _tntRingPrefab == null)
            {
                Debug.LogWarning("[JuiceController] PlayTNTRings: missing references — skipping ring VFX.");
                return seq;
            }

            foreach (var pos in tntGridPositions)
            {
                Vector3    worldPos = _gridView.GridToWorld(pos.y, pos.x);
                TNTRingVFX ring     = GetOrCreateRing();
                Sequence   ringSeq  = ring.Play(worldPos);
                if (ringSeq == null) continue;
                ringSeq.OnComplete(() => ring.Hide());
                seq.Join(ringSeq);
            }

            return seq;
        }

        TNTRingVFX GetOrCreateRing()
        {
            foreach (var r in _ringPool)
                if (!r.gameObject.activeInHierarchy) return r;

            TNTRingVFX nr = Instantiate(_tntRingPrefab, transform);
            _ringPool.Add(nr);
            return nr;
        }
    }
}
