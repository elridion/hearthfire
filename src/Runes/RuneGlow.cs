using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Lights the rune carvings by the time of day. The runes themselves stay burnt-in black; only
    /// the halo around them and a faint point light on each carving fade in with the dark: faint at
    /// dusk and dawn, medium at night and in always-dark places, flickering slowly like embers. The
    /// halo is worked out once per frame and set on the shared material, and every carving also
    /// writes it onto its own renderers as a property block: after tabbing out of the game, carvings
    /// placed earlier stopped changing although the shared material kept changing.
    /// </summary>
    internal static class RuneGlow
    {
        // Daylight follows the game's day fraction (0 midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset)
        // as vanilla's own day weight: 0 before sunrise and after sunset, 1 at noon. Twilight glow
        // is gone once it reaches FullDaylight, about 3.6 game hours after sunrise (mid-morning);
        // 0.35 (2 hours) made the glow fade too quickly at dawn and dusk. Reading the live sun and
        // ambient light instead stopped finding night after tabbing out of the game.
        private const float FullDaylight = 0.6f;
        // Share of the night glow reached by sunset (and kept from sunrise); the rest fades in over
        // NightFade of the day (0.04 is about one game hour) after sunset and out before sunrise.
        private const float TwilightShare = 0.3f;
        private const float NightFade = 0.04f;

        // Burnt-in near black at any hour; the halo and the light do the glowing.
        private static readonly Color s_runeColour = new Color(0.09f, 0.06f, 0.04f, 1f);

        // Ember halo from the font shader's glow, which the shader adds on top of the runes. Its
        // colour goes above 1 so the game's bloom haloes it; its alpha follows the dark up to
        // NightHaloAlpha. Glow distances are in distance-field units and must stay inside the atlas
        // padding; GlowInner 0 keeps the strokes themselves black.
        private static readonly Color s_haloColour = new Color(1.6f, 0.55f, 0.12f, 1f);
        private const float NightHaloAlpha = 0.6f;
        public const float GlowOuter = 0.25f;
        public const float GlowInner = 0f;
        public const float GlowPower = 0.75f;

        // Faint warm point light on each carving, without shadows and with a small range, so it
        // only tints the surface around the runes.
        public static readonly Color LightColour = new Color(1f, 0.5f, 0.2f, 1f);
        public const float LightRange = 1.5f;
        private const float NightLightIntensity = 0.5f;

        // Slow ember pulse: brightness wanders between 1 - FlickerDepth and 1.
        private const float FlickerDepth = 0.2f;
        private const float FlickerSpeed = 0.6f;

        private static Material s_material;
        private static bool s_hasHalo;
        private static int s_lastFrame = -1;
        private static Color s_halo;
        private static readonly MaterialPropertyBlock s_block = new MaterialPropertyBlock();

        // This frame's intensity for every carving's light; 0 by day.
        public static float LightIntensity { get; private set; }

        // The carvings' own copy of the font material; the font's is the runestones' too.
        public static void Use(Material material, bool hasHalo)
        {
            s_material = material;
            s_hasHalo = hasHalo;
            s_material.SetColor(ShaderUtilities.ID_FaceColor, s_runeColour);
            Apply(0f, 1f);
        }

        // Called by every carving each frame; only the first call in a frame does the work.
        public static void Tick()
        {
            if (!s_material || s_lastFrame == Time.frameCount) return;
            s_lastFrame = Time.frameCount;
            float flicker = 1f - FlickerDepth * Mathf.PerlinNoise(Time.time * FlickerSpeed, 0.37f);
            Apply(Strength(), flicker);
        }

        private static void Apply(float strength, float flicker)
        {
            s_halo = s_haloColour;
            s_halo.a = NightHaloAlpha * strength * flicker;
            if (s_hasHalo) s_material.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
            LightIntensity = NightLightIntensity * strength * flicker;
        }

        // Puts this frame's halo on one carving's renderer. Other values in its property block
        // (vanilla's hover highlight) are kept.
        public static void ApplyTo(Renderer renderer)
        {
            if (!s_hasHalo || !renderer) return;
            renderer.GetPropertyBlock(s_block);
            s_block.SetColor(ShaderUtilities.ID_GlowColor, s_halo);
            renderer.SetPropertyBlock(s_block);
        }

        // 0 in daylight, TwilightShare at sunset and sunrise, 1 deeper into the night and in
        // environments the game keeps dark at any hour (the Mistlands, dungeons). Local fires and
        // torches don't count.
        private static float Strength()
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
