"""Mass dome flipbook (mass_boil_00..07) + textures (spec §13.1, §13.4).

DECISION (deviation from spec §13.2, recorded in docs/decisions.md by the integrator): the mesh is an
UPPER DOME of radius 1 and height 1 (origin at the centre of its base, skirt down to y = -0.05 to hide
the seam with the ground), not a full sphere; the game scales it by (r, 0.8 r, r) (spec §4.5).
Run: blender --background --python make_mass.py
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402

SUBDIV = 4
TARGET_TRIS = 2200
FRAMES = 8
SKIRT_Y = -0.05
TEX = 1024


# --------------------------------------------------------------------------- geometry
def icosphere(sub):
    t = (1.0 + 5 ** 0.5) / 2.0
    v = np.array([[-1, t, 0], [1, t, 0], [-1, -t, 0], [1, -t, 0], [0, -1, t], [0, 1, t],
                  [0, -1, -t], [0, 1, -t], [t, 0, -1], [t, 0, 1], [-t, 0, -1], [-t, 0, 1]], float)
    f = np.array([[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4],
                  [11, 10, 2], [10, 7, 6], [7, 1, 8], [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8],
                  [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]])
    verts = [tuple(x / np.linalg.norm(x)) for x in v]
    faces = [tuple(x) for x in f]
    for _ in range(sub):
        cache = {}
        new_faces = []

        def mid(a, b):
            key = (a, b) if a < b else (b, a)
            if key not in cache:
                m = np.array(verts[a]) + np.array(verts[b])
                m = m / np.linalg.norm(m)
                verts.append(tuple(m))
                cache[key] = len(verts) - 1
            return cache[key]
        for a, b, c in faces:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            new_faces += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        faces = new_faces
    verts = np.array(verts)
    faces = np.array(faces, dtype=np.int64)
    # make sure every face is CCW seen from outside
    n = np.cross(verts[faces[:, 1]] - verts[faces[:, 0]], verts[faces[:, 2]] - verts[faces[:, 0]])
    c = verts[faces].mean(axis=1)
    flip = np.einsum("ij,ij->i", n, c) < 0
    faces[flip] = faces[flip][:, ::-1]
    return verts, faces


class Dome:
    def __init__(self):
        verts, faces = icosphere(SUBDIV)
        # tilt the icosahedron so no vertex/edge pattern lines up with the dome axis
        a, b = 0.37, 0.61
        ca, sa, cb, sb = np.cos(a), np.sin(a), np.cos(b), np.sin(b)
        rx = np.array([[1, 0, 0], [0, ca, -sa], [0, sa, ca]])
        rz = np.array([[cb, -sb, 0], [sb, cb, 0], [0, 0, 1]])
        verts = verts @ (rz @ rx).T
        cy = verts[faces][:, :, 1].mean(axis=1)
        order = np.sort(cy)[::-1]
        self.yc = float(order[TARGET_TRIS - 1]) - 1e-9
        keep = cy >= self.yc
        faces = faces[keep]
        used = np.unique(faces)
        remap = -np.ones(len(verts), dtype=np.int64)
        remap[used] = np.arange(len(used))
        self.faces = remap[faces]
        self.d = verts[used]                      # unit directions of the surviving vertices
        # boundary vertices = ends of edges used by a single face
        edges = {}
        for f in self.faces:
            for i in range(3):
                e = tuple(sorted((f[i], f[(i + 1) % 3])))
                edges[e] = edges.get(e, 0) + 1
        bnd = set()
        for (a_, b_), cnt in edges.items():
            if cnt == 1:
                bnd.update((a_, b_))
        self.boundary = np.array(sorted(bnd), dtype=np.int64)
        d = self.d
        self.yp = (d[:, 1] - self.yc) / (1.0 - self.yc)          # 0 at the cut, 1 at the top
        self.base_pos = np.stack([d[:, 0], self.yp, d[:, 2]], 1)
        # UV: azimuthal equidistant disc, centre = dome top. glTF convention (v down).
        theta = np.arccos(np.clip(d[:, 1], -1, 1))
        self.theta_max = float(theta.max()) + 0.01
        phi = np.arctan2(d[:, 2], d[:, 0])
        rho = theta / self.theta_max
        self.uv = np.stack([0.5 + 0.5 * rho * np.cos(phi), 0.5 + 0.5 * rho * np.sin(phi)], 1)
        self.loop_uv = self.uv[self.faces]
        rng = np.random.default_rng([config.SEED, 4242])
        # head-tip bumps ("heads breaking the surface"): position, phase
        cand = rng.normal(size=(140, 3))
        cand[:, 1] = np.abs(cand[:, 1]) + 0.15
        cand /= np.linalg.norm(cand, axis=1, keepdims=True)
        self.bump_dir = cand
        self.bump_phase = rng.random(len(cand)) * 2 * np.pi
        self.bump_amp = 0.035 + 0.04 * rng.random(len(cand))
        self.bump_sig = 0.04 + 0.03 * rng.random(len(cand))
        self.weight = 0.25 + 0.75 * C.smoothstep(self.yp / 0.18)

    def frame(self, k):
        tau = 2 * np.pi * k / FRAMES
        R = 0.8
        d = self.d
        o1 = R * np.array([np.cos(tau), np.sin(tau), 0.0])
        o2 = R * np.array([0.0, np.cos(tau + 1.1), np.sin(tau + 1.1)])
        o3 = R * np.array([np.sin(tau + 2.3), 0.0, np.cos(tau + 2.3)])
        big = C.ridged3(d * 4.0 + o1, 1, octaves=2, sharp=1.6) - 0.35          # long worm bodies
        mid = C.ridged3(d * 8.5 + o2, 2, octaves=2, sharp=2.6) - 0.30          # thinner loops
        lump = C.fbm3(d * 2.5 + o3, 3, octaves=2) - 0.5
        disp = 0.05 * big + 0.035 * mid + 0.05 * lump
        # head tips pulsing in and out of the surface, phase-shifted so the boil never syncs
        for i in range(len(self.bump_dir)):
            g = np.exp(-np.sum((d - self.bump_dir[i]) ** 2, axis=1) / (2 * self.bump_sig[i] ** 2))
            disp = disp + g * self.bump_amp[i] * (0.15 + 0.85 * (0.5 + 0.5 * np.sin(tau + self.bump_phase[i])))
        disp = disp * self.weight
        pos = self.base_pos + d * disp[:, None]
        pos[self.boundary, 1] = SKIRT_Y
        return pos


# --------------------------------------------------------------------------- textures
def paint_worms(S, rng, count=3300):
    """Painter's-algorithm field of overlapping worms in the dome's disc UV space."""
    height = np.zeros((S, S))
    layer = np.full((S, S), -1.0)
    col = np.zeros((S, S, 3))
    flow = C.fbm2_tile(S, S, 3, 21, octaves=2)
    palette = np.array([[120, 78, 74], [98, 54, 56], [86, 60, 52], [134, 92, 86], [72, 40, 46]], float) / 255.0
    cx = cy = S / 2.0
    R = S / 2.0 * 0.99
    step = 3.5
    for n in range(count):
        rad = 1.03 * R * np.sqrt(rng.random())
        ang = rng.random() * 2 * np.pi
        px, py = cx + rad * np.cos(ang), cy + rad * np.sin(ang)
        fi = flow[int(np.clip(py, 0, S - 1)), int(np.clip(px, 0, S - 1))]
        hd = fi * 2 * np.pi * 2.0 + rng.normal(0, 0.5)
        length = rng.uniform(60, 115)
        K = int(length / step)
        curv = rng.normal(0, 0.06)
        pts = np.zeros((K, 2))
        for k in range(K):
            pts[k] = (px, py)
            hd += curv + rng.normal(0, 0.10)
            px += step * np.cos(hd)
            py += step * np.sin(hd)
        t = np.linspace(0, 1, K)
        w0 = rng.uniform(5.5, 7.5)
        wk = w0 * (0.40 + 0.60 * C.smoothstep(t * 3.0)) * (1.0 - 0.45 * C.smoothstep((t - 0.82) / 0.18))
        x0 = int(max(0, pts[:, 0].min() - w0 - 2))
        x1 = int(min(S, pts[:, 0].max() + w0 + 3))
        y0 = int(max(0, pts[:, 1].min() - w0 - 2))
        y1 = int(min(S, pts[:, 1].max() + w0 + 3))
        if x1 <= x0 or y1 <= y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
        d2 = (gx[..., None] - pts[None, None, :, 0]) ** 2 + (gy[..., None] - pts[None, None, :, 1]) ** 2
        q = d2 / (wk[None, None, :] ** 2)
        qm = q.min(axis=2)
        ti = t[q.argmin(axis=2)]
        h = np.sqrt(np.clip(1.0 - qm, 0, 1))
        h = h * (1.0 + 0.10 * np.cos(2 * np.pi * 14 * ti))        # body annuli
        lay = rng.random() * 0.45
        top = lay + 0.5 * h
        cur = layer[y0:y1, x0:x1]
        win = (h > 0.02) & (top > cur)
        tint = palette[rng.integers(len(palette))] * rng.uniform(0.85, 1.12)
        shade = 0.40 + 0.80 * np.clip(h, 0, 1) ** 0.8
        headmask = C.smoothstep((ti - 0.84) / 0.1)
        pix = tint[None, None, :] * shade[..., None]
        pix = pix * (1 - 0.35 * headmask[..., None])
        sub = col[y0:y1, x0:x1]
        sub[win] = pix[win]
        height[y0:y1, x0:x1] = np.where(win, top, height[y0:y1, x0:x1])
        layer[y0:y1, x0:x1] = np.where(win, top, cur)
    return height, col


def make_textures(out_dir):
    S = TEX
    rng = np.random.default_rng([config.SEED, 777])
    height, col = paint_worms(S, rng)
    covered = height > 0
    gap = np.array([54, 20, 24]) / 255.0
    col = np.where(covered[..., None], col, gap[None, None, :] * (0.7 + 0.6 * C.fbm2_tile(S, S, 12, 5)[..., None]))
    # red veins: Voronoi borders, broken up by noise so they read as capillaries and not as a net
    f1, f2 = C.voronoi_tile(S, S, 16, 16, 31)
    vein = 1.0 - C.smoothstep((f2 - f1) / 0.10)
    vmask = C.smoothstep((C.fbm2_tile(S, S, 6, 32, octaves=3) - 0.42) / 0.15)
    vein = vein * vmask
    red = np.array([150, 18, 24]) / 255.0
    col = col * (1 - 0.4 * vein[..., None]) + red[None, None, :] * 0.4 * vein[..., None] * (0.5 + 0.8 * np.clip(height, 0, 1))[..., None]
    # mottling and wet glints (the "very specular" read is carried by baked highlights)
    mott = C.fbm2_tile(S, S, 8, 33, octaves=4)
    col = col * (0.62 + 0.4 * mott[..., None])
    glint = C.smoothstep((C.fbm2_tile(S, S, 48, 34, octaves=2) - 0.62) / 0.08) * C.smoothstep((height - 0.35) / 0.25)
    col = col + glint[..., None] * np.array([0.20, 0.15, 0.14])[None, None, :]
    C.write_png(os.path.join(out_dir, "mass_albedo.png"), C.to_u8(col))
    hn = C.box_blur(height, 1)
    C.write_png(os.path.join(out_dir, "mass_normal.png"), C.normal_map_from_height(hn, 4.0))


# --------------------------------------------------------------------------- previews
def render_previews(out_dir, prev_dir, dome, frame_pos):
    import bpy
    r = 0.83
    scale = np.array([r, 0.8 * r, r])

    def scene(pos_list):
        C.reset_scene()
        C.setup_world((0.04, 0.045, 0.05), 1.0)
        mat = C.make_pbr_material("mass", os.path.join(out_dir, "mass_albedo.png"),
                                  os.path.join(out_dir, "mass_normal.png"), roughness=0.28, normal_strength=1.0, specular=0.5)
        for name, pos, off in pos_list:
            o = C.make_object(name, pos * scale + np.array(off), dome.faces, dome.loop_uv)
            o.data.materials.append(mat)
        C.add_ground(8.0, (0.14, 0.14, 0.15))

    # player-scale reference: 1.9 m pillar next to an n=10 mass
    scene([("m10", frame_pos[0], (0.0, 0.0, 0.0))])
    v, f = C._box((1.6, 0.0, -0.15), (1.9, 1.9, 0.15))
    ref = C.make_object("player_ref", v, f)
    ref.data.materials.append(C.flat_material("ref", (0.2, 0.35, 0.6), 0.6))
    C.add_light("AREA", C.gltf_to_blender((1.0, 3.5, 2.5)), 260, size=3.0, target=C.gltf_to_blender((0, 0.3, 0)))
    C.add_light("AREA", C.gltf_to_blender((-2.5, 1.5, -2.0)), 60, size=3.0, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0, 0.3, 0)))
    C.add_camera(C.gltf_to_blender((0.9, 1.1, 5.2)), C.gltf_to_blender((0.9, 0.85, 0)), lens=40)
    C.configure_render(1200, 700, 48)
    C.render_to(os.path.join(prev_dir, "mass_n10_vs_player.png"))

    # close-up on the unit dome, 3/4 view
    scene([("m", frame_pos[2], (0.0, 0.0, 0.0))])
    C.add_light("AREA", C.gltf_to_blender((1.0, 2.5, 2.0)), 130, size=2.5, target=C.gltf_to_blender((0, 0.3, 0)))
    C.add_light("AREA", C.gltf_to_blender((-2.0, 1.0, -1.5)), 40, size=2.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0, 0.3, 0)))
    C.add_camera(C.gltf_to_blender((0.9, 1.2, 2.6)), C.gltf_to_blender((0, 0.4, 0)), lens=40)
    C.configure_render(1200, 800, 64)
    C.render_to(os.path.join(prev_dir, "mass_closeup.png"))

    # texture sheet for a quick look
    from shutil import copyfile
    copyfile(os.path.join(out_dir, "mass_albedo.png"), os.path.join(prev_dir, "mass_albedo_tex.png"))


def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    C.reset_scene()
    dome = Dome()
    print("mass: %d vertices, %d triangles, yc=%.3f, %d boundary verts" %
          (len(dome.d), len(dome.faces), dome.yc, len(dome.boundary)))
    frames = [dome.frame(k) for k in range(FRAMES)]
    C.check_outward(frames[0] + np.array([0, 0.0, 0]), dome.faces, "mass")
    obj = C.make_object("mass", frames[0], dome.faces, dome.loop_uv)
    for k, pos in enumerate(frames):
        C.set_positions(obj, pos)
        C.export_glb(obj, os.path.join(out, "mass_boil_%02d.glb" % k))
    print("  bounds f0 min %s max %s" % (np.round(frames[0].min(0), 3), np.round(frames[0].max(0), 3)))
    make_textures(out)
    render_previews(out, prev, dome, frames)
    C.stats("make_mass", t0)


if __name__ == "__main__":
    main()
