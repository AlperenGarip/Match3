using System;
using System.Collections.Generic;
using DG.Tweening;
using Match3.Core;
using Match3.GameLogic.Commands;
using Match3.Grid;
using Match3.Input;
using Match3.PowerUps;
using Match3.VFX;
using UnityEngine;

namespace Match3.GameLogic
{
    public struct GameWonEvent  {}
    public struct GameLostEvent {}

    public struct PowerUpActivatedEvent
    {
        public CellType PowerUpType;
        public Vector3  WorldPos;
    }

    // Fired for every cube or power-up cleared (not obstacles — those use TileClearedEvent).
    // Used by JuiceController to play color-matched particle bursts.
    public struct TilePoppedEvent
    {
        public CellType   Type;
        public Vector2Int GridPos;
    }

    public struct RocketFiredEvent
    {
        public CellType   RocketType;  // HorizontalRocket or VerticalRocket
        public Vector2Int GridPos;     // origin cell — row drives H-beam, col drives V-beam
        public float      Scale;       // 1.0 = normal, 2.0 = super (TNT+Rocket combo)
    }

    // State machine for the Match-3 gameplay loop.
    // All state transitions after animations use DOTween OnComplete — never fixed delays (Refinement 3).
    // Swipe events are enqueued during processing so no input is lost (Refinement 3).
    public class BoardController : MonoBehaviour
    {
        GridModel _model;
        GridView _view;
        MatchFinder _matchFinder;
        SwapValidator _validator;
        GravitySystem _gravity;
        ObstacleSystem _obstacleSystem;
        System.Random _rng;

        bool _isProcessing;
        Queue<SwipedEvent> _inputQueue = new();

        // ── Lifecycle ──────────────────────────────────────────────────────
        void OnEnable()
        {
            EventBus.Subscribe<LevelReadyEvent>(OnLevelReady);
            EventBus.Subscribe<SwipedEvent>(OnSwipedEvent);
            EventBus.Subscribe<TappedEvent>(OnTappedEvent);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<LevelReadyEvent>(OnLevelReady);
            EventBus.Unsubscribe<SwipedEvent>(OnSwipedEvent);
            EventBus.Unsubscribe<TappedEvent>(OnTappedEvent);
        }

        // ── Initialisation ─────────────────────────────────────────────────
        void OnLevelReady(LevelReadyEvent evt)
        {
            _model = evt.Model;
            _view = evt.View;
            _matchFinder = new MatchFinder();
            _validator = new SwapValidator();
            _gravity = new GravitySystem();
            _obstacleSystem = new ObstacleSystem();
            _rng = new System.Random();

            Debug.Log("[BoardController] Ready.");

            // Clear any pre-existing matches in the initial board layout so the player
            // starts from a stable, match-free state. Deferred via DOTween so all other
            // LevelReadyEvent subscribers (GoalTracker, MoveCounter, JuiceController …)
            // finish initialising first.
            DOVirtual.DelayedCall(0.1f, RunInitialMatchCycle);
        }

        void RunInitialMatchCycle()
        {
            if (_model == null) return;
            _isProcessing = true;
            RunMatchCycle(Vector2Int.zero, Vector2Int.zero, isFirstSwap: false);
        }

        // ── Input handling ─────────────────────────────────────────────────
        void OnSwipedEvent(SwipedEvent evt)
        {
            if (_model == null) return;
            if (_isProcessing)
            {
                // Keep only the latest pending move — discard earlier buffered inputs
                _inputQueue.Clear();
                _inputQueue.Enqueue(evt);
                return;
            }

            _isProcessing = true;
            HandleSwipe(evt.From, evt.To);
        }

        // Tap on a power-up → activate it without a partner (single activation).
        void OnTappedEvent(TappedEvent evt)
        {
            if (_model == null || _isProcessing) return;

            if (!_model.IsInBounds(evt.Cell.y, evt.Cell.x)) return;
            Cell cell = _model.GetCell(evt.Cell.y, evt.Cell.x);
            if (!cell.IsPowerUp) return;

            _isProcessing = true;
            ActivateSinglePowerUp(evt.Cell, cell);
        }

