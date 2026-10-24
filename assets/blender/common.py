"""Shared helpers for the headless Blender asset scripts (spec §13.1).

Conventions: all geometry is authored in glTF space (x right, y up, z forward, metres) as numpy
arrays and converted to Blender's Z-up space only when a bpy object is created (a pure rotation,
so triangle winding is preserved). Textures are generated with numpy and written with a tiny PNG
writer: reliable in --background and byte-for-byte deterministic. UVs are authored in glTF
convention (v = 0 at the TOP row of the image) and flipped when stored in Blender.
"""
import os
import sys
import time
import zlib
import struct

import numpy as np
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402


# --------------------------------------------------------------------------- args / paths
def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = None
    for i, a in enumerate(argv):
        if a == "--repo-root" and i + 1 < len(argv):
            root = argv[i + 1]
    if root is None:
        root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    return {"root": root,
            "build": os.path.join(root, "assets", "build"),
            "previews": os.path.join(root, "assets", "build", "previews"),
            "thunderstore": os.path.join(root, "thunderstore")}


def ensure_dir(p):
    os.makedirs(p, exist_ok=True)
    return p


# --------------------------------------------------------------------------- math helpers
def smoothstep(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def normalize(v, axis=-1):
    n = np.linalg.norm(v, axis=axis, keepdims=True)
    return v / np.maximum(n, 1e-12)


def gltf_to_blender(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([v[..., 0], -v[..., 2], v[..., 1]], axis=-1)


def lerp(a, b, t):
    return a + (b - a) * t


# --------------------------------------------------------------------------- noise (numpy)
def _hash_u32(x):
    x = np.asarray(x).astype(np.int64).astype(np.uint32)
    with np.errstate(over="ignore"):
        x = x ^ (x >> np.uint32(16))
        x = x * np.uint32(0x7FEB352D)
        x = x ^ (x >> np.uint32(15))
        x = x * np.uint32(0x846CA68B)
        x = x ^ (x >> np.uint32(16))
    return x


def _lattice3(ix, iy, iz, seed):
    h = _hash_u32(ix.astype(np.int64) + np.int64(seed) * 7919)
    h = _hash_u32(h.astype(np.int64) + iy.astype(np.int64) * 31337)
    h = _hash_u32(h.astype(np.int64) + iz.astype(np.int64) * 1000003)
    return h.astype(np.float64) / 4294967295.0


def value_noise3(p, seed=0):
    """Smooth value noise in [0,1] at points p of shape (N,3)."""
    p = np.asarray(p, dtype=np.float64)
    i = np.floor(p)
    f = p - i
    u = f * f * f * (f * (f * 6 - 15) + 10)
    ix, iy, iz = i[:, 0], i[:, 1], i[:, 2]
    out = 0.0
    for dx in (0, 1):
        wx = u[:, 0] if dx else 1 - u[:, 0]
        for dy in (0, 1):
            wy = u[:, 1] if dy else 1 - u[:, 1]
            for dz in (0, 1):
                wz = u[:, 2] if dz else 1 - u[:, 2]
                out = out + wx * wy * wz * _lattice3(ix + dx, iy + dy, iz + dz, seed)
    return out


def fbm3(p, seed=0, octaves=4, lacunarity=2.0, gain=0.5):
    total = 0.0
    amp = 1.0
    norm = 0.0
    q = np.asarray(p, dtype=np.float64)
    for o in range(octaves):
        total = total + amp * value_noise3(q + o * 17.3, seed + o)
        norm += amp
        amp *= gain
        q = q * lacunarity
    return total / norm


def ridged3(p, seed=0, octaves=3, sharp=2.0):
    """Ridge noise in [0,1]: 1 along thin crest lines, which read as snaking worm bodies."""
    total = 0.0
    amp = 1.0
    norm = 0.0
    q = np.asarray(p, dtype=np.float64)
    for o in range(octaves):
        n = value_noise3(q + o * 31.7, seed + 11 * o)
        r = (1.0 - np.abs(2.0 * n - 1.0)) ** sharp
        total = total + amp * r
        norm += amp
        amp *= 0.5
        q = q * 2.0
    return total / norm


def noise2_tile(h, w, cells_x, cells_y, seed):
    """Tileable 2D value noise in [0,1], shape (h,w); cells_* = lattice cells across the image."""
    r = np.random.default_rng([config.SEED, seed, cells_x, cells_y])
    lat = r.random((cells_y, cells_x))
    xs = np.arange(w) / w * cells_x
    ys = np.arange(h) / h * cells_y
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    fx = xs - x0
    fy = ys - y0
    fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
    x1 = (x0 + 1) % cells_x
    y1 = (y0 + 1) % cells_y
    x0 = x0 % cells_x
    y0 = y0 % cells_y
    a = lat[y0][:, x0]
    b = lat[y0][:, x1]
    c = lat[y1][:, x0]
    d = lat[y1][:, x1]
    top = a + (b - a) * fx[None, :]
    bot = c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fbm2_tile(h, w, cells, seed, octaves=4, gain=0.5, aspect=1.0):
    total = np.zeros((h, w))
    amp = 1.0
    norm = 0.0
    cx = cells
    for o in range(octaves):
        cy = max(1, int(round(cx * aspect)))
        total += amp * noise2_tile(h, w, int(cx), cy, seed * 13 + o)
        norm += amp
        amp *= gain
        cx *= 2
    return total / norm


def voronoi_tile(h, w, cells_x, cells_y, seed):
    """Tileable Worley noise. Returns (F1, F2) distances in cell units."""
    r = np.random.default_rng([config.SEED, seed, cells_x, cells_y, 77])
    pts = r.random((cells_y, cells_x, 2))
    xs = (np.arange(w) + 0.5) / w * cells_x
    ys = (np.arange(h) + 0.5) / h * cells_y
    cxi = np.floor(xs).astype(int)
    cyi = np.floor(ys).astype(int)
    f1 = np.full((h, w), 1e9)
    f2 = np.full((h, w), 1e9)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            nx = cxi + dx
            ny = cyi + dy
            px = pts[ny % cells_y][:, nx % cells_x, 0] + nx[None, :]
            py = pts[ny % cells_y][:, nx % cells_x, 1] + ny[:, None]
            d = np.sqrt((px - xs[None, :]) ** 2 + (py - ys[:, None]) ** 2)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            f1 = np.where(closer, d, f1)
    return f1, f2


def box_blur(a, radius):
    """Separable wrapping box blur used to soften height fields before deriving normals."""
    if radius <= 0:
        return a
    k = 2 * radius + 1
    out = a
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for s in range(-radius, radius + 1):
            acc += np.roll(out, s, axis=axis)
        out = acc / k
    return out


def normal_map_from_height(hmap, strength):
    """Tangent-space normal map, OpenGL convention (+Y green = up in the image), uint8 RGB."""
    dcol = (np.roll(hmap, -1, axis=1) - np.roll(hmap, 1, axis=1)) * 0.5
    drow = (np.roll(hmap, -1, axis=0) - np.roll(hmap, 1, axis=0)) * 0.5
    n = np.stack([-dcol * strength, drow * strength, np.ones_like(hmap)], axis=-1)
    n = normalize(n)
    return np.clip((n * 0.5 + 0.5) * 255.0 + 0.5, 0, 255).astype(np.uint8)


def to_u8(rgb01):
    return np.clip(np.asarray(rgb01) * 255.0 + 0.5, 0, 255).astype(np.uint8)


# --------------------------------------------------------------------------- PNG
def write_png(path, arr):
    """8-bit RGB/RGBA PNG, no external dependency, deterministic output."""
    arr = np.ascontiguousarray(arr, dtype=np.uint8)
    h, w, c = arr.shape
    ctype = {3: 2, 4: 6}[c]
    raw = np.concatenate([np.zeros((h, 1), np.uint8), arr.reshape(h, w * c)], axis=1).tobytes()

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, ctype, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


# --------------------------------------------------------------------------- Blender scene
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_object(name, verts, faces, loop_uv=None):
    """Create a smooth-shaded triangle mesh object. verts: (V,3) glTF space, faces: (F,3),
    loop_uv: (F,3,2) in glTF uv convention (v=0 at image top)."""
    faces = np.asarray(faces, dtype=np.int64)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(gltf_to_blender(verts).tolist(), [], faces.tolist())
    mesh.update()
    if loop_uv is not None:
        uv = mesh.uv_layers.new(name="UVMap")
        t = np.array(loop_uv, dtype=np.float64).reshape(-1, 2)
        t[:, 1] = 1.0 - t[:, 1]
        uv.data.foreach_set("uv", t.reshape(-1).astype(np.float32))
    mesh.polygons.foreach_set("use_smooth", np.ones(len(faces), dtype=bool))
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def set_positions(obj, verts):
    """Re-pose an existing object: keeps topology and UVs bit-identical across frames."""
    co = gltf_to_blender(verts).astype(np.float32).reshape(-1)
    obj.data.vertices.foreach_set("co", co)
    obj.data.update()


def check_outward(verts, faces, name):
    """Cheap winding sanity check: face normals should point away from the mesh centre on average."""
    v = np.asarray(verts)
    f = np.asarray(faces)
    c = v.mean(axis=0)
    n = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    cen = v[f].mean(axis=1) - c
    score = float((np.einsum("ij,ij->i", n, cen) > 0).mean())
    if score < 0.7:
        raise RuntimeError("%s: only %.0f%% of triangles face outward (winding wrong?)" % (name, score * 100))


def export_glb(obj, path):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    kwargs = dict(
        filepath=path, export_format="GLB", use_selection=True, export_apply=True,
        export_yup=True, export_normals=True, export_texcoords=True, export_materials="NONE",
        export_animations=False, export_skins=False, export_cameras=False, export_lights=False,
        export_extras=False, export_tangents=False)
    bpy.ops.export_scene.gltf(**kwargs)


# --------------------------------------------------------------------------- preview rendering
def make_pbr_material(name, albedo_png, normal_png, roughness=0.3, normal_strength=1.0, specular=0.5,
                      gloss_png=None, closest=False):
    """gloss_png: RGBA map whose alpha is the lustre; roughness is then 1 - lustre (same convention as the game).
    closest: nearest-neighbour sampling, like the Point filter used in game."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = roughness
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = specular
            break
    img = bpy.data.images.load(albedo_png)
    img.colorspace_settings.name = "sRGB"
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest" if closest else "Linear"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if gloss_png:
        gimg = bpy.data.images.load(gloss_png)
        gimg.colorspace_settings.name = "Non-Color"
        gimg.alpha_mode = "CHANNEL_PACKED"
        gtex = nt.nodes.new("ShaderNodeTexImage")
        gtex.image = gimg
        gtex.interpolation = "Closest" if closest else "Linear"
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(gtex.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
    if normal_png:
        nimg = bpy.data.images.load(normal_png)
        nimg.colorspace_settings.name = "Non-Color"
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = nimg
        ntex.interpolation = "Closest" if closest else "Linear"
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nmap.inputs["Strength"].default_value = normal_strength
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def flat_material(name, rgb, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    b.inputs["Roughness"].default_value = roughness
    return mat


def setup_world(color=(0.02, 0.025, 0.03), strength=1.0):
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (color[0], color[1], color[2], 1.0)
    bg.inputs["Strength"].default_value = strength
    bpy.context.scene.world = world
    return world


def point_at(obj, target):
    import mathutils
    d = mathutils.Vector(target) - mathutils.Vector(obj.location)
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def add_light(kind, loc, energy, color=(1, 1, 1), size=None, target=None, rot=None):
    data = bpy.data.lights.new("L", kind)
    data.energy = energy
    data.color = color
    if size is not None:
        if kind == "AREA":
            data.size = size
        elif kind in ("POINT", "SPOT"):
            data.shadow_soft_size = size
    obj = bpy.data.objects.new("L", data)
    obj.location = loc
    bpy.context.scene.collection.objects.link(obj)
    if target is not None:
        point_at(obj, target)
    elif rot is not None:
        obj.rotation_euler = rot
    return obj


def add_camera(loc, target, lens=50.0, ortho_scale=None):
    """loc/target in Blender space."""
    cam = bpy.data.cameras.new("Cam")
    if ortho_scale:
        cam.type = "ORTHO"
        cam.ortho_scale = ortho_scale
    else:
        cam.lens = lens
    cam.clip_start = 0.005
    cam.clip_end = 200
    obj = bpy.data.objects.new("Cam", cam)
    obj.location = loc
    bpy.context.scene.collection.objects.link(obj)
    point_at(obj, target)
    bpy.context.scene.camera = obj
    return obj


def configure_render(width, height, samples=32, engine="EEVEE"):
    sc = bpy.context.scene
    sc.render.resolution_x = width
    sc.render.resolution_y = height
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    names = {"EEVEE": ["BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"], "CYCLES": ["CYCLES"],
             "WORKBENCH": ["BLENDER_WORKBENCH"]}[engine]
    for n in names:
        try:
            sc.render.engine = n
            break
        except TypeError:
            continue
    if sc.render.engine == "CYCLES":
        sc.cycles.device = "CPU"
        sc.cycles.samples = samples
        sc.cycles.use_denoising = False
    elif "EEVEE" in sc.render.engine:
        try:
            sc.eevee.taa_render_samples = samples
        except AttributeError:
            pass
    return sc.render.engine


def render_to(path):
    ensure_dir(os.path.dirname(path))
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def _box(lo, hi):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    v = np.array([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0],
                  [x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]])
    f = np.array([[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4],
                  [2, 3, 7], [2, 7, 6], [1, 2, 6], [1, 6, 5], [0, 4, 7], [0, 7, 3]])
    return v, f


def add_scale_bar(origin, length=0.2, step=0.05, height=0.004, depth=0.01):
    """Alternating dark/light blocks along X, one per `step` metres: a visual ruler (glTF space)."""
    for i in range(int(round(length / step))):
        lo = (origin[0] + i * step, origin[1], origin[2])
        hi = (origin[0] + (i + 1) * step, origin[1] + height, origin[2] + depth)
        v, f = _box(lo, hi)
        o = make_object("ruler%d" % i, v, f)
        o.data.materials.append(flat_material("ruler", (0.02, 0.02, 0.02) if i % 2 == 0 else (0.9, 0.9, 0.9), 0.8))


def add_ground(size=4.0, rgb=(0.12, 0.12, 0.13)):
    v = np.array([[-size, 0, -size], [size, 0, -size], [size, 0, size], [-size, 0, size]], float)
    f = np.array([[0, 3, 2], [0, 2, 1]])  # normal +Y in glTF space (right-handed, CCW from above)
    o = make_object("ground", v, f)
    o.data.materials.append(flat_material("ground", rgb, 0.9))
    return o


def stats(label, t0):
    print("[%s] %.1f s" % (label, time.time() - t0))
