using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Makes a part (a worker, a sign) inspect as another inspectable. EconomyViews swaps a selected
    /// forwarder for its target, so the inspect panel and the action buttons always see the building.
    /// </summary>
    public sealed class InspectForward : MonoBehaviour, IInspectable
    {
        public IInspectable Target;
        public string Title => Target != null ? Target.Title : name;
        public IEnumerable<string> Lines => Target != null ? Target.Lines : new string[0];
    }
}
