using DG.Tweening;
using UnityEngine;

namespace Match3.Grid
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileView : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer { get; private set; }

        // Sprite references assigned by GridView or TilePool based on CellType
        [Header("Cube Sprites")]
        public Sprite SpriteRed;
        public Sprite SpriteGreen;
        public Sprite SpriteBlue;
        public Sprite SpriteYellow;

        [Header("Obstacle Sprites")]
        public Sprite SpriteBox;
        public Sprite SpriteStone;
        public Sprite SpriteVase1;
        public Sprite SpriteVase2;

        [Header("Power-Up Sprites")]
        public Sprite SpriteHRocket;
        public Sprite SpriteVRocket;
        public Sprite SpriteTNT;
        public Sprite SpriteLightBall;

        Sequence _activeSequence;
        Vector3  _naturalScale;

        void Awake()
        {
            SpriteRenderer = GetComponent<SpriteRenderer>();
            _naturalScale  = transform.localScale; // cache prefab scale (e.g. 0.6, 0.6, 0.6)
        }

        public void UpdateVisual(Cell cell)
        {
            SpriteRenderer.sprite = cell.Type switch
            {
                CellType.Red              => SpriteRed,
                CellType.Green            => SpriteGreen,
                CellType.Blue             => SpriteBlue,
                CellType.Yellow           => SpriteYellow,
                CellType.Box              => SpriteBox,
                CellType.Stone            => SpriteStone,
                CellType.Vase1            => SpriteVase1,
                CellType.Vase2            => SpriteVase2,
                CellType.HorizontalRocket => SpriteHRocket,
                CellType.VerticalRocket   => SpriteVRocket,
                CellType.TNT              => SpriteTNT,
                CellType.LightBall        => SpriteLightBall,
                _                         => null,
            };

            SpriteRenderer.enabled = cell.Type != CellType.Empty;
        }

        // Swap animation — returns the tween so BoardController can chain OnComplete
        public Tween AnimateSwap(Vector3 targetWorldPos)
        {
            KillAllTweens();
            return transform.DOMove(targetWorldPos, 0.15f).SetEase(Ease.OutSine);
        }

        // Invalid swap: bounce to target and back
        public Sequence AnimateInvalid(Vector3 targetWorldPos)
        {
            KillAllTweens();
            Vector3 origin = transform.position;
            _activeSequence = DOTween.Sequence()
                .Append(transform.DOMove(targetWorldPos, 0.12f).SetEase(Ease.OutSine))
                .Append(transform.DOMove(origin, 0.12f).SetEase(Ease.InSine));
            return _activeSequence;
        }

        // Fall animation — duration scales with distance, with a minimum so short falls
        // don't appear to teleport (especially single-row spawns from above the grid).
        public Tween AnimateFall(Vector3 targetWorldPos, float durationPerUnit = 0.08f,
                                 float minDuration = 0.18f)
        {
            KillAllTweens();
            float distance = Mathf.Abs(transform.position.y - targetWorldPos.y);
            float duration = Mathf.Max(minDuration, distance * durationPerUnit);
            return transform.DOMove(targetWorldPos, duration).SetEase(Ease.InSine);
        }

        // Land squish — called after fall completes
        public Sequence AnimateLand()
        {
            _activeSequence = DOTween.Sequence()
                .Append(transform.DOPunchScale(new Vector3(0.3f, -0.3f, 0f), 0.25f, 1, 0.5f));
            return _activeSequence;
        }

        // Kill all active tweens — MUST be called by TilePool.Release() before returning to pool.
        // Always resets scale so interrupted land/punch animations don't leave tiles squished.
        public void KillAllTweens()
        {
            transform.DOKill();
            SpriteRenderer.DOKill();
            _activeSequence?.Kill();
            _activeSequence = null;
            transform.localScale = _naturalScale;
        }

        public void ResetToDefaults()
        {
            transform.localScale = _naturalScale;
            var c = SpriteRenderer.color;
            SpriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
        }
    }
}
