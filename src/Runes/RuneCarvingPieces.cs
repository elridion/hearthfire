using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Builds and registers the two rune carvings: clones of the vanilla sign (wood WearNTear, needs
    /// support) stripped down to a thin collider and a TextMeshPro text in the vanilla rune font,
    /// driven by <see cref="RuneCarving"/>. The horizontal carving runs its runes left to right for
    /// walls and beams, the vertical one top to bottom for posts. Placement only turns pieces around
    /// the vertical axis, so both stand upright; vanilla pushes the collider's back face onto the
    /// surface aimed at.
    /// </summary>
    internal static class RuneCarvingPieces
    {
        public const string PrefabName = "piece_runecarving";
        public const string VerticalPrefabName = "piece_runecarving_vertical";
        public const string DescriptionKey = "piece_runecarving_description";
        private const string BasePrefab = "sign";

        // Runes along a line sit in RuneCarving.RuneCell. A vertical carving stacks one rune per line,
        // so its runes move closer through the line spacing, in hundredths of an em.
        private const float ColumnLineSpacing = -30f;

        // TextMeshPro scales 3D text by fontSize / pointSize * faceInfo.scale * 0.1 per font unit.
        private const float WorldTextScale = 0.1f;

        // Thickens the rune strokes through the distance field (-1 to 1): the font's thin staves
        // were hard to read on wood by day, while 0.3 and even 0.15 made them too heavy.
        private const float StrokeDilate = 0.08f;

        private static Material s_runeMaterial;

        // The build menu shows icons on a dark background, so the icon draws pale runes.
        private const string IconText = "Rune";
        private static readonly Color32 s_iconColour = new Color32(232, 216, 186, 255);

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            if (!PrefabManager.Cache.GetPrefab<GameObject>(BasePrefab))
            {
                Jotunn.Logger.LogWarning($"Vanilla prefab '{BasePrefab}' not found; skipping the rune carvings.");
                return;
            }
            if (!RuneFont.Load())
            {
                Jotunn.Logger.LogWarning("Vanilla rune font not found; skipping the rune carvings.");
                return;
            }

            foreach (bool vertical in new[] { false, true })
            {
                string name = vertical ? VerticalPrefabName : PrefabName;
                try
                {
                    Build(name, vertical);
                }
                catch (System.Exception e)
                {
                    Jotunn.Logger.LogError($"Failed to build {name}: {e}");
                }
            }
        }

        private static void Build(string name, bool vertical)
        {
            var config = new PieceConfig
            {
                Name = "$" + name,
                Description = "$" + DescriptionKey,
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Furniture,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[] { new RequirementConfig("Coal", 1, 0, true) },
            };
            var piece = new CustomPiece(name, BasePrefab, config);
            var prefab = piece.PiecePrefab;

            StripSign(prefab);
            PieceUtils.StripModel(prefab, null);
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t && t != prefab.transform && t.CompareTag("snappoint")) Object.DestroyImmediate(t.gameObject);

            var text = MakeText(prefab, vertical);

            // Placement only snaps to convex colliders. The slab's back face (z = 0) is what lands on
            // the surface, and the runes float TextLift in front of it. RuneCarving refits it to the text.
            var box = prefab.AddComponent<BoxCollider>();
            float length = RuneCarving.DefaultText.Length * RuneCarving.LineHeight * 0.6f;
            box.size = vertical
                ? new Vector3(RuneCarving.LineHeight, RuneCarving.DefaultText.Length * RuneCarving.LineHeight, RuneCarving.Thickness)
                : new Vector3(length, RuneCarving.LineHeight, RuneCarving.Thickness);
            box.center = RuneCarving.SlabCenter(box.size);

            var carving = prefab.AddComponent<RuneCarving>();
            carving.m_vertical = vertical;
            carving.m_text = text;
            carving.m_light = MakeLight(prefab, RuneCarving.LightPosition(box.center));

            var icon = MakeIcon(vertical);
            if (icon) prefab.GetComponent<Piece>().m_icon = icon;

            PieceManager.Instance.AddPiece(piece);
        }

        // The text faces -Z, the side the player faces while placing it, like vanilla sign text.
        private static TextMeshPro MakeText(GameObject prefab, bool vertical)
        {
            var go = new GameObject("runes");
            go.transform.SetParent(prefab.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -RuneCarving.TextLift);
            // Lights in Valheim cull by layer; the piece layer also makes it hoverable and lets the
            // build hammer target it.
            go.layer = prefab.layer;

            var font = RuneFont.Font;
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSharedMaterial = RuneMaterial(font);
            text.fontSize = RuneCarving.LineHeight * font.faceInfo.pointSize / (font.faceInfo.lineHeight * font.faceInfo.scale * WorldTextScale);
            // White vertex colour: RuneGlow sets the colour and halo on the shared material.
            text.color = new Color32(255, 255, 255, RuneCarving.RuneAlpha);
            // With a zero-size rect, top-left alignment starts the first rune at the origin.
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = vertical ? ColumnLineSpacing : 0f;
            // Rich text carries only the rune cell tag (RuneCarving.Layout); ToRunes never puts out
            // '<', so typed text can't open a tag of its own.
            text.richText = true;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.text = RuneCarving.Layout(RuneFont.ToRunes(RuneCarving.DefaultText));

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return text;
        }

        // The faint glow light, off until RuneGlow gives it an intensity in the dark. No shadows:
        // it only tints the surface right around the runes.
        private static Light MakeLight(GameObject prefab, Vector3 position)
        {
            var go = new GameObject("glow light");
            go.transform.SetParent(prefab.transform, false);
            go.transform.localPosition = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RuneGlow.LightColour;
            light.range = RuneGlow.LightRange;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        // A copy of the font's material, shared by both carvings, black and haloed by RuneGlow. The
        // font's own material is the runestones' too, so it is never changed. A shader without glow
        // (a mobile variant) gets no halo, only the light.
        private static Material RuneMaterial(TMP_FontAsset font)
        {
            if (s_runeMaterial) return s_runeMaterial;

            var source = font.material;
            var material = new Material(source) { name = source.name + "_hearthfire" };
            bool halo = material.HasProperty(ShaderUtilities.ID_GlowColor);
            if (halo)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Glow);
                if (material.HasProperty(ShaderUtilities.ID_GlowOffset)) material.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
                if (material.HasProperty(ShaderUtilities.ID_GlowOuter)) material.SetFloat(ShaderUtilities.ID_GlowOuter, RuneGlow.GlowOuter);
                if (material.HasProperty(ShaderUtilities.ID_GlowInner)) material.SetFloat(ShaderUtilities.ID_GlowInner, RuneGlow.GlowInner);
                if (material.HasProperty(ShaderUtilities.ID_GlowPower)) material.SetFloat(ShaderUtilities.ID_GlowPower, RuneGlow.GlowPower);
            }
            else
            {
                Jotunn.Logger.LogWarning($"Shader '{source.shader.name}' has no glow; the rune carvings glow without a halo, only with their light.");
            }
            if (material.HasProperty(ShaderUtilities.ID_FaceDilate)) material.SetFloat(ShaderUtilities.ID_FaceDilate, StrokeDilate);
            // The scale ratios set how much room TMP leaves around each glyph for dilation and glow;
            // without updating them the thicker strokes and halo would be cut at the glyph quads.
            ShaderUtilities.UpdateShaderRatios(material);
            RuneGlow.Use(material, halo);
            s_runeMaterial = material;
            return s_runeMaterial;
        }

        // Removes the sign's own text box and board, which the carving replaces.
        private static void StripSign(GameObject prefab)
        {
            var sign = prefab.GetComponent<Sign>();
            if (sign) Object.DestroyImmediate(sign);
            foreach (var canvas in prefab.GetComponentsInChildren<Canvas>(true))
                if (canvas) Object.DestroyImmediate(canvas.gameObject);

            // WearNTear swaps the new, worn and broken models and throws when only some of them are
            // set; the carving has a single look, so all of them are cleared.
            var wear = prefab.GetComponent<WearNTear>();
            if (wear)
            {
                wear.m_new = null;
                wear.m_worn = null;
                wear.m_broken = null;
                wear.m_wet = null;
            }
        }

        // A few pale runes centred in a square texture, laid out like the carving itself. They are
        // drawn by RuneScript: the font's atlas is a distance field, which can't be shown as a sprite.
        private static Sprite MakeIcon(bool vertical)
        {
            if (!RuneScript.CanDraw) return null;
            var source = RuneScript.Draw(new List<string> { IconText }, vertical, s_iconColour, out int width, out int height);
            int side = Mathf.Max(width, height);
            var pixels = new Color32[side * side];
            int dx = (side - width) / 2, dy = (side - height) / 2;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(y + dy) * side + x + dx] = source[y * width + x];

            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "hearthfire_runes_icon" };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f));
        }
    }
}
