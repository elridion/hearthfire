using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Helpers shared by pieces that clone a vanilla piece and swap its model. Everything here works
    /// on the clone only; vanilla prefabs are shared and must never be edited.
    /// </summary>
    internal static class PieceUtils
    {
        // Removes the clone's own meshes, LOD groups and solid colliders, keeping everything under
        // keep (the new model) and every trigger.
        public static void StripModel(GameObject prefab, Transform keep)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (keep && component.transform.IsChildOf(keep)) continue;
                    if (component is MeshRenderer || component is SkinnedMeshRenderer || component is MeshFilter ||
                        component is LODGroup || (component is Collider collider && !collider.isTrigger))
                        Object.DestroyImmediate(component);
                }
            }
        }

        // Combined mesh bounds of everything under root, expressed in space's local coordinates.
        // Meshes for which skip returns true are left out.
        public static Bounds MeshBounds(Transform root, Transform space, System.Func<Transform, bool> skip = null)
        {
            var toSpace = space.worldToLocalMatrix;
            bool any = false;
            var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                if (skip != null && skip(filter.transform)) continue;
                var b = filter.sharedMesh.bounds;
                var toLocal = toSpace * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = toLocal.MultiplyPoint3x4(new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (any) result.Encapsulate(corner);
                    else result = new Bounds(corner, Vector3.zero);
                    any = true;
                }
            }
            return result;
        }

        // turn spins the model around its own vertical axis before the isometric view, for models
        // whose front faces away from the icon camera.
        public static Sprite RenderIcon(GameObject prefab, float turn = 0f)
        {
            // Dedicated servers have no graphics device to render with, and never show icons.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            try
            {
                return RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation * Quaternion.Euler(0f, turn, 0f));
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogWarning($"Icon render failed for {prefab.name}: {e.Message}");
                return null;
            }
        }
    }
}
