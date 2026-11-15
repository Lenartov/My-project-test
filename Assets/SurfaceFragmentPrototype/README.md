# Surface fragment reveal prototype

Independent experiment for Unity 6000.3 / URP 17.3. The existing Voxelization system is unchanged. A single source MeshFilter is cut against a cubic grid. Exterior polygons retain their shape, interpolated UVs, normals, vertex colors, and material assignment. Cut surfaces receive flat interior faces. All fragments share one temporary combined mesh; the GPU handles their movement, rotation, scaling, and visibility.

## Use

1. Add **Surface Fragment Reveal Prototype** to an empty GameObject and assign the object's MeshFilter to **Source**. Concave meshes, disconnected shells, overlapping parts, multiple material submeshes, and open surfaces are accepted. For example, a tree can contain a trunk and a separate crown in the same mesh.
2. Add **Boundaries** entries, each with **Start** and **End** transforms along the desired sides. Segments use world XZ; height is ignored. Extend a segment across the full edge for a straight front. Multiple segments produce simultaneous fronts based on the nearest boundary, not an averaged origin. Short segments still have rounded propagation beyond their ends. The shader accepts one to sixteen boundaries.
3. Set any finite, positive **Cell Size** in source mesh local units. There is no Inspector minimum and no fixed cell/vertex/work budget. Smaller values increase time and memory use; they are not clamped or rejected because of a preset prototype budget.
4. Press the visible **Build Preview** button beneath the Inspector fields. The component context menu also retains Build Preview / Clear Preview. The Inspector shows a candidate-cell estimate before generation and actual fragment/vertex counts afterward. Large estimates produce a warning only, not a prohibition.
5. During generation use the progress dialog's **Cancel** button if necessary. Canceling retains the previous valid preview. Closing the progress dialog is handled even when generation fails. Generation is synchronous, but clipping, cap construction, and mesh traversal periodically check cancellation.
6. Manually disable only the original **MeshRenderer** to inspect the preview alone. The source object, its renderer visibility, mesh, materials, textures, and import settings are not changed by this tool.
7. Scrub **Reveal Progress**, including in Edit Mode. At 1 the original exterior is assembled; at 0 every fragment is hidden. At intermediate values the flying fragments continue hovering and rotating even when the slider is stationary. Tune **Scatter Distance**, **Scatter Rotation**, **Wave Strength**, **Transition Width**, and **Reveal Chaos**. Width/chaos use world distance; scatter/wave use the prototype object's local units. Rebuild previews generated before the automatic interior-color and cap-winding changes.
8. Use **Clear Preview**, disable/remove the component, or reload scripts to release temporary resources. Re-enable the original renderer yourself. Rebuild after changing Source, Cell Size, source geometry/materials, or the source transform.

Source transforms are baked into the prototype object's local coordinates at generation time. Moving the prototype moves the whole preview. Moving only Source requires rebuilding. Boundary transforms are tracked without rebuilding. Keep the prototype at unit scale initially when comparing source and preview.

The Editor reads mesh data through MeshUtility.AcquireReadOnlyMeshData, including imported meshes without Read/Write enabled. Texture sampling stays on the GPU and does not require readable textures. Import settings are never modified. Outside the Editor, Unity's normal CPU mesh readability requirement still applies to generation.

## Geometry behavior

- Balanced recursive splits cut the current geometry at grid-aligned planes instead of testing the whole source against every cell. Empty regions are skipped. There is no convexity test or closed-manifold rejection.
- Each cut projects its boundary edges into a plane. A winding-based sweep triangulates concave sections, separate contours, and holes. Intersections of overlapping contours add sweep levels so sections are not connected by the old centroid/angle-sorted convex cap. Cap triangles preserve signed winding and layer multiplicity, including reversed and overlapping shells; dropping either can break closure during subsequent cuts on another axis.
- Closed shells retain their authored surfaces and winding. Disconnected parts in the same grid cell move together as one fragment. This is not a full boolean cleanup of intersecting source shells: authored interior overlap surfaces can remain.
- Connected surface components with open/non-manifold edges receive a thin backing shell, ten percent of Cell Size, and edge skirts. This provides thickness for surfaces such as leaves or an open trunk instead of refusing to generate. Their backs retain texture/vertex color; skirts sample the fragment's exterior appearance. This is a best-effort thickness interpretation, not a reconstruction of an unknown solid volume.
- Inconsistent source winding, self-intersections, badly welded seams, and floating-point precision can still produce cap artifacts. Incomplete cut contours generate a warning and retain the generated result for inspection rather than blocking the whole model.
- Degenerate triangles are skipped. Triangle and quad submeshes are supported; point/line indices are skipped with a warning because they do not describe a surface.
- One mesh uses 32-bit indices when necessary. There are no per-fragment GameObjects, colliders, rigid bodies, or runtime per-fragment Transform updates.

