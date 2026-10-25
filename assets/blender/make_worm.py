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
RING_BULGE = 0.02     # alternate rings +-2 % (was 5 %: tyre look)
FLAT = 0.92           # belly flattening of the cross-section

# Radius profile along u (tail 0 -> head 1): tail 0.012, body 0.030, head bulb 0.035.
PROFILE_U = [0.0, 1 / 28, 0.08, 0.2, 0.35, 0.5, 0.7, 0.8, 0.88, 0.94, 27 / 28, 1.0]
PROFILE_R = [0.0, 0.0105, 0.0125, 0.0215, 0.0285, 0.0300, 0.0305, 0.0335, 0.0350, 0.0330, 0.0285, 0.0]


def smoothstep(x):
    return C.smoothstep(x)


def topology(rings=RINGS, around=AROUND, mandibles=True):
    """Faces (F,3) and per-loop UVs (F,3,2); vertex layout: 0 tail pole, rings 1..rings-1, head pole, mandibles.
    Defaults: the LOD0 worm (28 x 14 + 2 mandibles = 766 triangles); LOD1 uses rings=16, around=8, no mandibles (240)."""
    def vid(i, j):
        return 1 + (i - 1) * around + (j % around)
    tail, head = 0, 1 + (rings - 1) * around
    faces, uvs = [], []
    for j in range(around):
        u0, u1, um = j / around, (j + 1) / around, (j + 0.5) / around
        # tail fan (normal -T)
        faces.append([tail, vid(1, j + 1), vid(1, j)])
        uvs.append([[um, 0.0], [u1, 1 / rings], [u0, 1 / rings]])
        # head fan (normal +T)
        faces.append([head, vid(rings - 1, j), vid(rings - 1, j + 1)])
        uvs.append([[um, 1.0], [u0, (rings - 1) / rings], [u1, (rings - 1) / rings]])
    for i in range(1, rings - 1):
        v0, v1 = i / rings, (i + 1) / rings
        for j in range(around):
            u0, u1 = j / around, (j + 1) / around
            a, b, c, d = vid(i, j), vid(i, j + 1), vid(i + 1, j), vid(i + 1, j + 1)
            faces.append([a, b, c])
            uvs.append([[u0, v0], [u1, v0], [u0, v1]])
            faces.append([b, d, c])
            uvs.append([[u1, v0], [u1, v1], [u0, v1]])
    base = head + 1
    if not mandibles:
        return np.array(faces, dtype=np.int64), np.array(uvs, dtype=np.float64), base
    for m in range(2):
        r0 = base + m * 6
        tip = r0 + 5
        for k in range(5):
            faces.append([r0 + k, r0 + (k + 1) % 5, tip])
            uvs.append([[k / 5, 0.975], [(k + 1) / 5, 0.975], [(k + 0.5) / 5, 1.0]])
    return np.array(faces, dtype=np.int64), np.array(uvs, dtype=np.float64), base + 12


def ring_u(rings=RINGS):
    return np.arange(rings + 1) / rings


CRAWL_FRAMES = 16
IDLE_FRAMES = 8
LOD1_RINGS = 16       # LOD1: 16 intervals x 8 around, no mandibles, no ring bulge = 240 triangles
LOD1_AROUND = 8

# Crawl (plan 4A-1 point 2): lateral wave travelling head -> tail, peristalsis and stretch travelling tail -> head.
CRAWL_WAVE_LEN = 0.35
CRAWL_AMP_HEAD = 0.02
CRAWL_AMP_TAIL = 0.045
CRAWL_PERI_LEN = 0.13
CRAWL_PERI_AMP = 0.25
CRAWL_STRETCH = 0.06
# Idle (point 3): head yaw sweep, slow lateral undulation, light peristalsis.
IDLE_YAW_DEG = 15.0
IDLE_WAVE_LEN = 0.40
IDLE_WAVE_AMP = 0.01
IDLE_PERI_AMP = 0.08
IDLE_HEAD_START = 0.75   # the front 25 % of the body carries the sweep
IDLE_HEAD_RAMP = 0.12


