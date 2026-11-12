using System.IO;
using UnityEngine;

namespace Bedsit
{
    /// <summary>Renders a camera to a PNG (used for evidence screenshots; independent of window focus).</summary>
    public static class ScreenshotUtil
    {
        public static string EvidenceDir
        {
            get
            {
                var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "screenshots"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string Capture(Camera cam, string fileName, int width = 1280, int height = 720)
        {
            var path = Path.Combine(EvidenceDir, fileName);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.Destroy(rt);
            }
            return path;
        }

        /// <summary>Mean luminance 0..1 of a camera render (used by tests to prove a view is not black/empty).</summary>
        public static float MeanLuminance(Camera cam, int width = 160, int height = 90)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            float sum = 0f;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                var px = tex.GetPixels32();
                foreach (var p in px) sum += (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                sum /= px.Length;
                Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.Destroy(rt);
            }
            return sum;
        }
    }
}
