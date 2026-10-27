using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// The vanilla rune font that runestones write their English text in: it draws Latin letters as
    /// Elder Futhark runes. Letters it draws pass through; c and e, which it leaves blank, take the
    /// rune of the closest sound; every other character except a little punctuation becomes one of
    /// its letters by code point modulo the letter count, so any language carves into runes and
    /// nothing falls back to a plain font or the font's Latin digits.
    /// </summary>
    internal static class RuneFont
    {
        private const string FontName = "Valheim-Rune";
        // Punctuation the font draws and carvers used between words; everything else becomes a rune.
        // '<' must never pass, or typed text could open a rich text tag.
        private const string KeptPunctuation = ".,:;!?-'";

        // The font has no ehwaz and nothing for c, both drawn blank. Viking-age carvers wrote e with
        // the i rune and c with k, so those stand in.
        private static readonly Dictionary<char, char> s_soundAlike = new Dictionary<char, char> { { 'c', 'k' }, { 'e', 'i' } };

        public static TMP_FontAsset Font { get; private set; }
        private static char[] s_letters;

        public static bool Load()
        {
            if (Font) return true;
            var font = Find();
            if (!font) return false;

            Font = font;
            var letters = new List<char>();
            for (char c = 'a'; c <= 'z'; c++) if (Has(c)) letters.Add(c);
            // Fonts drawing only capitals would leave every lowercase letter mapped elsewhere.
            if (letters.Count == 0)
                for (char c = 'A'; c <= 'Z'; c++) if (Has(c)) letters.Add(c);
            // Runestones show English text in this font, so it draws its letters even when the check
            // can't tell (a dynamic font that can't add glyphs here, e.g. on a dedicated server).
            if (letters.Count == 0)
            {
                Jotunn.Logger.LogWarning($"Font '{font.name}' reports no Latin letters; assuming a-z.");
                for (char c = 'a'; c <= 'z'; c++) if (!s_soundAlike.ContainsKey(c)) letters.Add(c);
            }
            s_letters = letters.ToArray();
            return true;
        }

        public static string ToRunes(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                char lower = char.ToLowerInvariant(c);
                if (char.IsWhiteSpace(c)) result.Append(' ');
                else if (char.IsControl(c)) continue;
                else if (!char.IsLetter(c)) Append(result, KeptPunctuation.IndexOf(c) >= 0 && Has(c) ? c : Fallback(c));
                else if (Has(c)) result.Append(c);
                else if (Has(lower)) result.Append(lower);
                else if (s_soundAlike.TryGetValue(lower, out char alike) && Has(alike)) result.Append(alike);
                else Append(result, Fallback(c));
            }
            return result.ToString();
        }

        private static char Fallback(char c) => s_letters != null && s_letters.Length > 0 ? s_letters[c % s_letters.Length] : '\0';

        private static void Append(StringBuilder result, char c)
        {
            if (c != '\0') result.Append(c);
        }

        // The rune font may be dynamic: its atlas starts empty and gains each glyph the first time it
        // is asked for, so the check must let it add the character. Its own fallbacks count, but not
        // TMP's global ones, which would draw the character in a plain Noto font. A glyph with no ink
        // (the font maps c, e and accented letters to blanks) counts as missing.
        private static bool Has(char c)
        {
            if (!Font) return false;
            if (Draws(Font, c)) return true;
            var fallbacks = Font.fallbackFontAssetTable;
            if (fallbacks == null) return false;
            foreach (var fallback in fallbacks)
                if (fallback && Draws(fallback, c)) return true;
            return false;
        }

        private static bool Draws(TMP_FontAsset font, char c)
        {
            if (!font.HasCharacter(c, false, true)) return false;
            return font.characterLookupTable.TryGetValue(c, out var character)
                && character.glyph != null && character.glyph.metrics.width > 0f;
        }

        // The font sits in the game's resources, but may not be loaded yet in the main menu.
        private static TMP_FontAsset Find()
        {
            var font = FindLoaded();
            if (font) return font;
            Resources.LoadAll<TMP_FontAsset>("");
            return FindLoaded();
        }

        // Only the font's atlas name ("Valheim-Rune Atlas") could be checked outside the game, so any
        // font with "Rune" in its name is the fallback.
        private static TMP_FontAsset FindLoaded()
        {
            TMP_FontAsset similar = null;
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (!font) continue;
                if (font.name == FontName) return font;
                if (!similar && font.name.IndexOf("Rune", System.StringComparison.OrdinalIgnoreCase) >= 0) similar = font;
            }
            return similar;
        }
    }
}
