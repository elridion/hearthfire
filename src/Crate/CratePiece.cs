using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the dvergr crate: a clone of the vanilla wood chest whose model is
    /// swapped for the dvergr crate prop, for storage that looks at home outside. The clone keeps
    /// the chest's Container (and so its size), wood WearNTear and open/close effects. Only the
    /// crate's most detailed LOD is kept, so the icon draws one crate rather than two overlapping.
    /// </summary>
    internal static class CratePiece
    {
        public const string PrefabName = "piece_dvergrcrate";
        public const string HoverName = "$" + PrefabName;
        private const string ChestPrefab = "piece_chest_wood";
        private const string CratePrefab = "dvergrprops_crate";

        // Hammer tab. Like the cask, the Usage tag decides it; without one the crate keeps whatever
        // the cloned chest carries.
        private static readonly string[] s_buildUsage = { PieceUsages.Storage };

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!RequirePrefab(ChestPrefab) || !RequirePrefab(CratePrefab)) return;
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
            // The wood chest's 10 Wood, plus resin for the pitch and pegs that hold the planks. The
            // crafting station (workbench) stays the cloned chest's.
            var config = new PieceConfig
            {
                Name = HoverName,
                Description = HoverName + "_description",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                Usage = s_buildUsage,
                Requirements = new[] { Req("Wood", 10), Req("Resin", 2) },
            };
            var piece = new CustomPiece(PrefabName, ChestPrefab, config);
            var prefab = piece.PiecePrefab;

            var container = prefab.GetComponent<Container>();
            if (!container)
            {
                Jotunn.Logger.LogWarning($"'{ChestPrefab}' has no Container; skipping {PrefabName}.");
                return;
            }
            container.m_name = HoverName;

            ReplaceModel(prefab);

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) prefab.GetComponent<Piece>().m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // Swaps the chest's meshes for the dvergr crate at its own size, standing on the piece
        // origin. The chest's lid objects stay as empty transforms, so Container can still toggle them.
        private static void ReplaceModel(GameObject prefab)
        {
            // Build and fit the model before stripping the chest, so a failure here leaves a working
            // (vanilla-looking) chest instead of an invisible one.
            var model = Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(CratePrefab), prefab.transform, false);
            model.name = "dvergr_crate";
            PieceUtils.StripToVisual(model);
            PieceUtils.KeepFirstLod(model);
            // The prop isn't necessarily stored at the origin; start from zero before measuring.
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var raw = PieceUtils.MeshBounds(model.transform, prefab.transform);
            if (raw.size.y <= 0f)
            {
                Jotunn.Logger.LogWarning($"'{CratePrefab}' has no mesh; keeping the vanilla chest model.");
                Object.DestroyImmediate(model);
                return;
            }
            model.transform.localPosition = new Vector3(-raw.center.x, -raw.min.y, -raw.center.z);
            var bounds = PieceUtils.MeshBounds(model.transform, prefab.transform);

            PieceUtils.StripModel(prefab, model.transform);
            // Crates are placed freely; snapping them to each other felt wrong in game.
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t && t != prefab.transform && t.CompareTag("snappoint")) Object.DestroyImmediate(t.gameObject);

            // Lights in Valheim cull by layer; the piece layer also makes the model hoverable and
            // lets the build hammer target it.
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prefab.layer;

            // Placement only snaps to convex colliders; a box around the crate is enough to place,
            // hover, hit and support it, and to stack crates on top of each other.
            var box = prefab.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping {PrefabName}.");
            return false;
        }
    }
}
