# Task: improve `unity/blender/build_avatar.py` (v2)

Keep the same CLI, outputs (12 GLBs + 4 previews), material names and file names as in `unity/briefs/codex-avatar.md`. Edit only `unity/blender/build_avatar.py`.

I rendered v1; the previews are in `unity/blender/previews_v1/` (look at them). Problems to fix:

1. **Crystals are too few and too small.** The statue must look like it is *being overtaken by gold crystal*. Make 5–8 dense clusters: a large cluster (30–45 shards, the biggest ≈ 0.22 units tall) rising from the pedestal top on one side and climbing the leg; a cluster bursting from one shoulder and the upper back (15–25 shards); smaller seeds on the opposite forearm and the head/hat area (crown-like for the goblin). Shards: tapered hexagonal prisms with sharp pyramid tips, varied thickness, many branching at 20–40° from a parent shard, oriented along the surface normal with jitter. Some shards should penetrate into the body so they look grown, not stuck on.
2. **The figure reads as a jointed wooden mannequin.** Make it one smooth sculpted statue: merge all body parts with a voxel remesh (≈0.004) and smooth it (Corrective Smooth or a few iterations of smooth), then decimate to budget. No visible ball joints. Elegant elongated statuette proportions (long legs, small head, defined but streamlined shoulders, hands as simple tapered mitten shapes).
3. **Clothing silhouettes must read clearly**: gentleman — jacket flare + lapel ridges + trouser crease; lady — fitted knee-length dress with a slight A-line and a hair bun; gown — long flowing gown with folds (add 6–8 vertical pleat ridges) and a train sweeping onto the pedestal; goblin — exaggerated ears, big nose, crouched posture, long arms, oversized feet, a small cape.
4. **Pedestal too dominant.** Pedestal height ≈ 0.22 of total (not 0.35+). Two slim tiers with a gold rim between them; the figure + crystals ≈ 0.78 of the height.
5. **Previews**: camera a bit lower and closer so the figure fills ~80% of the frame; add a warm key, a cyan rim from behind-left, and a soft fill; use Eevee with bloom-like glare if available (or Compositor glare node) so crystals sparkle.
6. Keep each GLB ≤ 35k triangles. Keep it deterministic and robust in `--background`. Comments in Japanese.

At the end print what you changed.
