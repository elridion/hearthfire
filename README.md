# Hearthfire

Valheim mod (BepInEx + Jotunn) adding building pieces that fit the vanilla look: the Dvergr cask (a
fermenter that uses the dvergr barrel prop from the Mistlands camps), the Dvergr crate (storage
built from the dvergr crate prop), a hanging surtling core light, twelve carved stone idols and
rune carvings that players write on like signs.

Players: install it from a mod manager. Features and recipes are on the mod page, whose source is
[`package/README.md`](package/README.md).

## Building

Requires the .NET SDK (8+) and a Valheim install. The build locates Valheim through the Steam
registry entry; override with `-p:VALHEIM_INSTALL="<path>"`.

```
dotnet build -c Release                           # build bin/Release/hearthfire.dll
dotnet build -c Release -t:Package                # also write dist/hearthfire-<version>.zip
powershell -File tools/make_icon.ps1              # regenerate package/icon.png
```

The first build generates publicized Valheim assemblies in `Valheim_Data/Managed/publicized_assemblies`.

The build touches nothing outside the repo. To test in game, install the zip with a mod manager
(e.g. import it into a [Gale](https://github.com/Kesomannen/gale) profile).

## Project layout

| Path | Purpose |
| --- | --- |
| `src/` | Plugin source, one folder per piece family: `Cask/` (cask and fermenter patch), `Crate/`, `Lights/` (hanging surtling core), `Idols/`, `Runes/` (rune carvings, font mapping, glow); `Common/` shared piece helpers and the OBJ loader |
| `assets/` | Embedded models (gzipped OBJ) and textures for the idols and the hanging core's rope; see the README in each `assets/models/` folder |
| `tools/make_icon.ps1` | Generates `package/icon.png` from game-icons.net |
| `package/` | Mod page README and icon shipped in the package |

No Unity project or asset bundles: the cask is a clone of the vanilla fermenter with the dvergr
barrel's meshes in place of its own; the idols are clones of the vanilla stone pillar with an
embedded mesh and textures in place of the pillar's; the crate is a clone of the vanilla wood chest
with the dvergr crate prop's meshes; the hanging core is a clone of the vanilla hanging brazier, its
fire removed, with an embedded rope and the core from the burial chambers' core stand; the rune carvings are clones of the vanilla
sign whose board is replaced by text in the vanilla runestone font.

## Releases

CI (`.github/workflows/build.yml`) builds the package on every push and pull request. Pushing a
`vX.Y.Z` tag checks that the tag matches the version in `hearthfire.csproj` and
`src/HearthfirePlugin.cs` and has a `CHANGELOG.md` section, then creates a GitHub release and
publishes to Hexium.

## License

Code: [MIT](LICENSE). The stone idol models and textures are from "Celtic Pagan Idols" by ELargin
(Unity Asset Store), used with the author's permission, and are not covered by the MIT license; see
`assets/models/celtic_idols/README.md`. The hanging core's rope is "Hello Neighbor 2 Rope" by
irons3th ([Sketchfab](https://sketchfab.com/3d-models/hello-neighbor-2-rope-0cdb55aff5644b0e8addfcd91801207e)),
licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), converted and
decimated; see `assets/models/hanging_core/README.md`. The icon is "Viking longhouse" by Delapouite from
[game-icons.net](https://game-icons.net), licensed under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).
