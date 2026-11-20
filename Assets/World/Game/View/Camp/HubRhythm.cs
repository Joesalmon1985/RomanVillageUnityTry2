using System.Collections.Generic;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// HUB-RHYTHM (WORLD_UI_PLAN 2.1): before the first spire rises, tapping a drum or the fire ring at the home circle starts the
    /// dance (<c>rhythm_party</c>) exactly as Sol's "Dance at the fire" does. The circle is switched off when it retires (D-6), so
    /// this never offers a home dance after the first spire. Owner: WU-1.
    /// </summary>
    public sealed class DanceStarter : MonoBehaviour, IInteractable, IInspectable
    {
        public string Prompt => "Dance at the fire";
        public string Title => "The dance circle";
        public IEnumerable<string> Lines => new[] { "Four drums round a fire. Touch one to start the dance with Sol." };
        public void Interact()
        {
            var sol = Npc.Find(NpcIds.Host);
            if (sol == null) return;
            if (SolGuide.CircleIsCold) { sol.Interact(); return; }
            EncounterDirectory.TryStart(EncounterDirectory.RhythmParty, sol);
        }
    }

    /// <summary>
    /// After the first spire (D-6) the home circle is cold: Sol stays by it and offers to lead the player to the spires. He walks
    /// to the first spire's stage and the player follows (the walker's own follow); there he says so and walks back.
    /// </summary>
    public sealed class SolGuide : MonoBehaviour
    {
        public static SolGuide Instance { get; private set; }
        public Npc Sol { get; private set; }
        public bool Leading { get; private set; }
        public int Led { get; private set; }
        public string LastSaid { get; private set; }
        private Vector3 _goal, _home; private float _homeYaw, _giveUp;
        private bool _arrived; private float _backAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>True once a spire has risen and the home circle is out of use.</summary>
        public static bool CircleIsCold => SpireDirector.Instance != null && SpireDirector.Instance.Count > 0;

        public static SolGuide Attach(Npc sol, Vector3 home, float homeYaw)
        {
            var g = sol.gameObject.AddComponent<SolGuide>();
            g.Sol = sol; g._home = home; g._homeYaw = homeYaw;
            Instance = g;
            sol.DialogueBuilder = g.BuildDialogue;
            return g;
        }

        private void BuildDialogue(Npc n)
        {
            n.Lines.Clear(); n.Options.Clear();
            if (!CircleIsCold)
            {
                n.Lines.Add("Four drums round the fire. Embers fly from the fire to the drum you strike: D F J K, left to right.");
                n.Lines.Add("Hit as the ember lands. Keep the beat and the bass joins in; at the end, leap the fire or call the spirits.");
                n.Options.Add(DialogueOption.Encounter("Dance at the fire", EncounterDirectory.RhythmParty));
                n.Options.Add(DialogueOption.Leave("Not now"));
                return;
            }
            n.Lines.Add("This fire went cold when the first spire rose. The drums stand at the spires now.");
            n.Options.Add(DialogueOption.Do("Lead me to the spires", () => LeadToSpire()));
            n.Options.Add(DialogueOption.Leave("Not now"));
        }

        /// <summary>Sol walks to the first spire's stage; the player follows.</summary>
        public bool LeadToSpire()
        {
            var sd = SpireDirector.Instance;
            var w = PlayerWalker.Instance;
            if (sd == null || sd.Count == 0 || Sol == null || w == null || Leading) return false;
            _goal = sd.Spires[0].Layout.DancerSpot;
            Leading = true; _arrived = false; _giveUp = Time.time + 90f;
            Sol.Directed = true; Sol.WalkSpeed = 5f;
            Sol.WalkTo(_goal);
            w.Follow(Sol);
            LastSaid = "Follow me. It is a short walk.";
            SpeechBubbles.Say(Sol, LastSaid, 3f);
            return true;
        }

        private void Update()
        {
            if (Sol == null) return;
            if (Leading && !_arrived)
            {
                if (!Sol.IsWalking || Time.time > _giveUp)
                {
                    _arrived = true; Led++;
                    var w = PlayerWalker.Instance; if (w != null && w.Following != null) w.StopAuto();
                    Sol.StopWalking();
                    LastSaid = "Here is the first spire. Touch it, or any of its drums, to dance.";
                    SpeechBubbles.Say(Sol, LastSaid, 4f);
                    _backAt = Time.time + 6f;
                }
            }
            else if (Leading && Time.time >= _backAt)
            {
                Leading = false;
                Sol.WalkSpeed = Npc.StrollSpeed;
                Sol.Directed = false;
                Sol.WalkTo(_home);
            }
        }
    }
}
