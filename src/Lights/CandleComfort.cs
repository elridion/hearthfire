using Jotunn.Managers;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Gives the vanilla resin candle the same comfort as the hanging surtling core: 1 in the lights
    /// group. Unlike the other pieces this edits the vanilla prefab itself, on purpose, so every
    /// candle in the world counts, including ones built before the mod. Comfort pieces register in
    /// Piece.Awake, so the prefab must be changed before any candle spawns.
    /// </summary>
    internal static class CandleComfort
    {
        private const string CandlePrefab = "Candle_resin";
        private const int Comfort = 1;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            var candle = PrefabManager.Cache.GetPrefab<GameObject>(CandlePrefab);
            var piece = candle ? candle.GetComponent<Piece>() : null;
            if (!piece)
            {
                Jotunn.Logger.LogWarning($"Vanilla piece '{CandlePrefab}' not found; candles give no comfort.");
                return;
            }
            // A candle that already gives comfort (a game update or another mod) is left alone.
            if (piece.m_comfort > 0) return;
            piece.m_comfort = Comfort;
            piece.m_comfortGroup = Piece.ComfortGroup.Lantern;
        }
    }
}
