using UnityEngine;

namespace World.Game
{
    /// <summary>3D text and primitive helpers for village props (all materials via MaterialFactory).</summary>
    public static class Signs
    {
        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// <summary>Upright 3D text facing local -Z (readable from the front of its parent). height = cap height in metres.</summary>
        public static TextMesh Text(Transform parent, string text, Vector3 localPos, float height, Color color, float yawDeg = 180f)
        {
            var go = new GameObject("Text " + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yawDeg, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = height * 10f / 96f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFactory.TextMaterial(Font);
            return tm;
        }

        /// <summary>Text lying flat on the ground, readable from above with north up.</summary>
        public static TextMesh FlatText(Transform parent, string text, Vector3 worldPos, float height, Color color)
        {
            var tm = Text(parent, text, Vector3.zero, height, color, 0f);
            tm.transform.position = worldPos;
            tm.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return tm;
        }

        /// <summary>Unit-cube primitive with a shared lit colour; collider kept only if asked.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 localCentre, Vector3 size, Color color, bool collider = true, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Shared(color);
            return go;
        }

        /// <summary>Primitive with a given material, no collider.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A board with text on its front (front = local -Z of the parent unless yaw given).</summary>
        public static GameObject Board(Transform parent, string text, Vector3 localCentre, Vector2 size, Color boardColor, Color textColor, float yaw = 0f)
        {
            var root = new GameObject("Sign " + text).transform;
            root.SetParent(parent, false);
            root.localPosition = localCentre;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(root, "Board", Vector3.zero, new Vector3(size.x, size.y, 0.12f), boardColor, false);
            // front face is local +Z of the sign root; text faces +Z, so rotate 180 (TextMesh reads along -Z by default)
            Text(root, text, new Vector3(0, 0, 0.08f), size.y * 0.55f, textColor, 180f);
            return root.gameObject;
        }
    }
}
