using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the dvergr cask: a clone of the vanilla fermenter whose model is swapped
    /// for the dvergr barrel prop. It is the fermenter's later step up: the fermenter's recipe with
    /// black metal in place of bronze, so it comes with the Plains, and it ferments faster and yields
    /// more per mead base than the vanilla one. The cloned WearNTear and effects stay as they are.
    /// </summary>
    internal static class CaskPiece
    {
        public const string PrefabName = "piece_dvergrcask";
        public const string HoverName = "$" + PrefabName;
        public const string FermenterPrefab = "fermenter";
        private const string BarrelPrefab = "dvergrprops_barrel";

        // Hammer tab. Jotunn's Usage tags, not the category, decide where a piece shows up; the
        // fermenter's own Meads tag put the cask in the mead tab instead of Crafting.
        private static readonly string[] s_buildUsage = { PieceUsages.Crafting };

        // Seconds per batch; the vanilla fermenter takes 2400 (40 minutes).
        private const float FermentationDuration = 1800f;
        // Meads per batch: the vanilla fermenter's 6 become 8; other counts scale by the same ratio.
        private const float YieldRatio = 8f / 6f;

        public static int CaskYield(int fermenterYield) => Mathf.Max(1, Mathf.CeilToInt(fermenterYield * YieldRatio - 0.001f));

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(FermenterPrefab) || !RequirePrefab(BarrelPrefab)) return;
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
            // The vanilla fermenter's recipe (30 Fine wood, 5 Bronze, 10 Resin) with black metal for
            // the bronze. The crafting station (workbench) stays the cloned fermenter's.
            var config = new PieceConfig
            {
                Name = HoverName,
                Description = HoverName + "_description",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Crafting,
                Usage = s_buildUsage,
                Requirements = new[] { Req("FineWood", 30), Req("BlackMetal", 5), Req("Resin", 10) },
            };
            var piece = new CustomPiece(PrefabName, FermenterPrefab, config);
            var prefab = piece.PiecePrefab;

            var fermenter = prefab.GetComponent<Fermenter>();
            if (!fermenter)
            {
                Jotunn.Logger.LogWarning($"'{FermenterPrefab}' has no Fermenter; skipping {PrefabName}.");
                return;
            }
            fermenter.m_name = HoverName;
            // The yield is set per conversion when a cask spawns (FermenterPatches).
            fermenter.m_fermentationDuration = FermentationDuration;

            ReplaceModel(prefab, fermenter);

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) prefab.GetComponent<Piece>().m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // Swaps the fermenter's meshes for the dvergr barrel at its own size, standing on the piece
        // origin. The fermenting and ready states keep their particles and sounds; they, the lid
        // object, the output point and the roof check point move up or down with the new barrel top,
        // so bubbles sit on the barrel and the roof check stays just above it.
        private static void ReplaceModel(GameObject prefab, Fermenter fermenter)
        {
            var states = new List<Transform>();
            foreach (var go in new[] { fermenter.m_fermentingObject, fermenter.m_readyObject, fermenter.m_topObject })
                if (go) states.Add(go.transform);
            bool IsState(Transform t) => states.Exists(s => t.IsChildOf(s));

            var body = PieceUtils.MeshBounds(prefab.transform, prefab.transform, IsState);

            // Build and fit the model before stripping the vanilla barrel, so a failure here leaves
            // a working (vanilla-looking) fermenter instead of an invisible one.
            var model = Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(BarrelPrefab), prefab.transform, false);
            model.name = "dvergr_barrel";
            StripToVisual(model);
            // The prop isn't necessarily stored at the origin; start from zero before measuring.
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var raw = PieceUtils.MeshBounds(model.transform, prefab.transform);
            if (raw.size.y <= 0f)
            {
                Jotunn.Logger.LogWarning($"'{BarrelPrefab}' has no mesh; keeping the vanilla fermenter model.");
                Object.DestroyImmediate(model);
                return;
            }
            model.transform.localPosition = new Vector3(-raw.center.x, -raw.min.y, -raw.center.z);
            var bounds = PieceUtils.MeshBounds(model.transform, prefab.transform);

            PieceUtils.StripModel(prefab, model.transform);

            float lift = body.size.y > 0f ? bounds.max.y - body.max.y : 0f;
            var moved = new List<Transform>(states);
            if (fermenter.m_outputPoint) moved.Add(fermenter.m_outputPoint);
            if (fermenter.m_roofCheckPoint) moved.Add(fermenter.m_roofCheckPoint);
            foreach (var t in moved)
            {
                // A point nested under another moved transform already moves with it.
                if (moved.Exists(other => other != t && t.IsChildOf(other))) continue;
                t.position += Vector3.up * lift;
            }
            fermenter.m_hoverOffset += lift;

            // Lights in Valheim cull by layer; the piece layer also makes the model hoverable and
            // lets the build hammer target it.
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prefab.layer;

            // Placement only snaps to convex colliders; a box around the barrel is enough to place,
            // hover, hit and support it.
            var box = prefab.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        // Removes everything but rendering from an instantiated vanilla prop: its ZNetView,
        // Destructible and drops would otherwise make it a second networked object.
        private static void StripToVisual(GameObject root)
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

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping {PrefabName}.");
            return false;
        }
    }
}
