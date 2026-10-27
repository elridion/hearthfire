using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the hanging surtling core: a surtling core held in the big loop of a
    /// knotted rope that hangs by its small loop under a beam or roof, glowing without fuel. It
    /// clones the hanging brazier for its placement and support from above, then strips everything
    /// that makes the brazier a fire (fuel, flames, heat, smoke, sound): the core never goes out, but
    /// it gives no warmth or comfort either. The brazier is iron, so its build, hit and break sounds
    /// are swapped for the wooden sign's. The rope is an embedded model (irons3th, CC BY 4.0).
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
        private const string SoundPrefab = "sign";

        private const string RopeModel = "Hearthfire.rope.obj.gz";
        private const string RopeAlbedo = "Hearthfire.rope_albedo.jpg";
        private const string RopeNormal = "Hearthfire.rope_n.png";

        private static readonly string[] s_buildUsage = { PieceUsages.Lighting };

        // Measured in the rope model (see assets/models/hanging_core/README.md, X mirrored like
        // ObjLoader; the model's origin is the top of its small loop): s_hangPoint is the top of the
        // small loop's opening (its middle sank too much of the loop into the beam) and
        // s_loopCentre the middle of the big loop's. The piece origin, which placement puts on the
        // aimed underside, is the hang point, so the top of the small loop reaches into the beam
        // as if hung on a nail. The model is 0.75 m long; RopeScale makes it
        // about 0.73 m. In game 0.65 looked too small next to the vanilla-size core and 1.3 too big.
        private const float RopeScale = 0.975f;
        private static readonly Vector3 s_hangPoint = new Vector3(-0.005f, -0.075f, 0f);
        private static readonly Vector3 s_loopCentre = new Vector3(-0.0118f, -0.5993f, -0.0261f);
        private const float RopeGlossiness = 0.05f;
        // The core hangs upside down, on one of its tips, tilted a little off true and turned, so it
        // looks hung rather than set down.
        private static readonly Quaternion s_coreRotation = Quaternion.Euler(192f, 27f, 9f);
        // How far the core sits below the big loop's centre, in metres: centred, it looked to sit
        // too high in the loop.
        private const float CoreDrop = 0.08f;

        // A soft glow for a corner rather than a torch that lights the room.
        private const float LightRange = 6f;
        private const float LightIntensity = 1.2f;
        private static readonly Color s_lightColor = new Color(1f, 0.45f, 0.15f);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(BrazierPrefab) || !RequirePrefab(CoreStandPrefab) || !RequirePrefab(SoundPrefab)) return;
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
            var coreSource = FindChild(PrefabManager.Cache.GetPrefab<GameObject>(CoreStandPrefab).transform, CoreChild);
            if (!coreSource || !coreSource.GetComponent<MeshFilter>())
            {
                Jotunn.Logger.LogWarning($"'{CoreStandPrefab}' has no '{CoreChild}' mesh; skipping {PrefabName}.");
                return;
            }
            var materialSource = PieceUtils.StandardMaterialSource(PrefabName);
            if (!materialSource) return;
            var ropeMesh = ObjLoader.LoadEmbedded(RopeModel, "hearthfire_rope");
            var ropeAlbedo = PieceUtils.LoadEmbeddedTexture(RopeAlbedo, false, TextureWrapMode.Repeat);
            if (!ropeMesh || !ropeAlbedo)
            {
                Jotunn.Logger.LogWarning($"Rope model or texture missing; skipping {PrefabName}.");
                return;
            }
            var ropeNormal = PieceUtils.LoadEmbeddedTexture(RopeNormal, true, TextureWrapMode.Repeat);

            // The workbench, set explicitly: the cloned brazier is a metal piece and would keep its
            // own station.
            var config = new PieceConfig
            {
                Name = HoverName,
                Description = HoverName + "_description",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                Usage = s_buildUsage,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[] { Req("SurtlingCore", 1), Req("LeatherScraps", 2) },
            };
            var piece = new CustomPiece(PrefabName, BrazierPrefab, config);
            var prefab = piece.PiecePrefab;

            var model = new GameObject("hanging_core");
            model.transform.SetParent(prefab.transform, false);
            var rope = new GameObject("rope");
            rope.transform.SetParent(model.transform, false);
            rope.transform.localScale = Vector3.one * RopeScale;
            rope.transform.localPosition = -s_hangPoint * RopeScale;
            rope.AddComponent<MeshFilter>().sharedMesh = ropeMesh;
            rope.AddComponent<MeshRenderer>().sharedMaterial =
                PieceUtils.MakeStandardMaterial(materialSource, ropeAlbedo, ropeNormal, "hearthfire_rope", RopeGlossiness);
            AddCore(model.transform, coreSource);

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

            // Placement only snaps to convex colliders and pushes the collider's closest point onto
            // the aimed surface, so the box stops at the hang point (the origin) and the part of
            // the small loop above it goes into the beam. Touching the underside, it takes support
            // from it.
            var bounds = PieceUtils.MeshBounds(model.transform, prefab.transform);
            var box = prefab.AddComponent<BoxCollider>();
            box.center = new Vector3(bounds.center.x, bounds.min.y / 2f, bounds.center.z);
            box.size = new Vector3(bounds.size.x, -bounds.min.y, bounds.size.z);

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) pieceComponent.m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // A copy of the core stand's core and its light, centred in the rope's big loop. It keeps
        // the size it has on the stand (about 14 cm radius), far wider than the loop's opening, so
        // the rope's loop sinks into it.
        private static void AddCore(Transform parent, Transform source)
        {
            var core = Object.Instantiate(source.gameObject, parent, false);
            core.name = "core";
            core.transform.localScale = source.lossyScale;
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = s_coreRotation;
            StripToGlow(core);

            var raw = PieceUtils.MeshBounds(core.transform, parent);
            var centre = (s_loopCentre - s_hangPoint) * RopeScale + Vector3.down * CoreDrop;
            core.transform.localPosition = centre - raw.center;

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
                go.transform.localPosition = centre;
                light = go.AddComponent<Light>();
            }
            light.type = LightType.Point;
            light.color = s_lightColor;
            light.range = LightRange;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.Soft;
            light.gameObject.SetActive(true);
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