def pose(rear=0.0, lunge=0.0, crawl_k=None, pulse=1.0, flat=FLAT, sink=0.0, curl=0.0, idle_k=None,
         rings=RINGS, around=AROUND, mandibles=True, bulge=RING_BULGE):
    """Returns glTF-space vertices (V,3) for one frame. crawl_k / idle_k are frame indices of the looping flipbooks
    (always counted on the LOD0 period: CRAWL_FRAMES / IDLE_FRAMES). rings/around/mandibles/bulge select the LOD."""
    u = ring_u(rings)
    s_nom = u * BODY_LEN
    s_mid = 0.5 * (s_nom[:-1] + s_nom[1:])

    # --- longitudinal stretch (crawl): interval lengths, renormalised so the mean body length stays BODY_LEN
    w = np.ones(rings)
    if crawl_k is not None:
        w = 1.0 + CRAWL_STRETCH * np.sin(2 * np.pi * (s_mid / CRAWL_PERI_LEN - crawl_k / CRAWL_FRAMES))
    ds = BODY_LEN * w / w.sum()
    s = np.concatenate([[0.0], np.cumsum(ds)])

    # --- bending (rear): pitch grows from mid body to the head, integrated along the arc
    theta = rear * (np.pi / 2) * smoothstep((s - REAR_S0) / REAR_W)
    th_mid = 0.5 * (theta[:-1] + theta[1:])
    # --- head yaw sweep (idle): heading angle in the ground plane, integrated along the arc
    psi = np.zeros(rings + 1)
    if idle_k is not None:
        psi = np.radians(IDLE_YAW_DEG) * np.sin(2 * np.pi * idle_k / IDLE_FRAMES) *             smoothstep((u - IDLE_HEAD_START) / IDLE_HEAD_RAMP)
    psi_mid = 0.5 * (psi[:-1] + psi[1:])
    x = np.concatenate([[0.0], np.cumsum(np.sin(psi_mid) * np.cos(th_mid) * ds)])
    z = np.concatenate([[0.0], np.cumsum(np.cos(psi_mid) * np.cos(th_mid) * ds)])
    y = np.concatenate([[0.0], np.cumsum(np.sin(th_mid) * ds)])
    # --- front lunge (strike): the front part stretches forward, the radius thins to keep volume
    z = z + lunge * smoothstep((s - 0.15) / 0.20)
    # --- lateral motion
    peri = np.zeros(rings + 1)
    if crawl_k is not None:
        amp = CRAWL_AMP_HEAD + (CRAWL_AMP_TAIL - CRAWL_AMP_HEAD) * (1.0 - u)
        x = x + amp * np.sin(2 * np.pi * (s / CRAWL_WAVE_LEN + crawl_k / CRAWL_FRAMES))
        peri = CRAWL_PERI_AMP * np.sin(2 * np.pi * (s / CRAWL_PERI_LEN - crawl_k / CRAWL_FRAMES))
    if idle_k is not None:
        x = x + IDLE_WAVE_AMP * np.sin(2 * np.pi * (s / IDLE_WAVE_LEN + idle_k / IDLE_FRAMES))
        peri = IDLE_PERI_AMP * np.sin(2 * np.pi * (s / CRAWL_PERI_LEN - idle_k / IDLE_FRAMES))
    x = x + curl * np.sin(np.pi * u)

    # radius
    rb = np.interp(u, PROFILE_U, PROFILE_R)
    ann = 1.0 + bulge * np.where(np.arange(rings + 1) % 2 == 0, 1.0, -1.0)
    rr = rb * ann * pulse * (1.0 + peri)
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

    ph = 2 * np.pi * np.arange(around) / around
    cs, sn = np.cos(ph), np.sin(ph)
    ring_pts = (P[1:-1, None, :] + rr[1:-1, None, None] *
                (cs[None, :, None] * B[1:-1, None, :] + flat * sn[None, :, None] * N[1:-1, None, :]))
    tail = P[0] + T[0] * TAIL_INSET
    head = P[-1] - T[-1] * HEAD_INSET
    verts = [tail[None, :], ring_pts.reshape(-1, 3), head[None, :]]

    if not mandibles:
        return np.concatenate(verts, 0)
    # mandibles: two short hooks on the head frame
    hP, hT, hB, hN = P[-1], T[-1], B[-1], N[-1]
    ka = 2 * np.pi * np.arange(5) / 5
    for side_s in (-1.0, 1.0):
        centre = hP - 0.006 * hT + side_s * 0.007 * hB - 0.006 * flat * hN
        ring = centre[None, :] + 0.0045 * (np.cos(ka)[:, None] * hB[None, :] + np.sin(ka)[:, None] * hN[None, :])
        tip = centre + 0.024 * hT - side_s * 0.007 * hB - 0.003 * hN
        verts += [ring, tip[None, :]]
    return np.concatenate(verts, 0)


