# Prepared fragment island

Drag **Generated/Island.prefab** into a scene. Select its root and change **Fragment Reveal Group > Reveal Progress** between 1 and 0. Progress starts at 1. In Play Mode, Play Hide / Play Reveal use the group's 2.5-second Reveal Duration. The eight child renderers share one reveal front and clock.

**Island_Source.prefab** is the editable source. Move, rotate, add, or remove its children, save the prefab, and press **Regenerate Linked Island** on Fragment Island Authoring. Generated/BakeRecord.asset links this source to the existing generated meshes, metadata, materials, and prefab.

Defaults: Cell Size 0.35, Transition Width 0.6, Reveal Chaos 0.9, Scatter Distance 2, Scatter Rotation 120 degrees, Wave Strength 0.2. The default front enters from the west (local negative X). West, East, North, and South ports are approximate straight sides of the ground's bounds, not authored doorway/contact contours. Adjust their points on the source if the map needs more precise boundaries, then regenerate.

The source is a self-contained geometry/material snapshot of **Assets/Models/Islands/Island_Source.fbx**, not a nested live FBX instance. SourceMeshes and SourceMaterials preserve the imported island's eight visual parts and material assignments, with duplicate bush geometry shared. Prefab placement edits support normal linked regeneration. Re-exporting the original FBX does not automatically replace these snapshot assets; replace the relevant source MeshFilter meshes/materials before regenerating if the original geometry changes. The original FBX and its importer settings were not edited.

The FBX contains flat diffuse materials without texture/UV layers. Those diffuse colors were copied to source URP Lit materials and generated reveal materials. FBX right-handed coordinates were converted to Unity's left-handed coordinates by reflecting X, reversing triangle winding, and converting child rotations accordingly.

Geometry was generated without starting Unity, using the existing CellFragmentBuilder clipping/capping algorithm on decoded FBX triangles. Only the mesh input/output adapter was replaced in a temporary external utility; the project's generator, runtime scripts, and shader were not changed. Six unique generated meshes represent 2,231 fragments across eight renderers; fragments do not have individual GameObjects. Three meshes require UInt32 indices. All generated assets are stored as Unity YAML with explicit GUID/fileID references. Bake identity strings use Unity's [GlobalObjectId format](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GlobalObjectId.html).

Static checks passed for vertex/index buffer lengths and ranges, finite vertex values, per-triangle fragment metadata, motion radii, and local/external asset references. The clipping algorithm reported zero open cut contours for all eight source meshes. This does not replace Unity import or visual verification.

## Manual Unity verification

1. Let Unity import these assets and confirm there are no missing scripts, meshes, materials, data, or shader errors.
2. Place Generated/Island.prefab in a scene and scrub the root progress through 1, 0.5, and 0. Check the complete original surface at 1 and no visible geometry at 0.
3. At partial progress, inspect closed fragment backs/interiors, colors, hover, the shared west-to-east front, lighting, and cast/received shadows. Refine the approximate boundary points if needed.
4. In Play Mode, use Play Hide / Play Reveal and confirm the duration. Retain the group enabled; disabling it clears effect overrides and shows assembled geometry.
5. Move a child in Island_Source.prefab, save, regenerate, and confirm the generated prefab updates in place while keeping its asset references.

Source FBX SHA-256: `f474ecca5066073eff68099c43425ae1a5acbc1aa2d3eaa15c6906baf1c38ab5`.
