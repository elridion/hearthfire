using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Colours the rune carvings by the time of day: from the MinGlow setting at noon in a straight
    /// line to MaxGlow at midnight and in always-dark places, a slowly flickering, patchy ember glow. The rune text is unlit, so it can't darken or
    /// light up by itself. The colour is worked out once per frame and set on the shared material, and
    /// every carving also writes it onto its own renderers as a property block: after tabbing out of
    /// the game, carvings placed earlier stayed black although the shared material kept changing.
    /// </summary>
    internal static class RuneGlow
    {
        // Warm grey at no glow, the ember colour at a glow of 1 (the MinGlow and MaxGlow settings).
        // Both are multiplied by the ember noise below, which averages about 0.75. Near black
        // (0.09, 0.06, 0.04) was too black. At night 1.6, 1.15 and then 1.0 red were too bright,
        // hence MaxGlow's default of 0.8.
        private static readonly Color s_greyColour = new Color(0.3f, 0.29f, 0.27f, 1f);
        private static readonly Color s_emberColour = new Color(1f, 0.35f, 0.075f, 1f);

        // Soft halo from the font shader's glow, its alpha scaled by the glow. Glow distances are in
        // distance-field units and must stay inside the atlas padding. At night 0.45, 0.3 and 0.2
        // alpha were too strong; this gives 0.15 at the default MaxGlow.
        private static readonly Color s_haloColour = new Color(1f, 0.45f, 0.1f, 0.19f);
        public const float GlowOuter = 0.25f;
        public const float GlowInner = 0.05f;
        public const float GlowPower = 0.75f;

        // Slow ember pulse: brightness wanders between 1 - FlickerDepth and 1.
        private const float FlickerDepth = 0.2f;
        private const float FlickerSpeed = 0.6f;

        // Ember noise: a tiling grey texture on the font shader's face texture slot, which it
        // multiplies the face colour by, so the glow is patchy instead of flat. NoiseScale is how
        // much of the texture one glyph covers; it drifts by NoiseDrift per second, and every
        // carving starts at its own offset so neighbours don't show the same pattern.
        private const int NoiseSize = 64;
        private const int NoiseCells = 4;
        private const int NoiseSeed = 1729;
        private const float NoiseFloor = 0.45f;
        private const float NoiseScale = 0.5f;
        private static readonly Vector2 s_noiseDrift = new Vector2(0.02f, 0.035f);
        private static readonly int s_faceTexST = Shader.PropertyToID("_FaceTex_ST");

        private static Material s_material;
        private static bool s_hasHalo;
        private static bool s_hasNoise;
        private static int s_lastFrame = -1;
        private static Color s_face = s_greyColour;
        private static Color s_halo;
        private static readonly MaterialPropertyBlock s_block = new MaterialPropertyBlock();

        // The carvings' own copy of the font material; the font's is the runestones' too.
        public static void Use(Material material, bool hasHalo)
        {
            s_material = material;
            s_hasHalo = hasHalo;
            // Dedicated servers have no graphics device to make the texture on.
            s_hasNoise = material.HasProperty(ShaderUtilities.ID_FaceTex)
                && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (s_hasNoise)
            {
                material.SetTexture(ShaderUtilities.ID_FaceTex, MakeNoise());
                material.SetTextureScale(ShaderUtilities.ID_FaceTex, new Vector2(NoiseScale, NoiseScale));
            }
            Apply(0f, 1f);
        }

        // Called by every carving each frame; only the first call in a frame does the work.
        public static void Tick()
        {
            if (!s_material || s_lastFrame == Time.frameCount) return;
            s_lastFrame = Time.frameCount;
            float flicker = 1f - FlickerDepth * Mathf.PerlinNoise(Time.time * FlickerSpeed, 0.37f);
            Apply(Darkness(), flicker);
        }

        // The glow runs from the MinGlow setting at noon to MaxGlow at midnight. Unclamped, so a
        // glow above 1 brightens the ember colour past itself for the bloom.
        private static void Apply(float dark, float flicker)
        {
            float glow = Mathf.Lerp(HearthfirePlugin.MinGlow.Value, HearthfirePlugin.MaxGlow.Value, dark) * flicker;
            s_face = Color.LerpUnclamped(s_greyColour, s_emberColour, glow);
            s_face.a = 1f;
            s_halo = s_haloColour;
            s_halo.a *= glow;
            s_material.SetColor(ShaderUtilities.ID_FaceColor, s_face);
            if (s_hasHalo) s_material.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
        }

        // Puts this frame's colour, and the noise drifted from the carving's own offset, on one
        // carving's renderer. Other values in its property block (vanilla's hover highlight) are kept.
        public static void ApplyTo(Renderer renderer, Vector2 noiseOffset)
        {
            if (!renderer) return;
            renderer.GetPropertyBlock(s_block);
            s_block.SetColor(ShaderUtilities.ID_FaceColor, s_face);
            if (s_hasHalo) s_block.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
            if (s_hasNoise)
            {
                Vector2 offset = noiseOffset + s_noiseDrift * Time.time;
                s_block.SetVector(s_faceTexST, new Vector4(NoiseScale, NoiseScale, offset.x % 1f, offset.y % 1f));
            }
            renderer.SetPropertyBlock(s_block);
        }

        // Tiling value noise in grey, NoiseFloor to white: a coarse lattice plus a finer one at half
        // weight, both wrapped at the edges so the texture repeats without seams.
        private static Texture2D MakeNoise()
        {
            var random = new System.Random(NoiseSeed);
            float[] coarse = Lattice(random, NoiseCells);
            float[] fine = Lattice(random, NoiseCells * 2);
            var pixels = new Color32[NoiseSize * NoiseSize];
            for (int y = 0; y < NoiseSize; y++)
            {
                for (int x = 0; x < NoiseSize; x++)
                {
                    float n = (2f * Sample(coarse, NoiseCells, x, y) + Sample(fine, NoiseCells * 2, x, y)) / 3f;
                    byte v = (byte)(255f * Mathf.Lerp(NoiseFloor, 1f, n));
                    pixels[y * NoiseSize + x] = new Color32(v, v, v, 255);
                }
            }

            var texture = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, true)
            {
                name = "hearthfire_rune_noise",
                wrapMode = TextureWrapMode.Repeat,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float[] Lattice(System.Random random, int cells)
        {
            var values = new float[cells * cells];
            for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();
            return values;
        }

        // The lattice value at pixel x, y, smoothly interpolated between its wrapped neighbours.
        private static float Sample(float[] lattice, int cells, int x, int y)
        {
            float fx = x * cells / (float)NoiseSize;
            float fy = y * cells / (float)NoiseSize;
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
            float ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            float bottom = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], tx);
            float top = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        // 0 at noon, the brightest, rising in a straight line to 1 at midnight, the darkest, by the
        // game's day fraction (0 midnight, 0.5 noon); 1 in environments the game keeps dark at any
        // hour (the Mistlands, dungeons). Reading the live sun and ambient light instead stopped
        // finding night after tabbing out of the game, and fades with a window around sunset felt
        // uneven. Local fires and torches don't count.
        private static float Darkness()
        {
            var env = EnvMan.instance;
            if (!env) return 0f;
            var setup = env.GetCurrentEnvironment();
            if (setup != null && setup.m_alwaysDark) return 1f;
            return Mathf.Abs(env.GetDayFraction() - 0.5f) / 0.5f;
        }
    }
}
