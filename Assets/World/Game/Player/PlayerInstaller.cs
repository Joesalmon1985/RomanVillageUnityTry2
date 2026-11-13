using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// Package P's entry point (REFACTOR_SPEC 8.0, appendix B): the player walker on the lattice at
    /// <see cref="CampLayout.Spawn"/>, the F1 God View camera and view switch, the pointer, speech bubbles, the map, the
    /// HUD and the inspect panel of the developer tools. Called first by <see cref="WorldBootstrap"/>. Owner: package P.
    /// </summary>
    public static class PlayerInstaller
    {
        public static void Install(WorldBootstrap boot)
        {
            var go = boot.gameObject;
            var camp = new CampLayout(boot.Sim.State.Board);
            var walker = PlayerWalker.Create(boot.transform, boot.MainCamera, camp.Spawn, camp.SpawnHeading);
            var god = GodCamera.Create(boot.transform);
            god.ClampRadius = BoardSpace.BoardRadius;
            god.MaxSize = GodCamera.SizeToFrame(BoardSpace.IslandRadius);
            god.HomeFocus = camp.Hearth;
            var views = go.AddComponent<ViewSwitcher>();
            views.Bind(walker, god);
            boot.SetViewpoint(walker, god, views);

            go.AddComponent<PointerController>();
            go.AddComponent<SpeechBubblesView>();
            go.AddComponent<DialoguePanel>();
            go.AddComponent<MapScreenView>();
            go.AddComponent<InspectPanel>();
            boot.SetHud(go.AddComponent<Hud>());
            Hud.Visible = false; // off at the start (spec 10.31)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
