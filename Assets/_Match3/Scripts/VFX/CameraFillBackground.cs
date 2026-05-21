using UnityEngine;

namespace Match3.VFX
{
    // Scales a SpriteRenderer to exactly fill the orthographic camera view.
    // Attach alongside a SpriteRenderer set to background.png, Order in Layer = -100.
    [RequireComponent(typeof(SpriteRenderer))]
    public class CameraFillBackground : MonoBehaviour
    {
        void Start()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var sr = GetComponent<SpriteRenderer>();
            if (sr.sprite == null) return;

            float camH = cam.orthographicSize * 2f;
            float camW = camH * cam.aspect;

            Vector2 spriteSize      = sr.sprite.bounds.size;
            transform.position      = new Vector3(0f, 0f, 10f); // behind everything
            transform.localScale    = new Vector3(camW / spriteSize.x, camH / spriteSize.y, 1f);
        }
    }
}
