"""Off-game previews of the worm heap, from the REAL shipped meshes (plan 4B-3, revision 3: organic lobes, no core, engulf).

Imports each worm .glb from assets/build ONCE, then creates the instances with bpy.data.objects.new(name, mesh). Placement comes
from `mass_layout.py` (the Python reference of MassLayout.Evaluate) with the game's draw selection: priority threshold, LOD cap,
inner-place culling against the camera. Bodies always use the 240-triangle LOD1 meshes. Also writes
assets/build/mass_layout_golden.json (the parity contract of the C# port) and the shape-change GIF (make_gif.py, Pillow).

Conversion Unity -> Blender: (x, y, z) -> (-x, -z, y). The layout's local frame is (X forward, Y up, Z left); local -> Unity is
(-z, y, x), hence local -> Blender is (z, -x, y). A worm instance has columns (-B(R), -B(F), B(U)) * scale.

Previews in assets/build/previews/ (eye height 1.7 m, 3 m from the rim, warm torch at the camera, dark mine):
  mass_n{10,50,200}_rest.png       one frame at rest          mass_n{10,50,200}_seq.png   4 frames, 1 s apart (the shape changes)
  mass_n200_move.png               on the move                mass_n50_pseudo.png         arm toward a 1.8 m mannequin
  mass_n{50,200}_engulf{05,10}.png engulf = 0.5 and 1         mass_n200_dive45.png        seen from above at 45 degrees
  mass_n50_tread.png               4 frames, 0.25 s apart     mass_lod1_20m.png           LOD1 at 20 m
  mass_n200_shape.gif              24 frames, 0.25 s, n = 200    mass_n200_engulf.gif  engulf 0 -> 1 over 4 s then held 2 s
Run: blender --background --python make_mass_preview.py
"""
import json
import os
import subprocess
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402
import mass_layout as L  # noqa: E402

ML = config.MASS_LAYOUT
_state = {}


def LB(v):
    """local (X fwd, Y up, Z left) -> Blender."""
    return (float(v[2]), -float(v[0]), float(v[1]))


def G(v):
    """local -> glTF-like space understood by common.gltf_to_blender / make_object: (z, y, x)."""
    return (float(v[2]), float(v[1]), float(v[0]))


def import_glb(path):
    import bpy
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    mesh = None
    for o in list(bpy.data.objects):
        if o in before:
            continue
        if o.type == "MESH" and mesh is None:
            mesh = o.data
        bpy.data.objects.remove(o, do_unlink=True)
    return mesh


def load_meshes(build):
    names = (["worm_coil_lod1_%02d" % k for k in range(6)] + ["worm_crawl_%02d" % k for k in range(16)] +
             ["worm_idle_%02d" % k for k in range(8)] + ["worm_crawl_lod1_%02d" % k for k in range(8)] +
             ["worm_idle_lod1_%02d" % k for k in range(4)])
    return {n: import_glb(os.path.join(build, n + ".glb")) for n in names}


def setup_materials(build, meshes):
    worm = C.make_pbr_material("worm", os.path.join(build, "worm_albedo.png"), os.path.join(build, "worm_normal.png"),
                               roughness=0.2, normal_strength=1.0, specular=0.6,
                               gloss_png=os.path.join(build, "worm_gloss.png"), closest=True)
    for m in meshes.values():
        m.materials.append(worm)


def clear_objects():
    import bpy
    for o in list(bpy.context.scene.collection.objects):
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if d is not None and d.users == 0 and d.name not in _state["keep"]:
            if isinstance(d, bpy.types.Mesh):
                bpy.data.meshes.remove(d)
            elif isinstance(d, bpy.types.Light):
                bpy.data.lights.remove(d)
            elif isinstance(d, bpy.types.Camera):
                bpy.data.cameras.remove(d)


def instance_matrix(pos, fwd, up, scale, origin=(0.0, 0.0, 0.0)):
    import mathutils
    f = (float(fwd[0]), float(fwd[1]), float(fwd[2]))
    u = (float(up[0]), float(up[1]), float(up[2]))
    r = L.vcross(L.v3(*u), L.v3(*f))
    s = float(scale)
    cx = [-s * c for c in LB(r)]
    cy = [-s * c for c in LB(f)]
    cz = [s * c for c in LB(u)]
    p = LB((float(pos[0]) + origin[0], float(pos[1]) + origin[1], float(pos[2]) + origin[2]))
    return mathutils.Matrix(((cx[0], cy[0], cz[0], p[0]), (cx[1], cy[1], cz[1], p[1]),
                             (cx[2], cy[2], cz[2], p[2]), (0.0, 0.0, 0.0, 1.0)))