## Appearance and lifecycle

Each submesh gets a temporary reveal material using its original base texture/tint: _BaseMap / _BaseColor, then _MainTex / _Color, then Fallback Color. Texture scale/offset and vertex colors are retained. Source alpha clipping is carried over when exposed through standard properties; shadow casting uses the same clipping. Rendering is two-sided with back-face normals flipped for lighting; the source material's culling setting is not copied. Lighting is diffuse URP ambient/main/additional lighting with animated casting and receiving of shadows, not an exact reproduction of arbitrary source shaders. Normal maps, metallic/smoothness, emission, true transparency, and lightmaps are not reproduced.

There is no Interior Color setting. Each fragment selects a nearby exterior polygon and stores its average UV, vertex color, and material for all its interior vertices. The GPU samples this one exterior location for a flat matching cap color; no CPU texture readback or import changes are needed. Completely internal cells inherit a nearby exterior sample from a parent cutting region. Cap alpha is forced opaque so texture cutout holes do not remove the interior faces.

SetProgress(float) allows external driving; changes apply in the next Update. Reveal ordering and base scatter remain deterministic. A time-dependent hover and rotation are added only while a fragment is between hidden and assembled; fully assembled fragments stay still. Wave Strength controls hovering, while Reveal Chaos and Scatter Distance also control irregular motion. Only one clock value per model is sent from C#; all fragment positions remain GPU-computed. In Edit Mode an owned preview callback refreshes Scene View at up to 30 Hz while overall progress is partial; the callback is removed on Clear Preview/disable. The prototype does not use VoxelRevealGroup, VoxelBoundaryRevealGroup, or their debug toggle.

No mesh/material/prefab assets are saved. Disabling the component or reloading scripts deliberately discards the temporary preview. Shader.Find is intended for this Editor experiment; production shader stripping/inclusion, depth/normals prepasses, and motion vectors are not implemented.

Extremely small cells can consume all available memory. Warnings and cancellation replace the old fixed budgets; they cannot make the requested geometry free. Only actual invalid inputs or representation limits remain errors, such as zero/negative Cell Size, non-finite coordinates, no surface geometry, singular transforms, or grid coordinates exceeding the 32-bit coordinate representation.

## Validation

Isolated C# compilation and in-memory numeric checks of cap construction were run without launching Unity. Concave L sections, disconnected sections, sections with holes, and overlapping sections preserve their expected areas. A concave L prism preserves volume when cut through its notch, and a subsequent coplanar boundary cut does not invent another solid.

Additional numerical checks cover repeated cuts on all three axes for outward, reversed, and overlapping box shells: signed volume is preserved and the surface-area closure flux is zero within floating-point tolerance. Exterior UV/color/material selection and inheritance into fully internal cells were also checked without Unity rendering.

Manual Unity checks:

- Generate the actual low-poly tree with trunk and crown, then a concave prop, disconnected shells, a shape with a hole, and an open leaf/plane. Inspect partial progress for interior caps, holes, accidental bridges, and backing thickness.
- Try a mesh with multiple materials, vertex colors, and a non-readable import. Compare exterior shape, UVs, texture scale/offset, colors, and source asset/import state at Progress 1.
- Reduce Cell Size beyond the old 4096-cell/500000-vertex budgets. Confirm there is no forced minimum or budget exception. Cancel a larger generation and confirm the previous preview remains intact.
- Scrub 0, intermediate values, and 1; check reversible paths, normals during rotation, flying-fragment bounds, one boundary and multiple simultaneous boundaries.
- Leave progress at 0.5 in Scene View and Play Mode: partial fragments should keep hovering/rotating without moving the slider. Check Wave Strength, Reveal Chaos, and Scatter Distance; progress 1 must remain motionless and progress 0 fully hidden.
- Rebuild and compare cap colors on crown versus trunk, textured versus vertex-colored surfaces, and multiple material slots. Inspect fragments from all six directions, including reversed-winding and overlapping source parts; they should have capped geometry rather than three missing sides.
- Check directional and additional lights, animated shadows, source alpha-cutout materials, and transformed/scaled sources after rebuilding.
- Rebuild repeatedly, clear, disable/remove, reload scripts, and enter/exit Play Mode. Temporary meshes/materials/objects must be released, while source assets and the existing voxel system stay unchanged.
- Unity script/shader compilation and the visual result still require verification in the Editor; neither Unity Editor nor batch mode was launched for these changes.
