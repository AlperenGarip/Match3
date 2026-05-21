using Match3.Core;
using Match3.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Match3.Input
{
    public struct SwipedEvent
    {
        public Vector2Int From; // x=col, y=row
        public Vector2Int To;   // x=col, y=row
    }

    // Fired when the player taps (presses and releases without moving past the swipe threshold).
    public struct TappedEvent
    {
        public Vector2Int Cell; // x=col, y=row
    }

    public class SwipeHandler : MonoBehaviour
    {
        [SerializeField] GridView _gridView;

        // Minimum world-space distance the finger/mouse must travel to register a swipe
        [SerializeField] float _minSwipeWorldDistance = 0.25f;

        Camera _camera;
        bool   _isDragging;
        Vector2 _startScreenPos;

        void Awake()
        {
            _camera = Camera.main;
        }

        void Update()
        {
            if (Pointer.current == null) return;

            bool pressed      = Pointer.current.press.isPressed;
            Vector2 screenPos = Pointer.current.position.ReadValue();

            if (pressed && !_isDragging)
            {
                _isDragging     = true;
                _startScreenPos = screenPos;
            }
            else if (!pressed && _isDragging)
            {
                _isDragging = false;
                TryFireSwipe(_startScreenPos, screenPos);
            }
        }

        void TryFireSwipe(Vector2 startScreen, Vector2 endScreen)
        {
            Vector3 startWorld = ScreenToWorld(startScreen);
            Vector3 endWorld   = ScreenToWorld(endScreen);

            Vector2 delta = (Vector2)(endWorld - startWorld);

            if (delta.magnitude < _minSwipeWorldDistance)
            {
                // Tap: press-and-release without meaningful movement
                if (_gridView.TryWorldToGrid(startWorld, out Vector2Int tapCell))
                    EventBus.Publish(new TappedEvent { Cell = tapCell });
                return;
            }

            if (!_gridView.TryWorldToGrid(startWorld, out Vector2Int from)) return;

            Vector2Int dir = CardinalDirection(delta.normalized);
            Vector2Int to  = from + dir;

            EventBus.Publish(new SwipedEvent { From = from, To = to });
        }

        Vector3 ScreenToWorld(Vector2 screenPos)
        {
            float depth = Mathf.Abs(_camera.transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = 0f;
            return world;
        }

        // Returns the dominant cardinal direction of a normalised 2D vector.
        // x=col (+right/-left), y=row (+up/-down) matches our grid convention.
        static Vector2Int CardinalDirection(Vector2 normalised)
        {
            if (Mathf.Abs(normalised.x) >= Mathf.Abs(normalised.y))
                return normalised.x > 0 ? Vector2Int.right : Vector2Int.left;
            return normalised.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
