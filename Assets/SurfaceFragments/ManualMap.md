# Manual island map

This page covers already baked fragment-island prefab instances placed by hand. For procedural docking of different-size, irregular tiles and runtime prefab instantiation, see [Seeded island maps](GeneratedMap.md). The map controller supplies reveal/discovery and Inspector controls; player movement, a graph-editor package, permanent branch locks, bridges, and save/load are not included. The existing single-island workflow remains usable without a map.

## 1. Define named ports on a source island

A port is a named island-local polyline describing one incoming/outgoing side. It can contain several points and follow an irregular edge. Separate ports remain separate distance sources; they are never averaged or joined by an extra diagonal. Height is ignored, as in the reveal effect.

1. On **Fragment Island Authoring**, configure Boundary Paths using the existing point handles.
2. Click **Create Named Ports From Boundaries**. A closed polygon becomes one port per edge; an open polyline remains one complete port. This button replaces existing port definitions after confirmation.
3. Rename the generated IDs to stable, meaningful names such as `Left`, `Right`, or `NorthGate`. Edit the Ports list and yellow Scene View point handles to adjust the port shapes independently of the default reveal paths. IDs must be nonempty and unique within the island.
4. Save the source prefab and regenerate the linked island. Baking copies the named ports into **Fragment Island Ports** on the generated root. If the source Ports list is empty, baking derives `Edge_1`, `Edge_2`, etc. from its Boundary Paths automatically.

An older generated island can instead be given a Fragment Island Ports component manually, with **Create Named Ports From Boundaries** reading the sibling group's paths. Prefer defining ports on the source and regenerating so future bakes retain the intended definitions. Port IDs are referenced by graph connections; update those references when renaming or removing ports.

Ports can also use **Add Docking Point**: set a root-local Attachment Position, an outward XZ Attachment Normal, and Attachment Width. With an empty Boundary, this generates a straight reveal edge centered on the point. A populated Boundary overrides that generated edge and can follow an irregular contact. Custom attachments control procedural placement; manual maps still use the resolved reveal boundaries without moving their islands. Footprint describes occupied XZ area for the procedural generator and does not change manually placed maps.

## 2. Place and connect island instances

1. Put several generated island prefab instances under an empty map root. Position/rotate/scale them as desired. Keep each island GameObject and Fragment Reveal Group enabled; visibility is controlled through progress.
2. Add **Fragment Island Map** to the empty root. Click **Collect Child Islands** to add its top-level child groups without replacing existing entries. Alternatively, assign Nodes manually. Give each instance a unique node ID, even when several instances use the same prefab.
3. Choose **Initially Open**, or **None (all hidden)**. This only sets the initial visual state; it does not establish a current island. Add Connections using the Island A / Port A / Island B / Port B dropdowns. Both ports must exist on the corresponding generated island roots.
4. Connections describe undirected adjacency and visual influence. They do not permit or prohibit opening an island. The old serialized bidirectional flag is retained for data compatibility and ignored by runtime logic.
5. Click **Validate Graph** and correct any reported missing group, port, or baked data. Selecting the map draws its connections and direction labels in Scene View. These lines visualize the topology; they do not move islands, detect contacts, or create walkable bridges.

A small example is `A -- B`, `A -- C`, `B -- D`, `C -- D`. Give B and C different destination ports on D. Open B and C in either order or simultaneously. D should preview from both contributed sides as they assemble. D can itself be opened at any time, including before B or C.

## 3. Run and inspect discovery

Enter Play Mode. With **Initialize On Start**, the controller applies the optional initially open island, hides the others, and immediately previews its connected neighbors at **Preview Progress** (default 0.2). With no initially open island, everything starts hidden. Disable Initialize On Start to call InitializeMap explicitly after runtime setup.

The map Inspector shows each node's state and an **Open** button for any hidden/preview island, including disconnected nodes. Open animates that island from its current visual state to 1 using its own group's **Reveal Duration**. All fragment renderers within that island share one distance domain and clock. Different islands can animate concurrently with independent durations and start times. Repeated requests for an already opening/open island do not restart its animation. There is no current island, return action, route restriction, or global animation lock.

