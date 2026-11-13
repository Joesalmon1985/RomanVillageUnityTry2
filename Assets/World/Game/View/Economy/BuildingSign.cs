using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// A signpost with the building's name by its flag. In first person, E selects the building (the same inspect
    /// panel as God View) and opens a short dialogue with the same commands as the God View buttons: priority,
    /// switch on/off, or cancel a site. Commands go through <see cref="GodCommands"/>.
    /// </summary>
    public sealed class BuildingSign : MonoBehaviour, IInteractable
    {
        public BuildingView Building { get; private set; }
        public string Prompt => "Read the " + (Building != null ? Building.DisplayName.ToLowerInvariant() : "building") + " sign";

        public static BuildingSign Create(BuildingView b)
        {
            var into = b.Plot.Into;
            var side = Vector3.Cross(Vector3.up, into).normalized;
            var pos = b.FlagPosition + into * 2.6f - side * 2.2f;
            var go = new GameObject("Sign " + b.DisplayName);
            go.transform.SetParent(b.transform.parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, BoardSpace.YawOf(-into), 0));
            var s = go.AddComponent<BuildingSign>();
            s.Building = b;
            Signs.Box(go.transform, "Post", new Vector3(0, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.12f), BuildingModels.DarkWood, false);
            float w = Mathf.Max(1.4f, 0.24f * b.DisplayName.Length + 0.3f);
            Signs.Board(go.transform, b.DisplayName.ToUpperInvariant(), new Vector3(0, 1.65f, 0.08f), new Vector2(w, 0.46f), BuildingModels.Wood, new Color(1f, 0.95f, 0.8f));
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 1.2f, 0);
            col.size = new Vector3(w + 0.2f, 2.4f, 0.6f);
            go.AddComponent<InspectForward>().Target = b;
            return s;
        }

        public void Interact()
        {
            if (Building == null) return;
            if (Picker.Instance != null) Picker.Instance.Select(Building);
            OpenDialogue(Building);
        }

        /// <summary>The sign dialogue for a building (also used by tests).</summary>
        public static void OpenDialogue(BuildingView view)
        {
            var boot = WorldBootstrap.Instance;
            if (boot == null || view == null) return;
            var s = boot.Sim.State;
            var b = EconomyApi.GetBuilding(s, view.BuildingId);
            if (b == null) return;
            var info = EconomyApi.Describe(s, b.Id);
            var type = EconomyApi.DescribeType(s, b.Type);
            var lines = new List<string> { EconomyApi.WaitingFor(s, b.Id).Sentence };
            if (!string.IsNullOrEmpty(type.RecipeText)) lines.Add("Recipe: " + type.RecipeText);
            if (info.StockLines.Length > 0) lines.Add(string.Join(",  ", info.StockLines));
            var options = new List<DialogueOption>();
            int id = b.Id;
            if (EconomyLook.IsSite(b))
            {
                lines.Add("A construction site. Carriers bring its materials one load at a time.");
                options.Add(DialogueOption.Do("Cancel this site (materials go back to the storehouse)", () => GodCommands.CancelSite(id)));
            }
            else if (b.State == BuildingState.Active)
            {
                var next = b.Priority == Priority.High ? Priority.Low : b.Priority + 1;
                lines.Add("Priority " + b.Priority.ToString().ToLowerInvariant() + ", " + (b.Enabled ? "switched on." : "switched off."));
                options.Add(DialogueOption.Do("Set priority to " + next.ToString().ToLowerInvariant(), () => GodCommands.SetPriority(id, next)));
                options.Add(DialogueOption.Do(b.Enabled ? "Switch it off" : "Switch it on", () => GodCommands.Toggle(id, !b.Enabled)));
            }
            options.Add(DialogueOption.Leave());
            DialoguePanel.Open(info.Title + " sign", lines, options);
        }
    }
}
