# Saved fragment islands

For the Blender/MCP modular asset workflow, handoff contract, and implementation roadmap, start with [Island Pipeline](../../Docs/IslandPipeline/README.md). The baker now uses a [shared cross-island mesh cache](SharedMeshCache.md); JSON import and logical asset-ID mapping remain planned work.

Unity 6000.3 / URP 17.3. This is the persistent island workflow built on the existing surface-fragment geometry/effect. The original SurfaceFragmentPrototype preview and Voxelization tools remain available. A runtime graph connects islands, previews their neighbors, and animates entry. Use [Manual island map](ManualMap.md) for placed scene instances or [Seeded island maps](GeneratedMap.md) to dock different-size, irregular baked prefabs in Play Mode. Placement uses authored docking points and XZ footprints, without a grid. Stitched platforms/bridges and save/load are not implemented yet.

Opening is independent per island: OpenIsland(id) can start any valid island at any time, including concurrently. Connections define adjacency and reveal influence, not travel permissions. GetVisibility, GetIslandIds, and GetConnectedIslandIds expose state/topology; there is no current island. See the manual map page for the public API and initial all-hidden mode.

## Author and bake

1. Assemble the island under one root: platform, decorations, and static mesh markers. Save it as a normal source prefab. A saved scene root also works, but a prefab is the recommended editable template.
2. Add **Fragment Island Authoring** to that root. Choose **Cell Size**, Fallback Color, and Reveal Settings. Cell Size is in each original mesh's local units, as in the prototype. There is no fixed cell/vertex budget or forced minimum beyond a finite positive size.
3. Use **Add Left Edge** for a quick incoming front, or **Add Bounds Outline** for a closed rectangular starting outline. Then edit **Boundary Paths → Points** or drag the numbered point handles in Scene View. These helpers use geometry bounds only as a starting point; edit the points to describe the desired island edges.
4. Each path is an ordered island-local polyline. Enable **Closed** to connect its last point to its first; disable **Enabled** to remove that path's influence. Add/remove/reorder points to draw zigzags or irregular polygon sides. Use separate paths for disconnected active sides: they will not be bridged by an unintended diagonal. Height is ignored by the distance field. Empty/one-point paths are ignored while setting them up.
5. Save source prefab changes before baking, including point edits. The saved source gives child objects persistent identities used during regeneration.
6. Set **Output Folder** by typing an Assets-relative or absolute project path, browsing, or dragging a folder into Folder Asset. Set Output Name if desired. These fields belong to the source component and persist when the source prefab is saved. Press **Bake Island Prefab**.
7. The first bake creates a uniquely named output folder containing Island.prefab, BakeRecord.asset, and island-local materials. Mesh/data assets are created or reused under Assets/Generated/FragmentLibrary. It does not overwrite an unrelated folder with the same name. Save/apply the source component's new Linked Bake Record reference once; subsequent bakes use that record's existing outputs instead of generating numbered duplicates.
8. Press **Open Generated Prefab** or drag the saved Island.prefab into a scene. The generated root has **Fragment Reveal Group**, with all visuals sharing one reveal range and clock, plus **Fragment Island Ports** for named map boundaries. It does not contain the original authoring script, arbitrary source scripts, colliders, rigid bodies, lights, cameras, or gameplay components.

Source MeshRenderer.enabled and child activeSelf states are preserved. If original renderers were disabled only to inspect the old temporary preview, re-enable them before baking; otherwise they intentionally remain invisible in the saved output. A warning identifies disabled source renderers.