def add_instance(name, mesh, mat):
    import bpy
    o = bpy.data.objects.new(name, mesh)
    o.matrix_world = mat
    bpy.context.scene.collection.objects.link(o)
    return o


def worm_mesh(lod, anim, frame):
    if lod == 0:
        kind = {L.ANIM_CRAWL: "worm_crawl_%02d", L.ANIM_IDLE: "worm_idle_%02d"}.get(anim)
        if kind is not None:
            return _state["meshes"][kind % frame]
    kind = {L.ANIM_COIL: "worm_coil_lod1_%02d", L.ANIM_CRAWL: "worm_crawl_lod1_%02d", L.ANIM_IDLE: "worm_idle_lod1_%02d"}[anim]
    return _state["meshes"][kind % (frame >> 1)]


def populate(params, slots, lod=0, cap=None, origin=(0.0, 0.0, 0.0), tag="h"):
    P = L.MassFrameParams(**params)
    outs = L.evaluate(P, slots)
    cap = cap if cap is not None else (ML["Lod0Cap"] if lod == 0 else ML["Lod1Cap"])
    drawn = L.select_drawn(outs, cap)
    stats = [0, 0, 0]
    for o in drawn:
        li = 1 if (o.role == L.ROLE_BODY or lod == 1 or not o.lod0) else 0
        add_instance("%s_w%d" % (tag, o.i), worm_mesh(li, o.anim, o.frame), instance_matrix(o.pos, o.fwd, o.up, o.scale, origin))
        stats[o.role] += 1
        _state["tris"] = _state.get("tris", 0) + (240 if li == 1 else 766)
    return len(drawn), stats


def ground(size, rgb=(0.13, 0.11, 0.09)):
    v = np.array([[-size, 0, -size], [size, 0, -size], [size, 0, size], [-size, 0, size]], float)
    o = C.make_object("ground", v, np.array([[0, 3, 2], [0, 2, 1]]))
    o.data.materials.append(C.flat_material("ground", rgb, 0.9))


def mannequin(x, z):
    """A 1.8 m capsule (cylinder + two half spheres) at local (x, z)."""
    import bpy
    mat = C.flat_material("mannequin", (0.18, 0.28, 0.42), 0.6)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.28, depth=1.24, location=LB((x, 0.9, z)))
    cyl = bpy.context.active_object
    cyl.data.materials.append(mat)
    for h in (0.28, 1.52):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.28, location=LB((x, h, z)))
        bpy.context.active_object.data.materials.append(mat)


def read_png(path):
    import bpy
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]
    bpy.data.images.remove(img)
    return (np.clip(px[..., :3], 0, 1) * 255 + 0.5).astype(np.uint8)


def heap_params(n, sigma, t, **kw):
    return L.steady_params(n, t, sigma, **kw)


def shot(path, params, cam, target, lens=24, w=1280, h=720, lod=0, mannequin_at=None, spp=32, torch=True):
    """One frame: heap evaluated with the camera direction (inner-place culling), warm torch at the camera."""
    clear_objects()
    cam = np.array(cam, float)
    c = cam / np.linalg.norm(cam)
    params = dict(params)
    params["camL"] = tuple(float(x) for x in c)
    _state["tris"] = 0
    populate(params, _state["slots"], lod)
    print("TRIS %s %d" % (os.path.basename(path), _state["tris"]))
    ground(60.0)
    if mannequin_at is not None:
        mannequin(*mannequin_at)
    if torch:
        C.add_light("POINT", C.gltf_to_blender((cam[2] - 0.35, cam[1] - 0.15, cam[0] - 0.2)), 900.0, color=(1.0, 0.72, 0.42), size=0.15)
        C.add_light("POINT", C.gltf_to_blender((0.0, 4.0, -3.0)), 150.0, color=(0.5, 0.6, 0.9), size=0.5)
    C.add_camera(C.gltf_to_blender(G(cam)), C.gltf_to_blender(G(target)), lens=lens)
    C.configure_render(w, h, spp)
    C.render_to(path)


def set_world(color, strength):
    import bpy
    bg = bpy.context.scene.world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (color[0], color[1], color[2], 1.0)
    bg.inputs["Strength"].default_value = strength