# --------------------------------------------------------------------------- coil (plan 4B-3 pass A1)
COIL_FRAMES = 12
COIL_TURN = 4.0            # rad: total turn of the body at tau = 0 (a C of about 230 degrees)
COIL_S_GAIN = 2.0          # weight of the S mode (tau = pi/2); plan said 1.6, raised so the XZ extent stays under 0.30 m
COIL_YAW_DEG = 30.0        # head yaw, double frequency, on u > COIL_YAW_U0
COIL_YAW_U0 = 0.78
COIL_PITCH_DEG = 12.0      # head pitch on u > COIL_PITCH_U0
COIL_PITCH_U0 = 0.85
COIL_PERI_AMP = 0.12       # peristalsis, tail -> head
COIL_PERI_LEN = 0.13
COIL_TWIST_DEG = 35.0      # roll of the cross-section about the tangent, travelling along the body
COIL_RISE = 0.025          # arch of the middle of the body (m)


def coil_pose(k, rings=RINGS, around=AROUND, mandibles=True, bulge=RING_BULGE, flat=FLAT):
    """The coiled worm: a planar C/S curve (curvature mode cos(tau) + S gain * sin(tau) * sin(2 pi u)), head yaw and pitch,
    peristalsis, a travelling twist and a small arch. Frame k of COIL_FRAMES, tau = 2 pi k / COIL_FRAMES, a clean loop.
    Reference frame B = norm(Y x T) (T is never vertical here), with a fallback against degeneracy."""
    tau = 2 * np.pi * k / COIL_FRAMES
    u = ring_u(rings)
    ds = BODY_LEN / rings
    s = u * BODY_LEN
    u_mid = 0.5 * (u[:-1] + u[1:])
    kappa = (COIL_TURN / BODY_LEN) * (np.cos(tau) + COIL_S_GAIN * np.sin(tau) * np.sin(2 * np.pi * u_mid))
    psi = np.concatenate([[0.0], np.cumsum(kappa * ds)])
    psi = psi + np.radians(COIL_YAW_DEG) * np.sin(2 * tau) * smoothstep((u - COIL_YAW_U0) / 0.12)
    theta = np.radians(COIL_PITCH_DEG) * np.sin(2 * tau + 1.2) * smoothstep((u - COIL_PITCH_U0) / 0.10)
    psi_mid = 0.5 * (psi[:-1] + psi[1:])
    th_mid = 0.5 * (theta[:-1] + theta[1:])
    x = np.concatenate([[0.0], np.cumsum(np.sin(psi_mid) * np.cos(th_mid) * ds)])
    z = np.concatenate([[0.0], np.cumsum(np.cos(psi_mid) * np.cos(th_mid) * ds)])
    y = np.concatenate([[0.0], np.cumsum(np.sin(th_mid) * ds)])
    y = y + COIL_RISE * np.sin(np.pi * u) * (0.6 + 0.4 * np.sin(tau))
    peri = COIL_PERI_AMP * np.sin(2 * np.pi * (s / COIL_PERI_LEN - k / COIL_FRAMES))
    rb = np.interp(u, PROFILE_U, PROFILE_R)
    ann = 1.0 + bulge * np.where(np.arange(rings + 1) % 2 == 0, 1.0, -1.0)
    rr = rb * ann * (1.0 + peri)
    rr[0] = rr[-1] = 0.0
    rc = rr.copy()
    rc[0] = 0.8 * rr[1]
    rc[-1] = 0.8 * rr[-2]
    y = y + flat * rc * np.cos(theta)
    P = np.stack([x, y, z], 1)

    T = C.normalize(np.gradient(P, axis=0))
    B = np.stack([T[:, 2], np.zeros(len(T)), -T[:, 0]], 1)        # Y x T
    bl = np.linalg.norm(B, axis=1)
    for i in range(len(B)):
        if bl[i] < 1e-3:
            B[i] = B[i - 1] if i > 0 else np.array([1.0, 0.0, 0.0])
        else:
            B[i] = B[i] / bl[i]
    N = np.cross(T, B)
    tw = np.radians(COIL_TWIST_DEG) * np.sin(2 * np.pi * (u - k / COIL_FRAMES))
    ct, st = np.cos(tw)[:, None], np.sin(tw)[:, None]
    B, N = ct * B + st * N, -st * B + ct * N

    ph = 2 * np.pi * np.arange(around) / around
    cs, sn = np.cos(ph), np.sin(ph)
    ring_pts = (P[1:-1, None, :] + rr[1:-1, None, None] *
                (cs[None, :, None] * B[1:-1, None, :] + flat * sn[None, :, None] * N[1:-1, None, :]))
    tail = P[0] + T[0] * TAIL_INSET
    head = P[-1] - T[-1] * HEAD_INSET
    verts = [tail[None, :], ring_pts.reshape(-1, 3), head[None, :]]
    if mandibles:
        hP, hT, hB, hN = P[-1], T[-1], B[-1], N[-1]
        ka = 2 * np.pi * np.arange(5) / 5
        for side_s in (-1.0, 1.0):
            centre = hP - 0.006 * hT + side_s * 0.007 * hB - 0.006 * flat * hN
            ring = centre[None, :] + 0.0045 * (np.cos(ka)[:, None] * hB[None, :] + np.sin(ka)[:, None] * hN[None, :])
            tip = centre + 0.024 * hT - side_s * 0.007 * hB - 0.003 * hN
            verts += [ring, tip[None, :]]
    return np.concatenate(verts, 0)


