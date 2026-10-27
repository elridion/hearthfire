---
paths:
  - "src/**/*.cs"
---

# Hearthfire code specifics

Adds to the workspace-wide `code-style.md` and `valheim-modding.md` in `../.claude/rules/`.

## Structure

- One class per piece family (`CaskPiece`, `IdolPieces`, `RuneCarvingPieces`), each with a static
  `Register` hooked to `PrefabManager.OnVanillaPrefabsAvailable` in `HearthfirePlugin.Awake`. Each
  family has its own folder under `src/` (`Cask/`, `Idols/`, `Runes/`) with its components and
  patches; a new family gets one too. Helpers more than one family needs go in `src/Common/`
  (`PieceUtils`). Folders don't add namespaces: everything stays in `Hearthfire`.
- Localization keys: `piece_dvergrcask*`, `piece_celticidol01`..`12` (the idols share
  `piece_celticidol_description`), `piece_runecarving`, `piece_runecarving_vertical` and
  `piece_runecarving_description`. The text box reuses vanilla `$piece_sign_input`.

## How the pieces work

- `CaskPiece.Build` clones `fermenter` through `new CustomPiece(name, "fermenter", config)`, so the
  clone keeps vanilla's `Fermenter`, `WearNTear` and effects. It is the fermenter's later step up:
  the fermenter's recipe with black metal for bronze at the workbench, `FermentationDuration` 1800 s (vanilla 2400) set on the clone's
  `Fermenter`, and `CaskYield` (6 meads become 8, other counts by the same ratio). The hammer tab
  comes from Jotunn's `PieceConfig.Usage` tags, not `Category`: the cask uses `PieceUsages.Crafting`
  (with `Meads` it showed in the mead tab, and setting only `Category` didn't move it).
- `CaskPiece.ReplaceModel` instantiates `dvergrprops_barrel`, strips it to renderers, stands it on the
  piece origin at its own scale, strips every other mesh, LOD group and solid collider in the clone
  (`PieceUtils.StripModel`) and adds one `BoxCollider` around the barrel. The fermenting/ready state
  objects, output point and roof check point move by the difference between the new and old barrel tops.
- The cask is cloned before other mods add meads, so `FermenterPatches` gives each spawned cask
  (identified by `Fermenter.m_name == CaskPiece.HoverName`) a list built from the vanilla
  fermenter's live `m_conversion` in `ZNetScene`, each conversion copied with `CaskYield`. The copy
  is shared by all casks and rebuilt when the fermenter's list changes; the fermenter's own list
  and conversions are never touched. The patch runs for every vanilla fermenter too.
- `IdolPieces` clones `stone_pillar` (stone `WearNTear`, stonecutter, destruction effects), sits in
  the Furniture category with only the `PieceUsages.Decor` tag (the hammer's Decor tab), strips its
  model, colliders and snap points, and adds the idol mesh (in metres, base on the origin). Its
  colliders are convex `MeshCollider` hulls, one per horizontal band (`HullBands`) of the triangles
  reaching into it: one box around the idol was far too big in game. Stone idol I (`BowlIdol`) has a
  big stone bowl in front of the figure, which banded hulls filled; it gets hulls for the figure
  only (`x <= BowlEdgeX`, measured in ObjLoader's mirrored space), a box up to `BowlFloor` and one
  hull per rim sector (`RimSectors`), so things can be placed in the bowl. Its material copies `guard_stone`'s `PrivateArea.m_model` material (Standard shader)
  with the idol's albedo and normal map and a constant glossiness. Textures are compressed to DXT on
  clients after loading. Icons render the idol turned 180° (`IconTurn`), since the isometric
  camera otherwise sees its back; placed idols keep the model's orientation.
- `RuneCarvingPieces` clones `sign` (wood `WearNTear`, needs support), removes its `Sign`, its text
  canvas, model, colliders and snap points, and clears `WearNTear.m_new/m_worn/m_broken/m_wet`
  (vanilla throws when only some are set). It adds a `runes` child `TextMeshPro` and a 1 cm
  `BoxCollider` slab whose back face is at z = 0: placement rotates pieces only around Y and pushes
  the closest collider point onto the aimed surface, so the slab lands flat on a wall the piece
  faces. The text faces -Z, `TextLift` (5 mm, the slab's middle) in front of the surface; 2.5 cm
  floated visibly off a wooden pillar. On the 2 m wood beam's right half something writes depth
  just in front of the visible face (its readable mesh is a flat box there, so no lift measured
  from the mesh helps), which hid the runes from the beam's centre on. `RuneCarving.LateUpdate`
  therefore moves the text `DepthNudge` (3 cm) toward the camera and scales it by the same ratio,
  so it looks unchanged but wins the depth test; only geometry that close in front draws over it.
  A per-rune raycast fit against the meshes was tried and removed: it measured 0 everywhere and
  pulled vertical carvings onto pieces below. Rich text is on only for the `<mspace>` cell tag
  (`RuneCarving.Layout`); `ToRunes` never puts out `<`, so typed text can't open a tag.
  The material is a `_hearthfire` copy of the font's (`RuneMaterial`, shared by both carvings; the
  font material is the runestones' too), with the shader's glow on for a halo when it has
  `_GlowColor`, and `_FaceDilate` at `StrokeDilate` because the font's thin staves were hard to
  read by day; `ShaderUtilities.UpdateShaderRatios` runs after these, or TMP leaves too little room
  around each glyph and cuts the strokes and halo. The text is unlit, so `RuneGlow` colours it each frame (first carving's `Update`
  per frame) by the time of day: vanilla's day weight from `EnvMan.GetDayFraction` (0 before
  sunrise and after sunset, 1 at noon), near-black from `FullDaylight` (mid-morning; earlier faded too fast) on, only
  `TwilightShare` of the glow by sunset and from sunrise (faint at dusk and dawn), and HDR ember
  amber with the halo, pulsing slowly, from `NightFade` (about a game hour) after sunset;
  environments with `m_alwaysDark` glow at any hour. The night ember was 1.6 red with a 0.45 halo,
  too bright; it is now softer. The same darkness and flicker drive a shadowless point light per
  carving (`glow light` child, `LightLift` in front of the slab's middle, moved when the slab is
  refit, off by day and beyond `LightDistance`). Measuring
  the live sun and ambient light instead stopped finding night after tabbing out of the game.
  Vertex colour is white with `RuneAlpha`, so the material decides the colour. Local fires don't
  count. The shared material alone was not
  enough: after tabbing out of the game, carvings placed earlier stayed black while new ones
  glowed. So each carving also writes the frame's colour onto its own text and sub-mesh renderers
  as a property block in `LateUpdate` (`RuneGlow.ApplyTo`, keeping vanilla's values in the block)
  and rescans those renderers every `RefreshInterval`.
  It is top-left aligned in a zero-size rect, so the piece origin (the aimed point) is the first
  rune's top-left corner and the slab extends to +X and -Y from it (`RuneCarving.SlabCenter`).
- The font is vanilla `Valheim-Rune`, the one `TextViewer` shows runestone text in (it draws Latin
  letters as runes). `RuneFont` finds it with `Resources.FindObjectsOfTypeAll`, loading the game's
  resources first if it isn't loaded yet, and falls back to any font named like "Rune". Its font
  size comes from `faceInfo` (needs the `UnityEngine.TextCoreFontEngineModule` reference): 3D TMP
  scales by `fontSize / pointSize * scale * 0.1` per font unit. Its source font ("Rune", in
  `resources.assets`) draws Elder Futhark for a-z but leaves c, e and accented letters as blank
  glyphs, and its digits are plain serif numerals. `ToRunes` keeps letters the font draws (or
  their lowercase), writes c as k and e as i (`s_soundAlike`, as Younger Futhark carvers did),
  keeps only `KeptPunctuation`, and maps every other character (digits, symbols, `<`) by code point
  modulo the font's drawn a-z letters, so nothing falls back to a plain font. `Has` counts a glyph
  only if it has ink (`metrics.width > 0`). The font's atlas is filled on demand: a plain
  `HasCharacter(c)` saw no letters in game (0.1.0 skipped the pieces), so `RuneFont.Has` passes
  `tryAddCharacter` and searches only the font's own fallbacks. If it still finds no letters, it
  warns and assumes a-z rather than skipping the pieces.
- `RuneCarving` stores the raw text in ZDO `hearthfire_runes` (writer claims ownership, like
  `Sign.SetText`) and polls `ZDO.DataRevision` every 2 s, starting in `Start` because TMP measures
  only after its own `Awake`. The text never wraps: horizontal carvings are one line, vertical ones
  one column with a line break after every rune. Every rune is centred in a `RuneCell` (0.55 em)
  wide `<mspace>` cell and a space takes one cell: the font's advances barely follow its runes, so
  spacing by them left uneven gaps and squeezing them made thin runes such as ᛁ collide. Vertical
  carvings tighten rows with `ColumnLineSpacing`. Spaces inside the text are kept, only the ends
  trimmed. The collider is refit
  from `GetPreferredValues`, also on the placement ghost. `WearNTear` caches its support bounds once;
  the back face stays on the surface, so support still holds. Empty text shows `DefaultText`.
- Build-menu icons are pale Elder Futhark strokes drawn by `RuneScript` (the font's distance-field
  atlas can't be shown as a sprite).

## Failure handling

A missing `fermenter` or `dvergrprops_barrel` skips the cask; a barrel without meshes keeps the
fermenter's model; a failed icon render keeps the fermenter's icon; a missing `stone_pillar` or
`guard_stone` skips the idols, and an idol whose model or albedo fails to load is skipped alone.
A missing `sign` or rune font skips both rune carvings; dedicated servers get no icons.