        void HandleSwipe(Vector2Int from, Vector2Int to)
        {
            // Bounds check
            if (!_model.IsInBounds(from.y, from.x) || !_model.IsInBounds(to.y, to.x))
            { EndProcessing(); return; }

            // Same cell
            if (from == to) { EndProcessing(); return; }

            // Adjacency check
            if (!_validator.IsAdjacent(from, to))
            { EndProcessing(); return; }

            Cell cellFrom = _model.GetCell(from.y, from.x);
            Cell cellTo   = _model.GetCell(to.y,   to.x);

            // Stone and Box cannot be swapped by the player; Vase can
            if (!cellFrom.CanBeSwapped || !cellTo.CanBeSwapped)
            { _view.AnimateInvalidSwap(from, to, EndProcessing); return; }

            // LightBall + any obstacle is an illegal move (LightBall only combos with cubes/power-ups)
            bool lightBallObstacleCombo =
                (cellFrom.Type == CellType.LightBall && cellTo.IsObstacle)   ||
                (cellTo.Type   == CellType.LightBall && cellFrom.IsObstacle);
            if (lightBallObstacleCombo)
            { _view.AnimateInvalidSwap(from, to, EndProcessing); return; }

            // ── Power-up activation ────────────────────────────────────────
            if (cellFrom.IsPowerUp || cellTo.IsPowerUp)
            {
                ActivatePowerUp(from, to, cellFrom, cellTo);
                return;
            }

            // At least one side must be a cube
            if (!cellFrom.IsCube && !cellTo.IsCube)
            { EndProcessing(); return; }

            // Would it produce a match?
            if (!_validator.WouldProduceMatch(_model, from, to))
            {
                _view.AnimateInvalidSwap(from, to, EndProcessing);
                return;
            }

            // Valid swap — update model silently, animate view
            var cmd = new SwapCommand(_model, from, to);
            cmd.Execute();

            _view.AnimateSwap(from, to)
                .OnComplete(() => RunMatchCycle(from, to, isFirstSwap: true, cmd));
        }

