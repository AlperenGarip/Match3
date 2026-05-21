using DG.Tweening;
using Match3.Core;
using Match3.Pools;
using UnityEngine;

namespace Match3.Grid
{
    public class GridView : MonoBehaviour
    {
        [Header("Tile")]
        [SerializeField] TileView _tilePrefab;

        [Header("Layout")]
        [SerializeField] float _cellSize = 1f;
        [SerializeField] Vector3 _gridOrigin = new Vector3(-4f, -5f, 0f);
        [Tooltip("Auto-calculate cellSize and origin to fit the board on screen.")]
        [SerializeField] bool _autoFit = true;
        [Tooltip("Vertical offset applied after auto-fit (negative = shift board down for top UI).")]
        [SerializeField] float _verticalOffset = 0f;

        [Header("Background")]
        [Tooltip("SpriteRenderer that displays grid_background.png — auto-sized to board dimensions.")]
        [SerializeField] SpriteRenderer _gridBackground;
        [Tooltip("Extra world-unit padding added around all four sides of the board (for the frame border).")]
        [SerializeField] float _gridBackgroundPadding = 0.15f;

        TileView[,] _tiles;
        GridModel _model;

        public void BuildView(GridModel model)
        {
            _model = model;

            if (_autoFit)
                AutoFitToCamera(model);

            if (_tiles != null)
            {
                for (int r = 0; r < _tiles.GetLength(0); r++)
                    for (int c = 0; c < _tiles.GetLength(1); c++)
                        if (_tiles[r, c] != null)
                            ClearCellInternal(r, c);
            }

            _tiles = new TileView[model.Rows, model.Columns];

            for (int r = 0; r < model.Rows; r++)
                for (int c = 0; c < model.Columns; c++)
                {
                    Cell cell = model.GetCell(r, c);
                    if (cell.IsEmpty) continue;
                    SpawnTileInternal(r, c, cell);
                }
        }

        // ── Coordinate helpers ─────────────────────────────────────────────
        public Vector3 GridToWorld(int row, int col)
        {
            return _gridOrigin + new Vector3(col * _cellSize, row * _cellSize, 0f);
        }

        public bool TryWorldToGrid(Vector3 worldPos, out Vector2Int gridPos)
        {
            int col = Mathf.RoundToInt((worldPos.x - _gridOrigin.x) / _cellSize);
            int row = Mathf.RoundToInt((worldPos.y - _gridOrigin.y) / _cellSize);
            gridPos = new Vector2Int(col, row); // x = col, y = row
            return _model != null && _model.IsInBounds(row, col);
        }

        // ── Tile access ────────────────────────────────────────────────────
        public TileView GetTileView(int row, int col) => _tiles?[row, col];

        // ── Animated operations (called by BoardController) ────────────────

        // Swap two tile views in the _tiles array and animate both to their new positions.
        public Sequence AnimateSwap(Vector2Int a, Vector2Int b)
        {
            TileView tileA = _tiles[a.y, a.x];
            TileView tileB = _tiles[b.y, b.x];
            _tiles[a.y, a.x] = tileB;
            _tiles[b.y, b.x] = tileA;

            var seq = DOTween.Sequence();
            if (tileA != null) seq.Join(tileA.AnimateSwap(GridToWorld(b.y, b.x)));
            if (tileB != null) seq.Join(tileB.AnimateSwap(GridToWorld(a.y, a.x)));
            return seq;
        }

        // Animate both tiles toward each other and back, then invoke onComplete.
        // Does NOT modify the _tiles array.
        // Uses a counter instead of nested Sequences to avoid DOTween recycling conflicts.
        public void AnimateInvalidSwap(Vector2Int from, Vector2Int to, System.Action onComplete)
        {
            TileView tileFrom = _tiles[from.y, from.x];
            TileView tileTo   = _tiles[to.y,   to.x];

            int pending = 0;
            void OnOneDone() { if (--pending == 0) onComplete?.Invoke(); }

            if (tileFrom != null) { pending++; tileFrom.AnimateInvalid(GridToWorld(to.y,   to.x)).OnComplete(OnOneDone); }
            if (tileTo   != null) { pending++; tileTo  .AnimateInvalid(GridToWorld(from.y, from.x)).OnComplete(OnOneDone); }
            if (pending == 0) onComplete?.Invoke();
        }

