using DG.Tweening;
using UnityEngine;

namespace Match3.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        void Awake()
        {
            // recycleAllByDefault must stay false — recycling sequences causes
            // "inactive/killed Sequence" errors when DOTween hands a dead sequence
            // back from its pool before it is fully reset.
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true);

            // Clear stale state from any previous scene
            ServiceLocator.Clear();
            EventBus.Clear();

            // Services are registered by the components that own them (GridView, GoalTracker, etc.)
            // via their own Awake() after this runs. GameBootstrapper just ensures DOTween and
            // the registries are clean before anyone else initialises.
        }
    }
}
