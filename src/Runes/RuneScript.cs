using System.Collections.Generic;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// The Elder Futhark strokes the carvings' build-menu icons are drawn with. The carvings themselves use the vanilla rune font (<see cref="RuneFont"/>), whose
    /// distance-field atlas can't be shown as an icon sprite.
    /// </summary>
    internal static class RuneScript
    {
        // Rune height in icon pixels.
        private const int RunePixels = 48;

        // Layout in pixels: the advance from one rune to the next along a line, between lines, and
        // the empty border that keeps the antialiased stroke ends inside the texture.
        private const int RuneAdvance = 34;
        private const int LineAdvance = 64;
        private const int Border = 6;
        private const float StrokeRadius = 3f;
        private const float GlyphWidth = 0.6f;

        // Elder Futhark in futhark order, each rune as polylines (x, y pairs) in a cell 1 high and
        // GlyphWidth wide.
        private static readonly float[][][] s_runes =
        {
            new[] { Stave(0f), Line(0f, 0.55f, 0.45f, 0.85f), Line(0f, 0.8f, 0.35f, 1f) },                       // fehu
            new[] { new[] { 0f, 0f, 0f, 1f, 0.45f, 0.7f, 0.45f, 0f } },                                           // uruz
            new[] { Stave(0f), new[] { 0f, 0.72f, 0.4f, 0.5f, 0f, 0.28f } },                                      // thurisaz
            new[] { Stave(0f), Line(0f, 0.9f, 0.4f, 0.68f), Line(0f, 0.65f, 0.4f, 0.43f) },                      // ansuz
            new[] { Stave(0f), new[] { 0f, 1f, 0.4f, 0.8f, 0f, 0.55f, 0.4f, 0f } },                               // raidho
            new[] { new[] { 0.4f, 0.85f, 0f, 0.55f, 0.4f, 0.25f } },                                              // kaunan
            new[] { Line(0f, 0f, 0.6f, 1f), Line(0f, 1f, 0.6f, 0f) },                                             // gebo
            new[] { Stave(0f), new[] { 0f, 1f, 0.4f, 0.8f, 0f, 0.6f } },                                          // wunjo
            new[] { Stave(0f), Stave(0.5f), Line(0f, 0.7f, 0.5f, 0.3f) },                                         // hagalaz
            new[] { Stave(0.25f), Line(0f, 0.65f, 0.5f, 0.35f) },                                                 // naudiz
            new[] { Stave(0.3f) },                                                                                 // isaz
            new[] { new[] { 0.2f, 0.9f, 0f, 0.65f, 0.2f, 0.4f }, new[] { 0.3f, 0.6f, 0.5f, 0.35f, 0.3f, 0.1f } },   // jera
            new[] { Stave(0.25f), Line(0.25f, 1f, 0.5f, 0.8f), Line(0.25f, 0f, 0f, 0.2f) },                      // eihwaz
            new[] { Stave(0f), new[] { 0f, 1f, 0.3f, 0.8f, 0.5f, 1f }, new[] { 0f, 0f, 0.3f, 0.2f, 0.5f, 0f } },  // perthro
            new[] { Stave(0.3f), Line(0.3f, 0.6f, 0f, 0.95f), Line(0.3f, 0.6f, 0.6f, 0.95f) },                   // algiz
            new[] { new[] { 0.45f, 1f, 0f, 0.65f, 0.45f, 0.35f, 0f, 0f } },                                       // sowilo
            new[] { Stave(0.3f), new[] { 0f, 0.7f, 0.3f, 1f, 0.6f, 0.7f } },                                      // tiwaz
            new[] { Stave(0f), new[] { 0f, 1f, 0.4f, 0.75f, 0f, 0.5f, 0.4f, 0.25f, 0f, 0f } },                   // berkanan
            new[] { Stave(0f), Stave(0.5f), new[] { 0f, 1f, 0.25f, 0.75f, 0.5f, 1f } },                           // ehwaz
            new[] { Stave(0f), Stave(0.5f), Line(0f, 1f, 0.5f, 0.6f), Line(0.5f, 1f, 0f, 0.6f) },                 // mannaz
            new[] { Stave(0f), Line(0f, 1f, 0.4f, 0.75f) },                                                       // laguz
            new[] { new[] { 0.3f, 0.75f, 0.55f, 0.5f, 0.3f, 0.25f, 0.05f, 0.5f, 0.3f, 0.75f } },                  // ingwaz
            new[] { Stave(0f), Stave(0.6f), Line(0f, 1f, 0.6f, 0f), Line(0f, 0f, 0.6f, 1f) },                     // dagaz
            new[] { new[] { 0f, 0f, 0.55f, 0.7f, 0.3f, 1f, 0.05f, 0.7f, 0.6f, 0f } },                             // othala
        };

        private static float[] Stave(float x) => new[] { x, 0f, x, 1f };
        private static float[] Line(float x0, float y0, float x1, float y1) => new[] { x0, y0, x1, y1 };

        // Dedicated servers have no graphics device to build textures with.
        public static bool CanDraw => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private static void PixelSize(List<string> lines, bool vertical, out int width, out int height)
        {
            int longest = 1;
            foreach (string line in lines) longest = Mathf.Max(longest, line.Length);
            int count = Mathf.Max(1, lines.Count);
            width = 2 * Border + RunePixels + (vertical ? (count - 1) * LineAdvance : (longest - 1) * RuneAdvance);
            height = 2 * Border + RunePixels + (vertical ? (longest - 1) * LineAdvance : (count - 1) * LineAdvance);
        }

        // Rune index for a character, or -1 for whitespace and control characters, which leave a gap.
        private static int RuneFor(char c) => char.IsWhiteSpace(c) || char.IsControl(c) ? -1 : c % s_runes.Length;

        // Draws the lines as runes in colour with antialiased alpha, rows from the bottom. Horizontal
        // layouts run each line left to right and stack the lines downwards; vertical ones run each
        // line top to bottom and set the lines side by side, left to right. Each character picks a
        // rune by code point modulo 24; drawing every stroke into one alpha keeps crossings even.
        public static Color32[] Draw(List<string> lines, bool vertical, Color32 colour, out int width, out int height)
        {
            PixelSize(lines, vertical, out width, out height);
            var pixels = new Color32[width * height];
            var clear = new Color32(colour.r, colour.g, colour.b, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int l = 0; l < lines.Count; l++)
            {
                for (int k = 0; k < lines[l].Length; k++)
                {
                    int rune = RuneFor(lines[l][k]);
                    if (rune < 0) continue;
                    // Bottom-left corner of the rune's cell; texture rows count up from the bottom.
                    int x = Border + (vertical ? l * LineAdvance : k * RuneAdvance);
                    int y = height - Border - RunePixels - (vertical ? k * LineAdvance : l * LineAdvance);
                    DrawRune(pixels, width, height, s_runes[rune], x, y, colour);
                }
            }
            return pixels;
        }

        private static void DrawRune(Color32[] pixels, int width, int height, float[][] strokes, int cellX, int cellY, Color32 colour)
        {
            float left = cellX + (1f - GlyphWidth) * RunePixels * 0.5f;
            int pad = Mathf.CeilToInt(StrokeRadius) + 1;
            int x0 = Mathf.Max(0, cellX - pad), x1 = Mathf.Min(width - 1, cellX + RunePixels + pad);
            int y0 = Mathf.Max(0, cellY - pad), y1 = Mathf.Min(height - 1, cellY + RunePixels + pad);

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    var p = new Vector2(px + 0.5f, py + 0.5f);
                    float distance = float.MaxValue;
                    foreach (var stroke in strokes)
                    {
                        for (int i = 0; i + 3 < stroke.Length; i += 2)
                        {
                            var a = new Vector2(left + stroke[i] * RunePixels, cellY + stroke[i + 1] * RunePixels);
                            var b = new Vector2(left + stroke[i + 2] * RunePixels, cellY + stroke[i + 3] * RunePixels);
                            distance = Mathf.Min(distance, SegmentDistance(p, a, b));
                        }
                    }
                    // One pixel of falloff at the stroke edge antialiases it.
                    byte alpha = (byte)(Mathf.Clamp01(StrokeRadius + 0.5f - distance) * colour.a);
                    int index = py * width + px;
                    if (alpha > pixels[index].a) pixels[index] = new Color32(colour.r, colour.g, colour.b, alpha);
                }
            }
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
