using UnityEngine;
using World.Sim;
using World.Sim.Encounters.Council;

namespace World.Game
{
    /// <summary>
    /// HUB-COUNCIL (WORLD_UI_PLAN 2.1): a council steward who is always at the foot of the lookout tower. When the council can
    /// be held (eligible, offered, or in progress with the player helping) tapping him opens it through the existing accept path
    /// (<see cref="CouncilDirector.StewardOpen"/>); otherwise he says so honestly. When the election is over the office holder
    /// sits at the tower in his place. Never writes state itself. Owner: WU-1.
    /// </summary>
    public sealed class CouncilSteward : MonoBehaviour
    {
        public const string NotYet = "No dispute is called yet.";
        public static CouncilSteward Instance { get; private set; }
        public Npc Steward { get; private set; }
        public bool OfficeHolderSits { get; private set; }
        public string LastLine { get; private set; }
        public Vector3 Spot { get; private set; }

        private WorldBootstrap _boot;
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public static CouncilSteward Build(Transform parent, WorldBootstrap boot)
        {
            var dir = CouncilDirector.Instance;
            if (dir == null) return null;
            var go = new GameObject("Council steward");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CouncilSteward>();
            Instance = c;
            c._boot = boot;
            var site = dir.Site;
            c.Spot = dir.StewardSpot;
            c.Steward = Npc.Spawn("council_steward", "Council steward", "Keeps the council's tower and calls its disputes.", "Disputes are called at the tower.",
                new Color(0.30f, 0.42f, 0.68f), c.Spot, site.Yaw + 180f);
            c.Steward.DialogueBuilder = c.Build;
            return c;
        }

        private void Build(Npc n)
        {
            n.Lines.Clear(); n.Options.Clear();
            var dir = CouncilDirector.Instance;
            var s = _boot.Sim.State;
            if (OfficeHolderSits)
            {
                var h = CouncilApi.OfficeHolderOf(s, dir != null ? dir.HomeSettlement : 0);
                n.Lines.Add("I hold the office of " + (h != null ? h.Office : "the council") + ". The village is at peace.");
                n.Options.Add(DialogueOption.Leave("Good"));
                return;
            }
            if (dir != null && CanHold(s, dir.HomeSettlement))
            {
                n.Lines.Add("A dispute waits at the tower. Will you hear it?");
                n.Options.Add(DialogueOption.Do("Hear the dispute", () =>
                {
                    string r = dir.StewardOpen(n);
                    if (r != null) { LastLine = r; SpeechBubbles.Say(n, r, 3.5f); }
                }));
                n.Options.Add(DialogueOption.Leave("Not now"));
                return;
            }
            var sc = dir != null ? CouncilApi.Get(s, dir.HomeSettlement) : null;
            LastLine = sc != null && sc.Status == CouncilStatus.InProgress ? "The others are settling it among themselves. Come back when it is decided." : NotYet;
            n.Lines.Add(LastLine);
            n.Options.Add(DialogueOption.Leave("I will wait"));
        }

        /// <summary>True when tapping the steward would open the council (eligible, offered, or in progress with the player helping).</summary>
        public static bool CanHold(WorldState s, int settlement)
        {
            var sc = CouncilApi.Get(s, settlement);
            if (sc == null) return CouncilApi.CanOffer(s, settlement);
            return sc.Status == CouncilStatus.Offered || (sc.Status == CouncilStatus.InProgress && sc.PlayerHelps);
        }

        private void Update()
        {
            if (Steward == null || Time.time < _next) return;
            _next = Time.time + 0.5f;
            var dir = CouncilDirector.Instance;
            if (dir == null) return;
            var s = _boot.Sim.State;
            var sc = CouncilApi.Get(s, dir.HomeSettlement);
            bool over = sc != null && sc.Status == CouncilStatus.Decided;
            if (over && !OfficeHolderSits)
            {
                var h = CouncilApi.OfficeHolderOf(s, dir.HomeSettlement);
                if (h == null) return;
                OfficeHolderSits = true;
                Steward.DisplayName = h.Name; Steward.Role = h.Office + " (" + h.Occupation + ")";
                Steward.Greeting = "The disputes are settled.";
                if (Steward.Actor is PlaceholderActor pa) pa.Seated = true;
            }
        }
    }
}