Once an island is fully revealed, the same button becomes **Close**. Closing uses that group's Reveal Duration and returns it to the neighboring preview, or hides it completely without an influencing neighbor. Its state is Hiding during the animation, and its outgoing influence fades rather than switching off immediately. The button becomes Open during closing, allowing reversal. Closing is available for fully revealed islands; the opening button remains disabled until an initial opening finishes.

Fully revealed and currently assembling islands contribute preview boundaries. Opening an island registers its neighboring previews immediately; their visible portion follows the contributing assembly fronts. A preview never reveals its own neighbors. A hidden island with no revealed/assembling neighbor stays hidden until explicitly opened. Opening, adjacency, and any future gameplay travel system are separate concerns.

Each contributed port carries a smooth coverage profile along its length. The controller samples the donor's mean assembly envelope at 33 points per port segment and maps those points onto the receiving port by normalized XZ arc length. Endpoint order is reversed when that better matches the ports' world-space ends. On an L-shaped route, the receiving edge can therefore grow from its nearer corner toward its farther corner. A short normalized entrance ramp starts a new contribution at zero even when the donor already had a partial preview. Independent contributions combine by maximum coverage, including multiple neighbors sharing one destination port.

When a preview island is opened during another island's animation, it retains its current boundary profiles and front geometry. Its own progress gradually releases that captured profile toward full coverage, so it starts from the existing image and can finish independently of slower donors. New neighbors do not rotate an already running assembly front halfway through the effect. An island opened without an influencing neighbor uses the group's uniform assembly fallback. Coverage calculation and publication occur in separate phases each frame to avoid update-order feedback between concurrent donors.

Existing fronts retain their reached distance to avoid retracting the old preview when the shared maximum distance changes. This preserves a distance front, not a permanent per-fragment mask. Outgoing port segments also contribute a conservative reveal-range bound, so coverage can reach their endpoints smoothly before the completion event even when those endpoints lie beyond the last fragment center. Port correspondence is cached when entry begins or previews are refreshed; keep port shapes and island placement stable during an entry. Per-fragment chaos remains on the GPU, so boundary coupling follows the mean cloud front rather than individual moving fragment positions.

**Preview Progress** is front progress, not an exact percentage of surface area or fragments. Use **Refresh Neighbor Previews** after changing it during Play Mode. **Animate Previews** controls these manual global-progress changes; live neighbor coverage always follows the assembling island. Initial previews from the already revealed starting island are set immediately. **Reset Map** restores the starting state. The graph configuration is locked in the Inspector during Play Mode. Do not also drive these groups with another reveal controller.

## Runtime API

The controller separates graph state (`FragmentMapState`) from scene bindings (`FragmentIslandMap`). A future generator can instantiate existing baked prefabs, supply node IDs/links through Configure before initialization, and then initialize the map. Runtime geometry generation is not required.

```csharp
map.IslandOpened += OnIslandOpened;
map.InitializeMap();

map.OpenIsland("B");
map.OpenIsland("D"); // Works while B is still opening, even without a connection to B.
map.OpenIsland("C", immediate: true);
map.CloseIsland("C");

FragmentMapVisibility visibility = map.GetVisibility("B");
List<string> opened = map.GetIslandIds(FragmentMapVisibility.Revealed);
List<string> opening = map.GetIslandIds(FragmentMapVisibility.Revealing);
List<string> hidden = map.GetIslandIds(FragmentMapVisibility.Hidden);
List<string> previewed = map.GetIslandIds(FragmentMapVisibility.Preview);
List<string> neighbors = map.GetConnectedIslandIds("B");
```

