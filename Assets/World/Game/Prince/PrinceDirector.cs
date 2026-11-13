using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The prince, the sawyer and the merchant, the pitch loop and the first scene (REFACTOR_SPEC 2.C.5, 8.5,
    /// appendix B). Owner: package N. F-0 stub: nothing creates it yet; no villagers, step 0.
    /// </summary>
    public sealed class PrinceDirector : MonoBehaviour
    {
        public static PrinceDirector Instance { get; private set; }

        public Npc Prince { get; private set; }
        public Npc Sawyer { get; private set; }
        public Npc Merchant { get; private set; }

        /// <summary>Step of the first scene (saved in the extras key <c>scene.first</c>).</summary>
        public int FirstSceneStep { get; private set; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Called by the save system (F) after a load.</summary>
        public void RestoreFirstScene(int step) { FirstSceneStep = step; }
    }
}
