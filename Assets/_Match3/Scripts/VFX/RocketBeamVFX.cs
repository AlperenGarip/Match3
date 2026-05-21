using DG.Tweening;
using UnityEngine;

namespace Match3.VFX
{
    // Animates one rocket beam (one row or one column).
    // Uses separate left/right sprite halves — no rotation flipping needed.
    // For vertical beams the horizontal sprites are rotated 90° / -90°.
    public class RocketBeamVFX : MonoBehaviour
    {
        [Header("Beam")]
        [SerializeField] SpriteRenderer _beamGlow;

        [Header("Projectile Renderers")]
        [SerializeField] SpriteRenderer _rendererA;   // travels toward endA
        [SerializeField] SpriteRenderer _rendererB;   // travels toward endB

        [Header("Projectile Sprites")]
        [SerializeField] Sprite _spriteLeft;    // rocket nose pointing left  (horizontal)
        [SerializeField] Sprite _spriteRight;   // rocket nose pointing right (horizontal)
        [SerializeField] Sprite _spriteUp;      // rocket nose pointing up    (vertical)
        [SerializeField] Sprite _spriteDown;    // rocket nose pointing down  (vertical)

        [Header("Trails (optional)")]
        [SerializeField] TrailRenderer _trailA;
        [SerializeField] TrailRenderer _trailB;

        const float LaunchDuration = 0.35f;

        public Sequence Play(Vector3 origin, Vector3 endA, Vector3 endB, bool isHorizontal,
                             float scale = 1f)
        {
            if (_beamGlow == null || _rendererA == null || _rendererB == null)
            {
                Debug.LogWarning("[RocketBeamVFX] Missing serialized fields — skipping beam animation.");
                return null;
            }

            gameObject.SetActive(true);

            // ── Beam glow ─────────────────────────────────────────────────────
            float spanLength  = Vector3.Distance(endA, endB);
            _beamGlow.transform.position = (endA + endB) * 0.5f;

            if (isHorizontal)
            {
                _beamGlow.transform.localScale    = new Vector3(spanLength, _beamGlow.transform.localScale.y * scale, 1f);
                _beamGlow.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _beamGlow.transform.localScale    = new Vector3(_beamGlow.transform.localScale.x * scale, spanLength, 1f);
                _beamGlow.transform.localRotation = Quaternion.identity;
            }

            var col = _beamGlow.color;
            _beamGlow.color = new Color(col.r, col.g, col.b, 0f);

            // ── Projectiles ───────────────────────────────────────────────────
            _rendererA.transform.position   = origin;
            _rendererA.transform.localScale = Vector3.one * scale;
            _rendererB.transform.position   = origin;
            _rendererB.transform.localScale = Vector3.one * scale;

            if (isHorizontal)
            {
                // endA is always the LEFT end, endB is always the RIGHT end
                // (JuiceController passes GridToWorld(row, 0) as endA)
                _rendererA.sprite             = _spriteLeft;
                _rendererA.transform.rotation = Quaternion.identity;

                _rendererB.sprite             = _spriteRight;
                _rendererB.transform.rotation = Quaternion.identity;
            }
            else
            {
                // Vertical: dedicated up/down sprites — endA is BOTTOM (row 0 = low Y), endB is TOP
                _rendererA.sprite             = _spriteDown;
                _rendererA.transform.rotation = Quaternion.identity;

                _rendererB.sprite             = _spriteUp;
                _rendererB.transform.rotation = Quaternion.identity;
            }

            _trailA?.Clear();
            _trailB?.Clear();

            // ── Animate ───────────────────────────────────────────────────────
            var seq = DOTween.Sequence();

            seq.Append(_beamGlow.DOFade(0.9f, 0.05f).SetEase(Ease.Linear));
            seq.Join(_rendererA.transform.DOMove(endA, LaunchDuration).SetEase(Ease.InCubic));
            seq.Join(_rendererB.transform.DOMove(endB, LaunchDuration).SetEase(Ease.InCubic));
            seq.Append(_beamGlow.DOFade(0f, 0.1f).SetEase(Ease.Linear));

            return seq;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
