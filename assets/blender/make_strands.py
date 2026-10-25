"""Worm STRANDS (V2 visual rework, pass V2-A): 6 worms twisted into a rope along a gently curved axis of about 40 cm (mesh along Z), for the
flows and the edges of a heap. Flipbook of 12 frames (a perfect loop: the worms undulate and slide along their helix), LOD0 (6 x 170 triangles)
and LOD1 (6 x 48, frames 2j at half the rate). Same worm material and textures as the knots (make_knots.py provides the curves and the tubes).
Worm i winds around the axis with a phase of 120 degrees x i, over its own stretch of the axis (they are staggered along it), so every point of the
strand is crossed by two or three worms that touch each other. Origin: centroid of the vertices of the 12 frames.
Run: blender --background --python make_strands.py
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402
import make_worm as W  # noqa: E402
import make_knots as K  # noqa: E402

AXIS_LEN = 0.40
RHO = 0.0042             # distance of a worm to the axis (two worms 120 degrees apart: 0.73 cm centre to centre, they touch)
PITCH = 0.07             # long pitch: the rings (18 per worm, 0.8 cm apart) follow the helix without zigzag
RINGS, AROUND = 18, 5    # LOD0 worm of a strand: 2 x 5 x 17 = 170 triangles
RINGS1, AROUND1 = 9, 3   # LOD1: 2 x 3 x 8 = 48


def build_strand(n=config.STRAND_WORMS):
    rng = np.random.default_rng([config.SEED, 7101])
    z = np.linspace(-AXIS_LEN / 2, AXIS_LEN / 2, 4000)
    axis_pts = np.stack([0.03 * np.sin(2 * np.pi * z / AXIS_LEN + 0.5), 0.006 * np.sin(4 * np.pi * z / AXIS_LEN + 1.0), z], 1)
    axis = K.Curve(axis_pts)
    stretch = np.sqrt(1.0 + (2 * np.pi * RHO / PITCH) ** 2)
    seg = K.PATH_LEN / stretch * 1.02
    specs = []
    for i in range(n):
        a0 = (axis.length - seg - K.STEP) * i / (n - 1)
        pts = K.helix_points(axis, a0, a0 + seg, RHO, PITCH, 2 * np.pi * i / 3.0)
        w = K.window(pts, 0.0, K.PATH_LEN)
        assert w is not None
        specs.append({"curve": K.Curve(w), "kind": "strand", "gen": None, "flip": bool(i % 2), "psi": rng.uniform(0, 2 * np.pi), "twist": 0.0,
                      "amp": rng.uniform(0.0015, 0.0025), "lam": rng.uniform(0.055, 0.075), "m": 1,
                      "ph": rng.uniform(0, 2 * np.pi), "slide_ph": rng.uniform(0, 2 * np.pi) + 2 * np.pi * i / n})
    return specs


def render_preview(build, prev, d):
    mat = K.setup_scene(build)
    v = d["frames"][2]
    K.add_mesh("strand", v + np.array([0.0, -v[:, 1].min(), 0.0]), d["faces"], d["uvs"], mat)
    C.add_ground(2.0, (0.30, 0.30, 0.32))
    C.add_scale_bar((-0.2, 0.0, 0.16), 0.4, 0.05, height=0.003, depth=0.01)
    C.add_light("AREA", C.gltf_to_blender((0.5, 0.9, 0.7)), 22, size=0.8, target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-0.7, 0.5, -0.2)), 8, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_camera(C.gltf_to_blender((0.30, 0.34, 0.52)), C.gltf_to_blender((0.0, 0.02, 0.0)), lens=50)
    C.configure_render(1400, 800, 64)
    C.render_to(os.path.join(prev, "strand_34.png"))


def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    C.reset_scene()
    specs = build_strand()
    n = len(specs)
    st = K.analyse(specs, (0, 3, 6, 9))
    if not st["connected"] or st["min_deg"].min() < 1:
        raise RuntimeError("strand: worms do not touch each other (%s)" % st)
    f0 = [K.knot_verts(specs, k, RINGS, AROUND) for k in range(K.FRAMES)]
    f1 = [K.knot_verts(specs, 2 * j, RINGS1, AROUND1) for j in range(K.FRAMES // 2)]
    c = np.concatenate(f0, 0).mean(0)
    f0 = [v - c for v in f0]
    f1 = [v - c for v in f1]
    faces0, uvs0, _ = K.knot_topology(n, RINGS, AROUND)
    faces1, uvs1, _ = K.knot_topology(n, RINGS1, AROUND1)
    ext = np.concatenate(f0, 0)
    print("strand_a: %d worms, LOD0 %d tris / LOD1 %d tris, extent %s, contacts: mean %.2f per worm (min %d)" %
          (n, len(faces0), len(faces1), np.round(ext.max(0) - ext.min(0), 3), st["mean_deg"], st["min_deg"].min()))
    obj = C.make_object("strand_a", f0[0], faces0, uvs0)
    for k, v in enumerate(f0):
        C.set_positions(obj, v)
        C.export_glb(obj, os.path.join(out, "strand_a_%02d.glb" % k))
    obj1 = C.make_object("strand_a_lod1", f1[0], faces1, uvs1)
    for j, v in enumerate(f1):
        C.set_positions(obj1, v)
        C.export_glb(obj1, os.path.join(out, "strand_a_lod1_%02d.glb" % j))
    render_preview(out, prev, {"frames": f0, "faces": faces0, "uvs": uvs0})
    C.stats("make_strands", t0)


if __name__ == "__main__":
    main()
