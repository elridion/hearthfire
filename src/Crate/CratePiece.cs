using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the dvergr crate: a clone of the vanilla wood chest whose model is
    /// swapped for the dvergr crate prop, for storage that looks at home outside. The clone keeps
    /// the chest's Container (resized to a square grid), wood WearNTear and open/close effects. Only the
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

        // Slots: a square grid for a square crate, one slot fewer than the wood chest's 5 x 2.
        private const int InventoryWidth = 3;
        private const int InventoryHeight = 3;

        // How far the open lid swings up and back about its back edge, in degrees; past upright,
        // so it leans back instead of standing on its edge.
        private const float LidOpenAngle = 110f;

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
            container.m_width = InventoryWidth;
            container.m_height = InventoryHeight;

            ReplaceModel(prefab, container);

            var icon = PieceUtils.RenderIcon(prefab);
            if (icon) prefab.GetComponent<Piece>().m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // Swaps the chest's meshes for the dvergr crate at its own size, standing on the piece
        // origin, and gives Container the crate's lid to open (see AddOpenLid).
        private static void ReplaceModel(GameObject prefab, Container container)
        {
            // Build and fit the model before stripping the chest, so a failure here leaves a working
            // (vanilla-looking) chest instead of an invisible one.
            var model = Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(CratePrefab), prefab.transform, false);
            model.name = "dvergr_crate";
            PieceUtils.StripToVisual(model);
            PieceUtils.KeepFirstLod(model);
            // The prop's inactive worn, broken and destruction models belonged to its Destructible;
            // nothing shows them on the piece, and they would widen the measured bounds.
            foreach (Transform child in model.transform)
                if (!child.gameObject.activeSelf) Object.DestroyImmediate(child.gameObject);
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

            AddOpenLid(prefab, model, container);
        }

        // Vanilla chests swap two lid objects while open: Container shows m_open and hides
        // m_closed. The crate's own lid (its highest mesh) becomes the closed one; a copy hinged on
        // the lid's back top edge and swung up and back by LidOpenAngle becomes the open one, like
        // the wood chest's lid, which also opens to the back (-Z). Built after the collider, so the
        // open lid does not widen it.
        private static void AddOpenLid(GameObject prefab, GameObject model, Container container)
        {
            Renderer lid = null;
            Bounds lidBounds = default;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = PieceUtils.MeshBounds(renderer.transform, prefab.transform);
                if (lid && b.min.y <= lidBounds.min.y) continue;
                lid = renderer;
                lidBounds = b;
            }
            if (!lid || lid.transform == model.transform)
            {
                Jotunn.Logger.LogWarning($"'{CratePrefab}' has no separate lid; {PrefabName} opens without moving it.");
                return;
            }

            var hinge = new GameObject("lid_open");
            hinge.transform.SetParent(prefab.transform, false);
            hinge.transform.localPosition = new Vector3(lidBounds.center.x, lidBounds.max.y, lidBounds.min.z);
            // The prefab sits at the world origin while it is built, so world and prefab space agree.
            var open = Object.Instantiate(lid.gameObject, lid.transform.position, lid.transform.rotation);
            open.transform.localScale = lid.transform.lossyScale;
            open.transform.SetParent(hinge.transform, true);
            hinge.transform.localRotation = Quaternion.Euler(-LidOpenAngle, 0f, 0f);
            foreach (var t in hinge.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = prefab.layer;
            hinge.SetActive(false);

            container.m_open = hinge;
            container.m_closed = lid.gameObject;
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping {PrefabName}.");
            return false;
        }
    }
}
