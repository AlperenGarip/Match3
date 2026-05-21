using DG.Tweening;
using UnityEngine;

namespace Match3.VFX
{
    // Plays a single expanding shockwave ring at a TNT detonation position.
    // Scales out from a small starting size while fading alpha to 0.
    // JuiceController pools instances and calls Play / Hide.
    public class TNTRingVFX : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _ring;

        [Header("Animation")]
        [SerializeField] float _duration   = 0.4f;
        [SerializeField] float _startScale = 0.2f;
        [SerializeField] float _endScale   = 3.0f;

        public Sequence Play(Vector3 worldPos)
        {
            if (_ring == null)
            {
                Debug.LogWarning("[TNTRingVFX] Missing SpriteRenderer reference — skipping ring animation.");
                return null;
            }

            gameObject.SetActive(true);
            transform.position    = worldPos;
            transform.localScale  = Vector3.one * _startScale;

            Color c = _ring.color;
            _ring.color = new Color(c.r, c.g, c.b, 1f);

            var seq = DOTween.Sequence();
            seq.Join(transform.DOScale(_endScale, _duration).SetEase(Ease.OutCubic));
            seq.Join(_ring.DOFade(0f, _duration).SetEase(Ease.InQuad));
            return seq;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
