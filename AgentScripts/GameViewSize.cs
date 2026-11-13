using UnityEditor;

/// <summary>
/// Sets the Game view to a fixed rendering resolution so captures and IMGUI scale match a real screen.
/// Run: unity command script run --file AgentScripts/GameViewSize.cs --entry GameViewSize.Set1080
/// </summary>
public static class GameViewSize
{
    public static string Set1080() => Set(1920, 1080);

    public static string Set(int w, int h)
    {
        PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "WorldMVP " + w + "x" + h);
        PlayModeWindow.GetRenderingResolution(out uint rw, out uint rh);
        return "game view rendering " + rw + "x" + rh;
    }
}
