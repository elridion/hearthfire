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
        // The vanilla piece whose material embedded models copy (see StandardMaterialSource).
        private const string MaterialPrefab = "guard_stone";

        // DXT5nm-style flat normal (R = 1, G = Y, B = 1, A = X).
        private static readonly Color32 s_flatNormal = new Color32(255, 128, 255, 128);

        // The ward stone's model renderer (PrivateArea.m_model) uses the Standard shader with a
        // normal map, so its copy keeps a shader variant that renders embedded albedo and normal
        // maps as they are. Null (with a warning naming what) when it is missing.
        public static Material StandardMaterialSource(string what)
        {
            var ward = PrefabManager.Cache.GetPrefab<GameObject>(MaterialPrefab);
            var area = ward ? ward.GetComponent<PrivateArea>() : null;
            var material = area && area.m_model ? area.m_model.sharedMaterial : null;
            if (!material) Jotunn.Logger.LogWarning($"No material on vanilla '{MaterialPrefab}'; skipping {what}.");
            return material;
        }

        // Every slot the source carries belongs to the ward stone's UVs, so each one is set
        // explicitly; a cleared slot would fall back to the shader's default, which can be white.
        public static Material MakeStandardMaterial(Material source, Texture2D albedo, Texture2D normal, string name, float glossiness)
        {
            var material = new Material(source) { name = name };
            material.mainTexture = albedo;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal ? normal : FlatNormalTexture());
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", Texture2D.blackTexture);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", Texture2D.whiteTexture);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", glossiness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", glossiness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            // With a metallic/gloss map bound, the two values above would be ignored.
            if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", Texture2D.blackTexture);
            material.DisableKeyword("_METALLICGLOSSMAP");
            return material;
        }

        // Stands in for a normal map that fails to load.
        private static Texture2D FlatNormalTexture()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { name = "hearthfire_flat_normal" };
            texture.SetPixels32(new[] { s_flatNormal });
            texture.Apply(false, true);
            return texture;
        }

        // Normal maps must load as linear: sRGB decoding would bend the encoded vectors. Textures
        // are compressed on clients (DXT1 for an opaque albedo, DXT5 for a normal map) to keep
        // them small in video memory. Models whose UVs run past 0..1 need Repeat.
        public static Texture2D LoadEmbeddedTexture(string resourceName, bool linear, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            using (var stream = typeof(PieceUtils).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Jotunn.Logger.LogWarning($"Embedded texture '{resourceName}' not found.");
                    return null;
                }

                // Stream.Read may return fewer bytes than asked for; read until the buffer is full.
                var bytes = new byte[stream.Length];
                int read = 0, count;
                while (read < bytes.Length && (count = stream.Read(bytes, read, bytes.Length - read)) > 0) read += count;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = resourceName };
                if (!Jotunn.Utils.AssetUtils.LoadImage(texture, bytes))
                {
                    Jotunn.Logger.LogWarning($"Embedded texture '{resourceName}' failed to decode.");
                    Object.Destroy(texture);
                    return null;
                }
                texture.wrapMode = wrap;
                texture.filterMode = FilterMode.Trilinear;
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    texture.Compress(true);
                    texture.Apply(false, true);
                }
                return texture;
            }
        }

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

        // Removes everything but rendering from an instantiated vanilla prop: its ZNetView,
        // Destructible and drops would otherwise make it a second networked object.
        public static void StripToVisual(GameObject root)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is Transform || component is MeshFilter || component is Renderer || component is LODGroup)
                        continue;
                    Object.DestroyImmediate(component);
                }
            }
        }

        // Keeps only the most detailed level of each LOD group under root and removes the groups.
        // Jotunn's icon renderer drops LOD groups, so every level would draw on top of each other
        // and fight over the same surfaces.
        public static void KeepFirstLod(GameObject root)
        {
            foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                if (lods.Length > 1)
                {
                    var keep = new System.Collections.Generic.HashSet<Renderer>(lods[0].renderers);
                    for (int i = 1; i < lods.Length; i++)
                    {
                        foreach (var renderer in lods[i].renderers)
                        {
                            if (!renderer || keep.Contains(renderer)) continue;
                            var filter = renderer.GetComponent<MeshFilter>();
                            Object.DestroyImmediate(renderer);
                            if (filter) Object.DestroyImmediate(filter);
                        }
                    }
                }
                Object.DestroyImmediate(group);
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