        // ── Power-up activation (swipe: one or both cells are power-ups) ──
        void ActivatePowerUp(Vector2Int from, Vector2Int to, Cell cellFrom, Cell cellTo)
        {
            if (ServiceLocator.TryGet(out MoveCounter mc)) mc.ConsumeMove();
            Debug.Log("[BoardController] Move consumed (power-up).");

            EventBus.Publish(new PowerUpActivatedEvent
            {
                PowerUpType = cellFrom.IsPowerUp ? cellFrom.Type : cellTo.Type,
                WorldPos    = _view.GridToWorld(from.y, from.x)
            });

            // Swap the model to match what AnimateSwap does to the view —
            // both must agree on which tile is where before ClearCells runs.
            // After this: model[from] = cellTo, model[to] = cellFrom.
            _model.SetCellSilent(from.y, from.x, cellTo);
            _model.SetCellSilent(to.y,   to.x,   cellFrom);

            // LightBall + non-LightBall partner: route to the sequenced animation handler.
            // LightBall + LightBall stays on the normal strategy path (instant board-clear).
            bool fromIsLB = cellFrom.Type == CellType.LightBall;
            bool toIsLB   = cellTo.Type   == CellType.LightBall;
            if ((fromIsLB || toIsLB) && !(fromIsLB && toIsLB))
            {
                HandleLightBallCombo(from, to, cellFrom, cellTo);
                return;
            }

            // Strategy is resolved using the original cell types before any swap.
            IActivationStrategy strategy = ComboResolver.Resolve(cellFrom, cellTo, from, to, _model);

            // Derive origin and primary positions from the power-up's landing cell.
            // AnimateSwap physically moves the tile that was at 'from' to 'to' and vice versa,
            // so a power-up that was at 'to' lands at 'from', and vice versa.
            Vector2Int origin;
            var primaryPowerUps = new HashSet<Vector2Int>();

            if (cellFrom.IsPowerUp && cellTo.IsPowerUp)
            {
                // Origin = swipe destination (where the dragged power-up lands).
                // All rocket+rocket combos fire a cross centred here.
                origin = to;
                primaryPowerUps.Add(from);
                primaryPowerUps.Add(to);
            }
            else if (cellFrom.IsPowerUp)
            {
                origin = to;   // power-up was at 'from', lands at 'to'
                primaryPowerUps.Add(to);
            }
            else
            {
                origin = from; // power-up was at 'to', lands at 'from'
                primaryPowerUps.Add(from);
            }

            var seeds = new HashSet<Vector2Int>(primaryPowerUps);
            foreach (var pos in strategy.GetAffectedCells(origin, _model)) seeds.Add(pos);

            var toClear      = ExpandChain(seeds, primaryPowerUps, out var chainRockets);
            var rocketEvents = BuildRocketEvents(cellFrom, cellTo, from, to);
            rocketEvents.AddRange(chainRockets);

            // TNT+Rocket combo: the primary TNT's ring is suppressed because the 3-rocket
            // cross is the signature visual. Chain TNTs further out still ring normally.
            HashSet<Vector2Int> tntExclude = null;
            bool isTNTRocketCombo =
                (cellFrom.Type == CellType.TNT && cellTo.Type is CellType.HorizontalRocket or CellType.VerticalRocket) ||
                (cellTo.Type   == CellType.TNT && cellFrom.Type is CellType.HorizontalRocket or CellType.VerticalRocket);
            if (isTNTRocketCombo)
            {
                tntExclude = new HashSet<Vector2Int>();
                // TNT lands at the opposite cell after swap.
                tntExclude.Add(cellFrom.Type == CellType.TNT ? to : from);
            }

            _view.AnimateSwap(from, to).OnComplete(() =>
            {
                var tntPositions = CollectTNTs(toClear, tntExclude);
                ClearCells(toClear);
                RunVFXThenGravity(rocketEvents, tntPositions,
                    () => RunMatchCycle(from, to, isFirstSwap: false));
            });
        }

        // ── LightBall combo sequenced animation ────────────────────────────
        // Routes LightBall + cube/rocket/TNT into the slow theatrical animation:
        // highlight matching cubes one-by-one, then pop / transform-and-trigger.
        void HandleLightBallCombo(Vector2Int from, Vector2Int to, Cell cellFrom, Cell cellTo)
        {
            // Post-swap: cellFrom is now at 'to', cellTo is now at 'from'.
            bool       fromIsLB   = cellFrom.Type == CellType.LightBall;
            Vector2Int lbPos      = fromIsLB ? to   : from;  // LightBall's post-swap position
            Vector2Int partnerPos = fromIsLB ? from : to;    // partner's post-swap position
            Cell       partner    = fromIsLB ? cellTo : cellFrom;

            // Choose target color: partner cube's own colour, else most-common on the board.
            int color = partner.IsCube
                ? partner.ColorIndex
                : ComboResolver.MostCommonColorIndex(_model);

            // Collect all cubes of that colour, excluding the LightBall and partner cells.
            var targets = new List<Vector2Int>();
            for (int r = 0; r < _model.Rows; r++)
            for (int c = 0; c < _model.Columns; c++)
            {
                Cell cell = _model.GetCell(r, c);
                if (!cell.IsCube || cell.ColorIndex != color) continue;
                var p = new Vector2Int(c, r);
                if (p == lbPos || p == partnerPos) continue;
                targets.Add(p);
            }

            _view.AnimateSwap(from, to).OnComplete(() =>
            {
                if (partner.IsCube)
                    RunLightBallPop(lbPos, partnerPos, targets);
                else if (partner.Type is CellType.HorizontalRocket or CellType.VerticalRocket)
                    RunLightBallRocket(lbPos, partnerPos, targets);
                else if (partner.Type == CellType.TNT)
                    RunLightBallTNT(lbPos, partnerPos, targets);
                else
                    FinishLightBallInstantly(lbPos, partnerPos, targets); // fallback safety
            });
        }

