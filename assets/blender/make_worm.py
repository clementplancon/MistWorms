"""Worm flipbook meshes + textures (spec §13.1, §13.4).

One base topology (a 14-segment larva with two tiny mandibles) is deformed per frame, so every
animation frame has identical vertex/index counts. Run: blender --background --python make_worm.py
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402

RINGS = 28            # intervals along the body; ring i sits at u = i / RINGS (2 rings per segment)
AROUND = 14
BODY_LEN = 0.385
HEAD_INSET = 0.007
TAIL_INSET = 0.004    # blunt tail: the pole sits slightly inside the first ring
SEGMENTS = 14
FLAT = 0.92           # belly flattening of the cross-section

# Radius profile along u (tail 0 -> head 1): tail 0.012, body 0.030, head bulb 0.035.
PROFILE_U = [0.0, 1 / 28, 0.08, 0.2, 0.35, 0.5, 0.7, 0.8, 0.88, 0.94, 27 / 28, 1.0]
PROFILE_R = [0.0, 0.0105, 0.0125, 0.0215, 0.0285, 0.0300, 0.0305, 0.0335, 0.0350, 0.0330, 0.0285, 0.0]


def smoothstep(x):
    return C.smoothstep(x)


def topology():
    """Faces (F,3) and per-loop UVs (F,3,2); vertex layout: 0 tail pole, rings 1..27, head pole, mandibles."""
    def vid(i, j):
        return 1 + (i - 1) * AROUND + (j % AROUND)
    tail, head = 0, 1 + (RINGS - 1) * AROUND
    faces, uvs = [], []
    for j in range(AROUND):
        u0, u1, um = j / AROUND, (j + 1) / AROUND, (j + 0.5) / AROUND
        # tail fan (normal -T)
        faces.append([tail, vid(1, j + 1), vid(1, j)])
        uvs.append([[um, 0.0], [u1, 1 / RINGS], [u0, 1 / RINGS]])
        # head fan (normal +T)
        faces.append([head, vid(RINGS - 1, j), vid(RINGS - 1, j + 1)])
        uvs.append([[um, 1.0], [u0, (RINGS - 1) / RINGS], [u1, (RINGS - 1) / RINGS]])
    for i in range(1, RINGS - 1):
        v0, v1 = i / RINGS, (i + 1) / RINGS
        for j in range(AROUND):
            u0, u1 = j / AROUND, (j + 1) / AROUND
            a, b, c, d = vid(i, j), vid(i, j + 1), vid(i + 1, j), vid(i + 1, j + 1)
            faces.append([a, b, c])
            uvs.append([[u0, v0], [u1, v0], [u0, v1]])
            faces.append([b, d, c])
            uvs.append([[u1, v0], [u1, v1], [u0, v1]])
    base = head + 1
    for m in range(2):
        r0 = base + m * 6
        tip = r0 + 5
        for k in range(5):
            faces.append([r0 + k, r0 + (k + 1) % 5, tip])
            uvs.append([[k / 5, 0.975], [(k + 1) / 5, 0.975], [(k + 0.5) / 5, 1.0]])
    return np.array(faces, dtype=np.int64), np.array(uvs, dtype=np.float64), base + 12


def ring_u():
    return np.arange(RINGS + 1) / RINGS


def pose(rear=0.0, lunge=0.0, crawl_k=None, pulse=1.0, flat=FLAT, sink=0.0, curl=0.0, ring_wave=None):
    """Returns glTF-space vertices (V,3) for one frame."""
    u = ring_u()
    s = u * BODY_LEN
    ds = BODY_LEN / RINGS

    # --- bending (rear): pitch grows from mid body to the head, integrated along the arc
    theta = rear * (np.pi / 2) * smoothstep((s - REAR_S0) / REAR_W)
    th_mid = 0.5 * (theta[:-1] + theta[1:])
    z = np.concatenate([[0.0], np.cumsum(np.cos(th_mid) * ds)])
    y = np.concatenate([[0.0], np.cumsum(np.sin(th_mid) * ds)])
    # --- front lunge (strike): the front part stretches forward, the radius thins to keep volume
    z = z + lunge * smoothstep((s - 0.15) / 0.20)
    # --- lateral motion
    x = np.zeros(RINGS + 1)
    if crawl_k is not None:
        x = x + 0.02 * np.sin(2 * np.pi * (s / 0.28 - crawl_k / 12.0))
    x = x + curl * np.sin(np.pi * u)

    # radius
    rb = np.interp(u, PROFILE_U, PROFILE_R)
    ann = 1.0 + 0.05 * np.where(np.arange(RINGS + 1) % 2 == 0, 1.0, -1.0)
    rr = rb * ann * pulse
    if crawl_k is not None:
        rr = rr * (1.0 + 0.20 * np.sin(2 * np.pi * (s / 0.13 - crawl_k / 12.0)))
    seg = np.linalg.norm(np.diff(np.stack([x, y, z], 1), axis=0), axis=1) / ds
    ring_strain = np.concatenate([[seg[0]], 0.5 * (seg[:-1] + seg[1:]), [seg[-1]]])
    rr = rr * np.power(np.maximum(ring_strain, 0.2), -0.5)
    rr[0] = rr[-1] = 0.0

    # centre line height: belly rests on the ground while flat, lifts away when the body pitches up
    rc = rr.copy()
    rc[0] = 0.8 * rr[1]
    rc[-1] = 0.8 * rr[-2]
    y = y + (flat - sink) * rc * np.cos(theta)
    P = np.stack([x, y, z], 1)

    T = C.normalize(np.gradient(P, axis=0))
    side = np.array([1.0, 0.0, 0.0])
    B = C.normalize(side[None, :] - (T @ side)[:, None] * T)
    N = np.cross(T, B)

    ph = 2 * np.pi * np.arange(AROUND) / AROUND
    cs, sn = np.cos(ph), np.sin(ph)
    ring_pts = (P[1:-1, None, :] + rr[1:-1, None, None] *
                (cs[None, :, None] * B[1:-1, None, :] + flat * sn[None, :, None] * N[1:-1, None, :]))
    tail = P[0] + T[0] * TAIL_INSET
    head = P[-1] - T[-1] * HEAD_INSET
    verts = [tail[None, :], ring_pts.reshape(-1, 3), head[None, :]]

    # mandibles: two short hooks on the head frame
    hP, hT, hB, hN = P[-1], T[-1], B[-1], N[-1]
    ka = 2 * np.pi * np.arange(5) / 5
    for side_s in (-1.0, 1.0):
        centre = hP - 0.006 * hT + side_s * 0.007 * hB - 0.006 * flat * hN
        ring = centre[None, :] + 0.0045 * (np.cos(ka)[:, None] * hB[None, :] + np.sin(ka)[:, None] * hN[None, :])
        tip = centre + 0.024 * hT - side_s * 0.007 * hB - 0.003 * hN
        verts += [ring, tip[None, :]]
    return np.concatenate(verts, 0)


REAR_S0 = 0.07
REAR_W = 0.15


def make_textures(out_dir):
    H = W = 512
    V, U = np.meshgrid((np.arange(H) + 0.5) / H, (np.arange(W) + 0.5) / W, indexing="ij")
    ring = 0.5 + 0.5 * np.cos(2 * np.pi * SEGMENTS * V)         # 1 on a ridge, 0 in a groove
    groove = 1.0 - ring
    low = C.fbm2_tile(H, W, 4, 1, octaves=4)
    mid = C.fbm2_tile(H, W, 16, 2, octaves=3)
    fine = C.fbm2_tile(H, W, 64, 3, octaves=2)
    f1, f2 = C.voronoi_tile(H, W, 36, 72, 4)
    edge = C.smoothstep((f2 - f1) / 0.22)                         # 0 on cell borders (wrinkles)

    base = np.array([204, 166, 160]) / 255.0
    dark = np.array([142, 88, 92]) / 255.0
    col = base[None, None, :] * (0.92 + 0.16 * low)[..., None]
    col = col * (0.95 + 0.10 * edge)[..., None]
    col = col + (np.array([0.05, 0.0, -0.02]))[None, None, :] * (mid - 0.5)[..., None] * 2
    gw = (groove ** 1.4 * 0.75)[..., None]
    col = col * (1 - gw) + dark[None, None, :] * gw
    # dorsal vessel (darker) and paler belly
    dorsal = np.exp(-(((U - 0.25) / 0.045) ** 2)) * 0.30
    col = col * (1 - dorsal[..., None]) + np.array([0.42, 0.28, 0.30])[None, None, :] * dorsal[..., None]
    belly = np.exp(-(((U - 0.75) / 0.12) ** 2)) * 0.15
    col = col + belly[..., None] * np.array([0.10, 0.09, 0.06])[None, None, :]
    # painted-in glints, because the runtime shader has no real wet look: pale blobs on the ridges
    blob = C.smoothstep((C.fbm2_tile(H, W, 10, 5, octaves=2) - 0.58) / 0.12) * ring ** 1.2
    spec = C.smoothstep((fine - 0.6) / 0.1) * ring * 0.5
    col = col + (blob * 0.12 + spec * 0.15)[..., None] * np.array([1.0, 0.95, 0.9])[None, None, :]
    # head and mouth darker
    head = C.smoothstep((V - 0.915) / 0.05)
    col = col * (1 - head[..., None]) + np.array([0.36, 0.22, 0.18])[None, None, :] * head[..., None] * (0.85 + 0.3 * mid[..., None])
    tailm = 1.0 - C.smoothstep(V / 0.05)
    col = col * (1 - 0.25 * tailm[..., None])
    C.write_png(os.path.join(out_dir, "worm_albedo.png"), C.to_u8(col))

    height = ring * 0.55 + edge * 0.12 + mid * 0.12 + fine * 0.10
    height = C.box_blur(height, 1)
    C.write_png(os.path.join(out_dir, "worm_normal.png"), C.normal_map_from_height(height, 3.5))


def preview(paths, items, out_dir):
    """items: list of (name, verts, faces, uv, x_offset)."""
    import bpy
    C.reset_scene()
    C.setup_world((0.05, 0.055, 0.06), 1.0)
    mat = C.make_pbr_material("worm", os.path.join(out_dir, "worm_albedo.png"),
                              os.path.join(out_dir, "worm_normal.png"), roughness=0.22, normal_strength=1.0)
    for name, verts, faces, uv, xo in items:
        # turn the worm to face +X so a side-on camera sees its profile
        v = np.stack([verts[:, 2] + xo, verts[:, 1], -verts[:, 0]], 1)
        o = C.make_object("p_" + name, v, faces, uv)
        o.data.materials.append(mat)
    C.add_ground(3.0, (0.16, 0.16, 0.17))
    C.add_scale_bar((-0.2, 0.0, 0.2), 0.4, 0.05)
    return bpy


def render_previews(out_dir, prev_dir, frames):
    import bpy
    faces, uvs, _ = topology()
    pick = [("crawl0", frames["worm_crawl_00"]), ("crawl3", frames["worm_crawl_03"]),
            ("rear5", frames["worm_rear_05"]), ("strike3", frames["worm_strike_03"]),
            ("sleep0", frames["worm_sleep_00"])]
    # row of poses, true scale, with a ruler (dark/light = 5 cm)
    items = [(n, v, faces, uvs, -1.1 + 0.55 * i) for i, (n, v) in enumerate(pick)]
    preview(None, items, out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.4, 1.6, 1.2)), 90, size=1.5, target=C.gltf_to_blender((0.0, 0.1, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-1.5, 0.8, -1.0)), 40, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.1, 0.0)))
    C.add_camera(C.gltf_to_blender((0.0, 0.8, 3.6)), C.gltf_to_blender((0.0, 0.12, 0.0)), lens=60)
    C.configure_render(1400, 520, 48)
    C.render_to(os.path.join(prev_dir, "worm_poses.png"))

    # close-up of a single crawling worm from a 3/4 high angle
    preview(None, [("close", frames["worm_crawl_02"], faces, uvs, 0.0)], out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.5, 0.8, 0.6)), 25, size=0.8, target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-0.6, 0.4, -0.5)), 8, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_camera(C.gltf_to_blender((0.28, 0.20, 0.42)), C.gltf_to_blender((0.0, 0.02, 0.0)), lens=60)
    C.configure_render(1000, 700, 64)
    C.render_to(os.path.join(prev_dir, "worm_closeup.png"))


def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    import bpy
    C.reset_scene()

    faces, uvs, nverts = topology()
    frames = {}
    for k in range(12):
        frames["worm_crawl_%02d" % k] = pose(crawl_k=k)
    for k in range(6):
        frames["worm_rear_%02d" % k] = pose(rear=k / 5.0)
    for k in range(4):
        e = k / 3.0
        frames["worm_strike_%02d" % k] = pose(rear=0.85 * (1.0 - e), lunge=0.15 * e)
    for k in range(4):
        frames["worm_sleep_%02d" % k] = pose(pulse=1.0 + 0.05 * np.sin(2 * np.pi * k / 4.0),
                                             flat=0.62, sink=0.28, curl=0.03)

    # origin under the centre of mass: shift every frame by the same Z (rest-pose mean of the body)
    ref = frames["worm_crawl_00"][: 1 + (RINGS - 1) * AROUND + 1]
    zc = float(ref[:, 2].mean())
    for name in frames:
        frames[name] = frames[name] - np.array([0.0, 0.0, zc])

    C.check_outward(frames["worm_crawl_00"], faces, "worm")
    print("worm: %d vertices, %d triangles" % (nverts, len(faces)))
    for name in ("worm_crawl_00", "worm_crawl_05", "worm_rear_05", "worm_strike_03", "worm_sleep_01"):
        v = frames[name]
        print("  %s min %s max %s" % (name, np.round(v.min(0), 3), np.round(v.max(0), 3)))

    obj = C.make_object("worm", frames["worm_crawl_00"], faces, uvs)
    for name, v in frames.items():
        C.set_positions(obj, v)
        C.export_glb(obj, os.path.join(out, name + ".glb"))
    make_textures(out)
    render_previews(out, prev, frames)
    C.stats("make_worm", t0)


if __name__ == "__main__":
    main()
