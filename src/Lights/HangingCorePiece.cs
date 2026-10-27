using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the hanging surtling core: a surtling core on a rope that hangs under a
    /// beam or roof and glows without fuel. It clones the hanging brazier for its placement and
    /// support from above, then strips everything that makes the brazier a fire (fuel, flames, heat,
    /// smoke, sound): the core never goes out, but it gives no warmth or comfort either. The brazier
    /// is iron, so its build, hit and break sounds are swapped for the wooden sign's.
    /// </summary>
    internal static class HangingCorePiece
    {
        public const string PrefabName = "piece_hangingsurtlingcore";
        public const string HoverName = "$" + PrefabName;
        private const string BrazierPrefab = "piece_brazierceiling01";
        // The burial chambers' core stand: its core has a flickering light and, unlike the
        // SurtlingCore item, no "pick me up" sparkle.
        private const string CoreStandPrefab = "Pickable_SurtlingCoreStand";
        private const string CoreChild = "core";
        // The cart's pulling line is vanilla's only rope.
        private const string RopePrefab = "Cart";
        private const string SoundPrefab = "sign";

        private static readonly string[] s_buildUsage = { PieceUsages.Lighting };

        // The rope hangs from the piece origin, which placement puts on the aimed underside. A
        // single line ending on the core's top did not read as holding it, so the rope ends in a
        // net: strands from top to bottom around the core and a ring around its middle.
        private const float RopeLength = 0.5f;
        private const float RopeWidth = 0.045f;
        private const float StrandWidth = 0.028f;
        private const int StrandCount = 4;
        private const int StrandSegments = 12;
        private const int RingSegments = 16;
        // How far the net sits outside the core, as a factor of the core's radius.
        private const float NetGap = 1.05f;

        // A soft glow for a corner rather than a torch that lights the room.
        private const float LightRange = 6f;
        private const float LightIntensity = 1.2f;
        private static readonly Color s_lightColor = new Color(1f, 0.45f, 0.15f);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(BrazierPrefab) || !RequirePrefab(CoreStandPrefab) || !RequirePrefab(RopePrefab) ||
                !RequirePrefab(SoundPrefab))
                return;
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
            var rope = PrefabManager.Cache.GetPrefab<GameObject>(RopePrefab).GetComponentInChildren<LineRenderer>(true);
            if (!rope || !rope.sharedMaterial)
            {
                Jotunn.Logger.LogWarning($"'{RopePrefab}' has no rope line; skipping {PrefabName}.");
                return;
            }
            var coreSource = FindChild(PrefabManager.Cache.GetPrefab<GameObject>(CoreStandPrefab).transform, CoreChild);
            if (!coreSource || !coreSource.GetComponent<MeshFilter>())
            {
                Jotunn.Logger.LogWarning($"'{CoreStandPrefab}' has no '{CoreChild}' mesh; skipping {PrefabName}.");
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

            var model = new GameObject("hanging_core");
            model.transform.SetParent(prefab.transform, false);
            var coreBounds = AddCore(model.transform, coreSource);
            AddRope(model.transform, rope, coreBounds);

            StripFire(prefab, model.transform);
            PieceUtils.StripModel(prefab, model.transform);
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t && t != prefab.transform && t.CompareTag("snappoint")) Object.DestroyImmediate(t.gameObject);

            var pieceComponent = prefab.GetComponent<Piece>();
            pieceComponent.m_comfort = 0;
            pieceComponent.m_comfortGroup = Piece.ComfortGroup.None;
            UseWoodSounds(prefab);

            // Lights in Valheim cull by layer; the piece layer also makes the model hoverable and
            // lets the build hammer target it.
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prefab.layer;

            // Placement only snaps to convex colliders. The box runs from the rope's top at the
            // origin to the core's bottom, so it sits against the underside it hangs from and takes
            // support from it.
            var box = prefab.AddComponent<BoxCollider>();
            box.center = new Vector3(coreBounds.center.x, coreBounds.min.y / 2f, coreBounds.center.z);
            box.size = new Vector3(coreBounds.size.x, -coreBounds.min.y, coreBounds.size.z);

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) pieceComponent.m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // A straight line from the origin down to the core's top, then the net around the core,
        // all drawn like the cart's rope with its material and texture mode.
        private static void AddRope(Transform parent, LineRenderer source, Bounds core)
        {
            var center = core.center;
            float radius = Mathf.Max(core.extents.x, core.extents.y, core.extents.z) * NetGap;
            var top = center + Vector3.up * radius;

            AddLine(parent, source, "rope", RopeWidth, false, Vector3.zero, top);

            for (int s = 0; s < StrandCount; s++)
            {
                float around = s * Mathf.PI * 2f / StrandCount;
                var points = new Vector3[StrandSegments + 1];
                for (int i = 0; i <= StrandSegments; i++)
                {
                    float down = Mathf.PI * i / StrandSegments;
                    points[i] = center + new Vector3(Mathf.Sin(down) * Mathf.Cos(around), Mathf.Cos(down),
                        Mathf.Sin(down) * Mathf.Sin(around)) * radius;
                }
                AddLine(parent, source, "strand", StrandWidth, false, points);
            }

            var ring = new Vector3[RingSegments];
            for (int i = 0; i < RingSegments; i++)
            {
                float around = i * Mathf.PI * 2f / RingSegments;
                ring[i] = center + new Vector3(Mathf.Cos(around), 0f, Mathf.Sin(around)) * radius;
            }
            AddLine(parent, source, "ring", StrandWidth, true, ring);
        }

        private static void AddLine(Transform parent, LineRenderer source, string name, float width, bool loop,
            params Vector3[] points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = source.sharedMaterial;
            line.textureMode = source.textureMode;
            line.alignment = source.alignment;
            line.numCapVertices = source.numCapVertices;
            line.numCornerVertices = 2;
            line.shadowCastingMode = source.shadowCastingMode;
            line.receiveShadows = source.receiveShadows;
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = width;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        // A copy of the core stand's core and its light, hung with its top at the rope's end.
        // Returns the core's bounds in parent space.
        private static Bounds AddCore(Transform parent, Transform source)
        {
            var core = Object.Instantiate(source.gameObject, parent, false);
            core.name = "core";
            // The stand's visual is scaled up; keep the core at the size it has on the stand.
            core.transform.localScale = source.lossyScale;
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            StripToGlow(core);
            var raw = PieceUtils.MeshBounds(core.transform, parent);
            core.transform.localPosition = new Vector3(-raw.center.x, -RopeLength - raw.max.y, -raw.center.z);

            // Keep the stand's light, so the flicker on it still flickers it.
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
                go.transform.localPosition = new Vector3(0f, -RopeLength - raw.size.y / 2f, 0f);
                light = go.AddComponent<Light>();
            }
            light.type = LightType.Point;
            light.color = s_lightColor;
            light.range = LightRange;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.Soft;
            light.gameObject.SetActive(true);

            return PieceUtils.MeshBounds(core.transform, parent);
        }

        // Swaps the brazier's iron sounds and material for the wooden sign's: placing, hitting and
        // breaking a rope and a core should not ring like metal.
        private static void UseWoodSounds(GameObject prefab)
        {
            var sign = PrefabManager.Cache.GetPrefab<GameObject>(SoundPrefab);
            var signPiece = sign.GetComponent<Piece>();
            if (signPiece) prefab.GetComponent<Piece>().m_placeEffect = signPiece.m_placeEffect;

            var signWear = sign.GetComponent<WearNTear>();
            var wear = prefab.GetComponent<WearNTear>();
            if (!signWear || !wear) return;
            wear.m_materialType = signWear.m_materialType;
            wear.m_hitEffect = signWear.m_hitEffect;
            wear.m_destroyedEffect = signWear.m_destroyedEffect;
        }

        // Removes everything from the copied core but its mesh, its light and the light's flicker.
        private static void StripToGlow(GameObject root)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is Transform || component is MeshFilter || component is MeshRenderer ||
                        component is Light || component is LightFlicker)
                        continue;
                    Object.DestroyImmediate(component);
                }
            }
        }

        // Removes the brazier's fire outside keep: fuel, flames, heat and comfort area, smoke and
        // sound, and the trigger colliders the heat and burn areas used.
        private static void StripFire(GameObject prefab, Transform keep)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component.transform.IsChildOf(keep)) continue;
                    if (component is Fireplace || component is EffectArea || component is Aoe || component is SmokeSpawner ||
                        component is Light || component is LightFlicker || component is LightLod ||
                        component is ParticleSystem || component is ParticleSystemRenderer || component is AudioSource ||
                        component is ZSFX || (component is Collider collider && collider.isTrigger))
                        Object.DestroyImmediate(component);
                }
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping {PrefabName}.");
            return false;
        }
    }
}
