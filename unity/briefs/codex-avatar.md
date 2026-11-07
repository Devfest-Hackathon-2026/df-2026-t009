# Task: Blender Python script that builds the "onsite" trophy-statuette avatars

Write ONE Blender 4.5 Python script at `unity/blender/build_avatar.py` (and nothing else; delete any older draft of it). Do not run Blender yourself; I will run it with:

```
blender --background --factory-startup --python unity/blender/build_avatar.py -- --out unity/onsite/Assets/Onsite/Models
```

## Look (original design; do NOT copy the Oscar statuette, The Game Awards trophy, or any game asset)
Each avatar is a premium **award-trophy statuette**: an elongated, stylized, smooth figure (art-deco / streamlined, few sharp details, idealized proportions ~8.5 heads tall) standing on a **stepped cylindrical pedestal** (2–3 tiers, the top tier slightly narrower, subtle bevels). Total height ≈ 1.0 unit, figure ≈ 0.68 of it.

Fused with it: **gold crystal growth** — clusters of angular, branching crystal shards (tapered hexagonal prisms with pointed tips, varied sizes, rotated outward, a few branching) that grow from the pedestal top and partly encrust one shoulder, the back and one leg, like the statue is crystallising.

### Body types (each one a separate mesh/silhouette)
- `gentleman` – man in a tailored suit: jacket with lapels and tails hint, trousers, smooth shoes.
- `lady` – woman in a fitted short dress / slim silhouette, hair bun.
- `gown` – woman in a long flowing evening gown that flares to the pedestal (no visible legs).
- `goblin` – a small goblin: big pointed ears, hunched, long arms, big feet, mischievous; same statuette treatment.

### Poses
- `stand` – upright, arms relaxed (gentleman/lady/gown) or hands together (goblin).
- `wave` – right arm raised waving.
- `pose` – a proud trophy pose: both hands holding a small crystal shard against the chest (like a statuette holding an object).

Build each figure from well-shaped primitives combined smoothly (use Subdivision + Remesh (voxel) or metaballs, then decimate) so it reads as ONE sculpted, continuous statue, not loose parts. Prefer per-pose rigid modelling over armatures if more robust in background mode.

## Materials (names are important; Unity remaps them)
- `Body` – polished metal, base color #d9a441 gold, metallic 1, roughness 0.18.
- `Crystal` – glassy crystal, base #ffcf6b, metallic 0.2, roughness 0.05, transmission 0.6 (glTF KHR_materials_transmission ok).
- `Base` – black lacquer, base #0b0d10, metallic 0.3, roughness 0.25, with a thin gold rim ring using `Body`.

## Export
For every body type × pose export `avatar_<type>_<pose>.glb` (12 files) into `--out`. glTF defaults (+Y up), origin at the center bottom of the pedestal, height ≈ 1.0, transforms applied, triangulated, ≤ 30k triangles each, materials included, no cameras/lights.
Also render `avatar_<type>_preview.png` (512×512, Eevee, 3/4 front view, dark background #05080a, warm key light + cyan rim light) for each body type in `stand` pose, so I can review the look.

## Code quality
- Pure `bpy` / `bmesh` / `mathutils`, deterministic (seeded random).
- Parse `--out` from `sys.argv` after `--`. Clear the scene at start and between exports.
- Functions such as `make_pedestal()`, `make_gentleman(pose)`, `make_lady(pose)`, `make_gown(pose)`, `make_goblin(pose)`, `make_crystals(seed, anchors)`, `export(name)`, `render_preview(name)`.
- Robust in `--background` (no UI-context operators; use `bpy.context.temp_override` where needed).
- Comments in Japanese, short. Print the list of files written at the end.
