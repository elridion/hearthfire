---
paths:
  - "src/**/*.cs"
---

# Hearthfire code specifics

Adds to the workspace-wide `code-style.md` and `valheim-modding.md` in `../.claude/rules/`.

## Structure

- One class per piece family (`CaskPiece`, `CratePiece`, `HangingCorePiece`, `IdolPieces`,
  `RuneCarvingPieces`), each with a static `Register` hooked to
  `PrefabManager.OnVanillaPrefabsAvailable` in `HearthfirePlugin.Awake`. Each family has its own
  folder under `src/` (`Cask/`, `Crate/`, `Lights/`, `Idols/`, `Runes/`) with its components and
  patches; a new family gets one too. Helpers more than one family needs go in `src/Common/`
  (`PieceUtils`). Folders don't add namespaces: everything stays in `Hearthfire`.
- Localization keys: `piece_dvergrcask*`, `piece_dvergrcrate*`, `piece_hangingsurtlingcore*`, `piece_celticidol01`..`12` (the idols share
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
- `CratePiece` clones `piece_chest_wood`, so it keeps the chest's `Container` (ZDO contents),
  wood `WearNTear`, workbench and open/close effects; `m_name` changes and the grid becomes
  `InventoryWidth` x `InventoryHeight` (4 x 4, a square for a square crate). The
  `PieceUsages.Storage` tag puts it in the hammer's Storage tab (10 Corewood, 10 Bronze nails: a
  bronze-age step between the wood chest's 5 x 2 and the iron reinforced chest's 6 x 4; 3 x 3 for
  10 Wood and 2 Resin cost more than the wood chest and held less). Its model
  is `dvergrprops_crate` swapped in like the cask's barrel (`PieceUtils.StripToVisual`, standing on
  the origin, chest meshes, colliders and snap points stripped, one `BoxCollider`); the prop's
  inactive worn, broken and destruction children are removed, since they only widened the bounds.
  `AddOpenLid` takes the crate's highest mesh, its lid, as `Container.m_closed`; `m_open` is a copy
  under a pivot on the lid's back top edge, tipped back `LidTipAngle` (20°), then turned `LidTurn`
  (-12°, counter-clockwise from above; clockwise looked wrong) about Y, shifted `LidShift` (15 % of
  its width and depth) toward the back left (+X, -Z; back is -Z, where the wood chest's open lid
  sits too) and lowered by the shift times sin(tip), less `LidClearance` (5 mm), so its underside
  rests on the back rim instead of floating (a 2 cm lift on top of the tip left it hovering). It
  looks pried up and pushed aside rather than hinged (110° fully open looked like a chest; only
  tipped back was liked but too tidy), and inactive. `Container` swaps them while in use.
  Materials the lid doesn't use are the prop's contents (fruit): `DimContents` gives them copies
  darkened by `s_contentsTint` (40 %; 60 % was still too bright), since they read too bright
  through the open lid. The prop's high and low LODs overlap
  exactly, and Jotunn's icon render drops LOD groups, so both drew and the icon looked broken:
  `PieceUtils.KeepFirstLod` keeps only LOD 0. No snap points: crates snapping to each other felt
  wrong in game; the box collider still lets them stand on each other.
- `HangingCorePiece` clones `piece_brazierceiling01` for its hanging placement and support from
  above, `PieceUsages.Lighting` (1 Surtling core, 2 Leather scraps), with
  `CraftingStations.Workbench` set explicitly (the clone otherwise keeps the brazier's station). `StripFire` removes the
  brazier's `Fireplace`, `EffectArea` (heat, fire, player base), `Aoe`, smoke, lights, particles,
  sounds and trigger colliders, then `StripModel` its meshes, colliders and snap points; comfort is
  set to 0, so it is no fire and needs no fuel. The brazier is iron: `UseWoodSounds` copies the
  `sign`'s `Piece.m_placeEffect` and `WearNTear` material, hit and destroyed effects. The model
  hangs from the origin: the embedded knotted rope (`rope.obj.gz`, small loop up, big loop below)
  scaled by `RopeScale` (0.975, about 0.73 m; in game 0.65 looked too small next to the core and 1.3 too big) and moved so the small loop's opening (`s_hangPoint`)
  is the origin: the top of that opening (its middle sank too much of the loop into the beam), so
  the loop's top reaches a little into the beam above, as if hung on a nail, in a `PieceUtils.MakeStandardMaterial` copy of the
  ward stone's material with its maps loaded as Repeat (its UVs run past 0..1) and `_Color` set to
  `s_ropeTint` (60 % grey; the source texture read too bright). Tried before and
  dropped: a stretched Unity cylinder in the leather scraps' material, then `LineRenderer`s with
  the `Cart`'s rope line material (thin, and a net of lines did not read as holding the core). In
  the big loop sits a copy of `Pickable_SurtlingCoreStand`'s `core` child, stripped to mesh, light
  and `LightFlicker` (`StripToGlow`), turned upside down onto a tip and a little off true
  (`s_coreRotation`), at the stand's own size (about 14 cm radius, as in
  the game files; half that was tried and changed back) and `CoreDrop` (8 cm) below `s_loopCentre` (measured in
  the model, see its README). The loop's opening fits only about 4 cm at this rope size, so the
  loop sinks into the core. The `SurtlingCore` item was used first, but its root
  carries the dropped-item sparkle particles. The core's light gets `s_lightColor`, `LightRange`
  and `LightIntensity`, softer than a torch. One `BoxCollider` around rope and core, cut off at the
  origin, for hover, hits and support. `Piece.m_clipEverything` is on: vanilla placement otherwise
  lines up the collider point closest to the aimed point with it (after moving the ghost 50 m out
  along the surface normal), which on sloped undersides was the box's top edge, up to half the
  core's width off. With the flag the origin goes straight onto the aimed point; only
  `Player.UpdatePlacementGhost` reads it. The cloned brazier's `m_inCeilingOnly` stays.
