using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MakeViewerScene
{
    public static string Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("ClipViewer").AddComponent<RomanVillage.Probe.ClipViewer>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ClipViewer.unity");
        return "saved Assets/Scenes/ClipViewer.unity";
    }
}
