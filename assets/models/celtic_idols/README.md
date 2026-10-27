# Celtic idols

Source: "Celtic Pagan Idols" by ELargin, Unity Asset Store
(https://assetstore.unity.com/packages/3d/props/exterior/celtic-pagan-idols-177494).

## License

Used with the author's permission. Asked by email whether the idols could be used in this free mod,
ELargin replied (October 2026):

> I have no objection to you using my assets in any way you see fit.

ELargin also declined payment for the pack and offered the original Unreal Engine project (`.uasset`
files; the FBX sources are lost) as an alternative source. The files here were not re-exported from
it: they are the same models, so the permission covers them as they are. Keep the full email
outside the repo as the record of the grant.

The models and textures are not covered by the mod's MIT license. Credit ELargin in both READMEs.

## Conversion

The files were extracted from the asset bundle of the deprecated OdinPlus "Clutter" mod (`new2`
bundle, prefabs `$custompiece_celticidol1`..`12`), once, with a throwaway exporter (UnityFS bundle
reader, not kept in the repo):

- Meshes (`idolNN.obj.gz`): Clutter's prefab transforms baked in (x100 scale, -90 degrees about X,
  and 180 degrees about Y except idol 4), so each is in metres, Y up, base centred on the origin.
  X is mirrored and the winding reversed for `ObjLoader`, which mirrors back. Gzipped.
- Albedo (`../../textures/celtic_idols/idolNN_albedo.jpg`): 1024 px DXT1 source downscaled to
  512 px, JPEG quality 90.
- Normal maps (`../../textures/celtic_idols/idolNN_n.png`): 512 px DXT5nm source downscaled to
  256 px, written in the DXT5nm-style layout (R = 1, G = Y, B = 1, A = X) and loaded as linear.
- The metallic/smoothness maps are dropped; `IdolPieces.Glossiness` replaces them.
- Clutter's half-size copy of idol 1 (`$custompiece_celticidol1small`) is not included.