- `IdolPieces` clones `stone_pillar` (stone `WearNTear`, stonecutter, destruction effects), sits in
  the Furniture category with only the `PieceUsages.Decor` tag (the hammer's Decor tab), strips its
  model, colliders and snap points, and adds the idol mesh (in metres, base on the origin). Its
  colliders are convex `MeshCollider` hulls, one per horizontal band (`HullBands`) of the triangles
  reaching into it: one box around the idol was far too big in game. Stone idol I (`BowlIdol`) has a
  big stone bowl in front of the figure, which banded hulls filled; it gets hulls for the figure
  only (`x <= BowlEdgeX`, measured in ObjLoader's mirrored space), a box up to `BowlFloor` and one
  hull per rim sector (`RimSectors`), so things can be placed in the bowl. Its material (`PieceUtils.MakeStandardMaterial`, shared with the hanging core's rope) copies `guard_stone`'s `PrivateArea.m_model` material (Standard shader)
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
  therefore moves the text toward the camera and scales it by the same ratio, so it looks
  unchanged but wins the depth test; only geometry that close in front draws over it. The move is
  `DepthNudge` (3 cm) divided by the cosine of the view angle, capped at `MaxDepthNudge`, so the
  text always clears the surface by 3 cm: a fixed 3 cm along the line of sight cleared it by
  almost nothing when seen along a wall, and the runes flickered behind it at those angles.
  A per-rune raycast fit against the meshes was tried and removed: it measured 0 everywhere and
  pulled vertical carvings onto pieces below. Rich text is on only for the `<mspace>` cell tag
  (`RuneCarving.Layout`); `ToRunes` never puts out `<`, so typed text can't open a tag.
  The material is a `_hearthfire` copy of the font's (`RuneMaterial`, shared by both carvings; the
  font material is the runestones' too), with the shader's glow on for a halo when it has
  `_GlowColor`, and `_FaceDilate` at `StrokeDilate` because the font's thin staves were hard to
  read by day; `ShaderUtilities.UpdateShaderRatios` runs after these, or TMP leaves too little room
  around each glyph and cuts the strokes and halo. The text is unlit, so `RuneGlow` colours it
  each frame (first carving's `Update` per frame) by the time of day: a glow running in a straight
  line from the `MinGlow` config setting at noon to `MaxGlow` at midnight, by
  `EnvMan.GetDayFraction` (environments with `m_alwaysDark` get `MaxGlow` at any hour), with a slow
  flicker. The glow lerps the face, unclamped, from warm grey (near black was too black) at 0 to
  ember amber at 1, and scales the halo's alpha. Fades with a window around sunset (smoothstep
  curves, then a linear ramp) felt uneven, and the night ember was too bright at 1.6, 1.15 and 1.0
  red, hence `MaxGlow` 0.8. Both settings are client-side and read live. The face colour is multiplied by a
  generated tiling value-noise texture in `_FaceTex` (`RuneGlow.MakeNoise`, skipped on dedicated
  servers), so the glow is patchy; each carving drifts it from its own random offset through
  `_FaceTex_ST` in the same property block. A point light per carving was tried and removed: as
  an Auto light it showed nothing, and as `ForcePixel` it would add a forward pass to every
  surface near every carving, uncapped. Measuring
  the live sun and ambient light instead stopped finding night after tabbing out of the game.
  Vertex colour is white with `RuneAlpha`, so the material decides the colour. Local fires don't
  count. The shared material alone was not
  enough: after tabbing out of the game, carvings placed earlier stayed black while new ones
  glowed. So each carving also writes the frame's colour onto its own text and sub-mesh renderers
  as a property block in `LateUpdate` (`RuneGlow.ApplyTo`, keeping vanilla's values in the block)
  and rescans those renderers every `RefreshInterval` into a reused list. `LateUpdate` (colour
  and depth nudge) skips carvings whose text renderer no camera drew last frame, and writes the
  text scale only when the ratio moves by more than `RatioStep`, since a new scale makes TMP
  rewrite its mesh; dedicated servers skip `RuneGlow.Tick`.
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
fermenter's model; a failed icon render keeps the fermenter's icon. The crate does the same with
`piece_chest_wood` and `dvergrprops_crate`. A missing `piece_brazierceiling01`, `Pickable_SurtlingCoreStand`,
`sign` or `guard_stone` material, a stand without a `core` mesh, or a rope model or albedo that
fails to load skips the hanging core; a missing rope normal map falls back to a flat one. A missing `stone_pillar` or
`guard_stone` skips the idols, and an idol whose model or albedo fails to load is skipped alone.
A missing `sign` or rune font skips both rune carvings; dedicated servers get no icons.