def day_shot(path, params=None, w=1280, h=720):
    """Open-air daylight (Plains): a strong white sun, a bright sky; the worm alone (params None) or the heap."""
    import bpy
    clear_objects()
    set_world((0.55, 0.62, 0.72), 1.0)
    ground(60.0, (0.30, 0.26, 0.17))
    if params is None:
        add_instance("w", _state["meshes"]["worm_crawl_04"], instance_matrix((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), 1.0))
        cam, tgt, lens = (0.45, 0.30, -0.55), (0.0, 0.03, 0.0), 60
    else:
        cam, tgt = player_cam(params["n"], 0.0)
        p = dict(params)
        p["camL"] = tuple(float(x) for x in (np.array(cam) / np.linalg.norm(cam)))
        populate(p, _state["slots"], 0)
        lens = 24
    data = bpy.data.lights.new("Sun", "SUN")
    data.energy = 3.2
    data.angle = 0.02
    o = bpy.data.objects.new("Sun", data)
    o.rotation_euler = (0.9, 0.25, 0.7)
    bpy.context.scene.collection.objects.link(o)
    C.add_camera(C.gltf_to_blender(G(cam)), C.gltf_to_blender(G(tgt)), lens=lens)
    C.configure_render(w, h, 32)
    C.render_to(path)
    set_world((0.02, 0.02, 0.025), 1.0)


def player_cam(n, azim_turns, high=False, extra=3.0):
    r = L.radius_of(n)
    D = r + extra
    ca, sa = float(np.cos(2 * np.pi * azim_turns)), float(np.sin(2 * np.pi * azim_turns))
    return np.array([D * ca, D if high else 1.7, D * sa]), (0.0, 0.3 * r + 0.1, 0.0)


