using System.Collections.Generic;
using Bedsit;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A carrier: a placeholder figure walking the sim's carrier along its road segment (FromVertex -> ToVertex,
    /// (Progress + tick fraction) / Tuning.TicksPerEdge), with a crate in the load's ware colour carried on its head and,
    /// in God View, a disc in that colour under it. A carrier waiting at its last flag stands beside the flag; idle
    /// carriers at the storehouse stand in a block in front of it.
    /// </summary>
    public sealed class CarrierView : MonoBehaviour, IInspectable
    {
        /// <summary>Catch-up speed (m/s) when the view is far from the sim position (leaving the storehouse block).</summary>
        public const float CatchUpSpeed = 9f;
        /// <summary>Side offset (m) from the road centre line while walking (right-hand side).</summary>
        public const float Lane = 0.45f;

        public int CarrierId { get; private set; }
        public ICharacterActor Actor { get; private set; }
        public bool HasLoad { get; private set; }
        public Ware Load { get; private set; }
        public int ShownEdge { get; private set; } = -1;
        public bool IsIdleAtStore { get; private set; }
        public bool IsWaitingAtFlag { get; private set; }
        public Vector3 CratePosition => _crate != null ? _crate.transform.position : transform.position + Vector3.up * 2f;
        public bool CrateVisible => _crate != null && _crate.activeInHierarchy;

        private GameObject _crate, _disc;
        private Renderer _crateRenderer, _discRenderer;
        private bool _crateSet;
        private Ware _crateWare;
        private Vector3 _from, _to;
        private int _progress, _ticksPerEdge = 30;
        private bool _onEdge, _snap = true;
        private Vector3 _restPos, _restFace;
        private float _godScale = 1f;

        public string Title => WorldBootstrap.Instance == null ? "Carrier" : EconomyText.CarrierTitle(WorldBootstrap.Instance.Sim.State, CarrierId);
        public IEnumerable<string> Lines
        {
            get
            {
                var b = WorldBootstrap.Instance;
                return b == null ? new string[0] : EconomyText.CarrierLines(b.Sim.State, CarrierId);
            }
        }

        public static CarrierView Create(Transform parent, int id)
        {
            var actor = ActorFactory.Create("carrier_" + id, new Color(0.22f, 0.42f, 0.78f), parent);
            actor.Root.name = "Carrier " + (id + 1);
            var cc = actor.Root.GetComponent<CapsuleCollider>();
            if (cc != null) { cc.isTrigger = true; cc.height = 2.7f; cc.center = new Vector3(0, 1.35f, 0); cc.radius = 0.5f; }
            foreach (var r in actor.Root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var v = actor.Root.gameObject.AddComponent<CarrierView>();
            v.CarrierId = id;
            v.Actor = actor;
            v.BuildLoad();
            return v;
        }

        /// <summary>
        /// Where idle carriers wait: a block of 6 columns in front of the storehouse, beside (not around) Quartermaster
        /// Bram, who stands at the storehouse front on the other side.
        /// </summary>
        public static Vector3 IdleSpot(VillageLayout L, int index)
        {
            if (L == null) return Vector3.zero;
            var fwd = BoardSpace.DirOf(L.StorehouseYaw);
            var side = new Vector3(fwd.z, 0, -fwd.x);
            var bram = Vector3.Dot(L.QuartermasterSpot - L.Storehouse, side) >= 0 ? 1f : -1f;
            int col = index % 6, row = index / 6;
            return L.Storehouse + fwd * (4.5f + row * 1.2f) - side * bram * (3.2f + col * 1.1f);
        }

        /// <summary>Spot beside a flag where a carrier waits (spread by id so several do not overlap).</summary>
        public static Vector3 WaitSpot(int vertex, int id)
        {
            var p = BoardSpace.Vertex(vertex);
            float a = (id * 137.5f) % 360f;
            return p + BoardSpace.DirOf(a) * (1.4f + (id % 3) * 0.5f);
        }

        public void Sync(WorldState s, Carrier c, int idleIndex)
        {
            var e = s.Economy;
            _ticksPerEdge = Mathf.Max(1, e.Tuning.TicksPerEdge);
            HasLoad = c.HasLoad;
            Load = c.Load;
            IsIdleAtStore = false; IsWaitingAtFlag = false;
            if (c.CurrentEdge >= 0)
            {
                _onEdge = true;
                ShownEdge = c.CurrentEdge;
                _from = BoardSpace.Vertex(c.FromVertex);
                _to = BoardSpace.Vertex(c.ToVertex);
                _progress = c.Progress;
            }
            else
            {
                _onEdge = false;
                ShownEdge = -1;
                var L = WorldBootstrap.Instance != null ? WorldBootstrap.Instance.Layout : null;
                int store = e.Buildings[e.StorehouseId].Vertex;
                if (c.AtVertex == store && c.JobId < 0 && !c.HasLoad)
                {
                    IsIdleAtStore = true;
                    _restPos = IdleSpot(L, idleIndex);
                    _restFace = L != null ? L.SquareCentre : _restPos + Vector3.forward;
                }
                else
                {
                    IsWaitingAtFlag = c.State == CarrierState.Idle;
                    _restPos = c.State == CarrierState.Idle || c.HasLoad && c.JobId >= 0 && EconomyLook.Job(s, c.JobId)?.Reason != null
                        ? WaitSpot(c.AtVertex, c.Id) : BoardSpace.Vertex(c.AtVertex);
                    _restFace = BoardSpace.Vertex(c.AtVertex);
                }
            }
            SyncCrate();
        }

        /// <summary>The sim position now, interpolated with the fraction of the next tick that has elapsed.</summary>
        public Vector3 TargetPosition(float tickFraction)
        {
            if (!_onEdge) return _restPos;
            float u = Mathf.Clamp01((_progress + Mathf.Clamp01(tickFraction)) / _ticksPerEdge);
            var dir = _to - _from; dir.y = 0;
            var side = Vector3.Cross(Vector3.up, dir.normalized);
            // Lane by id (0.45, 0.8 or 1.1 m right of the centre line) so carriers walking together do not overlap.
            float lane = Lane + (CarrierId % 3) * 0.33f;
            return Vector3.Lerp(_from, _to, u) + side * lane * Mathf.Clamp01(Mathf.Min(u, 1f - u) * 8f);
        }

        /// <summary>Moves the figure toward the interpolated sim position (exact when close, so it stays on the road).</summary>
        public void Animate(float tickFraction, float dt)
        {
            var target = TargetPosition(tickFraction);
            var pos = transform.position;
            var d = target - pos; d.y = 0;
            if (_snap || d.magnitude < 1.5f) { transform.position = target; _snap = false; }
            else transform.position = Vector3.MoveTowards(pos, target, Mathf.Max(CatchUpSpeed, d.magnitude * 1.5f) * dt);
            if (_onEdge) Actor.FaceTowards(_to);
            else if (d.magnitude > 0.3f) Actor.FaceTowards(target);
            else Actor.FaceTowards(_restFace);
        }

        public void Snap() { _snap = true; }

        /// <summary>God View scale of the load crate and disc (1 in first person).</summary>
        public void SetGodScale(float s, bool god)
        {
            if (_disc != null) _disc.SetActive(god && (HasLoad || !IsIdleAtStore));
            if (Mathf.Approximately(s, _godScale)) return;
            _godScale = s;
            if (_crate != null)
            {
                _crate.transform.localScale = Vector3.one * s;
                _crate.transform.localPosition = new Vector3(0, 2.05f + 0.25f * (s - 1f), 0);
            }
            if (_disc != null) _disc.transform.localScale = new Vector3(1.6f * s, 0.02f, 1.6f * s);
        }

        private void BuildLoad()
        {
            _crate = new GameObject("Load");
            _crate.transform.SetParent(transform, false);
            _crate.transform.localPosition = new Vector3(0, 2.05f, 0);
            var box = Signs.Prim(PrimitiveType.Cube, _crate.transform, "Crate", Vector3.zero, new Vector3(0.62f, 0.46f, 0.62f), MaterialFactory.Shared(Color.white));
            _crateRenderer = box.GetComponent<Renderer>();
            var band = MaterialFactory.Shared(new Color(0.85f, 0.80f, 0.68f));
            Signs.Prim(PrimitiveType.Cube, _crate.transform, "Band", Vector3.zero, new Vector3(0.66f, 0.1f, 0.66f), band);
            // Arms up to hold it.
            var sleeve = MaterialFactory.Shared(new Color(0.22f, 0.42f, 0.78f));
            Signs.Prim(PrimitiveType.Cube, _crate.transform, "ArmL", new Vector3(-0.3f, -0.36f, 0), new Vector3(0.09f, 0.42f, 0.09f), sleeve);
            Signs.Prim(PrimitiveType.Cube, _crate.transform, "ArmR", new Vector3(0.3f, -0.36f, 0), new Vector3(0.09f, 0.42f, 0.09f), sleeve);
            foreach (var r in _crate.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _disc = Signs.Prim(PrimitiveType.Cylinder, transform, "GodDisc", new Vector3(0, 0.12f, 0), new Vector3(1.6f, 0.02f, 1.6f), MaterialFactory.SharedUnlit(new Color(0.30f, 0.45f, 0.75f)));
            _discRenderer = _disc.GetComponent<Renderer>();
            _discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _disc.SetActive(false);
            _crate.SetActive(false);
        }

        private void SyncCrate()
        {
            if (_crate.activeSelf != HasLoad) _crate.SetActive(HasLoad);
            if (!HasLoad)
            {
                if (_crateSet && _discRenderer != null) { _discRenderer.sharedMaterial = MaterialFactory.SharedUnlit(new Color(0.30f, 0.45f, 0.75f)); _crateSet = false; }
                return;
            }
            if (_crateSet && _crateWare == Load) return;
            _crateSet = true; _crateWare = Load;
            _crateRenderer.sharedMaterial = MaterialFactory.Shared(EconomyLook.WareColor(Load));
            _discRenderer.sharedMaterial = MaterialFactory.SharedUnlit(EconomyLook.WareColor(Load));
        }
    }
}