        // LightBall + Cube: pulse each matching cube, then pop everything together.
        void RunLightBallPop(Vector2Int lbPos, Vector2Int partnerPos, List<Vector2Int> targets)
        {
            Action onAllHighlighted = () =>
            {
                var toClear = new HashSet<Vector2Int>(targets) { lbPos, partnerPos };
                ClearCells(toClear);
                RunGravity(() => RunMatchCycle(lbPos, lbPos, isFirstSwap: false));
            };

            if (ServiceLocator.TryGet(out LightBallAnimator animator))
                animator.PlayHighlightSequence(targets).OnComplete(() => onAllHighlighted());
            else
                onAllHighlighted();
        }

        // LightBall + Rocket: pulse each matching cube, transform it into a rocket as it pulses,
        // then activate every transformed rocket simultaneously.
        void RunLightBallRocket(Vector2Int lbPos, Vector2Int partnerPos, List<Vector2Int> targets)
        {
            var rocketEvents = new List<RocketFiredEvent>();

            Action<Vector2Int> onEachHighlight = pos =>
            {
                var rocketType = UnityEngine.Random.value > 0.5f
                    ? CellType.HorizontalRocket
                    : CellType.VerticalRocket;
                _model.SetCellSilent(pos.y, pos.x, new Cell(rocketType));
                _view.GetTileView(pos.y, pos.x)?.UpdateVisual(_model.GetCell(pos.y, pos.x));
                rocketEvents.Add(new RocketFiredEvent { RocketType = rocketType, GridPos = pos, Scale = 0.4f });
            };

            Action onAllTransformed = () =>
            {
                // Seeds include every transformed rocket plus LightBall and partner cells.
                var seeds = new HashSet<Vector2Int>(targets) { lbPos, partnerPos };
                // skipPowerUps is the same set — ExpandChain only adds AoE from new power-ups,
                // and the transformed rockets are *already* expected to fire here.
                foreach (var p in targets)
                {
                    var rocketCell = _model.GetCell(p.y, p.x);
                    foreach (var cell in ComboResolver.Single(rocketCell, _model).GetAffectedCells(p, _model))
                        seeds.Add(cell);
                }
                var toClear = ExpandChain(seeds, seeds, out var chainRockets);
                rocketEvents.AddRange(chainRockets);

                var tntPositions = CollectTNTs(toClear);
                ClearCells(toClear);

                RunVFXThenGravity(rocketEvents, tntPositions,
                    () => RunMatchCycle(lbPos, lbPos, isFirstSwap: false));
            };

            if (ServiceLocator.TryGet(out LightBallAnimator animator))
                animator.PlayHighlightSequence(targets, onEachHighlight).OnComplete(() => onAllTransformed());
            else
            {
                foreach (var pos in targets) onEachHighlight(pos);
                onAllTransformed();
            }
        }

        // LightBall + TNT: pulse each matching cube, transform it into TNT as it pulses,
        // then activate every transformed TNT simultaneously.
        void RunLightBallTNT(Vector2Int lbPos, Vector2Int partnerPos, List<Vector2Int> targets)
        {
            Action<Vector2Int> onEachHighlight = pos =>
            {
                _model.SetCellSilent(pos.y, pos.x, new Cell(CellType.TNT));
                _view.GetTileView(pos.y, pos.x)?.UpdateVisual(_model.GetCell(pos.y, pos.x));
            };

            Action onAllTransformed = () =>
            {
                var seeds = new HashSet<Vector2Int>(targets) { lbPos, partnerPos };
                foreach (var p in targets)
                {
                    var tntCell = _model.GetCell(p.y, p.x);
                    foreach (var cell in ComboResolver.Single(tntCell, _model).GetAffectedCells(p, _model))
                        seeds.Add(cell);
                }
                var toClear = ExpandChain(seeds, seeds, out var chainRockets);

                var tntPositions = CollectTNTs(toClear);
                ClearCells(toClear);

                RunVFXThenGravity(chainRockets, tntPositions,
                    () => RunMatchCycle(lbPos, lbPos, isFirstSwap: false));
            };

            if (ServiceLocator.TryGet(out LightBallAnimator animator))
                animator.PlayHighlightSequence(targets, onEachHighlight).OnComplete(() => onAllTransformed());
            else
            {
                foreach (var pos in targets) onEachHighlight(pos);
                onAllTransformed();
            }
        }

