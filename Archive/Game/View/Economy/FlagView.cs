using UnityEngine;
using World.Sim;

namespace World.Game
{
    /// <summary>
    /// A flag on the carrier network (a building or site flag, or a road end): a pole with a pennant and, beside it,
    /// a stack of ware crates (one column per ware, coloured by EconomyApi.WareColour) showing what the building on
    /// this flag holds. Crates are rebuilt only when the counts change.
    /// </summary>
    public sealed class FlagView : MonoBehaviour
    {
        public const int MaxPerColumn = 6, MaxColumns = 5;
        public int Vertex { get; private set; }
        /// <summary>Crates shown now (tests).</summary>
        public int CrateCount { get; private set; }
        private Transform _cloth, _stack;
        private readonly int[] _shown = new int[Wares.Count];
        private Vector3 _stackDir = Vector3.right, _stackSide = Vector3.forward;

        public static FlagView Create(Transform parent, int vertex)
        {
            var go = new GameObject("Flag " + vertex);
            go.transform.SetParent(parent, false);
            go.transform.position = BoardSpace.Vertex(vertex);
            var f = go.AddComponent<FlagView>();
            f.Vertex = vertex;
            var corner = BoardSpace.Topo.IsCorner(vertex);
            // On a corner the stone disc and post of the terrain already stand at the flag: offset the pole a little.
            var off = corner ? new Vector3(-0.7f, 0, -0.7f) : Vector3.zero;
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Base", off + new Vector3(0, 0.06f, 0), new Vector3(1.4f, 0.06f, 1.4f), MaterialFactory.Shared(new Color(0.55f, 0.50f, 0.42f)));
            Signs.Prim(PrimitiveType.Cylinder, go.transform, "Pole", off + new Vector3(0, 1.4f, 0), new Vector3(0.09f, 1.4f, 0.09f), MaterialFactory.Shared(new Color(0.88f, 0.86f, 0.80f)));
            var cloth = new GameObject("Cloth").transform;
            cloth.SetParent(go.transform, false);
            cloth.localPosition = off + new Vector3(0, 2.5f, 0);
            Signs.Prim(PrimitiveType.Cube, cloth, "Pennant", new Vector3(0.4f, 0, 0), new Vector3(0.8f, 0.48f, 0.03f), MaterialFactory.Shared(new Color(0.98f, 0.80f, 0.20f)));
            f._cloth = cloth;
            f._stack = new GameObject("Wares").transform;
            f._stack.SetParent(go.transform, false);
            return f;
        }

        /// <summary>Sets the direction the crate stack sits in (from the flag, into the building's side, along the road).</summary>
        public void SetStackDirection(Vector3 into)
        {
            into.y = 0;
            if (into.sqrMagnitude < 1e-4f) return;
            _stackDir = into.normalized;
            _stackSide = Vector3.Cross(Vector3.up, _stackDir).normalized;
        }

        /// <summary>Shows the counts (index = (int)Ware), capped per column; null or all zero clears the stack.</summary>
        public void SetStack(int[] counts)
        {
            bool same = true;
            for (int w = 0; w < Wares.Count; w++)
            {
                int n = counts == null ? 0 : Mathf.Min(counts[w], MaxPerColumn);
                if (n != _shown[w]) same = false;
            }
            if (same) return;
            for (int i = _stack.childCount - 1; i >= 0; i--) Destroy(_stack.GetChild(i).gameObject);
            int col = 0; CrateCount = 0;
            for (int w = 0; w < Wares.Count; w++)
            {
                int n = counts == null ? 0 : Mathf.Min(counts[w], MaxPerColumn);
                _shown[w] = n;
                if (n == 0 || col >= MaxColumns) continue;
                var basePos = _stackDir * 1.5f + _stackSide * (1.2f + col * 0.75f);
                var mat = MaterialFactory.Shared(EconomyLook.WareColor((Ware)w));
                for (int k = 0; k < n; k++)
                {
                    Signs.Prim(PrimitiveType.Cube, _stack, "Crate " + (Ware)w, basePos + Vector3.up * (0.17f + k * 0.33f),
                        new Vector3(0.55f, 0.3f, 0.55f), mat, new Vector3(0, (k * 23) % 40 - 20, 0));
                    CrateCount++;
                }
                col++;
            }
        }

        private void Update()
        {
            if (_cloth != null) _cloth.localRotation = Quaternion.Euler(0, 20f + Mathf.Sin(Time.time * 2.3f + Vertex) * 18f, 0);
        }
    }
}
