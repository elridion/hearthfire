using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Gives every placed cask the vanilla fermenter's conversions with the cask's larger yield. The
    /// cask is cloned before other mods add their meads to the fermenter, so its own copy of the list
    /// would miss them; building it from the live list at spawn time picks up every conversion the
    /// fermenter knows. The fermenter's list itself is never changed.
    /// </summary>
    [HarmonyPatch(typeof(Fermenter), "Awake")]
    internal static class FermenterPatches
    {
        // Rebuilt when the fermenter's list changes, and shared by every cask.
        private static List<Fermenter.ItemConversion> s_source;
        private static int s_sourceCount = -1;
        private static List<Fermenter.ItemConversion> s_caskConversions;

        private static void Postfix(Fermenter __instance)
        {
            if (__instance.m_name != CaskPiece.HoverName || !ZNetScene.instance) return;

            var source = ZNetScene.instance.GetPrefab(CaskPiece.FermenterPrefab);
            var fermenter = source ? source.GetComponent<Fermenter>() : null;
            if (!fermenter || fermenter.m_conversion == null) return;
            __instance.m_conversion = CaskConversions(fermenter.m_conversion);
        }

        private static List<Fermenter.ItemConversion> CaskConversions(List<Fermenter.ItemConversion> fermenter)
        {
            if (fermenter == s_source && fermenter.Count == s_sourceCount) return s_caskConversions;

            s_source = fermenter;
            s_sourceCount = fermenter.Count;
            s_caskConversions = new List<Fermenter.ItemConversion>(fermenter.Count);
            foreach (var conversion in fermenter)
            {
                if (conversion == null) continue;
                s_caskConversions.Add(new Fermenter.ItemConversion
                {
                    m_from = conversion.m_from,
                    m_to = conversion.m_to,
                    m_producedItems = CaskPiece.CaskYield(conversion.m_producedItems),
                });
            }
            return s_caskConversions;
        }
    }
}