        // Fallback: if no animator is wired AND the combo type isn't recognised, do an instant clear
        // so the level doesn't soft-lock.
        void FinishLightBallInstantly(Vector2Int lbPos, Vector2Int partnerPos, List<Vector2Int> targets)
        {
            var toClear = new HashSet<Vector2Int>(targets) { lbPos, partnerPos };
            ClearCells(toClear);
            RunGravity(() => RunMatchCycle(lbPos, lbPos, isFirstSwap: false));
        }

        // Collect TNT cell positions before ClearCells empties them.
        // Used by RunVFXThenGravity to play a shockwave ring on each detonating TNT.
        // exclude: positions whose TNT should *not* trigger a ring (e.g. TNT+Rocket
        // combo, where the 3-rocket cross visual replaces the TNT ring).
        List<Vector2Int> CollectTNTs(IEnumerable<Vector2Int> cells, HashSet<Vector2Int> exclude = null)
        {
            var tnts = new List<Vector2Int>();
            foreach (var pos in cells)
            {
                if (exclude != null && exclude.Contains(pos)) continue;
                if (_model.GetCell(pos.y, pos.x).Type == CellType.TNT)
                    tnts.Add(pos);
            }
            return tnts;
        }

        // Plays rocket beams + TNT rings in parallel, then chains gravity → onGravityComplete.
        // If no VFX is needed and/or JuiceController is missing, runs gravity immediately.
        void RunVFXThenGravity(List<RocketFiredEvent> rocketEvents,
                                List<Vector2Int> tntPositions,
                                Action onGravityComplete)
        {
            Sequence rocketSeq = null;
            Sequence ringSeq   = null;

            if (ServiceLocator.TryGet(out JuiceController juice))
            {
                if (rocketEvents != null && rocketEvents.Count > 0)
                    rocketSeq = juice.PlayRocketBeams(rocketEvents);
                if (tntPositions != null && tntPositions.Count > 0)
                    ringSeq   = juice.PlayTNTRings(tntPositions);
            }

            if (rocketSeq != null || ringSeq != null)
            {
                var seq = DOTween.Sequence();
                if (rocketSeq != null) seq.Join(rocketSeq);
                if (ringSeq   != null) seq.Join(ringSeq);
                seq.OnComplete(() => RunGravity(onGravityComplete));
            }
            else
            {
                RunGravity(onGravityComplete);
            }
        }

