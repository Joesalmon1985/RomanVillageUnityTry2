using UnityEditor;
public static class GameViewPhone { public static string Set() { PlayModeWindow.SetCustomRenderingResolution(844, 390, "Phone 844x390"); PlayModeWindow.GetRenderingResolution(out uint w, out uint h); return w + "x" + h; } }