def main():
    t0 = time.time()
    args = C.parse_args()
    build = args["build"]
    prev = C.ensure_dir(args["previews"])
    import bpy
    C.reset_scene()
    C.setup_world((0.02, 0.02, 0.025), 1.0)
    meshes = load_meshes(build)
    setup_materials(build, meshes)
    _state["meshes"] = meshes
    _state["keep"] = set(m.name for m in meshes.values())
    _state["slots"] = L.Slots(L.GOLDEN_SEED)

    with open(os.path.join(build, "mass_layout_golden.json"), "w", encoding="utf8", newline="\n") as fh:
        json.dump(L.golden(), fh, indent=1)
        fh.write("\n")

    T0 = 17.3
    # rest: one frame and a 4 frame sequence 1 s apart
    for n in (10, 50, 200):
        cam, tgt = player_cam(n, 0.0)
        shot(os.path.join(prev, "mass_n%d_rest.png" % n), heap_params(n, 0.0, T0), cam, tgt)
        frames = []
        for k in range(4):
            f = os.path.join(prev, "_seq%d.png" % k)
            shot(f, heap_params(n, 0.0, T0 + 1.0 * k), cam, tgt, w=640, h=360, spp=16)
            frames.append(read_png(f))
            os.remove(f)
        C.write_png(os.path.join(prev, "mass_n%d_seq.png" % n), np.concatenate([np.concatenate(frames[:2], 1), np.concatenate(frames[2:], 1)], 0))
    # daylight: the worm alone and the n = 50 heap under a strong white sun
    day_shot(os.path.join(prev, "mass_worm_day.png"))
    day_shot(os.path.join(prev, "mass_n50_day.png"), heap_params(50, 0.0, T0))
    # on the move (3/4 front view)
    cam, tgt = player_cam(200, 0.12)
    shot(os.path.join(prev, "mass_n200_move.png"), heap_params(200, 1.0, T0), cam, tgt)
    # high view
    cam, tgt = player_cam(200, 0.0, high=True)
    shot(os.path.join(prev, "mass_n200_dive45.png"), heap_params(200, 0.0, T0), cam, tgt)
    # pseudopod toward a mannequin 4 m away
    r = L.radius_of(50)
    D = r + 2.2
    cam, tgt = player_cam(50, -0.25, extra=3.5)
    shot(os.path.join(prev, "mass_n50_pseudo.png"), heap_params(50, 0.0, T0, targetTurns=0.0, targetDist=D, pseudoRamp=1.0),
         cam, (D * 0.5, 0.6, 0.0), mannequin_at=(D, 0.0))
    # engulf
    for n, dist in ((50, 2.0), (200, 3.2)):
        for eg, tag in ((0.5, "05"), (1.0, "10")):
            r = L.radius_of(n)
            cam = np.array([dist * 0.5, 1.7, -(dist + 3.8)])
            shot(os.path.join(prev, "mass_n%d_engulf%s.png" % (n, tag)),
                 heap_params(n, 0.0, T0, targetTurns=0.0, targetDist=dist, engulf=eg), cam, (dist * 0.7, 0.8, 0.0), mannequin_at=(dist, 0.0))
    # tread strip
    frames = []
    for k in range(4):
        t = T0 + 0.25 * k
        clear_objects()
        p = heap_params(50, 1.0, t)
        x0 = 2.4 * 0.25 * (k - 1.5)
        p["camL"] = None
        populate(p, _state["slots"], 0, None, origin=(x0, 0.0, 0.0))
        ground(60.0)
        for i in range(-8, 9):
            vv, ff = C._box((-0.012, 0.0, i * 0.5 - 0.012), (0.012, 0.004, i * 0.5 + 0.012))
            C.make_object("tick%d" % i, vv, ff).data.materials.append(C.flat_material("tick", (0.8, 0.8, 0.8), 0.8))
        C.add_light("POINT", C.gltf_to_blender((-1.0, 1.2, x0 - 3.5)), 900.0, color=(1.0, 0.75, 0.5), size=0.2)
        C.add_camera(C.gltf_to_blender(G((0.0, 1.0, -9.0))), C.gltf_to_blender(G((0.0, 0.5, 0.0))), lens=50)
        C.configure_render(1000, 420, 32)
        f = os.path.join(prev, "_strip%d.png" % k)
        C.render_to(f)
        frames.append(read_png(f))
        os.remove(f)
    C.write_png(os.path.join(prev, "mass_n50_tread.png"), np.concatenate(frames, axis=0))
    # LOD1 at 20 m
    cam = np.array([0.0, 4.0, -19.0])
    clear_objects()
    for n, x in ((50, -3.0), (200, 3.6)):
        populate(heap_params(n, 0.0, T0), _state["slots"], 1, None, origin=(x, 0.0, 0.0), tag="lod%d" % n)
    ground(60.0)
    C.add_light("POINT", C.gltf_to_blender(G((0.0, 3.0, -17.0))), 2500.0, color=(1.0, 0.75, 0.5), size=0.3)
    C.add_camera(C.gltf_to_blender(G(cam)), C.gltf_to_blender(G((0.0, 0.8, 0.3))), lens=42)
    C.configure_render(1280, 720, 32)
    C.render_to(os.path.join(prev, "mass_lod1_20m.png"))
    # GIF: n = 200, 24 frames 0.25 s apart, player view
    gif_dir = C.ensure_dir(os.path.join(prev, "_gif"))
    cam, tgt = player_cam(200, 0.0)
    for k in range(24):
        shot(os.path.join(gif_dir, "f%02d.png" % k), heap_params(200, 0.0, T0 + 0.25 * k), cam, tgt, w=640, h=360, spp=16)
    gif = os.path.join(prev, "mass_n200_shape.gif")
    script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "make_gif.py")
    try:
        subprocess.run(["py", "-3", script, gif_dir, gif, "250"], check=True)
    except Exception as e:  # Pillow lives in the system Python, not in Blender's
        print("GIF not assembled (%s): frames stay in %s" % (e, gif_dir))
    # GIF: n = 200 engulfing the mannequin: engulf 0 -> 1 over 4 s, then held for 2 s (24 frames, 0.25 s)
    gif_dir = C.ensure_dir(os.path.join(prev, "_gif"))
    dist = 3.2
    cam = np.array([dist * 0.5, 1.7, -(dist + 3.8)])
    for k in range(24):
        eg = min(1.0, 0.25 * k / 4.0)
        shot(os.path.join(gif_dir, "f%02d.png" % k), heap_params(200, 0.0, T0 + 0.25 * k, targetTurns=0.0, targetDist=dist, engulf=eg),
             cam, (dist * 0.7, 0.8, 0.0), w=640, h=360, spp=16, mannequin_at=(dist, 0.0))
    gif = os.path.join(prev, "mass_n200_engulf.gif")
    try:
        subprocess.run(["py", "-3", script, gif_dir, gif, "250"], check=True)
    except Exception as e:
        print("GIF not assembled (%s): frames stay in %s" % (e, gif_dir))
    C.stats("make_mass_preview", t0)


if __name__ == "__main__":
    main()