        // Tap-activated single power-up (no partner).
        // TODO Phase 5: decide whether taps cost a move.
        void ActivateSinglePowerUp(Vector2Int pos, Cell cell)
        {
            if (ServiceLocator.TryGet(out MoveCounter mc)) mc.ConsumeMove();
            Debug.Log("[BoardController] Power-up tapped.");

            EventBus.Publish(new PowerUpActivatedEvent
            {
                PowerUpType = cell.Type,
                WorldPos    = _view.GridToWorld(pos.y, pos.x)
            });

            // LightBall tap: run the same sequenced pop animation as LightBall+Cube combo,
            // using the most-common color on the board as the target.
            if (cell.Type == CellType.LightBall)
            {
                int color = ComboResolver.MostCommonColorIndex(_model);
                var targets = new List<Vector2Int>();
                for (int r = 0; r < _model.Rows; r++)
                for (int c = 0; c < _model.Columns; c++)
                {
                    Cell other = _model.GetCell(r, c);
                    if (!other.IsCube || other.ColorIndex != color) continue;
                    var p = new Vector2Int(c, r);
                    if (p == pos) continue;
                    targets.Add(p);
                }
                // No partner — pass lbPos twice; HashSet dedups in the final clear set.
                RunLightBallPop(pos, pos, targets);
                return;
            }

            IActivationStrategy strategy = ComboResolver.Single(cell, _model);

            // pos is primary — skip it in chain expansion (already resolved via Single above)
            var primaryPowerUps = new HashSet<Vector2Int> { pos };
            var seeds = new HashSet<Vector2Int> { pos };
            foreach (var p in strategy.GetAffectedCells(pos, _model)) seeds.Add(p);

            var toClear = ExpandChain(seeds, primaryPowerUps, out var chainRockets);

            var tntPositions = CollectTNTs(toClear);
            ClearCells(toClear);

            var rocketEvents = new List<RocketFiredEvent>();
            if (cell.Type is CellType.HorizontalRocket or CellType.VerticalRocket)
                rocketEvents.Add(new RocketFiredEvent { RocketType = cell.Type, GridPos = pos, Scale = 0.4f });
            rocketEvents.AddRange(chainRockets);

            RunVFXThenGravity(rocketEvents, tntPositions,
                () => RunMatchCycle(pos, pos, isFirstSwap: false));
        }

        // Returns rocket beam events for power-up swipe combos.
        // TNT+Rocket → 3 rows + 3 cols, large scale.
        // Rocket+Rocket → 1 row + 1 col, normal scale.
        // LightBall combos → empty (different effect, no beam).
        // Post-swap positions: cellFrom lands at 'to', cellTo lands at 'from'.
        static List<RocketFiredEvent> BuildRocketEvents(Cell cellFrom, Cell cellTo,
                                                        Vector2Int from, Vector2Int to)
        {
            bool fromIsRocket = cellFrom.Type is CellType.HorizontalRocket or CellType.VerticalRocket;
            bool toIsRocket   = cellTo.Type   is CellType.HorizontalRocket or CellType.VerticalRocket;
            bool fromIsTNT    = cellFrom.Type == CellType.TNT;
            bool toIsTNT      = cellTo.Type   == CellType.TNT;

            // TNT + Rocket: 3 H beams (rows to-1/to/to+1) + 3 V beams (cols to-1/to/to+1)
            // Middle beam of each direction is centred on the move's end point.
            if ((fromIsTNT && toIsRocket) || (fromIsRocket && toIsTNT))
            {
                var events = new List<RocketFiredEvent>();
                for (int dr = -1; dr <= 1; dr++)
                    events.Add(new RocketFiredEvent
                    {
                        RocketType = CellType.HorizontalRocket,
                        GridPos    = new Vector2Int(to.x, to.y + dr),
                        Scale      = 0.4f,
                    });
                for (int dc = -1; dc <= 1; dc++)
                    events.Add(new RocketFiredEvent
                    {
                        RocketType = CellType.VerticalRocket,
                        GridPos    = new Vector2Int(to.x + dc, to.y),
                        Scale      = 0.4f,
                    });
                return events;
            }

            // Rocket + Rocket: one beam per rocket at its post-swap cell
            if (fromIsRocket && toIsRocket)
            {
                return new List<RocketFiredEvent>
                {
                    new RocketFiredEvent { RocketType = cellFrom.Type, GridPos = to,   Scale = 0.4f },
                    new RocketFiredEvent { RocketType = cellTo.Type,   GridPos = from, Scale = 0.4f },
                };
            }

            // Single rocket swiped into a cube/obstacle — beam at rocket's post-swap position
            if (fromIsRocket)
                return new List<RocketFiredEvent> { new RocketFiredEvent { RocketType = cellFrom.Type, GridPos = to,   Scale = 0.4f } };
            if (toIsRocket)
                return new List<RocketFiredEvent> { new RocketFiredEvent { RocketType = cellTo.Type,   GridPos = from, Scale = 0.4f } };

            return new List<RocketFiredEvent>(); // LightBall combos — no beam
        }


