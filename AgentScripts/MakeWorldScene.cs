using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rebuilds Assets/Scenes/World.unity (one WorldBootstrap object), the Resources base materials
/// for the World shaders, and the build scene list (World.unity only).
/// Run: unity command script run --file AgentScripts/MakeWorldScene.cs --entry MakeWorldScene.Run
/// </summary>
public static class MakeWorldScene
{
    public const string ScenePath = "Assets/Scenes/World.unity";

    public static string Run()
    {
        AssetDatabase.Refresh();
        string mats = MakeMaterial("Assets/World/Game/Shaders/WorldLit.shader", "Assets/World/Game/Resources/World/WorldLit.mat")
            + " " + MakeMaterial("Assets/World/Game/Shaders/WorldLitVertexColor.shader", "Assets/World/Game/Resources/World/WorldLitVertexColor.mat")
            + " " + MakeMaterial("Assets/World/Game/Shaders/WorldUnlit.shader", "Assets/World/Game/Resources/World/WorldUnlit.mat")
            + " " + MakeMaterial("Assets/World/Game/Shaders/WorldGlow.shader", "Assets/World/Game/Resources/World/WorldGlow.mat")
            + " " + MakeMaterial("Assets/World/Game/Shaders/WorldText.shader", "Assets/World/Game/Resources/World/WorldText.mat");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("WorldBootstrap").AddComponent<World.Game.WorldBootstrap>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        return "saved " + ScenePath + "; build scenes = [World.unity]; " + mats;
    }

    private static string MakeMaterial(string shaderPath, string matPath)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) return "MISSING shader " + shaderPath;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(matPath));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return "ok " + matPath;
    }
}
