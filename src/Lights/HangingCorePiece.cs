using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the hanging surtling core: a surtling core on a leather cord that hangs
    /// under a beam or roof and glows without fuel. It clones the hanging brazier for its placement
    /// and support from above, then strips everything that makes the brazier a fire (fuel, flames,
    /// heat, smoke, sound): the core never goes out, but it gives no warmth or comfort either.
    /// </summary>
    internal static class HangingCorePiece
    {
        public const string PrefabName = "piece_hangingsurtlingcore";
        public const string HoverName = "$" + PrefabName;
        private const string BrazierPrefab = "piece_brazierceiling01";
        private const string CorePrefab = "SurtlingCore";
        private const string CordPrefab = "LeatherScraps";

        private static readonly string[] s_buildUsage = { PieceUsages.Lighting };

        // The cord hangs from the piece origin, which placement puts on the aimed underside.
        private const float CordLength = 0.5f;
        private const float CordRadius = 0.008f;

        // A soft glow for a corner rather than a torch that lights the room.
        private const float LightRange = 6f;
        private const float LightIntensity = 1.2f;
        private static readonly Color s_lightColor = new Color(1f, 0.45f, 0.15f);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(BrazierPrefab) || !RequirePrefab(CorePrefab) || !RequirePrefab(CordPrefab)) return;
            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to build {PrefabName}: {e}");
            }
        }

        private static RequirementConfig Req(string item, int amount) => new RequirementConfig(item, amount, 0, true);

        private static void Build()
        {
            var cordSource = PrefabManager.Cache.GetPrefab<GameObject>(CordPrefab).GetComponentInChildren<Renderer>(true);
            if (!cordSource || !cordSource.sharedMaterial)
            {
                Jotunn.Logger.LogWarning($"'{CordPrefab}' has no material for the cord; skipping {PrefabName}.");
                return;
            }

            // The crafting station (workbench) stays the cloned brazier's.
            var config = new PieceConfig
            {
                Name = HoverName,
                Description = HoverName + "_description",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                Usage = s_buildUsage,
                Requirements = new[] { Req("SurtlingCore", 1), Req("LeatherScraps", 2) },
            };
            var piece = new CustomPiece(PrefabName, BrazierPrefab, config);
            var prefab = piece.PiecePrefab;

            // Build the model before stripping the brazier, so a failure here leaves a working
            // (vanilla-looking) brazier instead of an invisible one.
            var model = new GameObject("hanging_core");
            model.transform.SetParent(prefab.transform, false);
            AddCord(model.transform, cordSource.sharedMaterial);
            if (!AddCore(model.transform))
            {
                Jotunn.Logger.LogWarning($"'{CorePrefab}' has no mesh; skipping {PrefabName}.");
                Object.DestroyImmediate(model);
                return;
            }

            StripFire(prefab, model.transform);
            PieceUtils.StripModel(prefab, model.transform);
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t && t != prefab.transform && t.CompareTag("snappoint")) Object.DestroyImmediate(t.gameObject);

            var pieceComponent = prefab.GetComponent<Piece>();
            pieceComponent.m_comfort = 0;
            pieceComponent.m_comfortGroup = Piece.ComfortGroup.None;

            // Lights in Valheim cull by layer; the piece layer also makes the model hoverable and
            // lets the build hammer target it.
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prefab.layer;

            // Placement only snaps to convex colliders. The box's top is the cord's top at the
            // origin, so it sits against the underside it hangs from and takes support from it.
            var bounds = PieceUtils.MeshBounds(model.transform, prefab.transform);
            var box = prefab.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) pieceComponent.m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // A thin cylinder from the origin straight down, in the leather scraps' material. Unity's
        // cylinder is 2 m tall and 1 m wide, centred on its origin.
        private static void AddCord(Transform parent, Material material)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);

            var cord = new GameObject("cord");
            cord.transform.SetParent(parent, false);
            cord.transform.localPosition = new Vector3(0f, -CordLength / 2f, 0f);
            cord.transform.localScale = new Vector3(CordRadius * 2f, CordLength / 2f, CordRadius * 2f);
            cord.AddComponent<MeshFilter>().sharedMesh = mesh;
            cord.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // The surtling core item's model and its sparks, hung with its top at the cord's end, and
        // one light of our own colour and range in its middle.
        private static bool AddCore(Transform parent)
        {
            var core = Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(CorePrefab), parent, false);
            core.name = "core";
            StripToGlow(core);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            var raw = PieceUtils.MeshBounds(core.transform, parent);
            if (raw.size.y <= 0f) return false;
            core.transform.localPosition = new Vector3(-raw.center.x, -CordLength - raw.max.y, -raw.center.z);

            // Keep the item's own light if it has one, so a LightFlicker on it still flickers it.
            var lights = core.GetComponentsInChildren<Light>(true);
            Light light;
            if (lights.Length > 0)
            {
                light = lights[0];
                for (int i = 1; i < lights.Length; i++) Object.DestroyImmediate(lights[i]);
            }
            else
            {
                var go = new GameObject("light");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(0f, -CordLength - raw.size.y / 2f, 0f);
                light = go.AddComponent<Light>();
            }
            light.type = LightType.Point;
            light.color = s_lightColor;
            light.range = LightRange;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.Soft;
            light.gameObject.SetActive(true);
            return true;
        }

        // Removes everything but rendering, light and sparks from the instantiated item: its
        // ItemDrop, Rigidbody, ZNetView and colliders would make it a loose networked item.
        private static void StripToGlow(GameObject root)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is Transform || component is MeshFilter || component is Renderer || component is LODGroup ||
                        component is Light || component is LightFlicker || component is ParticleSystem)
                        continue;
                    Object.DestroyImmediate(component);
                }
            }
        }

        // Removes the brazier's fire outside keep: fuel, flames, heat and comfort area, smoke and sound.
        private static void StripFire(GameObject prefab, Transform keep)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component.transform.IsChildOf(keep)) continue;
                    if (component is Fireplace || component is EffectArea || component is SmokeSpawner || component is Light ||
                        component is LightFlicker || component is LightLod || component is ParticleSystem ||
                        component is ParticleSystemRenderer || component is AudioSource || component is ZSFX)
                        Object.DestroyImmediate(component);
                }
            }
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping {PrefabName}.");
            return false;
        }
    }
}