        // ── Chain reaction expansion ───────────────────────────────────────
        // Any power-up cell in the AoE also fires its own strategy.
        // skipPowerUps: cells whose power-up was already resolved — do not re-process them.
        // chainRockets: populated with beam events for every rocket activated by chain.
        HashSet<Vector2Int> ExpandChain(HashSet<Vector2Int> seeds, HashSet<Vector2Int> skipPowerUps,
                                         out List<RocketFiredEvent> chainRockets)
        {
            chainRockets = new List<RocketFiredEvent>();
            var all   = new HashSet<Vector2Int>(seeds);
            var queue = new Queue<Vector2Int>(seeds);

            while (queue.Count > 0)
            {
                var pos  = queue.Dequeue();
                if (skipPowerUps.Contains(pos)) continue; // already resolved

                Cell cell = _model.GetCell(pos.y, pos.x);
                if (!cell.IsPowerUp) continue;

                // Record beam events for rockets triggered by chain
                if (cell.Type is CellType.HorizontalRocket or CellType.VerticalRocket)
                    chainRockets.Add(new RocketFiredEvent { RocketType = cell.Type, GridPos = pos, Scale = 0.4f });

                var extra = ComboResolver.Single(cell, _model).GetAffectedCells(pos, _model);
                foreach (var p in extra)
                    if (all.Add(p)) queue.Enqueue(p);
            }
            return all;
        }

        // ── Clear helper ───────────────────────────────────────────────────
        // For cubes/power-ups: clear directly.
        // For obstacles: route through ObstacleSystem (hit logic, Vase HP, TileClearedEvent).
        // Stone cells are immune — skipped entirely.
        // After clearing, runs adjacent obstacle splash for all cleared cube positions.
        void ClearCells(IEnumerable<Vector2Int> cells)
        {
            var cubePositions = new List<Vector2Int>();
            foreach (var pos in cells)
            {
                Cell cell = _model.GetCell(pos.y, pos.x);
                if (cell.IsEmpty) continue;
                if (cell.IsObstacle)
                {
                    _obstacleSystem.HitObstacle(pos, _model, _view);
                }
                else
                {
                    _view.ClearCell(pos.y, pos.x);
                    _model.SetCellSilent(pos.y, pos.x, Cell.Empty());
                    EventBus.Publish(new TilePoppedEvent { Type = cell.Type, GridPos = pos });
                    if (cell.IsCube) cubePositions.Add(pos);
                }
            }
            _obstacleSystem.ProcessAdjacentObstacles(cubePositions, _model, _view);
        }

        // ── Match cycle ────────────────────────────────────────────────────
        // Called after swap animation and after every gravity + spawn cycle (cascades).
        void RunMatchCycle(Vector2Int swapFrom, Vector2Int swapTo,
                           bool isFirstSwap, SwapCommand cmd = null)
        {
            var matches = isFirstSwap
                ? _matchFinder.FindAllMatches(_model, swapTo:   swapTo,
                                                              swapFrom: swapFrom)
                : _matchFinder.FindAllMatches(_model);

            if (matches.Count == 0)
            {
                if (isFirstSwap && cmd != null)
                {
                    // No match — revert swap in model and animate back
                    cmd.Undo();
                    _view.AnimateSwap(swapTo, swapFrom).OnComplete(EndProcessing);
                }
                else
                {
                    // Cascade ended — no more matches
                    CheckGoal();
                }
                return;
            }

            // ── Consume one move on the first valid swap ───────────────────
            if (isFirstSwap)
            {
                if (ServiceLocator.TryGet(out MoveCounter mc)) mc.ConsumeMove();
                Debug.Log("[BoardController] Move consumed.");

                // Override Line4 rocket direction to match the player's swipe direction.
                // A horizontal swipe → HorizontalRocket, vertical swipe → VerticalRocket.
                // Cascade matches (isFirstSwap = false) keep their natural match-orientation.
                bool horizontalSwipe = swapTo.x != swapFrom.x;
                foreach (var group in matches)
                {
                    if (group.Shape != MatchShape.HLine4 && group.Shape != MatchShape.VLine4) continue;
                    group.Shape          = horizontalSwipe ? MatchShape.HLine4 : MatchShape.VLine4;
                    group.PowerUpToSpawn = horizontalSwipe
                        ? (CellType?)CellType.HorizontalRocket
                        : (CellType?)CellType.VerticalRocket;
                }
            }

            // ── Clear matched cells (spawning power-ups where applicable) ──
            foreach (var group in matches)
            {
                var cleared = PowerUpSpawner.ProcessMatchGroup(group, _model, _view);
                _obstacleSystem.ProcessAdjacentObstacles(cleared, _model, _view);
            }

            // ── Run gravity, then cascade-check ───────────────────────────
            RunGravity(() => RunMatchCycle(swapFrom, swapTo, isFirstSwap: false));
        }

