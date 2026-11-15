# Surface Voxelization

1. Open **Tools > Voxelization > Surface Voxelizer**.
2. Choose **Objects** for one or more individual models, or **Hierarchy** for a root container and its descendants.
3. Choose `Voxel Size`, optional `Voxel Gap`, `Surface Accuracy`, and `Fallback Color`. `Surface Accuracy` at `1` keeps every intersected cell; decrease it to reduce over-thickening on thin details. Voxel Size and Gap use each source mesh's local units, so differently scaled source objects produce differently sized world-space cubes.
4. Set `Output Folder`: type/paste a project-relative path such as `Assets/Generated/Voxels`, paste an absolute path within this project's Assets, click **Browse...**, or drag a Project folder into `Folder Asset`. Missing subfolders are created when saving. Folder, name, and overwrite preference are cached per project in EditorPrefs.
5. Optionally set `Output Name`, without a file extension. An empty name uses the source name. For several Objects, a custom name is used as a prefix followed by each object's name.
6. Use **Generate Preview** to view all models together. Previews start fully revealed and preserve source placement. **Clear Preview**, closing the window, script reload, or entering Play Mode removes the temporary objects, meshes, and material.

## Objects

Assign GameObjects with a readable `MeshFilter` mesh and a `MeshRenderer`. **Add Object** adds a field; **Add Selected** adds the current Hierarchy/Project selection without duplicate entries. This mode processes only the selected objects themselves, not descendants.

**Generate Mesh Assets** saves standalone combined meshes to Output Folder. As before, a standalone mesh can be assigned to a MeshFilter with a material using `Voxelization/URP Voxel Reveal` and a `VoxelRevealRenderer` component.

## Hierarchy

Assign the source container to `Source Root`. The window lists detected models and reports unreadable meshes and unsupported visuals before generation. Inactive descendants are included. LODGroup objects use only LOD0 geometry; other LOD nodes remain as empty transform nodes.

**Generate Prefab** saves this structure under Output Folder:

```text
Room_Voxel/
    Room_Voxel.prefab
    VoxelMaterial.mat
    Meshes/
        Table_VoxelMesh.asset
        ...
```

Mesh file names include sibling-index paths to disambiguate repeated names. The prefab preserves source object names, sibling order, active states, and local position/rotation/scale, including the root. Root transforms are relative to the original external parent; that external parent is not copied. Place the prefab under the same parent to reproduce that placement.

Only transforms and generated MeshFilter, MeshRenderer, and VoxelRevealRenderer components are created. Source scripts, colliders, rigidbodies, lights, LODGroup, and unsupported visual components are not copied. A shared persistent voxel material is assigned, and every model starts at Reveal Progress = 1. SkinnedMeshRenderer remains unsupported.

## Repeated saves

- With **Overwrite Existing** off, name collisions produce `Name 1`, `Name 2`, etc. Objects increment mesh filenames; Hierarchy increments the whole output folder so each exported prefab has its own meshes.
- With overwrite on, matching meshes are updated in place, preserving their GUIDs, and the matching generated prefab is resaved. Existing references see the new mesh data. Existing VoxelMaterial settings are retained.
- Source assets and paths occupied by another asset type cannot be overwritten. Mesh/material/prefab outputs are the only assets saved; unrelated dirty assets are not saved.
- Meshes from removed or renamed source nodes are not automatically deleted. Sibling reordering changes generated mesh filenames. Keep the source hierarchy stable for predictable updates.
- Batch entries with the same output name still get separate numbered paths within that batch. Generation can be cancelled between models. If saving fails partway through, already saved outputs are retained.

`VoxelRevealRenderer` starts with Reveal Progress at `1` and exposes `SetProgress(float)`, `ShowImmediate()`, and `HideImmediate()`. `Color Intensity` and `Saturation` adjust baked vertex colors through `MaterialPropertyBlock`, so several models can share one material. The shader receives and casts main-light shadows.

## Group reveal

Add `VoxelRevealGroup` to an empty parent of the generated models. Assign `Origin Transform`, or leave it empty and enter a world-space `Reveal Origin`. A single group `Reveal Progress` controls a radial assembly front across all cached models. The group starts fully visible; it does not automatically play on scene load.

The front travels according to the world-space distance from each voxel's final center to the origin. Height does not add a separate ordering offset. The origin is not clamped to any mesh bounds, and the radius starts at zero: an origin outside the group creates a travel delay before the first voxels appear. All renderers share the same maximum distance; they do not each restart their own local reveal. Reveal Chaos adds bounded, deterministic variation within this shared front.

`Transition Width` is the base assembling width in world units. `Reveal Chaos` (default 0.75) adds a random start delay of up to that distance and extends each voxel's flight by 0.5 to 1 times that distance. The full transition band is bounded by Transition Width + 2 * Reveal Chaos. Zero chaos restores the uniform radial front. Larger values produce a wider, irregular cloud of assembling cubes; they can reorder nearby voxels, but do not restart an independent reveal for each model.