def coil_frames(rings=RINGS, around=AROUND, mandibles=True, bulge=RING_BULGE, step=1, frames=COIL_FRAMES):
    """Coil frames (every `step` pose of the LOD0 loop). Origin: XZ centroid of the LOD0 body of the SAME pose (the body
    stays under its slot while it curls), y = 0 at the belly of the whole loop (one offset for every frame and LOD)."""
    ref = [coil_pose(k) for k in range(COIL_FRAMES)]
    nb = 1 + (RINGS - 1) * AROUND + 1
    ymin = min(v[:nb, 1].min() for v in ref)
    out = []
    for k in range(frames):
        c = ref[k * step][:nb, [0, 2]].mean(0)
        out.append(coil_pose(k * step, rings, around, mandibles, bulge) - np.array([c[0], ymin, c[1]]))
    return out


REAR_S0 = 0.07
REAR_W = 0.15


TEX = 128             # final texture size (Valheim creature textures are 64 to 128 px, Point filtered)
PAINT = 512           # painted at 4x then reduced with a box filter
WRINKLES = 2 * SEGMENTS

COL_BASE = np.array([0xBA, 0xB1, 0x93]) / 255.0       # cave cream, grey-yellow (was E6DDBE: white in daylight)
COL_BELLY = np.array([0xC9, 0xC1, 0xA6]) / 255.0       # was F2ECD4
COL_BACK = np.array([0xBB, 0xA5, 0x7C]) / 255.0       # a little warmer (was E6D2A2)
COL_PATCH = np.array([0xD2, 0xCA, 0xB0]) / 255.0      # translucent paler patches (was F6F1DC)
COL_VEIN = np.array([0x8D, 0xBA, 0x2E]) / 255.0       # acid green
COL_HEAD = np.array([0x8A, 0x5A, 0x2B]) / 255.0       # amber brown
COL_MOUTH = np.array([0x3B, 0x26, 0x15]) / 255.0

# lustre (alpha of worm_gloss): body, folds, head, mandibles
GLOSS_BODY, GLOSS_FOLD, GLOSS_HEAD, GLOSS_MOUTH = 0.85, 0.5, 0.7, 0.4     # body was 0.92 (glare in daylight)

HEAD_V0 = 0.895       # head capsule starts here (V = 0 tail, 1 head tip)
MOUTH_V0 = 0.968      # head cap tip and mandibles


