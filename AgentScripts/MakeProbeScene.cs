using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MakeProbeScene
{
    public static string Run()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("ClipProbe").AddComponent<RomanVillage.Probe.ClipProbe>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ClipProbe.unity");
        return "saved Assets/Scenes/ClipProbe.unity";
    }
}
