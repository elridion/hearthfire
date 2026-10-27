using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the twelve stone idols: clones of the vanilla stone pillar (so they keep
    /// its stone WearNTear, stonecutter requirement and destruction effects) whose model is replaced
    /// by an embedded idol mesh. The models are already in metres with their base on the origin, so
    /// they are placed as they are. The material is a copy of the vanilla ward stone's: it uses the
    /// Standard shader, which takes the idols' albedo and normal map as they are.
    /// </summary>
    internal static class IdolPieces
    {
        public const int Count = 12;
        public const string DescriptionKey = "piece_celticidol_description";
        private const string BasePrefab = "stone_pillar";
        private const string MaterialPrefab = "guard_stone";

        // Smoothness of the weathered stone; the source's smoothness maps average about this after
        // their 0.2 gloss scale, so a constant replaces them.
        private const float Glossiness = 0.1f;
        private const int StoneCost = 20;
        // Hammer tab. Like the cask, the Usage tag decides it; without one the idols kept the stone
        // pillar's Architecture tag.
        private static readonly string[] s_buildUsage = { PieceUsages.Decor };
        // The idol models face away from the isometric icon camera; the icon turns them round.
        private const float IconTurn = 180f;
        // Horizontal bands, each with its own convex hull collider; more bands fit the outline closer.
        private const int HullBands = 4;

        // Stone idol I has a large stone bowl in front of the figure. Measured in the space ObjLoader
        // loads idol01.obj in (X mirrored from the file): the bowl is the part of the model with x
        // above BowlEdgeX, the figure stands below it, the bowl floor is about BowlFloor high, and the
        // rim is split into RimSectors hulls so the bowl stays open and things can be placed in it.
        private const int BowlIdol = 1;
        private const float BowlEdgeX = -0.45f;
        private const float BowlFloor = 0.55f;
        private const int RimSectors = 8;

        // DXT5nm-style flat normal (R = 1, G = Y, B = 1, A = X).
        private static readonly Color32 s_flatNormal = new Color32(255, 128, 255, 128);

        public static string PrefabName(int index) => $"piece_celticidol{index:00}";

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(BasePrefab)) return;
            var source = SourceMaterial();
            if (!source) return;

            for (int i = 1; i <= Count; i++)
            {
                try
                {
                    Build(i, source);
                }
                catch (System.Exception e)
                {
                    Jotunn.Logger.LogError($"Failed to build {PrefabName(i)}: {e}");
                }
            }
        }

        private static void Build(int index, Material source)
        {
            var name = PrefabName(index);
            var mesh = ObjLoader.LoadEmbedded($"Hearthfire.idol{index:00}.obj.gz", name);
            var albedo = LoadEmbeddedTexture($"Hearthfire.idol{index:00}_albedo.jpg", false);
            // Without the albedo the pillar's textures would land scrambled on the idol's UVs.
            if (!mesh || !albedo)
            {
                Jotunn.Logger.LogWarning($"Idol {index} model or texture missing; skipping {name}.");
                return;
            }
            var normal = LoadEmbeddedTexture($"Hearthfire.idol{index:00}_n.png", true);

            var config = new PieceConfig
            {
                Name = "$" + name,
                Description = "$" + DescriptionKey,
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                Usage = s_buildUsage,
                CraftingStation = CraftingStations.Stonecutter,
                Requirements = new[] { new RequirementConfig("Stone", StoneCost, 0, true) },
            };
            var piece = new CustomPiece(name, BasePrefab, config);
            var prefab = piece.PiecePrefab;

            var model = new GameObject("idol");
            model.transform.SetParent(prefab.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeMaterial(source, albedo, normal, name);
            // Lights in Valheim cull by layer; the piece layer also makes the model hoverable and
            // lets the build hammer target it.
            model.layer = prefab.layer;

            PieceUtils.StripModel(prefab, model.transform);
            // The pillar's snap points would let walls and beams snap to thin air around the idol.
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t && t != prefab.transform && t.CompareTag("snappoint")) Object.DestroyImmediate(t.gameObject);

            if (index == BowlIdol) AddBowlIdolColliders(prefab, mesh, name);
            else AddHullColliders(prefab, mesh, name, null);

            var icon = PieceUtils.RenderIcon(prefab, IconTurn);
            if (icon) prefab.GetComponent<Piece>().m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // A box around a whole idol leaves wide empty corners around heads, arms and tapering bases.
        // Instead the idol is cut into horizontal bands and each band gets the convex hull of the
        // triangles reaching into it, so the stack follows the outline at every height. Placement
        // only snaps to convex colliders, and these are convex. A band without triangles is skipped.
        // include, when set, picks the triangles (by centroid) the hulls are built from.
        private static void AddHullColliders(GameObject prefab, Mesh mesh, string name, System.Func<Vector3, bool> include)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var bounds = mesh.bounds;
            float band = bounds.size.y / HullBands;

            for (int b = 0; b < HullBands; b++)
            {
                float bottom = bounds.min.y + b * band;
                float top = bottom + band;
                var picked = new List<int>();
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    float y0 = vertices[triangles[t]].y, y1 = vertices[triangles[t + 1]].y, y2 = vertices[triangles[t + 2]].y;
                    if (Mathf.Max(y0, Mathf.Max(y1, y2)) < bottom || Mathf.Min(y0, Mathf.Min(y1, y2)) > top) continue;
                    if (include != null && !include(Centroid(vertices, triangles, t))) continue;
                    picked.Add(t);
                }
                AddHull(prefab, $"{name}_hull{b}", vertices, triangles, picked);
            }
        }

        // Stone idol I: the figure gets the usual banded hulls, the bowl a box up to its floor and
        // one hull per rim sector. Banded hulls over the whole bowl filled it, so nothing could be
        // placed inside.
        private static void AddBowlIdolColliders(GameObject prefab, Mesh mesh, string name)
        {
            AddHullColliders(prefab, mesh, name, c => !InBowl(c));

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var bowl = BowlBounds(vertices, triangles);
            var centre = bowl.center;
            var sectors = new List<int>[RimSectors];
            for (int s = 0; s < RimSectors; s++) sectors[s] = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                var c = Centroid(vertices, triangles, t);
                if (!InBowl(c)) continue;
                float top = Mathf.Max(vertices[triangles[t]].y, Mathf.Max(vertices[triangles[t + 1]].y, vertices[triangles[t + 2]].y));
                if (top <= BowlFloor) continue;
                float angle = Mathf.Atan2(c.z - centre.z, c.x - centre.x) + Mathf.PI;
                sectors[Mathf.Min(RimSectors - 1, (int)(angle / (2f * Mathf.PI) * RimSectors))].Add(t);
            }
            for (int s = 0; s < RimSectors; s++) AddHull(prefab, $"{name}_rim{s}", vertices, triangles, sectors[s]);

            var floor = prefab.AddComponent<BoxCollider>();
            floor.center = new Vector3(centre.x, BowlFloor * 0.5f, centre.z);
            floor.size = new Vector3(bowl.size.x, BowlFloor, bowl.size.z);
        }

        // Footprint of the bowl part of idol I.
        private static Bounds BowlBounds(Vector3[] vertices, int[] triangles)
        {
            bool any = false;
            var bounds = new Bounds();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (!InBowl(Centroid(vertices, triangles, t))) continue;
                for (int k = 0; k < 3; k++)
                {
                    var v = vertices[triangles[t + k]];
                    if (any) bounds.Encapsulate(v);
                    else bounds = new Bounds(v, Vector3.zero);
                    any = true;
                }
            }
            return bounds;
        }

        // One convex MeshCollider around the picked triangles; nothing when none were picked.
        private static void AddHull(GameObject prefab, string name, Vector3[] vertices, int[] triangles, List<int> picked)
        {
            if (picked.Count == 0) return;
            var hullVertices = new List<Vector3>();
            var hullTriangles = new List<int>();
            var remap = new Dictionary<int, int>();
            foreach (int t in picked)
            {
                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[t + k];
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        mapped = hullVertices.Count;
                        remap[index] = mapped;
                        hullVertices.Add(vertices[index]);
                    }
                    hullTriangles.Add(mapped);
                }
            }

            var hull = new Mesh { name = name };
            if (hullVertices.Count > 65535) hull.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            hull.SetVertices(hullVertices);
            hull.SetTriangles(hullTriangles, 0);
            var collider = prefab.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = hull;
        }

        private static Vector3 Centroid(Vector3[] vertices, int[] triangles, int t) =>
            (vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f;

        private static bool InBowl(Vector3 point) => point.x > BowlEdgeX;

        // The ward stone's model renderer (PrivateArea.m_model) uses the Standard shader with a
        // normal map, so its copy keeps a shader variant that renders ours.
        private static Material SourceMaterial()
        {
            var ward = PrefabManager.Cache.GetPrefab<GameObject>(MaterialPrefab);
            var area = ward ? ward.GetComponent<PrivateArea>() : null;
            var material = area && area.m_model ? area.m_model.sharedMaterial : null;
            if (!material) Jotunn.Logger.LogWarning($"No material on vanilla '{MaterialPrefab}'; skipping the stone idols.");
            return material;
        }

        // Every slot the source carries belongs to the ward stone's UVs, so each one is set
        // explicitly; a cleared slot would fall back to the shader's default, which can be white.
        private static Material MakeMaterial(Material source, Texture2D albedo, Texture2D normal, string name)
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
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", Glossiness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Glossiness);
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
        // are compressed on clients (DXT1 for the opaque albedo, DXT5 for the normal map) to keep
        // twelve idols' worth of maps small in video memory.
        private static Texture2D LoadEmbeddedTexture(string resourceName, bool linear)
        {
            using (var stream = typeof(IdolPieces).Assembly.GetManifestResourceStream(resourceName))
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
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Trilinear;
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    texture.Compress(true);
                    texture.Apply(false, true);
                }
                return texture;
            }
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping the stone idols.");
            return false;
        }
    }
}