def box_reduce(a, factor):
    h, w = a.shape[:2]
    return a.reshape(h // factor, factor, w // factor, factor, *a.shape[2:]).mean(axis=(1, 3))


def circ(a, b):
    """Distance on the circle of circumference 1 (U wraps around the body)."""
    d = np.abs(a - b) % 1.0
    return np.minimum(d, 1.0 - d)


def make_textures(out_dir):
    H = W = PAINT
    V, U = np.meshgrid((np.arange(H) + 0.5) / H, (np.arange(W) + 0.5) / W, indexing="ij")
    low = C.fbm2_tile(H, W, 3, 1, octaves=2)

    # --- ring folds: 14 segments, ridges where V = m / 14 (the even mesh rings); thin folds, same hue, ~22 % darker
    ridge_c = np.cos(2 * np.pi * SEGMENTS * V)
    groove = C.smoothstep((0.5 - 0.5 * ridge_c - 0.76) / 0.20)
    groove = groove * (0.75 + 0.25 * C.fbm2_tile(H, W, 2, 11, octaves=2, aspect=4.0))   # not every fold equally deep

    # --- body colour: dorsal weight d (1 on top, U = 0.25) to belly (U = 0.75)
    d = 0.5 + 0.5 * np.cos(2 * np.pi * (U - 0.25))
    col = COL_BASE[None, None, :] * (0.97 + 0.06 * low)[..., None]
    belly_w = ((1.0 - d) ** 2 * 0.7)[..., None]
    col = col * (1 - belly_w) + COL_BELLY[None, None, :] * belly_w
    # translucent paler patches (big, soft shapes)
    patch = C.smoothstep((C.fbm2_tile(H, W, 5, 4, octaves=2, aspect=2.0) - 0.56) / 0.12) * 0.40
    col = col * (1 - patch[..., None]) + COL_PATCH[None, None, :] * patch[..., None]
    # dorsal line: a little warmer and darker
    dl = np.exp(-(circ(U, 0.25) / 0.035) ** 2)
    col = col * (1 - 0.55 * dl[..., None]) + (COL_BACK * 0.93)[None, None, :] * (0.55 * dl)[..., None]

    # --- veins seen through the skin: thin sinuous acid-green lines running ALONG the body (U varies slowly with V),
    # crossing the rings, with a few branches; irregular (fade) and half visible
    def vein(centre, wid, mix, fade):
        return np.exp(-(circ(U, centre) / wid) ** 2) * fade * (1.0 - 0.5 * groove) * mix
    veins = []
    for k, (u0, phi, mix) in enumerate(((0.22, 0.0, 0.36), (0.62, 2.1, 0.32), (0.92, 4.0, 0.28))):
        # drifts by about +-25 % of the circumference along the body, and only shows in stretches
        centre = u0 + 0.25 * np.sin(2 * np.pi * (0.9 * V) + phi) + 0.05 * np.sin(2 * np.pi * (3.1 * V) + 2 * phi)
        fade = C.smoothstep((C.fbm2_tile(H, W, 2, 20 + k, octaves=2, aspect=3.0) - 0.42) / 0.12)
        veins.append(vein(centre, 0.016, mix, fade))
        v0 = 0.25 + 0.3 * k
        rise = C.smoothstep((V - v0) / 0.25)
        win = C.smoothstep((V - v0) / 0.04) * (1.0 - C.smoothstep((V - v0 - 0.25) / 0.08))
        bc = centre + (1.0 if k % 2 == 0 else -1.0) * 0.06 * rise
        veins.append(vein(bc, 0.012, mix * 0.7, fade * win))
    for m in veins:
        col = col * (1 - m[..., None]) + COL_VEIN[None, None, :] * m[..., None]

    # --- grooves between the rings: the surrounding colour, darker
    gw = (groove * 0.11)[..., None]
    col = col * (1 - gw)

    # --- discreet painted sheen on the back (the real wet look comes from the lustre map)
    sheen = np.exp(-(circ(U, 0.36) / 0.028) ** 2) * (1 - groove) * 0.08
    col = col + sheen[..., None]

    # --- head capsule and mouth
    head = C.smoothstep((V - HEAD_V0) / 0.05)
    hcol = COL_HEAD[None, None, :] * (0.85 + 0.25 * d)[..., None] * (1 - 0.25 * groove)[..., None]
    col = col * (1 - head[..., None]) + hcol * head[..., None]
    mouth = C.smoothstep((V - MOUTH_V0) / 0.008)
    col = col * (1 - mouth[..., None]) + COL_MOUTH[None, None, :] * mouth[..., None]
    # blunt tail tip a little darker
    tailm = (1.0 - C.smoothstep(V / 0.05)) * 0.15
    col = col * (1 - tailm[..., None])

    albedo = box_reduce(col, PAINT // TEX)
    C.write_png(os.path.join(out_dir, "worm_albedo.png"), C.to_u8(albedo))

    # --- height: ring ridges, transverse wrinkles, a few longitudinal creases
    wr = np.cos(2 * np.pi * WRINKLES * V + 2.0 * np.sin(2 * np.pi * U))
    wr_mod = C.smoothstep((C.fbm2_tile(H, W, 4, 5, octaves=2) - 0.35) / 0.3)
    crease = np.cos(2 * np.pi * (7 * U + 0.4 * np.sin(2 * np.pi * 2 * V)))
    height = (1 - groove) * 0.85 + 0.10 * wr * wr_mod + 0.05 * crease * (1 - groove)
    height = box_reduce(height, PAINT // TEX)
    C.write_png(os.path.join(out_dir, "worm_normal.png"), C.normal_map_from_height(height, 1.5))

    # --- lustre: RGB = 0, A = gloss (0.92 body, 0.5 folds, 0.7 head, 0.4 mandibles)
    g = GLOSS_BODY + (GLOSS_FOLD - GLOSS_BODY) * groove
    g = g * (1 - head) + GLOSS_HEAD * head
    g = g * (1 - mouth) + GLOSS_MOUTH * mouth
    g = box_reduce(g, PAINT // TEX)
    rgba = np.zeros((TEX, TEX, 4), dtype=np.uint8)
    rgba[..., 3] = C.to_u8(g)
    C.write_png(os.path.join(out_dir, "worm_gloss.png"), rgba)


def preview(items, out_dir):
    """items: list of (name, verts, faces, uv, x_offset[, z_offset])."""
    import bpy
    C.reset_scene()
    C.setup_world((0.05, 0.055, 0.06), 1.0)
    mat = C.make_pbr_material("worm", os.path.join(out_dir, "worm_albedo.png"),
                              os.path.join(out_dir, "worm_normal.png"), roughness=0.2, normal_strength=1.0,
                              specular=0.6, gloss_png=os.path.join(out_dir, "worm_gloss.png"), closest=True)
    for it in items:
        name, verts, faces, uv, xo = it[:5]
        zo = it[5] if len(it) > 5 else 0.0
        # turn the worm to face +X so a side-on camera sees its profile
        v = np.stack([verts[:, 2] + xo, verts[:, 1], -verts[:, 0] + zo], 1)
        o = C.make_object("p_" + name, v, faces, uv)
        o.data.materials.append(mat)
    C.add_ground(3.0, (0.16, 0.16, 0.17))
    return bpy


def render_previews(out_dir, prev_dir, frames, extra=None):
    import bpy
    faces, uvs, _ = topology()
    pick = [("crawl0", frames["worm_crawl_00"]), ("crawl4", frames["worm_crawl_04"]),
            ("idle2", frames["worm_idle_02"]), ("rear5", frames["worm_rear_05"]),
            ("strike3", frames["worm_strike_03"]), ("sleep0", frames["worm_sleep_00"])]
    # row of poses, true scale, with a ruler (dark/light = 5 cm)
    items = [(n, v, faces, uvs, -1.05 + 0.42 * i) for i, (n, v) in enumerate(pick)]
    preview(items, out_dir)
    C.add_scale_bar((-0.2, 0.0, 0.2), 0.4, 0.05)
    C.add_light("AREA", C.gltf_to_blender((0.4, 1.6, 1.2)), 60, size=1.5, target=C.gltf_to_blender((0.0, 0.1, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-1.5, 0.8, -1.0)), 40, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.1, 0.0)))
    C.add_camera(C.gltf_to_blender((0.0, 0.8, 5.0)), C.gltf_to_blender((0.0, 0.12, 0.0)), lens=60)
    C.configure_render(1400, 520, 48)
    C.render_to(os.path.join(prev_dir, "worm_poses.png"))

    # close-up of a single crawling worm from a 3/4 high angle
    preview([("close", frames["worm_crawl_02"], faces, uvs, 0.0)], out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.5, 0.8, 0.6)), 10, size=0.8, target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-0.6, 0.4, -0.5)), 4, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_camera(C.gltf_to_blender((0.28, 0.20, 0.42)), C.gltf_to_blender((0.0, 0.02, 0.0)), lens=60)
    C.configure_render(1000, 700, 64)
    C.render_to(os.path.join(prev_dir, "worm_closeup.png"))

    # the 16 crawl frames, 4 x 4, seen from above at an angle: the wave must travel without a jump
    items = []
    for k in range(CRAWL_FRAMES):
        items.append(("c%02d" % k, frames["worm_crawl_%02d" % k], faces, uvs, -0.75 + 0.5 * (k % 4), -0.75 + 0.5 * (k // 4)))
    preview(items, out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.0, 2.5, 0.5)), 160, size=2.5, target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-1.5, 1.0, -1.0)), 40, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_camera(C.gltf_to_blender((0.0, 3.0, 1.9)), C.gltf_to_blender((0.0, 0.0, 0.0)), lens=45)
    C.configure_render(1400, 1100, 32)
    C.render_to(os.path.join(prev_dir, "worm_crawl_sheet.png"))

    if extra:
        render_coil_previews(out_dir, prev_dir, extra)


