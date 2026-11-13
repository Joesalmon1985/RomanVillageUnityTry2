using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Fast travel (God View tool "Go here", hotkey L): click open ground on the island and the avatar is put there,
    /// back in first person. Refused (with a sentence) beyond the board, on top of buildings, rocks or peaks, or where
    /// something stands in the way. The island is about 950 m across; walking it takes over three minutes.
    /// </summary>
    public sealed class TravelTool : GodTool
    {
        public const string ToolId = "travel";
        /// <summary>Highest ground (m) the avatar may be dropped on (hex tiles are at 0; peaks and roofs are far higher).</summary>
        public const float MaxGroundHeight = 1.2f;

        public Vector3 HoverPoint { get; private set; }
        public bool HoverValid { get; private set; }
        public string HoverWhy { get; private set; }

        public static void Register() => GodToolbar.Register(new TravelTool());

        public TravelTool()
        {
            Id = ToolId; Label = "Go here"; Hotkey = KeyCode.L; Order = 85; CapturesClicks = true; PanelWidth = 0f;
            TipText = () => "Fast travel: click open ground to walk there at once (back in first person).";
        }

        public override bool HasPanel => false;

        public override void Update()
        {
            HoverValid = false; HoverWhy = null;
            if (GodToolbar.MouseOverUi() || !BoardPicker.GroundPointUnderMouse(out var p)) return;
            HoverValid = TryFindSpot(p, out var feet, out var why);
            HoverPoint = feet; HoverWhy = why;
        }

        public override void Overlay()
        {
            if (HoverWhy == null && !HoverValid) return;
            GodToolbar.Tooltip(HoverValid ? "<b>Go here</b>: click to stand here in first person." : "<color=#FF9A80>" + HoverWhy + "</color>");
        }

        public override void WorldClick()
        {
            if (!BoardPicker.GroundPointUnderMouse(out var p)) return;
            TravelTo(p);
        }

        /// <summary>Puts the avatar at the ground point (if allowed) and switches to first person. Returns false with a toast if refused.</summary>
        public static bool TravelTo(Vector3 groundPoint)
        {
            var b = WorldBootstrap.Instance;
            if (b == null) return false;
            if (EncounterHost.Instance != null && EncounterHost.Instance.IsRunning) { EconomyHud.Toast("You can't travel during the duel or the party.", true); return false; }
            if (!TryFindSpot(groundPoint, out var feet, out var why)) { EconomyHud.Toast(why, true); return false; }
            b.Avatar.Teleport(feet + Vector3.up * 0.05f, b.Avatar.Yaw, 0f);
            GodToolbar.Instance?.Activate(GodToolbar.SelectId);
            b.Views.SetView(ViewMode.FirstPerson);
            EconomyHud.Toast("You are here now. Tab goes back to God View.", false);
            return true;
        }

        /// <summary>Whether the avatar can stand at the ground point; feet = the surface point to stand on.</summary>
        public static bool TryFindSpot(Vector3 ground, out Vector3 feet, out string why)
        {
            feet = new Vector3(ground.x, BoardSpace.GroundY, ground.z);
            why = null;
            if (new Vector2(ground.x, ground.z).magnitude > BoardSpace.BoardRadius) { why = "That is off the island's roads. Pick a spot nearer the middle."; return false; }
            const int mask = ~(1 << 2); // everything but Ignore Raycast (the avatar)
            if (Physics.Raycast(new Vector3(ground.x, 200f, ground.z), Vector3.down, out var hit, 400f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y > MaxGroundHeight) { why = "Something stands there (a building, rock or peak). Pick open ground."; return false; }
                feet.y = Mathf.Max(hit.point.y, BoardSpace.GroundY);
            }
            float r = FirstPersonAvatar.Radius + 0.1f;
            var a = feet + Vector3.up * (r + 0.15f);
            var c = feet + Vector3.up * (FirstPersonAvatar.Height - r);
            // Solid things (barriers, signs, people, peaks) and the pick volumes of buildings and cities (most models are
            // walk-through, so their volumes stand in for walls).
            foreach (var col in Physics.OverlapCapsule(a, c, r, mask, QueryTriggerInteraction.Collide))
            {
                bool building = col.GetComponentInParent<BuildingInfo>() != null || col.GetComponentInParent<BuildingView>() != null || col.GetComponentInParent<CityView>() != null;
                if (!col.isTrigger || building) { why = building ? "That is inside a building. Pick open ground." : "Something is in the way there. Pick open ground."; return false; }
            }
            return true;
        }
    }
}
