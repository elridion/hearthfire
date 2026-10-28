using System.Collections.Generic;
using HarmonyLib;

namespace Hearthfire
{
    /// <summary>
    /// Makes inventory grids build their slots for a 4 x 4 inventory such as the crate's. A grid
    /// starts out remembering a 4 x 4 size with no slots and only builds slots when the size
    /// changes, so a 4 x 4 container opened first after logging in showed an empty panel. Forgetting
    /// the size whenever the slot count is wrong makes vanilla build them.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class InventoryGridPatches
    {
        // Runs every frame for every open grid: one count comparison when the slots are right.
        private static void Prefix(Inventory ___m_inventory, List<InventoryElement> ___m_elements, ref int ___m_width)
        {
            if (___m_inventory == null || ___m_elements.Count == ___m_inventory.GetWidth() * ___m_inventory.GetHeight()) return;
            ___m_width = -1;
        }
    }
}
