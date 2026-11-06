# Hearthfire

A handful of building pieces that look like they shipped with the game. Brew mead in a Dvergr cask, store gear in a Dvergr crate, hang a surtling core that never goes out, and decorate with stone idols and rune carvings.

## Features

- The Dvergr cask is a better fermenter: it brews in 30 minutes instead of 40 and gives 8 meads
  instead of 6, also for other mods' meads.
- The Dvergr crate is storage that looks at home outside, with 4 x 4 slots.
- The hanging surtling core glows without fuel under a beam or roof.
- Twelve weathered stone idols, and rune carvings you write on like a sign.
- Translated into every language Valheim offers except Abenaki.

## Pieces

| Piece | Tab | Station | Cost |
| --- | --- | --- | --- |
| Dvergr cask | Crafting | Workbench | 30 Fine wood, 5 Black metal, 10 Resin |
| Dvergr crate | Storage | Workbench | 10 Corewood, 10 Bronze nails |
| Hanging surtling core | Lighting | Workbench | 1 Surtling core, 2 Leather scraps |
| Stone idol I to XII | Decor | Stonecutter | 20 Stone each |
| Rune carving (horizontal, vertical) | Furniture | Workbench | 1 Coal each |

## How to use

- The hanging surtling core gives no warmth or comfort; it is only a light.
- Stone idol I has a bowl that holds whatever you place in it.
- Rune carvings start writing where you aim. The runes are grey at noon and glow more like embers
  towards midnight. They write c as k and e as i, as Viking carvers did.

## Installation

- Needs [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
- Install on every client and the server, all at the same minor version of Hearthfire.
- Uninstalling removes placed pieces without refunding them, including mead fermenting in a cask.

## Configuration

Settings are in `BepInEx/config/elridion.hearthfire.cfg` (or a config manager). They only change
how rune carvings look on your screen and apply while playing.

| Key | Default | Description |
| --- | --- | --- |
| `MinGlow` | 0 | Rune glow at noon, as a fraction of the ember glow; 0 is plain grey (0-1). |
| `MaxGlow` | 0.8 | Rune glow at midnight and in always-dark places (0-2). |

## Credits

- Stone idols: "Celtic Pagan Idols" by ELargin
  ([Unity Asset Store](https://assetstore.unity.com/packages/3d/props/exterior/celtic-pagan-idols-177494)),
  used with the author's kind permission, converted and downscaled.
- Hanging core rope: "Hello Neighbor 2 Rope" by irons3th
  ([Sketchfab](https://sketchfab.com/3d-models/hello-neighbor-2-rope-0cdb55aff5644b0e8addfcd91801207e)),
  [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), converted, decimated and downscaled.
- Icon: "Viking longhouse" by Delapouite from [game-icons.net](https://game-icons.net),
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/), recoloured and outlined.
