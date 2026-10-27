using TMPro;
using UnityEngine;

namespace Hearthfire
{
    /// <summary>
    /// Runes carved onto whatever surface the piece sits on. Players write it like a vanilla sign;
    /// the text is saved in the ZDO and shown in the vanilla rune font of the runestones, black with
    /// an ember halo and a faint light in the dark (<see cref="RuneGlow"/>). The collider is resized with the text and
    /// keeps its back face on the surface, where placement put it.
    /// </summary>
    public class RuneCarving : MonoBehaviour, Hoverable, Interactable, TextReceiver
    {
        public const int MaxTextLength = 50;
        // Shown until someone writes on the carving, so a fresh one is visible and can be found.
        public const string DefaultText = "Hearthfire";
        // Thickness of the collider slab, whose back face lies on the surface.
        public const float Thickness = 0.01f;
        // How far the runes float in front of the surface: the middle of the slab. 2.5 cm floated
        // visibly off a wooden pillar.
        public const float TextLift = 0.005f;
        // Line height of the runes in metres; the font size and the slab follow it. 0.22 was too small
        // to read from a few steps away, 0.33 a little too large.
        public const float LineHeight = 0.3f;
        // The material sets the colour (RuneGlow); the alpha lets the surface grain show through.
        public const byte RuneAlpha = 225;
        // Every rune sits centred in a cell this many ems wide, and a space takes one cell. The font's
        // own advances barely follow the runes (a thin stave gets 0.32 em, a narrow wynn 0.99 em), so
        // spacing by them left uneven gaps and squeezing them made thin runes collide.
        public const float RuneCell = 0.55f;
        private static readonly string s_cellTag = "<mspace=" + RuneCell.ToString(System.Globalization.CultureInfo.InvariantCulture) + "em>";

        private const float RefreshInterval = 2f;
        // How much nearer the camera the runes are drawn than they are. Something on some pieces
        // (the 2 m wood beam's right half) writes depth a little in front of the visible face, and
        // hid runes 5 mm off it; the readable mesh is flat there, so no lift measured from it helps.
        // The text is moved this far toward the camera and shrunk by the same ratio, so it looks
        // exactly the same from the camera but wins the depth test. Kept small: only geometry this
        // close in front of the runes is drawn over.
        private const float DepthNudge = 0.03f;
        // The glow light sits this far in front of the middle of the runes, so it lights the
        // surface around them, and is off beyond LightDistance, where its tint can't be seen.
        public const float LightLift = 0.15f;
        private const float LightDistance = 40f;
        // Below this intensity the light is switched off rather than drawn invisibly.
        private const float MinLightIntensity = 0.01f;

        private static readonly int s_textKey = "hearthfire_runes".GetStableHashCode();
        private static readonly bool s_headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

        // Set when the prefab is built. The runes never wrap: a horizontal carving is one line, a
        // vertical one a single column read top to bottom.
        public bool m_vertical;
        public TextMeshPro m_text;
        public Light m_light;

        private ZNetView m_nview;
        private Piece m_piece;
        private BoxCollider m_collider;
        private string m_shownText;
        private uint m_lastRevision = uint.MaxValue;
        private Vector3 m_textLocalPosition;
        // The text's renderer and TMP's sub-mesh renderers for glyphs on further atlas pages. TMP adds
        // sub-meshes when it builds the text, after Show, so the list is rescanned now and then.
        private MeshRenderer[] m_renderers = new MeshRenderer[0];
        private float m_nextRendererScan;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_piece = GetComponent<Piece>();
            m_collider = GetComponent<BoxCollider>();
            if (m_text) m_textLocalPosition = m_text.transform.localPosition;
        }

        // TextMeshPro measures text only once its own Awake has run, so the first layout waits for Start.
        private void Start()
        {
            // The placement ghost shows the default runes, with a collider that fits them.
            if (!m_nview || !m_nview.IsValid())
            {
                Show(DefaultText);
                return;
            }
            InvokeRepeating(nameof(Refresh), 0f, RefreshInterval);
        }

        private void Update() => RuneGlow.Tick();

