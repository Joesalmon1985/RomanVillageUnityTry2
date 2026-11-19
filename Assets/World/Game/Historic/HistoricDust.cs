using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>Pooled dust puffs for the Historic age (temples falling, castle stages raised): grey blobs that swell, rise and shrink away.</summary>
    public sealed class HistoricDust
    {
        public const int MaxPuffs = 64;
        private sealed class Puff { public Transform T; public Vector3 Start, Vel; public float Age, Life, Size; }
        private readonly List<Puff> _live = new List<Puff>();
        private readonly Stack<Puff> _free = new Stack<Puff>();
        private readonly Transform _parent;
        private static Mesh s_mesh;
        private static Material s_mat;

        public int Active => _live.Count;

        public HistoricDust(Transform parent) { _parent = parent; }

        private static void Ensure()
        {
            if (s_mesh == null) { var mb = new MeshBuilder(); mb.Cylinder(Vector3.zero, 1f, 0.7f, 1.2f, 7, Color.white); s_mesh = mb.ToMesh("HistoricDust"); }
            if (s_mat == null) s_mat = MaterialFactory.CreateUnlit(new Color(0.66f, 0.62f, 0.55f), "HistoricDust");
        }

        /// <summary>A ring of puffs around <paramref name="at"/> at the given radius, living for <paramref name="life"/> seconds.</summary>
        public void Burst(Vector3 at, int count, float radius, float life, float size = 1.6f)
        {
            Ensure();
            for (int i = 0; i < count && _live.Count < MaxPuffs; i++)
            {
                var p = _free.Count > 0 ? _free.Pop() : null;
                if (p == null)
                {
                    var go = new GameObject("Dust");
                    go.transform.SetParent(_parent, false);
                    go.layer = 2;
                    go.AddComponent<MeshFilter>().sharedMesh = s_mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = s_mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    p = new Puff { T = go.transform };
                }
                float a = i * 360f / count + (i * 53 % 17);
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
                p.Start = at + d * radius * (0.5f + 0.5f * ((i * 7 % 5) / 4f));
                p.Vel = d * 0.8f + Vector3.up * (1.6f + (i % 3) * 0.5f);
                p.Age = 0f; p.Life = life * (0.8f + 0.1f * (i % 4)); p.Size = size * (0.7f + 0.15f * (i % 4));
                p.T.gameObject.SetActive(true);
                p.T.position = p.Start;
                _live.Add(p);
            }
        }

        public void Update(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life) { p.T.gameObject.SetActive(false); _free.Push(p); _live.RemoveAt(i); continue; }
                float u = p.Age / p.Life, s = Mathf.Sin(u * Mathf.PI);
                p.T.position = p.Start + p.Vel * p.Age;
                p.T.localScale = Vector3.one * p.Size * (0.4f + 1.6f * s) * (1f - 0.5f * u);
            }
        }
    }
}
