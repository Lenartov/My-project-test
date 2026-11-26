# Project instructions

Keep communicating with the user in Ukrainian. Write code, identifiers, code comments, and technical names in English. Preserve the user's other working and safety instructions.

## Island modeling and fragment pipeline

For island modeling, Blender/MCP export, reusable art assets, fragment baking, or island import tasks:

1. Read [Docs/IslandPipeline/README.md](Docs/IslandPipeline/README.md) first, then the relevant linked guide.
2. Distinguish implemented behavior from the documented target pipeline. Cross-island Mesh/data caching is implemented in the baker; the JSON importer, Blender exporter, logical asset registry, and shared material cache remain future work. Check the guides and current code before claiming a capability is ready.
3. Reuse canonical assets by stable `asset_id`; preserve `island_id`, `instance_id`, and port IDs when updating content. Do not duplicate geometry for placement-only changes.
4. Preserve editable source islands and the existing single-island bake/reveal workflow. Treat generated prefabs as derived output.
5. Do not launch Unity, run batch mode/builds, add dependencies or tests, or mutate Git state without the required user authorization. Creating or materially modifying serialized Unity assets requires explicit user authorization; a documentation request alone does not authorize those changes.

Changes outside this pipeline should follow their own relevant project context; these guides do not require refactoring unrelated systems.