Models with separated distance ranges finish/start in sequence when their gap exceeds the full transition band. Overlapping radial ranges can assemble simultaneously; this is spatial propagation, not whole-object sorting. All voxels are fully assembled by progress 1, without an endpoint snap. With zero chaos, the farthest voxel finishes exactly at 1. `Reveal Duration` is the total playback time in seconds, including travel through empty space; increase it for a large location or a slower flight effect.

Chaos also varies the cube scale curve, mixes radial scatter with random flight directions, and adds curved motion while cubes assemble. Cubes become visibly sized earlier instead of spending most of their flight as tiny dots. Scatter Distance still controls the spatial spread, and Wave Strength controls the existing wave motion. Flight paths and shadow paths use the same calculations and settle exactly at the final positions.

In Play Mode, use the group's **Play Reveal** context menu or call:

```csharp
group.PlayReveal(originTransform);
// Alternatively, pass a world-space point:
group.PlayReveal(worldOrigin);
```

For external animation or manual control:

```csharp
group.SetOrigin(worldOrigin); // Or group.SetOrigin(originTransform).
group.SetProgress(progress);
```

`SetProgress` stops built-in playback. `ShowImmediate()` and `HideImmediate()` set progress to 1 and 0. The Inspector slider also previews the effect in Edit Mode. Passing a Transform tracks its position; passing a Vector3 clears the Transform reference. Animator can animate the group's serialized revealProgress field. Disabling the group stops playback and leaves the last applied renderer state in place.

Children, including inactive objects, are cached on enable. Call `RefreshChildren()` or use **Refresh Children** after adding/reparenting descendants or regenerating their mesh data. `RevealRenderers` exposes the cache. A renderer on the parent itself and renderers owned by a nested VoxelRevealGroup are excluded, so separate chunks can be controlled independently.

The group supplies origin, progress, transition width, and reveal chaos. Scatter, wave, color intensity, and saturation remain per-renderer settings. A standalone renderer has its own Reveal Chaos and still supports SetProgress/ShowImmediate/HideImmediate; its `SetOrigin(Vector3)` switches back to an independent world-space reveal domain after group control.

Existing generated meshes can be reused; regeneration is not required. Runtime setup reads UV1 once per mesh change and caches one center per voxel. Exact world-space distances are recalculated when the origin or model transform changes; ordinary progress changes only update per-renderer property blocks. GPU animation positions and shadow positions use the same reveal function. Renderer bounds are expanded locally for Scatter/Wave displacement without modifying mesh assets. No graph, neighbor averaging, or location-level orchestration is implemented.

## Boundary reveal alternative

`VoxelBoundaryRevealGroup` is a separate alternative to `VoxelRevealGroup`, with the same progress, duration, chaos, immediate show/hide, child caching, and playback controls. The radial group is still available. Use only one group type on a given parent.

Setup in Unity:

1. Add **Voxel Boundary Reveal Group** to an empty parent containing generated voxel models with VoxelRevealRenderer components.
2. Create two empty child GameObjects at the endpoints of a chunk edge, for example `LeftStart` and `LeftEnd`. Keep these endpoint objects inside the chunk prefab if it should be reusable.
3. Expand **Boundaries**, add an entry, and assign those objects to **Start** and **End**. The endpoints must differ in XZ; Y is ignored. With the parent selected and Scene View Gizmos enabled, cyan lines and endpoint circles show the projected boundaries at the parent's height.
4. For another visible neighbor, add another pair, such as `TopStart` and `TopEnd`, and another Boundaries entry. One to sixteen valid boundaries are supported. Invalid or excess entries are reported when SetProgress/PlayReveal is called; they are not silently discarded. Incomplete configuration while editing leaves the last rendered state unchanged. ConfigurationError also exposes the current validation message to code.
5. Initially set **Reveal Chaos = 0** and scrub **Reveal Progress** to inspect the fronts. Then increase chaos for the flying-cube transition. In Play Mode, use the component's **Play Reveal** context menu to run the animation for Reveal Duration seconds.

Each voxel uses the shortest XZ distance to any of the finite boundary segments. A segment covering a complete left edge produces a straight front into the chunk; a second segment at the top produces a simultaneous front from above. Sources are not averaged. A short segment has rounded propagation beyond its endpoints, so extend it across the intended edge for a straight front. Distance ignores height: cubes sharing the same final XZ position have the same base timing, with per-voxel variation when chaos is enabled. Each boundary propagates on both sides; the child models define the geometry affected.

The group also supports runtime setup with Transform pairs:

```csharp
boundaryGroup.SetBoundaries(new[]
{
    new VoxelBoundaryRevealGroup.Boundary { start = leftStart, end = leftEnd },
    new VoxelBoundaryRevealGroup.Boundary { start = topStart, end = topEnd }
});
boundaryGroup.PlayReveal();
// For externally driven animation:
boundaryGroup.SetProgress(0.2f);
```