def render_coil_previews(out_dir, prev_dir, extra):
    """Coil sheet (12 frames, 4 x 3, from above at an angle), a close-up, and LOD0 / LOD1 side by side."""
    faces, uvs = extra["faces"], extra["uvs"]
    cf = extra["coil"]
    items = []
    for k in range(COIL_FRAMES):
        items.append(("k%02d" % k, cf[k], faces, uvs, -0.60 + 0.40 * (k % 4), -0.40 + 0.40 * (k // 4)))
    preview(items, out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.0, 2.5, 0.5)), 160, size=2.5, target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-1.5, 1.0, -1.0)), 40, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_camera(C.gltf_to_blender((0.0, 2.4, 1.6)), C.gltf_to_blender((0.0, 0.0, 0.0)), lens=45)
    C.configure_render(1400, 1000, 32)
    C.render_to(os.path.join(prev_dir, "worm_coil_sheet.png"))

    items = [("close", cf[2], faces, uvs, 0.0)]
    preview(items, out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.5, 0.8, 0.6)), 10, size=0.8, target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-0.6, 0.4, -0.5)), 4, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.03, 0.0)))
    C.add_camera(C.gltf_to_blender((0.30, 0.34, 0.40)), C.gltf_to_blender((0.0, 0.02, 0.0)), lens=60)
    C.configure_render(1000, 700, 64)
    C.render_to(os.path.join(prev_dir, "worm_coil_closeup.png"))

    f1, u1 = extra["faces1"], extra["uvs1"]
    items = [("a0", extra["crawl0"], faces, uvs, -0.45, -0.25), ("a1", extra["crawl1"], f1, u1, 0.05, -0.25),
             ("b0", cf[2], faces, uvs, -0.45, 0.25), ("b1", extra["coil1"][1], f1, u1, 0.05, 0.25)]
    preview(items, out_dir)
    C.add_light("AREA", C.gltf_to_blender((0.0, 2.5, 0.5)), 120, size=2.5, target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-1.5, 1.0, -1.0)), 30, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.0, 0.0)))
    C.add_camera(C.gltf_to_blender((0.0, 1.5, 0.9)), C.gltf_to_blender((0.0, 0.0, 0.0)), lens=45)
    C.configure_render(1200, 700, 32)
    C.render_to(os.path.join(prev_dir, "worm_lod_compare.png"))


