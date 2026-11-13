using System;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// One God View tool, tab or action on the bottom toolbar. Subclass it, or fill the delegate fields:
    /// <code>GodToolbar.Register(new GodTool { Id = "market", Label = "Market", Order = 60, Hotkey = KeyCode.M,
    ///     DrawPanel = (tool, area) => { ... IMGUI inside area ... } });</code>
    /// Only one tool is active at a time; an action tool (<see cref="IsAction"/>) runs and stays inactive.
    /// </summary>
    public class GodTool
    {
        /// <summary>Unique id ("build", "road", "remove_road", "select", "problems", "end_round", ...).</summary>
        public string Id;
        public string Label;
        public KeyCode Hotkey = KeyCode.None;
        /// <summary>Left-to-right position on the toolbar (built-ins use 10..90; leave gaps).</summary>
        public int Order = 100;
        /// <summary>Runs <see cref="Run"/> on click instead of becoming the active tool (e.g. End Round).</summary>
        public bool IsAction;
        /// <summary>While active, left clicks on the world go to the tool, not to selection.</summary>
        public bool CapturesClicks;
        /// <summary>Width of the left panel (1080p px) when the tool draws one; 0 = no panel.</summary>
        public float PanelWidth = 470f;

        // Delegate hooks (optional; the virtual methods call them).
        public Action<GodTool> OnActivate, OnDeactivate, OnUpdate, OnRun;
        /// <summary>Draw the tool's panel inside the given screen rect (GUI coordinates).</summary>
        public Action<GodTool, Rect> DrawPanel;
        /// <summary>Extra IMGUI drawn over the world while active (tooltips, hover text).</summary>
        public Action<GodTool> DrawOverlay;
        /// <summary>Left click on the world while active (only when CapturesClicks).</summary>
        public Action<GodTool> OnWorldClick;
        /// <summary>Text after the label, e.g. a count ("Problems 3").</summary>
        public Func<string> BadgeText;
        /// <summary>When it returns false the button is greyed out.</summary>
        public Func<bool> IsAvailable;
        /// <summary>Tooltip shown while the mouse is over the button (e.g. why it is greyed out).</summary>
        public Func<string> TipText;

        public bool IsActive => GodToolbar.Instance != null && GodToolbar.Instance.Active == this;

        public virtual void Activate() => OnActivate?.Invoke(this);
        public virtual void Deactivate() => OnDeactivate?.Invoke(this);
        /// <summary>Every frame while active and God View is showing.</summary>
        public virtual void Update() => OnUpdate?.Invoke(this);
        public virtual void Run() => OnRun?.Invoke(this);
        public virtual void WorldClick() => OnWorldClick?.Invoke(this);
        public virtual bool HasPanel => DrawPanel != null;
        public virtual void Panel(Rect area) => DrawPanel?.Invoke(this, area);
        public virtual void Overlay() => DrawOverlay?.Invoke(this);
        public virtual string Badge => BadgeText?.Invoke();
        public virtual bool Available => IsAvailable == null || IsAvailable();
        public virtual string Tip => TipText?.Invoke();
        /// <summary>Panel height for the given width (default: fills the left column).</summary>
        public virtual float PanelHeight(float width) => -1f;
    }
}
