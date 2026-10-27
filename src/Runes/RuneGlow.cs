using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Colours the rune carvings by the time of day: burnt-in black in daylight, a faint glow at
    /// dusk and dawn and a slowly flickering, patchy ember glow, with a faint light on the surface around
    /// each carving, at night and in always-dark places. The rune text is unlit, so it can't darken or
    /// light up by itself. The colour is worked out once per frame and set on the shared material, and
    /// every carving also writes it onto its own renderers as a property block: after tabbing out of
    /// the game, carvings placed earlier stayed black although the shared material kept changing.
    /// </summary>
    internal static class RuneGlow
    {
        // Daylight follows the game's day fraction (0 midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset)
        // as vanilla's own day weight: 0 before sunrise and after sunset, 1 at noon. The runes are
        // fully black once it reaches FullDaylight, about 3.6 game hours after sunrise (mid-morning);
        // 0.35 (2 hours) made the glow fade too quickly at dawn and dusk. Reading the live sun and
        // ambient light instead stopped finding night after tabbing out of the game.
        private const float FullDaylight = 0.6f;
        // Share of the night glow reached by sunset (and kept from sunrise), so dusk and dawn glow
        // only faintly; the rest fades in over NightFade of the day (0.04 is about one game hour)
        // after sunset and out before sunrise.
        private const float TwilightShare = 0.3f;
        private const float NightFade = 0.04f;

        // Burnt-in near black by day. The ember colour is multiplied by the ember noise below, which
        // averages about 0.75; 1.6 and then 1.15 red were too bright at night.
        private static readonly Color s_dayColour = new Color(0.09f, 0.06f, 0.04f, 1f);
        private static readonly Color s_emberColour = new Color(1f, 0.35f, 0.08f, 1f);

        // Soft halo from the font shader's glow, faded in with the dark. Glow distances are in
        // distance-field units and must stay inside the atlas padding. 0.45 and 0.3 alpha were too
        // strong.
        private static readonly Color s_haloColour = new Color(1f, 0.45f, 0.1f, 0.2f);
        public const float GlowOuter = 0.25f;
        public const float GlowInner = 0.05f;
        public const float GlowPower = 0.75f;

        // Faint warm point light on each carving, faded in with the dark like the runes. No
        // shadows and a small range: it only tints the surface around the runes.
        public static readonly Color LightColour = new Color(1f, 0.5f, 0.2f, 1f);
        public const float LightRange = 2f;
        private const float NightLightIntensity = 0.8f;

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
        private static Color s_face = s_dayColour;
        private static Color s_halo;
        private static readonly MaterialPropertyBlock s_block = new MaterialPropertyBlock();

        // This frame's intensity for every carving's light; 0 by day.
        public static float LightIntensity { get; private set; }

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

        private static void Apply(float dark, float flicker)
        {
            s_face = Color.Lerp(s_dayColour, s_emberColour * flicker, dark);
            s_face.a = 1f;
            s_halo = s_haloColour;
            s_halo.a *= dark * flicker;
            s_material.SetColor(ShaderUtilities.ID_FaceColor, s_face);
            if (s_hasHalo) s_material.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
            LightIntensity = NightLightIntensity * dark * flicker;
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

        // 0 in daylight, TwilightShare at sunset and sunrise, 1 deeper into the night and in
        // environments the game keeps dark at any hour (the Mistlands, dungeons). Local fires and
        // torches don't count.
        private static float Darkness()
        {
            var env = EnvMan.instance;
            if (!env) return 0f;
            var setup = env.GetCurrentEnvironment();
            if (setup != null && setup.m_alwaysDark) return 1f;
            float fromNoon = Mathf.Abs(env.GetDayFraction() - 0.5f);
            float daylight = Mathf.Clamp01(1f - fromNoon / 0.25f);
            float twilight = 1f - Mathf.SmoothStep(0f, 1f, daylight / FullDaylight);
            float night = Mathf.SmoothStep(0f, 1f, (fromNoon - 0.25f) / NightFade);
            return TwilightShare * twilight + (1f - TwilightShare) * night;
        }
    }
}
