# Shared fragment mesh cache

The island baker automatically reuses cut geometry across islands. No new authoring checkbox or setup asset is required. Unity creates the cache on the first successful bake after script import; installing the code alone does not change any existing prefab or create cache assets.

## Use

1. Build two editable source islands using the same source Mesh assets and the same Cell Size. Different transforms, scales, materials, footprints, and reveal settings are allowed.
2. Bake/regenerate the first island normally. New meshes are cut and saved to the shared library.
3. Bake/regenerate the second island. Matching meshes reuse the same generated Mesh and FragmentMeshData without another cut. The completion log and ready-to-save progress message report `Shared meshes reused` and `new/rebuilt` counts, once per distinct source Mesh in that bake.
4. Regenerate an unchanged island again: every unchanged mesh should be reused. The baker still refreshes the island hierarchy, metadata and its local materials.

Matching means the same source Mesh GUID/local file ID, not an equal name or appearance. Separately imported/copied files have distinct identities even if they look identical. Canonical model reuse must happen before baking; a logical `asset_id` registry and JSON importer are not included in this change. Source meshes need persistent asset identities; save temporary procedural meshes as assets first. Read/Write import settings are not changed: fingerprints use editor read-only MeshData snapshots.

## Storage and invalidation

```text
Assets/Generated/FragmentLibrary/
  Editor/Cache.asset          editor-only library metadata
  <slot hash>/Mesh.asset      runtime mesh
  <slot hash>/Data.asset      runtime fragment metadata
```

The stable slot identity contains source Mesh GUID/local ID and the exact positive Cell Size float. A SHA-256 fingerprint includes algorithm version, vertex layouts/buffers, UV/color/normal data, index format/buffer, submesh index ranges, base vertices and topology. Names, island transforms, materials and reveal settings do not enter it. Padding/unused vertex attributes are hashed conservatively, so changing buffer layout or other mesh attributes can also trigger a rebuild.

On a hit, mesh cutting and geometry asset writes are skipped. Reading/hashing the source still occurs to detect unsaved mesh edits as well as reimports. On a miss, the result is prepared in memory and only saved after the normal final cancellation point. Changed content overwrites the same owned mesh/data assets, preserves their GUIDs and increases Data.Revision. Algorithm changes require incrementing AlgorithmVersion in FragmentMeshCache.cs. A different Cell Size gets its own slot and leaves the previous variant available.

The library is Editor-only. Runtime prefab renderers reference the Mesh/data directly and do not depend on the library ScriptableObject, source meshes, fingerprinting or cutting code. Geometry is not combined into a unique island-wide Mesh.

## Ownership and older islands

FragmentIslandBakeRecord.SharedGeometry contains non-owned shared references. The existing Geometry collection retains legacy island-owned outputs; Materials remains island-owned. Validation accepts shared references only when they exactly match a registered cache slot and checks ownership of legacy outputs separately.

Regeneration migrates only the island being baked. Its prefab/bake link stays in place, but renderers switch to the shared Mesh/data. Old local meshes/data and their legacy bindings are retained, so external references are not silently broken. This initial cache population cuts once; old local results are not automatically adopted as verified cache entries. Older islands that have not been regenerated keep their previous assets.

Materials remain local to each island in this stage. Texture references retain their existing sharing behavior. Material changes do not invalidate the geometry fingerprint, and a general shared material cache remains future work.

Do not move/delete the shared library or its slot assets individually. Its current root path is fixed; all islands using a slot depend on it. Invalid ownership, missing outputs or orphaned/conflicting paths produce explicit errors instead of silently overwriting assets. Restore the affected assets/metadata from backup when damaged. There is no automatic pruning, cross-project deduplication or forced cache deletion command.

Geometry updates affect all already-migrated islands referencing the slot. If an edit also changes material slots, object hierarchy or other island bindings, regenerate the relevant source islands to refresh those bindings. Unmigrated legacy outputs do not receive shared updates. Native asset saves are not atomic: a save/import failure can leave partial outputs; inspect and restore the affected cache state before retrying.

## Manual Unity verification

Unity was not launched during implementation. Isolated player/editor C# checks do not validate AssetDatabase writes, serialization, native mesh hashing or rendered behavior.

- Import scripts, regenerate the prepared square island, then the long variant. With their existing common source meshes and Cell Size, the second bake should reuse all six unique meshes. Compare Mesh and Data references, not just names. Both should still have one synchronized reveal group.
- Regenerate the same island after script/domain reload: expect all unchanged geometry reused, unchanged GUIDs/revisions and no new slot folders.
- Move, rotate, scale or rename a source child, change footprint/ports, reveal settings or material tint, then regenerate. Geometry should be reused; source Mesh data must remain unchanged.
- Edit source vertex positions, normals, UVs, colors or indices and save/reimport, then regenerate. Expect the affected slot rebuilt with stable Mesh/Data GUIDs and increased Data.Revision. Check another migrated island's reveal range and shadows against the new data.
- Change Cell Size and regenerate. Expect separate slots while islands using the old size retain valid geometry. Return to the previous size and expect reuse when its content is current.
- Use a non-readable imported mesh. Confirm hashing/cutting works through MeshUtility and no import settings change.
- Cancel during cutting or at the ready-to-save step. Confirm existing island/cache assets remain unchanged and temporary meshes are released.
- Regenerate an older island with existing local bindings. Its output prefab GUID should remain, its renderers should point to SharedGeometry, and old local geometry files should remain intact.
- Use two different source mesh assets with the same display name; expect separate slots. Test a conflicting cache output path in a disposable copy: an explicit error should retain existing unrelated assets.
- Enter Play Mode, test reveal/shadows on several islands, and exit. When permitted, verify a Player build uses runtime Mesh/Data without editor library code.
