using UnityEditor.SceneManagement;
public static class OpenWorldScene
{
    public static string Run() { var s = EditorSceneManager.OpenScene("Assets/Scenes/World.unity"); return "opened " + s.path; }
}
