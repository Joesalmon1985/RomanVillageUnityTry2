using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World.Game;
using World.Sim;
using Terrain = World.Sim.Terrain;

/// <summary>
/// G1-4 captures (edit mode, no play needed): the five abundance levels of each terrain on a synthetic board (one row
/// per terrain, pips 1..5 left to right), plus a dealt island with its ports. 844 x 390 PNGs into Evidence/godboard/.
/// Run: unity command script run --file AgentScripts/DressingCaptures.cs --entry DressingCaptures.Levels
///      unity command script run --file AgentScripts/DressingCaptures.cs --entry DressingCaptures.Island
/// </summary>
public static class DressingCaptures
{
    private static readonly int[] Numbers = { 2, 3, 4, 5, 6 }; // pips 1..5

    public static string Levels()
    {
        var sb = new StringBuilder();
        Directory.CreateDirectory("Evidence/godboard");
        foreach (var terrain in new[] { Terrain.Forest, Terrain.Hills, Terrain.Fields, Terrain.Pasture, Terrain.Mountain })
        {
            var b = Synthetic(terrain);
            sb.Append(Shoot(b, "Evidence/godboard/levels_" + terrain.ToString().ToLower() + ".png", new Vector3(0, 0, 0), 120f, 62f)).Append(" | ");
        }
        return sb.ToString();
    }

    public static string Island()
    {
        var b = BoardGenerator.CreateDealtBoard(7UL);
        Directory.CreateDirectory("Evidence/godboard");
        string a = Shoot(b, "Evidence/godboard/island_seed7.png", Vector3.zero, 560f, 62f);
        string c = Shoot(b, "Evidence/godboard/island_seed7_close.png", new Vector3(0, 0, 0), 200f, 62f);
        var h0 = BoardSpace.Hex(b.Topology, b.Ports[0].Hex);
        var d = h0.normalized;
        string e = Shoot(b, "Evidence/godboard/port0_seed7.png", h0 + d * 120f, 90f, 62f);
        return a + " | " + c + " | " + e + " | port0 good=" + b.Ports[0].Good;
    }

    /// <summary>Row (r = 0) of five hexes of one terrain with pips 1..5; everything else Meadow.</summary>
    private static Board Synthetic(Terrain terrain)
    {
        var src = BoardGenerator.CreateDealtBoard(1UL);
        var t = src.Topology;
        var b = new Board { Radius = src.Radius, HexTerrain = new Terrain[t.HexCount], HexNumber = new int[t.HexCount] };
        for (int q = -2; q <= 2; q++)
        {
            int h = t.HexIndex(new HexCoord(q, 0));
            b.HexTerrain[h] = terrain; b.HexNumber[h] = Numbers[q + 2];
        }
        return b;
    }

    private static string Shoot(Board b, string path, Vector3 focus, float orthoSize, float pitch)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var view = HexDressingView.Create(b, null);
        var lg = new GameObject("Sun"); var l = lg.AddComponent<Light>();
        l.type = LightType.Directional; l.color = new Color(1f, 0.93f, 0.80f); l.intensity = 1.15f; l.shadows = LightShadows.Soft;
        lg.transform.rotation = Quaternion.Euler(38f, -150f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.80f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.52f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.24f);
        RenderSettings.fog = false;
        var cg = new GameObject("Cam"); var cam = cg.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = orthoSize; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.20f, 0.42f, 0.62f); cam.nearClipPlane = 1f; cam.farClipPlane = 3000f;
        cg.transform.rotation = Quaternion.Euler(pitch, 0, 0);
        cg.transform.position = focus - cg.transform.forward * 900f;
        var rt = new RenderTexture(844, 390, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(844, 390, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 844, 390), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        int items = 0; foreach (int n in view.ItemCounts) items += n;
        return path + " items=" + items;
    }
}
