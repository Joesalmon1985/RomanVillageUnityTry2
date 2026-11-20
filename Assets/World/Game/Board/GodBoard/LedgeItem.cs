using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// One modelled object on the 3D altar ledge (WU-2b): its id (the same ids as <see cref="GodBoardHud.LedgeRects"/>), the
    /// camera-facing hit box sized from the layout rect (never below <see cref="WorldHitTarget.MinSide"/>), and the model.
    /// </summary>
    public sealed class LedgeItem : MonoBehaviour
    {
        public string Id;
        public BoxCollider Hit;
        public Transform Model;
        /// <summary>Unscaled time the object came onto the ledge (envoys walk in).</summary>
        public float Born;
        /// <summary>A small signature of what the model shows; rebuilt when it changes.</summary>
        public string Sig = "";
        public Transform Dyn;
    }
}