        // ── Gravity ────────────────────────────────────────────────────────
        void RunGravity(System.Action onComplete)
        {
            var falls = _gravity.CalculateFalls(_model);
            _gravity.ApplyFalls(_model, falls);

            if (falls.Count == 0)
            {
                SpawnAndContinue(onComplete);
                return;
            }

            var fallSeq = DOTween.Sequence();
            foreach (var fall in falls)
                fallSeq.Join(_view.MoveTileView(fall.FromRow, fall.Col, fall.ToRow, fall.Col));

            fallSeq.OnComplete(() =>
            {
                // Land squish (fire-and-forget)
                foreach (var fall in falls)
                    _view.GetTileView(fall.ToRow, fall.Col)?.AnimateLand();

                SpawnAndContinue(onComplete);
            });
        }

        void SpawnAndContinue(System.Action onComplete)
        {
            var newCells = _gravity.FillFromTop(_model, _rng);

            if (newCells.Count == 0)
            {
                onComplete();
                return;
            }

            // Group rows per column and sort ASCENDING (lowest row first).
            // stackOffset 0 → spawnRow = model.Rows (closest above grid) → enters view first.
            // stackOffset 1 → one row higher above grid → enters view second, etc.
            // Result: bottom-most new tile appears first, top-most tile appears last —
            // the natural "cascade from above" look.
            var byColumn = new Dictionary<int, List<int>>(); // col → sorted row list
            foreach (var pos in newCells)
            {
                if (!byColumn.ContainsKey(pos.x)) byColumn[pos.x] = new List<int>();
                byColumn[pos.x].Add(pos.y);
            }

            var spawnSeq = DOTween.Sequence();

            foreach (var kvp in byColumn)
            {
                int col = kvp.Key;
                List<int> rows = kvp.Value;
                rows.Sort(); // ascending: row 0 first → spawns closest to grid

                for (int i = 0; i < rows.Count; i++)
                {
                    int row        = rows[i];
                    int spawnRow   = _model.Rows + i;
                    Vector3 from   = _view.GridToWorld(spawnRow, col);
                    spawnSeq.Join(_view.SpawnTileAt(row, col, _model.GetCell(row, col), from));
                }
            }

            spawnSeq.OnComplete(onComplete.Invoke);
        }

        // ── Goal check ─────────────────────────────────────────────────────
        void CheckGoal()
        {
            Debug.Log("[BoardController] Cascade complete — checking goals.");

            ServiceLocator.TryGet(out GoalTracker goalTracker);
            ServiceLocator.TryGet(out MoveCounter moveCounter);

            if (goalTracker != null && goalTracker.IsGoalMet)
            {
                EventBus.Publish(new GameWonEvent());
                return; // game over — don't EndProcessing
            }
            if (moveCounter != null && moveCounter.MovesRemaining <= 0)
            {
                EventBus.Publish(new GameLostEvent());
                return;
            }
            EndProcessing();
        }

        // ── End of processing ──────────────────────────────────────────────
        void EndProcessing()
        {
            _isProcessing = false;

            if (_inputQueue.Count > 0)
            {
                var next = _inputQueue.Dequeue();
                _isProcessing = true;
                HandleSwipe(next.From, next.To);
            }
        }
    }
}