For procedural placement, configure **Footprint** and **Ports** on the source before baking. **Set Bounds Footprint** and **Add Docking Point** provide starting data; edit the green contour handles and yellow port handles in Scene View. A long side can have several independent ports. See [tile authoring and the 1.5-length example](GeneratedMap.md#author-tile-shapes-and-docking-points). Existing prefabs with no explicit footprint use their mesh bounds and existing port midpoints.

The prefab root position/rotation are normalized to zero/identity for reuse; its source root scale and all child local positions, rotations, scales, hierarchy, sibling order, names, layers, renderer enabled state, and shadow settings are retained. Renderer data stays in each source mesh's local space. Place/rotate/scale the complete generated island freely at runtime.

## Regenerate an edited source

Edit the original source prefab, save, and press **Regenerate Linked Island** on its authoring component. Alternatively, select BakeRecord.asset and use **Regenerate From Saved Source**; this reads the saved source prefab without editing or saving it. **Open Source** locates the editable template. For a scene-authored island, open its source scene and use its authoring component instead.

- The generated prefab and island-local materials are updated in place. Mesh/data come from the shared cache by source Mesh identity and Cell Size. Matching content reuses them without slicing or saving new geometry. Changed mesh content updates its cache slot in place and increments data revision; a different Cell Size uses a separate slot.
- Regenerating an older island switches its renderers to shared geometry. Its legacy local mesh/data assets and owned bindings remain intact for external references; they are not updated or automatically deleted. Other older islands adopt shared geometry when individually regenerated. See [Shared mesh cache](SharedMeshCache.md).
- Source object identities are stored in lightweight **Fragment Bake Node** metadata components. Regeneration loads the existing output prefab and reuses matching GameObjects/components, including when source children are renamed or reparented. These markers have no runtime Update; retain them if regeneration should preserve object identities.
- New children are added; deleted source children are removed from the generated hierarchy. Source scripts are never cloned. LOD groups contribute LOD0 geometry only. Non-mesh renderers such as skinned meshes, sprites, and particles are not baked and produce a warning.
- Old generated mesh/material/data assets are intentionally not deleted when source parts disappear. This avoids breaking external references and allows later reuse. They do not become active visuals unless referenced by the current prefab.
- Treat the generated prefab as derived output. Its default group paths/settings are refreshed from the source recipe. Scene-instance overrides continue to follow Unity's normal prefab behavior; source generation does not modify unrelated scenes or save all dirty project assets.
- Move the entire generated folder if reorganizing it; regeneration resolves the record's current folder. Separately moving owned assets outside that folder is rejected rather than risking overwriting a non-owned/source asset.
- **Unlink Output (Keep Files)** clears only the source link. The old package remains; the next bake creates a separate package.

Generation is completed in memory before writes begin. Cancel during generation or at the ready-to-save prompt leaves existing output assets unchanged. Native temporary meshes/materials and isolated staging scenes are cleaned up on success, cancellation, and errors. Asset commits use ordinary Unity asset writes, not a filesystem transaction: a disk/import failure during the save phase may leave some generated assets updated; correct the external problem and inspect/recover the outputs before retrying. Original source geometry/material/texture assets are never overwritten. Bake records track island-owned outputs separately from shared geometry references; the cache owns and validates its own outputs. Targets matching source assets or unrelated output files are rejected.

## Runtime

Runtime code never clips a mesh and never reads mesh vertices/UVs. FragmentMeshData stores mesh references, one center per fragment, a conservative motion radius, and a revision. Instances share those data assets and materials; per-island animation parameters use MaterialPropertyBlock. Geometry changes during regeneration invalidate cached distance ranges through the data revision.

FragmentRevealGroup supplies one world-XZ distance domain, maximum range, progress, transition width, chaos, and animation clock to every descendant FragmentRevealRenderer. A distant model therefore waits for the same moving front that crosses nearer models; it does not normalize progress independently. Nested FragmentRevealGroups own their own descendants. Refresh Children after changing runtime hierarchy ownership or adding/removing renderers.

```csharp
island.SetProgress(0.2f);
island.PlayReveal(); // Animate from current progress to 1, using Reveal Duration.
island.PlayHide();   // Animate from current progress to 0.
island.ShowImmediate();
island.HideImmediate();

island.SetBoundaryPaths(new[]
{
    new FragmentBoundaryPath
    {
        points = new List<Vector3> { localLeftBottom, localLeftMiddle, localLeftTop }
    },
    new FragmentBoundaryPath
    {
        points = new List<Vector3> { localTopLeft, localTopRight }
    }
});
```

The points passed to SetBoundaryPaths are island-local, not world coordinates. Convert external world points with island.transform.InverseTransformPoint first. Updating paths does not regenerate geometry. A closed outline reveals inward from all its edges; for selected incoming sides, use only the desired open paths. With no usable segments, progress uniformly controls all fragments, so models can be baked before neighbors are known.

Reveal Progress is animation progress, not an exact fraction of the island's surface/fragment count. SetBoundaryPaths replaces the distance field and can redistribute a partial reveal. SetBoundaryPathsPreservingReveal additionally retains the reached front radius on segments present in both the previous and next path sets. The map uses this method when another revealed neighbor contributes a boundary, preventing the previously visible region from shrinking because the shared maximum distance changed. Local segment endpoints are matched in either orientation. The retained radius scales down when progress decreases; this is not a saved per-fragment visibility mask and assumes stable island geometry, transforms, and reveal settings between boundary changes.

PlayToProgress(target) animates to a partial target using the group's Reveal Duration, just like PlayReveal and PlayHide. One map controller must own its groups' progress and boundaries; avoid driving those same controls from an Animator, debug toggle, or another script at the same time.

Map-owned previews receive sampled coverage profiles from connected assembling islands. The map calculates and publishes those small profiles in two phases after group animation in LateUpdate; the vertex/shadow shaders interpolate them and apply them to the local front radius. Concurrent openings retain their initial profiles and independently release them toward full coverage, preserving their existing image at the start. This supports an edge sweeping from one corner to another without updating individual fragments in C#. Standalone groups retain their existing assembly equation. Port extents keep the final preview handoff continuous. Geometry, UV metadata, materials, and saved prefab references do not need regeneration for this change.

Hover continues at partial progress in Play Mode and Scene View; fully assembled/hidden endpoints remain exact. Disabling the group releases its GPU buffer and clears property-block/bounds overrides, leaving ordinary assembled materials visible; use HideImmediate to hide an island rather than disabling its effect component. Inactive/disabled original renderers remain inactive/disabled.

The saved runtime shader is **Surface Fragments/URP Reveal** and uses a dynamically sized StructuredBuffer for polyline segments, not the preview shader's old 16-segment array. It requires shader model 4.5 / an appropriate graphics backend, such as the project's desktop D3D11 configuration. This is ordinary MeshRenderer drawing, not indirect rendering or runtime GPU voxelization. Generated materials reference this shader as a persistent asset, avoiding a runtime Shader.Find dependency.

The buffer contains three float4 records per segment (world endpoints, held-front memory, profile offset/count), followed by optional scalar coverage samples stored in float4 records. Profile-only updates upload that trailing region; they do not recalculate mesh distance ranges or create extra segment geometry.

Appearance follows the validated prototype: original exterior shape/UVs, vertex colors and base texture/tint; flat interior color sampled from a representative exterior location; two-sided lighting, alpha cutout handling, shadows, scatter, and partial-progress hover. This does not reproduce every custom source shader, transparency, lightmaps, emission, or normal-map feature.

## Manual Unity verification

Unity Editor, batch mode, scene generation, and builds were not launched during implementation. Isolated runtime/editor C# checks are not authoritative Unity validation.

- Compile/import the new scripts and runtime shader. Bake a saved source prefab containing a platform, two separated trees, nested transforms, a renderer on the root, duplicate child names, and a repeated mesh/material.
- Confirm the generated prefab/data/materials survive script reload and entering/exiting Play Mode. Instantiate it without the original source prefab present in the scene; no temporary preview or geometry generation should be required.
- Check that original assets and unrelated dirty scenes/settings are not saved or altered. Check activeSelf, disabled renderers, transforms, mirrored/non-uniform scale, renderer shadow state, and visual-only component copying. Re-enable disabled source renderers if necessary.
- Edit the source mesh/material, rename/reparent a source child, add and remove children, save, and regenerate from both source Inspector and Bake Record. Check stable output GUIDs and references, correct refreshed geometry/centers, and no new duplicate output package. Verify existing scene prefab instances update through their normal prefab references.
- Move the entire generated output folder and regenerate. Unlink the output and bake a separate variant; confirm the first package remains.
- Cancel a large generation with an existing linked output; ensure the previous saved package is unchanged and staging resources are released.
- In a generated island, scrub one shared progress slider with near/far trees. At Chaos 0 the front must reach near centers before distant centers, regardless of model-local size or bounds. Test positive chaos, hovering and synchronized animated shadows.
- Test open, closed, irregular, and multiple independent polylines, including more than sixteen total segments. Move/rotate the island, edit points, and ensure no phantom bridge joins separate paths. Confirm local/world conversion and ignored height.
- Check SetProgress/PlayReveal/PlayHide duration and immediate endpoints in Play Mode, inactive children, nested group ownership, Refresh Children, buffer cleanup on disable/destroy/reload, and shared materials between two island instances with different progress and boundaries.
