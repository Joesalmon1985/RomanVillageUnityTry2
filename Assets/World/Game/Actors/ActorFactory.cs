using Bedsit;
using UnityEngine;

namespace World.Game
{
    /// <summary>
    /// The single creation point for characters (PRD_CHARACTERS CH-14, CH-15). Returns a <see cref="RiggedActor"/> when
    /// <see cref="CastRegistry"/> maps the id to a reviewed cast key whose local body prefab exists, otherwise the
    /// <see cref="PlaceholderActor"/> exactly as before. Both signatures and every call site are unchanged.
    /// </summary>
    public static class ActorFactory
    {
        public static ICharacterActor Create(string id, Color colour, Transform parent = null)
        {
            if (parent == null && WorldBootstrap.Instance != null) parent = WorldBootstrap.Instance.ActorsRoot;
            var prefab = CastRegistry.PrefabFor(id);
            if (prefab != null)
            {
                var r = RiggedActor.Build(id, colour, prefab, parent);
                if (r != null) return r;
            }
            return PlaceholderActor.Build(id, colour, parent);
        }

        public static ICharacterActor Create(string id, Color colour, Vector3 position, float yaw, Transform parent = null)
        {
            var a = Create(id, colour, parent);
            a.Root.position = position;
            if (a is PlaceholderActor p) p.SetYaw(yaw);
            else if (a is IExpressiveActor x) x.SetYaw(yaw);
            else a.Root.rotation = Quaternion.Euler(0, yaw, 0);
            return a;
        }
    }
}