        // Scaling about the camera keeps every point of the text on its line of sight, so only its
        // depth changes. The collider stays where placement put it.
        private void LateUpdate()
        {
            if (s_headless || !m_text) return;
            if (Time.time >= m_nextRendererScan)
            {
                m_nextRendererScan = Time.time + RefreshInterval;
                m_renderers = m_text.GetComponentsInChildren<MeshRenderer>(true);
            }
            foreach (var renderer in m_renderers) RuneGlow.ApplyTo(renderer);

            var camera = Camera.main;
            if (!camera) return;

            Vector3 eye = camera.transform.position;
            if (m_light)
            {
                float intensity = RuneGlow.LightIntensity;
                bool lit = intensity >= MinLightIntensity
                    && (m_light.transform.position - eye).sqrMagnitude < LightDistance * LightDistance;
                if (lit) m_light.intensity = intensity;
                if (m_light.enabled != lit) m_light.enabled = lit;
            }

            Vector3 position = transform.TransformPoint(m_textLocalPosition);
            float distance = Vector3.Distance(position, eye);
            float ratio = distance > DepthNudge * 2f ? (distance - DepthNudge) / distance : 1f;
            var text = m_text.transform;
            text.position = eye + (position - eye) * ratio;
            text.localScale = new Vector3(ratio, ratio, ratio);
        }

        private void Refresh()
        {
            if (!m_nview || !m_nview.IsValid()) return;
            var zdo = m_nview.GetZDO();
            if (zdo.DataRevision == m_lastRevision) return;
            m_lastRevision = zdo.DataRevision;

            string text = GetText();
            Show(string.IsNullOrEmpty(text) || text.Trim().Length == 0 ? DefaultText : text);
        }

        private void Show(string text)
        {
            if (text == m_shownText || !m_text) return;
            m_shownText = text;

            // Spaces stay in, so words keep their gaps; only the ends are trimmed, so the first rune
            // starts at the piece origin.
            string runes = RuneFont.ToRunes(text).Trim();
            string shown = Layout(m_vertical ? string.Join("\n", Characters(runes)) : runes);
            m_text.text = shown;

            if (m_collider)
            {
                var size = m_text.GetPreferredValues(shown);
                size = new Vector2(Mathf.Max(size.x, LineHeight), Mathf.Max(size.y, LineHeight));
                m_collider.size = new Vector3(size.x, size.y, Thickness);
                m_collider.center = SlabCenter(size);
                if (m_light) m_light.transform.localPosition = LightPosition(m_collider.center);
            }
        }

        // In front of the middle of the slab, whose centre is given.
        public static Vector3 LightPosition(Vector3 slabCenter) => new Vector3(slabCenter.x, slabCenter.y, -LightLift);

        // The runes start at the piece origin, the point aimed at while placing, and run right and
        // down from it, so a carving lines up with an edge or another carving by where it starts.
        // The text faces -Z, so its right is +X.
        public static Vector3 SlabCenter(Vector2 size) => new Vector3(size.x * 0.5f, -size.y * 0.5f, -Thickness * 0.5f);

        // The cell tag is the only markup: ToRunes never puts out '<', so typed text can't open a tag.
        public static string Layout(string runes) => s_cellTag + runes;

        private static string[] Characters(string line)
        {
            var characters = new string[line.Length];
            for (int i = 0; i < line.Length; i++) characters[i] = line[i].ToString();
            return characters;
        }

        public string GetText() => m_nview && m_nview.IsValid() ? m_nview.GetZDO().GetString(s_textKey) : "";

        public void SetText(string text)
        {
            if (!m_nview || !m_nview.IsValid()) return;
            if (!PrivateArea.CheckAccess(transform.position)) return;
            m_nview.ClaimOwnership();
            m_nview.GetZDO().Set(s_textKey, text ?? "");
            Refresh();
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (!PrivateArea.CheckAccess(transform.position)) return true;
            TextInput.instance.RequestText(this, "$piece_sign_input", MaxTextLength);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        // Like a vanilla sign, the hover shows what the runes say.
        public string GetHoverText()
        {
            string name = GetHoverName();
            if (!PrivateArea.CheckAccess(transform.position, 0f, false))
                return Localization.instance.Localize(name + "\n$piece_noaccess");
            string text = GetText();
            string said = string.IsNullOrEmpty(text) ? "" : "\"" + text.RemoveRichTextTags() + "\"\n";
            return said + Localization.instance.Localize(name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName() => m_piece ? m_piece.m_name : "";

        public float GetHoverOffset() => 0f;
    }
}