        // Move a tile view in the _tiles array (for gravity falls).
        // Returns a Tween animating the tile to its new world position.
        public Tween MoveTileView(int fromRow, int fromCol, int toRow, int toCol)
        {
            TileView tile = _tiles[fromRow, fromCol];
            _tiles[toRow, toCol] = tile;
            _tiles[fromRow, fromCol] = null;

            if (tile == null) return DOTween.Sequence();
            return tile.AnimateFall(GridToWorld(toRow, toCol));
        }

        // Create a new tile at fromWorldPos and animate it falling to its grid position.
        public Tween SpawnTileAt(int row, int col, Cell cell, Vector3 fromWorldPos)
        {
            TileView tile = SpawnTileView(fromWorldPos);
            tile.UpdateVisual(cell);
            _tiles[row, col] = tile;
            return tile.AnimateFall(GridToWorld(row, col));
        }

        // ── Direct cell management ─────────────────────────────────────────
        // Scales the tile out (0.15 s) then releases it to TilePool.
        public void ClearCell(int row, int col)
        {
            if (_tiles[row, col] == null) return;
            TileView tile = _tiles[row, col];
            _tiles[row, col] = null;

            tile.KillAllTweens();
            tile.transform.DOScale(0f, 0.15f).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (ServiceLocator.TryGet(out TilePool pool))
                    pool.Release(tile);
                else
                    Destroy(tile.gameObject);
            });
        }

        // ── Auto-fit ───────────────────────────────────────────────────────
        void AutoFitToCamera(GridModel model)
        {
            var cam = Camera.main;
            if (cam == null) return;

            float camH = cam.orthographicSize * 2f;
            float camW = camH * cam.aspect;

            // Pick the cell size that fits the tighter dimension, with 2% padding
            float cellByH = camH / model.Rows;
            float cellByW = camW / model.Columns;
            _cellSize = Mathf.Min(cellByH, cellByW) * 0.98f;

            // Center the board on screen then apply optional vertical offset
            float boardW = model.Columns * _cellSize;
            float boardH = model.Rows    * _cellSize;
            _gridOrigin = new Vector3(
                -boardW * 0.5f + _cellSize * 0.5f,
                -boardH * 0.5f + _cellSize * 0.5f + _verticalOffset,
                0f);

            // Size the grid background using Sliced draw mode so corners are preserved
            // and only the edges/centre stretch to fill the board area.
            // Requires grid_background.png to have 9-slice borders set in the Sprite Editor.
            if (_gridBackground != null && _gridBackground.sprite != null)
            {
                float targetW = boardW + _gridBackgroundPadding * 2f;
                float targetH = boardH + _gridBackgroundPadding * 2f;

                _gridBackground.transform.position   = new Vector3(0f, _verticalOffset, 0f);
                _gridBackground.transform.localScale  = Vector3.one;
                _gridBackground.drawMode              = SpriteDrawMode.Sliced;
                _gridBackground.size                  = new Vector2(targetW, targetH);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────
        void SpawnTileInternal(int row, int col, Cell cell)
        {
            TileView tile = SpawnTileView(GridToWorld(row, col));
            tile.UpdateVisual(cell);
            _tiles[row, col] = tile;
        }

        // Immediate release — used during level build/rebuild, no animation.
        void ClearCellInternal(int row, int col)
        {
            if (_tiles[row, col] == null) return;
            TileView tile = _tiles[row, col];
            _tiles[row, col] = null;

            if (ServiceLocator.TryGet(out TilePool pool))
                pool.Release(tile);
            else
            {
                tile.KillAllTweens();
                Destroy(tile.gameObject);
            }
        }

        // Shared factory: prefer TilePool, fall back to Instantiate if pool not yet ready.
        TileView SpawnTileView(Vector3 worldPos)
        {
            if (ServiceLocator.TryGet(out TilePool pool))
                return pool.Get(worldPos);

            TileView tile = Instantiate(_tilePrefab, worldPos, Quaternion.identity, transform);
            return tile;
        }
    }
}
