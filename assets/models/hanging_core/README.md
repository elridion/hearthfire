# Hanging core rope

Source: "Hello Neighbor 2 Rope" by irons3th on Sketchfab
(https://sketchfab.com/3d-models/hello-neighbor-2-rope-0cdb55aff5644b0e8addfcd91801207e).

## License

Listed on Sketchfab as Creative Commons Attribution 4.0 (CC BY 4.0,
https://creativecommons.org/licenses/by/4.0/). The download carried no license file. Credit
irons3th in both READMEs. The model and textures are not covered by the mod's MIT license.

Caveat: the title names the game Hello Neighbor 2, and the mesh, material and texture names
(`SM_Rope`, `MI_Rope_StartEnd_Inst`, `T_Rope_02_D`) follow Unreal Engine conventions, so the model
may have been extracted from that game rather than made by the uploader. If that is ever confirmed,
or the rights holder objects, replace this rope.

## Conversion

Converted once in Blender 5.0 with a throwaway script (not kept in the repo), from
`source/hello neighbor 2 rope.blend` (mesh `SM_Rope`, render UV map `UVMap`):

- Turned upside down (180 degrees about the vertical plane's axis) so the small loop is on top and
  the big loop below, then moved so the top of the small loop is the origin.
- Decimated to 45 % (3036 triangles) and exported as OBJ, Y up, triangulated, with UVs and
  normals, then gzipped (`rope.obj.gz`). `ObjLoader` mirrors X like for every OBJ.
- Measured in the exported OBJ's space: the big loop's opening fits a circle of radius 0.0437 m
  centred at (0.0118, -0.5993, -0.0261); the small loop's opening is centred at about
  (0.005, -0.108, 0), where the piece hangs from; the rope reaches down to y = -0.750. `HangingCorePiece`
  holds these values (X mirrored).
- Albedo (`../../textures/hanging_core/rope_albedo.jpg`): 1024 px PNG downscaled to 256 px, JPEG
  quality 90.
- Normal map (`../../textures/hanging_core/rope_n.png`): 512 px PNG downscaled to 256 px, green
  channel flipped (Unreal's DirectX convention to Unity's OpenGL one), RGB layout, loaded as linear.
- UVs run past 0..1, so both textures load with Repeat.
