# Seeded island maps

FragmentIslandMapGenerator docks already baked island prefabs through named attachment points. Tiles can have different sizes, arbitrary proportions, concave outlines, and any number of ports. There is no grid, size multiple, or restriction to quarter-turn rotations. The generated graph drives the existing FragmentIslandMap reveal/discovery controller; no geometry is cut at runtime.

## Quick setup

1. Add **Fragment Island Map Generator** to an empty scene root outside any Fragment Reveal Group. Leave room for its output; unrelated scene objects are not included in placement collision checks or removed by generation.
2. Add baked prefab assets to **Templates**. The prepared **Assets/Islands/Island_Source/Generated/Island.prefab** works with its existing West/East/North/South ports. Direction-name mappings are no longer needed.
3. Set Seed, Island Count, Extra Connection Chance, and Island Gap. Gap defaults to **0** for new components: connected attachment points coincide. Existing serialized Gap values are retained. Open First Island optionally starts Island_000 fully revealed.
4. Click **Preview Layout** in Edit Mode. Scene View shows actual footprints, template names, and connections. This reads saved prefab data and creates no scene objects or assets. After editing/rebaking templates, click Preview Layout again.
5. Enter Play Mode. Generate On Start creates the map; otherwise click **Generate Map**. Use each island's **Open / Close** button in any order, including concurrent animations. **Select Runtime Map** exposes preview settings and graph state.
6. **Regenerate Map** replaces this generator's output. **Clear Generated Map** removes only its runtime hierarchy. Changing settings alone does not rebuild a running map.

Generation does not modify source or baked assets. Runtime instances share baked meshes/data/materials. Multiple generators remain independent, but do not collision-check one another.

## Author tile shapes and docking points

Configure these on **Fragment Island Authoring**, save the source prefab, then **Regenerate Linked Island**. The baker copies the metadata to **Fragment Island Ports** on the generated root. Editing the generated prefab directly is temporary: another bake replaces its metadata from the source.

### Occupied footprint

**Footprint** is an ordered polygon in root-local XZ; the Vector2 Y field means Z. It is implicitly closed. Both winding orders and simple concave polygons are supported. Holes and self-intersecting outlines are not supported.

- **Set Bounds Footprint** creates a rectangular starting outline from model bounds.
- **Copy Closed Boundary To Footprint** copies the first enabled closed reveal path.
- Edit the list to add/remove points, and drag green handles in Scene View to fit the actual platform.
- An empty footprint uses the combined baked mesh bounds. This keeps existing prefabs usable but conservatively treats concave notches and decorative overhangs as occupied.

The generator rejects intersections between occupied polygon interiors. Touching edges/corners are allowed, with a small numerical tolerance. Collision checks are planar, without physics, colliders, or runtime mesh vertex reads. Include all geometry that must stay clear in the authored footprint; geometry outside it is not checked. The scattering animation can temporarily extend beyond it.

### Docking ports

Click **Add Docking Point** for each allowed connection, then set:

- **Id**: unique stable name, referenced by the graph.
- **Use Custom Attachment**: enabled by this button.
- **Attachment Position**: root-local contact point, normally on the footprint edge.
- **Attachment Normal**: nonzero outward XZ direction. Scene View has a yellow position handle, rotation handle, and direction arrow. Magnitude does not matter; Y is ignored.
- **Attachment Width**: positive reveal-edge length when Boundary is empty. This controls the visual entrance, not clearance or placement quantization.

An empty Boundary generates a straight reveal edge centered on the docking point and perpendicular to its normal. To describe a curved contact, supply Boundary points instead; those take precedence for reveal influence. A disabled Boundary makes the port invalid.

Existing ports also work with **Use Custom Attachment** off: their boundary's XZ arc-length midpoint is the attachment, and the closest footprint edge determines the outward normal. Use explicit attachments for corners, ambiguous outlines, and precise art alignment.

Each port accepts one connection. Put multiple ports along the same side to allow multiple neighbors. Names need not be cardinal, and connected ports need not have equal boundary lengths. For long sides with several neighbors, give each port the appropriate part of the reveal edge rather than repeating the entire side.

### Example: a 1.5-length side with two smaller neighbors

For a tile one unit wide and 1.5 units long, use footprint corners `(-0.5,-0.75)`, `(0.5,-0.75)`, `(0.5,0.75)`, `(-0.5,0.75)`.

Add two custom ports on its east side:

| Port | Attachment Position (X,Y,Z) | Outward normal | Width |
| --- | --- | --- | --- |
| EastLower | (0.5,0,-0.5) | (1,0,0) | 1 |
| EastUpper | (0.5,0,0.5) | (1,0,0) | 1 |

A one-unit square neighbor can use a west attachment at `(-0.5,0,0)` with normal `(-1,0,0)`. At Gap 0, the two neighbors sit at Z -0.5 and +0.5 and may overhang the long tile by 0.25 at either end. Their footprints touch without overlap. No two-times size rule is involved. The random catalog/seed determines which valid tiles are actually selected.

## Placement and graph behavior

The first randomly selected template uses its local pivot at the generator origin. Each new tile rotates about local Y until its chosen port faces opposite an existing free port. Translation aligns both anchors, including height, with optional Gap along the existing port's outward direction. Prefab root scale and child transforms are preserved; root rotation is chosen by the planner. Author maps upright because the effect and overlap tests use XZ.

