"""Nest mesh (nest_idle) + textures (spec §13.1, §13.4): a mucus mound with clusters of amber eggs.

Texture atlas: the mound uses a disc in the left 76 % of the image, the eggs use the right-hand strip.
Run: blender --background --python make_nest.py
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402

TEX = 1024
K = 10                 # mound rings
S = 28                 # mound sectors
EGG_SEG = 10
EGG_RINGS = 5          # latitude divisions -> 4 inner rings + 2 poles
EGG_COUNT = 18
MOUND_R = 0.45
MOUND_H = 0.28
STRIP_U0 = 0.77        # eggs live in u in [STRIP_U0, 1]
DISC_C = (0.38, 0.5)
DISC_R = 0.375


def footprint(phi):
    """Irregular outline: a few mucus tongues running out of the mound."""
    return MOUND_R * (1.0 + 0.05 * np.sin(3 * phi + 0.7) + 0.04 * np.sin(5 * phi + 2.1) + 0.03 * np.sin(8 * phi + 0.3))


def mound_height(x, z):
    """Analytic surface height at (x, z); shared by the mound mesh and egg placement."""
    x = np.asarray(x, dtype=float)
    z = np.asarray(z, dtype=float)
    shape = x.shape
    xf, zf = x.ravel(), z.ravel()
    phi = np.arctan2(zf, xf)
    rho = np.clip(np.hypot(xf, zf) / footprint(phi), 0, 1)
    profile = MOUND_H * np.power(np.clip(1.0 - rho ** 2.2, 0, 1), 0.85)
    lip = 0.018 * np.exp(-((rho - 0.9) / 0.08) ** 2)                    # rolled mucus rim
    p = np.stack([xf * 6.0, zf * 6.0, np.full(xf.shape, 3.7)], 1)
    lumps = (C.fbm3(p, 5, octaves=3) - 0.5) * 0.07 * C.smoothstep((1.0 - rho) / 0.3)
    spire = 0.11 * np.exp(-((xf + 0.02) ** 2 + (zf - 0.03) ** 2) / (2 * 0.11 ** 2))     # gluey central peak
    return (profile + lip + lumps * 3.2 + spire).reshape(shape)


def build_mound():
    verts = [np.array([0.0, mound_height(np.array(0.0), np.array(0.0)), 0.0])]
    for k in range(1, K + 1):
        rho = k / K
        for j in range(S):
            phi = 2 * np.pi * j / S
            R = footprint(phi)
            x, z = R * rho * np.cos(phi), R * rho * np.sin(phi)
            y = float(mound_height(np.array(x), np.array(z)))
            if k == K:
                y = -0.012
            verts.append(np.array([x, y, z]))
    verts = np.array(verts)

    def vid(k, j):
        return 1 + (k - 1) * S + (j % S)
    faces, uvs = [], []

    def uv_of(k, j):
        # planar top-down mapping: a low wide mound stretches far less than a polar one
        p = verts[0] if k == 0 else verts[vid(k, j)]
        return [DISC_C[0] + DISC_R * p[0] / 0.47, DISC_C[1] + DISC_R * p[2] / 0.47]
    for j in range(S):
        faces.append([0, vid(1, j + 1), vid(1, j)])
        uvs.append([uv_of(0, 0), uv_of(1, j + 1), uv_of(1, j)])
    for k in range(1, K):
        for j in range(S):
            a, b, c, d = vid(k, j), vid(k, j + 1), vid(k + 1, j), vid(k + 1, j + 1)
            faces.append([a, b, c])
            uvs.append([uv_of(k, j), uv_of(k, j + 1), uv_of(k + 1, j)])
            faces.append([b, d, c])
            uvs.append([uv_of(k, j + 1), uv_of(k + 1, j + 1), uv_of(k + 1, j)])
    return verts, np.array(faces), np.array(uvs)


def rotation_from_y(axis):
    y = np.array([0.0, 1.0, 0.0])
    axis = axis / np.linalg.norm(axis)
    v = np.cross(y, axis)
    c = float(np.dot(y, axis))
    if np.linalg.norm(v) < 1e-9:
        return np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1.0 / (1.0 + c))


def build_egg(centre, axis, a, b):
    """Ellipsoid, 64 triangles. Returns verts, faces (local indices), loop uvs."""
    R = rotation_from_y(axis)
    pts = [np.array([0.0, b, 0.0])]
    for i in range(1, EGG_RINGS):
        th = np.pi * i / EGG_RINGS
        for j in range(EGG_SEG):
            ph = 2 * np.pi * j / EGG_SEG
            pts.append(np.array([a * np.sin(th) * np.cos(ph), b * np.cos(th), a * np.sin(th) * np.sin(ph)]))
    pts.append(np.array([0.0, -b, 0.0]))
    pts = np.array(pts) @ R.T + centre
    bottom = len(pts) - 1

    def vid(i, j):
        return 1 + (i - 1) * EGG_SEG + (j % EGG_SEG)

    def uv(i, j):
        return [STRIP_U0 + (1 - STRIP_U0) * j / EGG_SEG, i / EGG_RINGS]
    faces, uvs = [], []
    for j in range(EGG_SEG):
        faces.append([0, vid(1, j + 1), vid(1, j)])
        uvs.append([[STRIP_U0 + (1 - STRIP_U0) * (j + 0.5) / EGG_SEG, 0.0], uv(1, j + 1), uv(1, j)])
    for i in range(1, EGG_RINGS - 1):
        for j in range(EGG_SEG):
            p, q, r, s = vid(i, j), vid(i, j + 1), vid(i + 1, j), vid(i + 1, j + 1)
            faces.append([p, q, r])
            uvs.append([uv(i, j), uv(i, j + 1), uv(i + 1, j)])
            faces.append([q, s, r])
            uvs.append([uv(i, j + 1), uv(i + 1, j + 1), uv(i + 1, j)])
    for j in range(EGG_SEG):
        faces.append([bottom, vid(EGG_RINGS - 1, j), vid(EGG_RINGS - 1, j + 1)])
        uvs.append([[STRIP_U0 + (1 - STRIP_U0) * (j + 0.5) / EGG_SEG, 1.0], uv(EGG_RINGS - 1, j), uv(EGG_RINGS - 1, j + 1)])
    return pts, np.array(faces), np.array(uvs)


def place_eggs(rng):
    clusters = [(0.08, 0.4, 4), (0.55, 0.2, 4), (0.50, 2.3, 3), (0.58, 3.7, 3), (0.45, 4.9, 2), (0.30, 1.5, 2)]
    eggs = []
    for rho, phi, n in clusters:
        cx, cz = rho * footprint(phi) * np.cos(phi), rho * footprint(phi) * np.sin(phi)
        tries = 0
        placed = 0
        while placed < n and tries < 400:
            tries += 1
            ang, rad = rng.random() * 2 * np.pi, 0.115 * np.sqrt(rng.random())
            x, z = cx + rad * np.cos(ang), cz + rad * np.sin(ang)
            if np.hypot(x, z) > 0.86 * footprint(np.arctan2(z, x)):
                continue
            if any(np.hypot(x - e[0], z - e[1]) < 0.085 for e in eggs):
                continue
            a = rng.uniform(0.040, 0.058)
            eggs.append((x, z, a))
            placed += 1
    return eggs[:EGG_COUNT]


def build_nest():
    rng = np.random.default_rng([config.SEED, 9001])
    mv, mf, muv = build_mound()
    verts, faces, uvs = [mv], [mf], [muv]
    offset = len(mv)
    for (x, z, a) in place_eggs(rng):
        eps = 0.01
        hx = (mound_height(np.array(x + eps), np.array(z)) - mound_height(np.array(x - eps), np.array(z))) / (2 * eps)
        hz = (mound_height(np.array(x), np.array(z + eps)) - mound_height(np.array(x), np.array(z - eps))) / (2 * eps)
        normal = np.array([-float(hx), 1.0, -float(hz)])
        normal /= np.linalg.norm(normal)
        axis = normal * 0.55 + (rng.random(3) - 0.5) * 0.9
        axis[1] = abs(axis[1]) + 0.2
        b = a * rng.uniform(1.25, 1.4)
        y = float(mound_height(np.array(x), np.array(z))) + 0.45 * b
        pts, f, uv = build_egg(np.array([x, y, z]), axis, a, b)
        verts.append(pts)
        faces.append(f + offset)
        uvs.append(uv)
        offset += len(pts)
    return np.concatenate(verts), np.concatenate(faces), np.concatenate(uvs)


# --------------------------------------------------------------------------- textures
def make_textures(out_dir):
    N = TEX
    sx = int(round((1.0 - STRIP_U0) * N))
    x0 = N - sx
    low = C.fbm2_tile(N, N, 5, 41, octaves=4)
    mid = C.fbm2_tile(N, N, 18, 42, octaves=3)
    fine = C.fbm2_tile(N, N, 64, 43, octaves=2)
    f1, f2 = C.voronoi_tile(N, N, 34, 34, 44)
    bubble = C.smoothstep((0.22 - f1) / 0.10)                      # small wet bubbles
    crust_n = C.fbm2_tile(N, N, 9, 45, octaves=3)

    brown = np.array([112, 80, 46]) / 255.0
    amber_mucus = np.array([182, 138, 66]) / 255.0
    crust = np.array([66, 46, 30]) / 255.0
    pale = np.array([206, 184, 132]) / 255.0
    col = brown[None, None, :] * (0.85 + 0.3 * low[..., None])
    wet = C.smoothstep((mid - 0.5) / 0.2)
    col = col * (1 - 0.7 * wet[..., None]) + amber_mucus[None, None, :] * 0.7 * wet[..., None]
    dry = C.smoothstep((0.42 - crust_n) / 0.12)
    col = col * (1 - 0.6 * dry[..., None]) + crust[None, None, :] * 0.6 * dry[..., None]
    strand = C.smoothstep((C.fbm2_tile(N, N, 24, 46, octaves=3) - 0.62) / 0.06) * 0.7
    col = col * (1 - 0.5 * strand[..., None]) + pale[None, None, :] * 0.5 * strand[..., None]
    col = col + bubble[..., None] * np.array([0.10, 0.09, 0.06])[None, None, :]

    # egg strip: amber that glows in the middle (suggested translucency), darker, redder at the poles
    sw = sx
    v = (np.arange(N) + 0.5) / N
    sn = C.fbm2_tile(N, sw, 4, 47, octaves=3)
    sn2 = C.fbm2_tile(N, sw, 12, 48, octaves=2)
    glow = np.sin(np.pi * v) ** 0.7
    dark = np.array([104, 44, 14]) / 255.0
    bright = np.array([214, 136, 40]) / 255.0
    core = np.array([238, 184, 92]) / 255.0
    egg = dark[None, None, :] * (1 - glow[:, None, None]) + bright[None, None, :] * glow[:, None, None]
    egg = egg * (0.88 + 0.24 * sn[..., None])
    corem = C.smoothstep((glow[:, None] * sn2 - 0.55) / 0.2)
    egg = egg * (1 - 0.5 * corem[..., None]) + core[None, None, :] * 0.5 * corem[..., None]
    veins = 1.0 - C.smoothstep(np.abs(sn2 - 0.5) / 0.03)
    egg = egg * (1 - 0.25 * veins[..., None]) + np.array([0.55, 0.2, 0.05])[None, None, :] * 0.25 * veins[..., None]
    col[:, x0:, :] = egg
    C.write_png(os.path.join(out_dir, "nest_albedo.png"), C.to_u8(col))

    height = 0.50 * low + 0.20 * mid + 0.10 * fine + 0.12 * bubble + 0.1 * (1 - dry)
    height = C.box_blur(height, 1)
    eggh = 0.15 * sn2 + 0.05 * C.fbm2_tile(N, sw, 40, 49, octaves=2)
    height[:, x0:] = eggh
    C.write_png(os.path.join(out_dir, "nest_normal.png"), C.normal_map_from_height(height, 4.5))


def render_previews(out_dir, prev_dir, verts, faces, uvs):
    import bpy
    C.reset_scene()
    C.setup_world((0.04, 0.045, 0.05), 1.0)
    mat = C.make_pbr_material("nest", os.path.join(out_dir, "nest_albedo.png"),
                              os.path.join(out_dir, "nest_normal.png"), roughness=0.22, normal_strength=1.0, specular=0.6)
    o = C.make_object("nest", verts, faces, uvs)
    o.data.materials.append(mat)
    C.add_ground(6.0, (0.14, 0.14, 0.15))
    C.add_scale_bar((-0.45, 0.0, 0.62), 0.9, 0.1, height=0.005, depth=0.02)
    C.add_light("AREA", C.gltf_to_blender((1.0, 2.2, 1.6)), 200, size=2.0, target=C.gltf_to_blender((0, 0.2, 0)))
    C.add_light("AREA", C.gltf_to_blender((-1.8, 1.0, -1.2)), 60, size=2.0, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0, 0.2, 0)))
    C.add_camera(C.gltf_to_blender((0.9, 0.95, 1.7)), C.gltf_to_blender((0, 0.2, 0)), lens=45)
    C.configure_render(1100, 760, 64)
    C.render_to(os.path.join(prev_dir, "nest.png"))
    C.add_camera(C.gltf_to_blender((0.05, 1.9, 0.9)), C.gltf_to_blender((0, 0.1, 0)), lens=45)
    C.render_to(os.path.join(prev_dir, "nest_top.png"))


def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    C.reset_scene()
    verts, faces, uvs = build_nest()
    C.check_outward(verts, faces, "nest")
    print("nest: %d vertices, %d triangles; min %s max %s" %
          (len(verts), len(faces), np.round(verts.min(0), 3), np.round(verts.max(0), 3)))
    obj = C.make_object("nest_idle", verts, faces, uvs)
    C.export_glb(obj, os.path.join(out, "nest_idle.glb"))
    make_textures(out)
    render_previews(out, prev, verts, faces, uvs)
    C.stats("make_nest", t0)


if __name__ == "__main__":
    main()
