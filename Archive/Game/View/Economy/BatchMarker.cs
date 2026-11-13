using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Marker on a hex with an authorised batch (any of the five resource terrains): a banner pole in the raw ware's
    /// colour and one block per unit left (up to the carry-over cap), placed near the hex centre clear of mountain
    /// peaks. Inspectable with the hex's batch lines.
    /// </summary>
    public sealed class BatchMarker : MonoBehaviour, IInspectable
    {
        public int Hex { get; private set; }
        public int Remaining { get; private set; } = -1;
        public Ware Ware { get; private set; }
        public Vector3 LabelAnchor => transform.position + Vector3.up * 5.4f;

        private Transform _blocks;

        public string Title => EconomyApi.WareTitle(Ware) + " batch";
        public IEnumerable<string> Lines
        {
            get
            {
                var b = WorldBootstrap.Instance;
                if (b == null) yield break;
                foreach (var l in EconomyText.HexLines(b.Sim.State, Hex)) yield return l;
            }
        }

        /// <summary>Ground spot for a hex's marker: the centre, or the first point on rings around it clear of peaks.</summary>
        public static Vector3 SpotFor(int hex)
        {
            var c = BoardSpace.Hex(hex);
            // 20 m south of the centre, so its God View tag reads below the hex's own label.
            var south = c + new Vector3(0, 0, -20f);
            if (!TerrainBuilder.InPeak(south, 3f)) return south;
            if (!TerrainBuilder.InPeak(c, 3f)) return c;
            for (int ring = 1; ring <= 3; ring++)
                for (int k = 0; k < 12; k++)
                {
                    var p = c + BoardSpace.DirOf(k * 30f + ring * 15f) * (10f + ring * 8f);
                    if (!TerrainBuilder.InPeak(p, 3f)) return p;
                }
            return c;
        }

        public static BatchMarker Create(Transform parent, int hex, Ware ware)
        {
            var go = new GameObject("Batch " + hex);
            go.transform.SetParent(parent, false);
            go.transform.position = SpotFor(hex);
            var m = go.AddComponent<BatchMarker>();
            m.Hex = hex; m.Ware = ware;
            var col = EconomyLook.WareColor(ware);
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Base", new Vector3(0, 0.05f, 0), new Vector3(3.6f, 0.05f, 3.6f), MaterialFactory.Shared(new Color(0.55f, 0.50f, 0.42f)));
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Pole", new Vector3(0, 2.3f, 0), new Vector3(0.14f, 2.3f, 0.14f), MaterialFactory.Shared(new Color(0.3f, 0.22f, 0.14f)));
            Signs.Prim(PrimitiveType.Cube, go.transform, "Banner", new Vector3(0.6f, 3.9f, 0), new Vector3(1.1f, 1.0f, 0.05f), MaterialFactory.SharedUnlit(col));
            m._blocks = new GameObject("Units").transform;
            m._blocks.SetParent(go.transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.center = new Vector3(0, 2.3f, 0); box.size = new Vector3(3.6f, 4.6f, 3.6f);
            return m;
        }

        public void SetRemaining(int n)
        {
            if (n == Remaining) return;
            Remaining = n;
            for (int i = _blocks.childCount - 1; i >= 0; i--) Destroy(_blocks.GetChild(i).gameObject);
            var mat = MaterialFactory.Shared(EconomyLook.WareColor(Ware));
            int shown = Mathf.Min(n, 24);
            for (int i = 0; i < shown; i++)
            {
                int ring = i / 12;
                float a = (i % 12) * Mathf.PI * 2f / 12f + ring * 0.26f;
                float r = 1.0f + ring * 0.6f;
                Signs.Prim(PrimitiveType.Cube, _blocks, "Unit", new Vector3(Mathf.Cos(a) * r, 0.25f + ring * 0.1f, Mathf.Sin(a) * r), new Vector3(0.36f, 0.36f, 0.36f), mat, new Vector3(0, -a * Mathf.Rad2Deg, 0));
            }
        }
    }
}
