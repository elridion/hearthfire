using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Colours the rune carvings by the time of day: burnt-in black in daylight, a faint glow at
    /// dusk and dawn and a slowly flickering ember glow at night and in always-dark places. The rune text is unlit, so it can't darken or
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

        // Burnt-in near black by day. The ember colour goes above 1 so the game's bloom haloes it.
        private static readonly Color s_dayColour = new Color(0.09f, 0.06f, 0.04f, 1f);
        private static readonly Color s_emberColour = new Color(1.6f, 0.55f, 0.12f, 1f);

        // Soft halo from the font shader's glow, faded in with the dark. Glow distances are in
        // distance-field units and must stay inside the atlas padding.
        private static readonly Color s_haloColour = new Color(1f, 0.45f, 0.1f, 0.45f);
        public const float GlowOuter = 0.25f;
        public const float GlowInner = 0.05f;
        public const float GlowPower = 0.75f;

        // Slow ember pulse: brightness wanders between 1 - FlickerDepth and 1.
        private const float FlickerDepth = 0.2f;
        private const float FlickerSpeed = 0.6f;

        private static Material s_material;
        private static bool s_hasHalo;
        private static int s_lastFrame = -1;
        private static Color s_face = s_dayColour;
        private static Color s_halo;
        private static readonly MaterialPropertyBlock s_block = new MaterialPropertyBlock();

        // The carvings' own copy of the font material; the font's is the runestones' too.
        public static void Use(Material material, bool hasHalo)
        {
            s_material = material;
            s_hasHalo = hasHalo;
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
        }

        // Puts this frame's colour on one carving's renderer. Other values in its property block
        // (vanilla's hover highlight) are kept.
        public static void ApplyTo(Renderer renderer)
        {
            if (!renderer) return;
            renderer.GetPropertyBlock(s_block);
            s_block.SetColor(ShaderUtilities.ID_FaceColor, s_face);
            if (s_hasHalo) s_block.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
            renderer.SetPropertyBlock(s_block);
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