OpenIsland returns true when it begins an opening or completes one immediately. It returns false for an already revealed island, or an already opening island unless immediate is true. CanOpenIsland reports whether a new animated opening can start; it does not check graph reachability. Invalid IDs/configuration produce explicit errors. OpeningIslandCount reports active openings. GetConnectedIslandIds returns unique neighbors regardless of visibility or endpoint order; Connections exposes the full port-pair definitions.

IslandOpened fires once when an island becomes fully revealed, including an explicit immediate opening. Subscribe before InitializeMap if you need the optional initially open island's notification. InitializeMap resets all states/animations. No API stores a player location, teleports the player, or selects a current island. The old CanEnter/TryEnterNode/NodeEntered API was replaced by CanOpenIsland/OpenIsland/IslandOpened; CurrentNodeId and the single RevealingNodeId were removed.

CloseIsland(id) starts an animated close on a fully revealed island; CanCloseIsland reports availability. CloseIsland(id, immediate: true) closes immediately or finishes a running close. IslandClosed fires on completion, when the state becomes Preview or Hidden according to remaining neighbors. GetIslandIds(Hiding) and ClosingIslandCount expose active closes. OpenIsland can reverse a close from its current image and outgoing influence. Closed/preview islands do not initiate a second close.

Configure accepts FragmentIslandMap.Node bindings, FragmentMapConnection links, and an optional initially open island ID. Omit the third argument or pass null to start entirely hidden. The graph and bindings are copied at initialization; rebuild/reset rather than mutating topology during animations. Runtime controls require an enabled map in Play Mode. Referenced groups/data must remain available and enabled. Disabling a group's effect clears its overrides; let the map own visibility instead.

## Manual Unity checks

- Compile/import the runtime scripts, custom Inspectors, and shader. Regenerate an existing source and verify that port IDs/shapes and the generated prefab survive reload. Confirm the source meshes, materials, and unrelated scenes remain unchanged.
- Run the four-node example plus a disconnected island. Open distant, neighboring, and disconnected islands in arbitrary order. Start several before any finishes; each must complete independently, with no return action or global lock. Test both initially open and all-hidden initialization, then Reset Map.
- Repeat OpenIsland on active/completed islands; confirm no restart or duplicate completion event. Use immediate: true to finish an active opening. Check state filters and undirected neighbor queries, including an isolated node and an invalid ID.
- Verify the same Inspector button changes Open → Close → Open. Close an isolated island, then one with an open neighbor; expect Hidden and Preview respectively. Close several concurrently, reverse a close with Open, test immediate closing and Reset Map during a close, and check neighboring previews fade and completion events occur once.
- Open B and C before D. Confirm D previews from both actual ports, with no averaged origin or diagonal between independent paths, and that the old side does not retract when the second side becomes active.
- Test a straight A–B–C chain and an L-shaped chain. While B is still assembling, C must begin its preview as B's front reaches the connected port. At the turn, C's nearer corner should appear first and the influence should grow along the edge. Check no visible start/completion snap, including with Chaos 0, reversed port point order, different port lengths, and multi-segment curves.
- Test irregular multi-point ports, a rotated/scaled island, repeated instances of one prefab, and more than sixteen segments. Inspect yellow point handles and graph connection labels. Test invalid/missing/duplicate IDs and missing data with Validate Graph.
- Use different Reveal Duration values on target groups. Confirm full assembly, hover/scatter while partially visible, exact hidden/full endpoints, synchronized shadows, and no per-model restart or normalization inside an island.
- Change Preview Progress and use Refresh Neighbor Previews. Test lower progress as well as higher progress after a second side has been added. Confirm all fronts disappear at progress 0 and the original mesh surface is complete at 1.
- Use a standalone Fragment Reveal Group without a map and compare the previous effect, paths, playback, and cleanup after disabling/re-enabling. The original Voxelization and SurfaceFragmentPrototype tools should remain unaffected.

Implementation checks used isolated C# compilation and inline graph/distance calculations. Unity Editor, Play Mode, shader compilation, scene/prefab creation, and builds were not run; the visual and lifecycle checks above still require Unity.