def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    import bpy
    C.reset_scene()

    faces, uvs, nverts = topology()
    frames = {}
    for k in range(CRAWL_FRAMES):
        frames["worm_crawl_%02d" % k] = pose(crawl_k=k)
    for k in range(IDLE_FRAMES):
        frames["worm_idle_%02d" % k] = pose(idle_k=k)
    for k in range(6):
        frames["worm_rear_%02d" % k] = pose(rear=k / 5.0)
    for k in range(4):
        e = k / 3.0
        frames["worm_strike_%02d" % k] = pose(rear=0.85 * (1.0 - e), lunge=0.15 * e)
    for k in range(4):
        frames["worm_sleep_%02d" % k] = pose(pulse=1.0 + 0.05 * np.sin(2 * np.pi * k / 4.0),
                                             flat=0.62, sink=0.28, curl=0.03)

    # origin under the centre of mass: shift every frame by the same Z (rest-pose mean of the body)
    ref = pose()[: 1 + (RINGS - 1) * AROUND + 1]
    zc = float(ref[:, 2].mean())
    for name in frames:
        frames[name] = frames[name] - np.array([0.0, 0.0, zc])

    C.check_outward(frames["worm_crawl_00"], faces, "worm")
    print("worm: %d vertices, %d triangles" % (nverts, len(faces)))
    for name in ("worm_crawl_00", "worm_crawl_05", "worm_idle_02", "worm_rear_05", "worm_strike_03", "worm_sleep_01"):
        v = frames[name]
        print("  %s min %s max %s" % (name, np.round(v.min(0), 3), np.round(v.max(0), 3)))

    obj = C.make_object("worm", frames["worm_crawl_00"], faces, uvs)
    for name, v in frames.items():
        C.set_positions(obj, v)
        C.export_glb(obj, os.path.join(out, name + ".glb"))

    # --- coil (12 frames) and the LOD1 variants; frame j of a LOD1 loop is pose 2j of the LOD0 loop, same origin as LOD0
    cframes = coil_frames()
    for k, v in enumerate(cframes):
        C.set_positions(obj, v)
        C.export_glb(obj, os.path.join(out, "worm_coil_%02d.glb" % k))
    faces1, uvs1, nverts1 = topology(LOD1_RINGS, LOD1_AROUND, False)
    lod1 = {}
    for j in range(CRAWL_FRAMES // 2):
        lod1["worm_crawl_lod1_%02d" % j] = pose(crawl_k=2 * j, rings=LOD1_RINGS, around=LOD1_AROUND, mandibles=False, bulge=0.0)
    for j in range(IDLE_FRAMES // 2):
        lod1["worm_idle_lod1_%02d" % j] = pose(idle_k=2 * j, rings=LOD1_RINGS, around=LOD1_AROUND, mandibles=False, bulge=0.0)
    for name in lod1:
        lod1[name] = lod1[name] - np.array([0.0, 0.0, zc])
    cf1 = coil_frames(LOD1_RINGS, LOD1_AROUND, False, 0.0, step=2, frames=COIL_FRAMES // 2)
    for j, v in enumerate(cf1):
        lod1["worm_coil_lod1_%02d" % j] = v
    C.check_outward(lod1["worm_crawl_lod1_00"], faces1, "worm_lod1")
    print("worm lod1: %d vertices, %d triangles; coil: %d triangles" % (nverts1, len(faces1), len(faces)))
    for name in ("worm_coil_00", "worm_coil_03", "worm_coil_06", "worm_coil_09"):
        v = cframes[int(name[-2:])]
        print("  %s min %s max %s" % (name, np.round(v.min(0), 3), np.round(v.max(0), 3)))
    obj1 = C.make_object("worm_lod1", lod1["worm_crawl_lod1_00"], faces1, uvs1)
    for name, v in lod1.items():
        C.set_positions(obj1, v)
        C.export_glb(obj1, os.path.join(out, name + ".glb"))
    make_textures(out)
    render_previews(out, prev, frames, extra={
        "faces": faces, "uvs": uvs, "coil": cframes, "faces1": faces1, "uvs1": uvs1, "coil1": cf1,
        "crawl0": frames["worm_crawl_00"], "crawl1": lod1["worm_crawl_lod1_00"]})
    C.stats("make_worm", t0)


if __name__ == "__main__":
    main()