The planner tries template/port combinations against every placed footprint. A rejected overlap does not forbid a different fitting candidate. Successful docking adds an undirected graph edge and consumes both ports. Extra Connection Chance adds edges only where two remaining free ports already face each other and meet at the requested gap; proximity alone creates no connection.

Placement uses a finite greedy search. If no available port can accept another template, the connected partial map is retained and the Inspector/runtime warning reports **Placed / Requested**. It does not force overlaps or guarantee that every seed/catalog can fill the requested count. More compatible ports/templates or another seed can improve packing.

Each template must be an active saved prefab with one enabled root FragmentRevealGroup, baked FragmentRevealRenderers/data, a valid footprint, and at least one valid port. Nested reveal groups are not accepted as part of one island. Footprints can have unrelated shapes and dimensions. The planner does not infer doors from mesh geometry, deform edges, stitch meshes, or build bridges. Gap 0 joins authored points exactly; a continuous full seam still requires compatible source geometry.

Connections carry adjacency and reveal influence, without travel restrictions or a current island. Open/Close, synchronized per-island assembly, and live neighbor coverage remain unchanged, including rotated/unequal-length port boundaries. Preview nodes do not propagate discovery. See [Manual island map](ManualMap.md) for the graph API and coverage behavior.

A private seeded System.Random chooses templates, frontier ports, candidates, and extra edges without affecting UnityEngine.Random. The same seed, settings, ordered catalog, and saved template metadata reproduce the layout within the current generator/runtime version. Changing geometry, ports, catalog, algorithm, or random implementation can change it. Old grid seeds intentionally produce different layouts. Seed alone is not a complete save format.

## Runtime integration

```csharp
generator.MapGenerated += OnMapGenerated;
generator.GenerateMap(); // Enabled generator in Play Mode.

FragmentIslandMap map = generator.GeneratedMap;
map.OpenIsland("Island_006"); // Check the placed count if packing was partial.
map.OpenIsland("Island_002");
map.CloseIsland("Island_002"); // After fully revealed.
List<string> neighbors = map.GetConnectedIslandIds("Island_006");
List<string> opened = map.GetIslandIds(FragmentMapVisibility.Revealed);

FragmentIslandLayout plan = generator.CreateLayout(); // No scene instantiation.
// Placements: templateIndex, generator-local position, yaw.
// Links: node indices and the two docking-port indices.
```

Subscribe before Start/GenerateMap to receive the initialized controller. Regeneration replaces it, so replace references/subscriptions when MapGenerated fires again. Existing OpenIsland, CloseIsland, state queries, and completion events retain their contracts.

FragmentIslandLayout.Generate accepts an ordered collection of Template descriptors (XZ polygon plus Socket positions/normals), seed, count, gap, and extra-connection chance. Templates, Placements, Links, GetFootprint, and GetSocketPosition/Normal expose the plan. IsComplete/Diagnostic distinguish a complete layout from exhausted packing. Grid Cells/CellSpacing and CreateLayout(out spacing) were replaced by these shape/pose APIs.

The old map remains until a replacement has been instantiated and initialized. Invalid input/staging failure leaves it intact. Successful replacement deactivates the old hierarchy immediately and destroys it normally. Do not independently delete/move generated children during use; moving islands invalidates the layout and cached reveal correspondence.

## Manual Unity checks

- Compile/import scripts. Preview the existing prepared prefab without rebaking; inspect bounds-based outlines and connections, with no new scene objects/assets.
- Author and bake the 1.5-length example with two small neighbors. Check overhang, two independent connections, Gap 0, and a positive Gap. For a predictable example, use only the long tile and single-port small tiles and inspect several seeds.
- Mix rectangles, five-sided tiles, and an L-shaped concave footprint. Verify a tile can fit inside the L's notch while actual occupied areas do not overlap. Check arbitrary yaw and multiple points along one side.
- Check off-center pivots, rotated/scaled child models, and nonuniform/mirrored prefab root scales. Inspect a moved/rotated generator root. Keep platform elevations and source seams compatible.
- Compare preview and runtime poses/template choices with the same seed. Regenerate with another seed; check Island Count 1 and Extra Connection Chance 0/1.
- Use a single-port-only catalog with a larger requested count. Expect a connected two-tile partial result and a clear warning, without forced overlap or hanging generation.
- Test duplicate/empty port IDs, invalid normals, self-crossing/zero-area footprints, missing data, scene objects as templates, and nested groups. Invalid regeneration must retain the prior runtime map.
- Open arbitrary islands concurrently and close/reopen them. Verify shared group duration, synchronized fragments/shadows, and live neighbor previews through straight paths, turns, unequal boundary lengths, and several incoming ports.
- Edit source footprints/ports, save, regenerate the linked prefab, and preview again. Confirm baked metadata updates and source model assets remain unchanged.
- Regenerate/clear during assembly, test two generators independently, and exit Play Mode. Check normal cleanup and no persistent generated hierarchy in the source scene.

Implementation validation used isolated player/editor C# compilation and inline geometry calculations: concave overlap/contact, the 1.5-length example, arbitrary pentagon rotation, and 50 mixed-template seeded layouts with zero/positive gaps. Unity was not launched; actual import, prefab instantiation, Scene View controls, and visual/runtime behavior require manual Unity verification.