Endpoints are tracked as they move. All models use one maximum distance to their nearest boundary, including transformed/scaled models. Geometry metadata and distance calculations are cached; progress changes do not scan vertices. Shader animation and shadow casting share the boundary calculations, and radial renderers explicitly use boundary count zero. The same material and existing mesh assets support both modes.

Refresh Children after adding/removing/reparenting models or group components. Nested groups of either type form separate ownership boundaries; refresh enclosing groups after changing that structure. Disabling a group leaves its last applied renderer state, as in the radial variant.

Reveal Progress remains animation progress, not a percentage of voxel coverage. Adding/removing boundaries recomputes the shared range and can redistribute a partially revealed area. This alternative does not yet implement graph neighbors, a persistent reveal mask, or a fixed 20% preview budget.

## Boundary reveal debug toggle

Add `VoxelBoundaryRevealDebugToggle` to an empty GameObject and assign a configured `VoxelBoundaryRevealGroup` to **Reveal Group**. Set **Reveal Duration** on the group in seconds; the debug component has no separate duration setting. In Play Mode, focus Game View and press **Space** to toggle reveal/hide. A visible group hides on the first press; a hidden group reveals. Pressing again during playback reverses from the current progress without jumping, using the group's Reveal Duration seconds to reach the new target. Disabling the debug component stops its animation at the current progress.

This uses `Keyboard.current.spaceKey` directly through the installed Input System package; no Input Actions or input asset changes are needed. It reads the group's RevealDuration property and drives `SetProgress` externally to support both directions. The same Reveal Duration setting controls built-in PlayReveal playback and this debug animation; manually scrubbing Reveal Progress does not use duration. Do not drive the same group with another animation controller at the same time.

Manual Unity checks:

- Confirm preview overlays the source without changing the source GameObject or its assets.
- Generate a single object, then multiple Objects, and confirm every preview is placed correctly and all output meshes are saved.
- Generate a Hierarchy prefab with nested, inactive, rotated, non-uniformly scaled, and identically named children; compare local transforms, sibling order, and renderer states against the source.
- Confirm LOD groups generate only LOD0 geometry and unsupported visuals produce a warning.
- Confirm the generated prefab contains only transforms and the three allowed visual components, with persistent mesh/material references after reopening the project.
- Save repeatedly with overwrite off to check numeric suffixes. Enable overwrite to check stable mesh GUIDs and existing prefab references. Verify that unrelated dirty assets are not saved.
- Reopen the window/editor to confirm Output Folder persists. Check typed relative/absolute paths, folder drag-and-drop, and Browse selection; reject folders outside Assets.
- Regenerate and clear previews, close the window, and enter Play Mode; confirm no temporary geometry or material remains. Generate a Hierarchy while earlier Objects previews exist beneath it and confirm previews are not included as sources.
- Check an unreadable source texture samples correctly and leaves its import settings unchanged.
- Check URP Lit-style `_BaseMap`/`_BaseColor` and legacy `_MainTex`/`_Color` materials.
- Animate Reveal Progress from 0 to 1 and confirm per-voxel movement, scale, hiding, and ordering.
- Put two models at separated distances from a group origin; scrub group Reveal Progress and confirm the farther model waits for the shared front. Test origin points both inside and far outside the group.
- Check progress 0 is fully hidden and progress 1 is fully assembled, including rotated and non-uniformly scaled descendants. With Reveal Chaos = 0, confirm the farthest voxel finishes last.
- In Play Mode, call PlayReveal with a Transform and a Vector3, check duration and immediate show/hide, and confirm a moving origin updates the front. Change Transition Width to inspect the overlap band.
- Compare Reveal Chaos at 0 and positive values at the same progress: zero should retain uniform rows, while positive values spread start times, flight durations, sizes, and directions. Increase Reveal Duration if the full location assembles too quickly.
- Scrub forward/backward to verify stable per-voxel delays, confirm progress 0/1 remain exact, and check moving cubes and their shadows stay synchronized and are not culled at the camera edge.
- Refresh after adding/removing/reparenting children. Check inactive children retain the group's applied progress when activated, and nested groups remain independent.
- For VoxelBoundaryRevealGroup, use a full edge with Chaos = 0 to check a straight front, then add a perpendicular edge and confirm simultaneous fronts without averaging. Confirm different heights at the same XZ have the same base timing.
- Check finite-segment endpoint rounding, moving endpoints, rotated/scaled models, and the 16-boundary limit. Missing or coincident XZ endpoints must report a configuration error on playback.
- With VoxelBoundaryRevealDebugToggle assigned, press Space in focused Game View to hide/reveal, reverse mid-animation, change Reveal Duration on the group, and confirm disabling the debug component stops at the current progress. A missing group reference must log an error and disable the debug component.
- Compare a separate radial group before/after the shader change, including standalone renderers and shared materials. Check boundary reveal at progress 0/1, with chaos, shadows, inactive children, and refreshed nested groups of either type.
- Confirm the generated model casts and receives directional-light shadows throughout the Assemble animation.
- Compare Color Intensity and Saturation against the source material under the same lighting.
- Generate a mesh over 65,535 vertices and confirm it renders with 32-bit indices.
