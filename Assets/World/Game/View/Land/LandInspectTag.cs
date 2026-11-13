using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>Makes any land-view part inspectable with sentences supplied by its owner (warriors, gatherers, builders).</summary>
    public sealed class LandInspectTag : MonoBehaviour, IInspectable
    {
        public Func<string> TitleSource;
        public Func<IEnumerable<string>> LinesSource;

        public string Title => TitleSource != null ? TitleSource() : name;
        public IEnumerable<string> Lines => LinesSource != null ? LinesSource() : new string[0];

        public static LandInspectTag Add(GameObject go, Func<string> title, Func<IEnumerable<string>> lines, Vector3 boxCentre, Vector3 boxSize)
        {
            var t = go.AddComponent<LandInspectTag>();
            t.TitleSource = title;
            t.LinesSource = lines;
            LandLook.PickBox(go, boxCentre, boxSize);
            return t;
        }
    }
}
