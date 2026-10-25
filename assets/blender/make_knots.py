"""Worm KNOTS (V2 visual rework, plan scratchpad visual-v2-plan.md section B, pass V2-A).

A knot is a tangle of worms precomputed as ONE mesh: 3 sizes (S: 8 worms in about 12 cm, M: 20 in 20 cm, L: 40 in 30 cm) x 2 variants (a: deformed
tori, b: braids + loose coils). Every worm of a knot follows its own space curve (deformed torus lap, braid strand around a curved axis, random coil
or a stray that leaves the volume); worms of a knot touch and cross each other: the generator measures the contacts at every frame and retries other
seeds until the contact graph is connected and dense enough. Flipbook of 12 frames, a perfect loop: each worm undulates along its curve (travelling
wave, peristalsis) and slides a little along it. LOD1: 6 rings x 3 around per worm (30 triangles), frames 2j of the loop at half the rate.

Same material and textures as the worm (worm_albedo / worm_normal / worm_gloss), UVs follow the worm topology (U around, V tail -> head).
Origin: centroid of the vertices of the 12 frames. Also renders the previews of the knots, the static dome composition of about 60 knots and the
swarm GIF of an M knot.
Run: blender --background --python make_knots.py
"""
import os
import subprocess
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import config  # noqa: E402
import make_worm as W  # noqa: E402  (profile, topology, insets of the worm; importing it only defines functions and constants)

FRAMES = 12
K_RINGS, K_AROUND = 12, 5            # LOD0 worm of a knot: 2 x 5 x 11 = 110 triangles
K1_RINGS, K1_AROUND = 6, 3           # LOD1 worm of a knot: 2 x 3 x 5 = 30 triangles
WORM_LEN = W.BODY_LEN                # 0.15 m
SLIDE = 0.012                        # amplitude of the slide along the curve (m)
PATH_LEN = WORM_LEN + 2 * SLIDE      # length of every worm curve
STEP = 0.0005                        # arc-length step of the dense curves
CONTACT = 0.0098                     # centre line distance under which two worms touch (body diameter 0.009 + 8 %)
DEEP = 0.0035                        # centre line distance under which two worms interpenetrate deeply
MIN_CURV_RADIUS = 0.010              # a curve may not bend tighter than this (m): the tube would fold on its inner side

# size -> core radius of the dense bulk (m). The nominal volume (12 / 20 / 30 cm) is reached by the strays and the protruding loops.
CORE = {"s": 0.036, "m": 0.052, "l": 0.070}
# (size, variant) -> list of (group kind, worm count)
RECIPE = {
    ("s", "a"): [("torus", 3), ("torus", 3), ("pelote", 1), ("stray", 1)],
    ("s", "b"): [("braid", 3), ("pelote", 3), ("stray", 2)],
    ("m", "a"): [("torus", 4), ("torus", 4), ("torus", 4), ("pelote", 5), ("stray", 3)],
    ("m", "b"): [("braid", 4), ("braid", 3), ("pelote", 10), ("stray", 3)],
    ("l", "a"): [("torus", 5), ("torus", 5), ("torus", 5), ("torus", 5), ("pelote", 14), ("stray", 6)],
    ("l", "b"): [("braid", 4), ("braid", 4), ("braid", 3), ("pelote", 22), ("stray", 7)],
}
# minimum mean number of touching neighbours per worm (all frames)
MIN_MEAN_DEG = {"s": 2.6, "m": 3.6, "l": 4.6}


# =========================================================================== curves
class Curve:
    """A space curve resampled every STEP metres, with a parallel-transport frame (T tangent, U, V = T x U)."""

    def __init__(self, pts):
        pts = np.asarray(pts, dtype=np.float64)
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        s = np.concatenate([[0.0], np.cumsum(seg)])
        n = int(s[-1] / STEP) + 1
        ss = np.arange(n) * STEP
        self.P = np.stack([np.interp(ss, s, pts[:, c]) for c in range(3)], 1)
        self.n = n
        self.length = ss[-1]
        self.T = C.normalize(np.gradient(self.P, axis=0))
        ref = np.array([0.31, 0.52, 0.79])
        u = ref - np.dot(ref, self.T[0]) * self.T[0]
        U = np.zeros_like(self.T)
        for i in range(n):
            u = u - np.dot(u, self.T[i]) * self.T[i]
            u = u / max(np.linalg.norm(u), 1e-9)
            U[i] = u
        self.U = U
        self.V = np.cross(self.T, U)

    def at(self, s):
        """Position, tangent, U, V at arc lengths s (clamped to the curve)."""
        x = np.clip(np.asarray(s) / STEP, 0, self.n - 1.001)
        i = np.floor(x).astype(int)
        f = (x - i)[:, None]
        out = [a[i] * (1 - f) + a[i + 1] * f for a in (self.P, self.T, self.U, self.V)]
        return out

    def max_curvature(self):
        dT = np.linalg.norm(np.diff(self.T, axis=0), axis=1) / STEP
        k = np.convolve(dT, np.ones(9) / 9.0, mode="same")
        return float(k[5:-5].max())


def window(P, start, length):
    """Sub-polyline of P (any sampling) starting at arc length `start` and `length` long; None when it does not fit."""
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    if start + length > s[-1]:
        return None
    ss = np.linspace(start, start + length, int(length / (STEP * 0.5)))
    return np.stack([np.interp(ss, s, P[:, c]) for c in range(3)], 1)


def random_rotation(rng):
    q = rng.normal(size=4)
    q = q / np.linalg.norm(q)
    a, b, c, d = q
    return np.array([[a * a + b * b - c * c - d * d, 2 * (b * c - a * d), 2 * (b * d + a * c)],
                     [2 * (b * c + a * d), a * a - b * b + c * c - d * d, 2 * (c * d - a * b)],
                     [2 * (b * d - a * c), 2 * (c * d + a * b), a * a - b * b - c * c + d * d]])


def walk_curve(rng, centre, amp, length=PATH_LEN, kmax=1.0 / 0.022, ell=0.04):
    """A worm-like curve of `length` metres: a unit-speed walk whose curvature (at most kmax) is a smooth random process, steered back
    toward `centre` when it leaves the ball of radius ~amp (so the curve is a loose coil that stays in the ball)."""
    n = int(length / STEP) + 2
    p = centre + amp * 0.5 * rng.normal(size=3) / 1.7
    t = C.normalize(rng.normal(size=3))
    U = C.normalize(np.cross(t, rng.normal(size=3)))
    a = np.zeros(2)
    pts = np.zeros((n, 3))
    for i in range(n):
        pts[i] = p
        V = np.cross(t, U)
        a = a * (1.0 - STEP / ell) + np.sqrt(2.0 * STEP / ell) * kmax * 0.75 * rng.normal(size=2)
        kv = a[0] * U + a[1] * V
        off = centre - p
        d = float(np.linalg.norm(off))
        if d > 0.55 * amp:
            w = min(1.5, (d - 0.55 * amp) / (0.45 * amp))
            to = off / d
            perp = to - np.dot(to, t) * t
            pn = float(np.linalg.norm(perp))
            perp = perp / pn if pn > 0.3 else U
            kv = kv + w * kmax * perp
        kn = float(np.linalg.norm(kv))
        if kn > kmax:
            kv = kv * (kmax / kn)
        t = C.normalize(t + kv * STEP)
        U = C.normalize(U - np.dot(U, t) * t)
        p = p + t * STEP
    return Curve(pts)


def torus_group(rng, core, n):
    """n worms winding around ONE deformed torus knot (the minor phases are spread, so the worms braid around each other)."""
    for _ in range(40):
        R0 = core * rng.uniform(0.55, 0.8)
        r0 = core * rng.uniform(0.28, 0.40)
        q = int(rng.choice([2, 3]))
        squash = np.array([1.0, rng.uniform(0.7, 1.0), rng.uniform(0.7, 1.0)])
        wphase = rng.uniform(0, 2 * np.pi, 3)
        wfreq = rng.uniform(1.5, 2.8, 3)
        rot = random_rotation(rng)
        shift = rng.uniform(-0.15, 0.15, 3) * core
        base = rng.uniform(0, 2 * np.pi)
        laps = []
        phi = np.linspace(0, 4 * np.pi, 9000)       # two laps: the windows may wrap around
        for i in range(n):
            psi = base + 2 * np.pi * i / n + rng.normal(0, 0.15)
            rho = R0 + r0 * np.cos(q * phi + psi)
            p = np.stack([rho * np.cos(phi), rho * np.sin(phi), r0 * np.sin(q * phi + psi)], 1) * squash[None, :]
            warp = 0.18 * core * np.stack([np.sin(wfreq[0] * p[:, 1] / core + wphase[0]),
                                           np.sin(wfreq[1] * p[:, 2] / core + wphase[1]),
                                           np.sin(wfreq[2] * p[:, 0] / core + wphase[2])], 1)
            laps.append((p + warp) @ rot.T + shift[None, :])
        lap_len = np.linalg.norm(np.diff(laps[0][:4500], axis=0), axis=1).sum()    # one lap = first half
        if lap_len < 1.15 * PATH_LEN:
            continue
        curves = []
        for p in laps:
            w = window(p, rng.uniform(0, lap_len), PATH_LEN)
            if w is None:
                break
            curves.append(Curve(w))
        if len(curves) == n and all(c.max_curvature() < 1.0 / MIN_CURV_RADIUS for c in curves):
            return curves
    raise RuntimeError("no torus group")


def helix_points(axis, a0, a1, rho, pitch, phase):
    """Polyline of a worm winding around `axis` (Curve) between the arc lengths a0 and a1 at distance rho."""
    a = np.arange(a0, a1, STEP * 0.5)
    P, T, U, V = axis.at(a)
    th = 2 * np.pi * a / pitch + phase
    return P + rho * (np.cos(th)[:, None] * U + np.sin(th)[:, None] * V)


def braid_group(rng, core, n):
    """n worms twisted around a curved axis (a braid): neighbours are 2 rho sin(pi / n) < body diameter apart, so they touch."""
    rho = 0.0042
    for _ in range(40):
        pitch = rng.uniform(0.032, 0.045)
        stretch = np.sqrt(1.0 + (2 * np.pi * rho / pitch) ** 2)
        alen = PATH_LEN * 1.06 / stretch
        axis = walk_curve(rng, rng.uniform(-0.2, 0.2, 3) * core, core * 0.85, alen, kmax=1.0 / 0.03)
        if axis.length < alen * 0.98:
            continue
        ph0 = rng.uniform(0, 2 * np.pi)
        curves = []
        for i in range(n):
            pts = helix_points(axis, 0.0, axis.length - STEP, rho, pitch, ph0 + 2 * np.pi * i / n)
            w = window(pts, 0.0, PATH_LEN)
            if w is None:
                break
            curves.append(Curve(w))
        if len(curves) == n and all(c.max_curvature() < 1.0 / MIN_CURV_RADIUS for c in curves):
            return curves
    raise RuntimeError("no braid group")


def make_worms(rng, size, variant):
    """The worm specs of one knot attempt: dict(curve, kind, gen, flip, psi, twist, amp, lam, m, ph, slide_ph). `gen(rng)` redraws the curve
    of a single coil or stray (torus and braid worms belong to a group and are never redrawn alone)."""
    core = CORE[size]
    vol = config.KNOT_VOLUME[size]

    def pelote(r):
        return walk_curve(r, r.uniform(-0.25, 0.25, 3) * core, core * 0.9)

    def stray(r):
        d = r.normal(size=3)
        d = d / np.linalg.norm(d)
        return walk_curve(r, d * (vol / 2 - 0.45 * 0.9 * core), 0.9 * core)

    items = []
    for kind, n in RECIPE[(size, variant)]:
        if kind == "torus":
            items += [(kind, c, None) for c in torus_group(rng, core, n)]
        elif kind == "braid":
            items += [(kind, c, None) for c in braid_group(rng, core, n)]
        else:
            gen = pelote if kind == "pelote" else stray
            items += [(kind, gen(rng), gen) for _ in range(n)]
    specs = []
    for kind, cv, gen in items:
        specs.append({"curve": cv, "kind": kind, "gen": gen, "flip": bool(rng.random() < 0.5), "psi": rng.uniform(0, 2 * np.pi),
                      "twist": rng.uniform(-25.0, 25.0), "amp": rng.uniform(0.0028, 0.0042), "lam": rng.uniform(0.055, 0.075),
                      "m": int(rng.choice([1, 1, 2])), "ph": rng.uniform(0, 2 * np.pi), "slide_ph": rng.uniform(0, 2 * np.pi)})
    return specs


# =========================================================================== worms along curves
def centerline(sp, k, rings):
    """Ring centres (rings + 1, 3) and the reference vector A (rings + 1, 3) of one worm at flipbook frame k (a loop of FRAMES)."""
    tau = 2 * np.pi * k / FRAMES
    u = np.arange(rings + 1) / rings
    s_start = SLIDE * (1.0 + np.sin(tau + sp["slide_ph"]))
    s = s_start + u * WORM_LEN
    if sp["flip"]:
        s = sp["curve"].length - s
    P, T, U, V = sp["curve"].at(s)
    th = sp["psi"] + sp["twist"] * (u * WORM_LEN)
    d = np.cos(th)[:, None] * U + np.sin(th)[:, None] * V
    amp = sp["amp"] * (1.0 + 0.6 * (1.0 - u))
    wave = np.sin(2 * np.pi * (u * WORM_LEN / sp["lam"]) - sp["m"] * tau + sp["ph"])
    P = P + (amp * wave)[:, None] * d
    A = U
    return P, A


def tube(P, A, rb, around):
    """Ring vertices around the centre line P: tail pole, (rings - 1) rings of `around`, head pole (the layout of W.topology)."""
    T = C.normalize(np.gradient(P, axis=0))
    Av = A - (A * T).sum(1, keepdims=True) * T
    Av = C.normalize(Av)
    Nv = np.cross(T, Av)
    ph = 2 * np.pi * np.arange(around) / around
    cs, sn = np.cos(ph), np.sin(ph)
    ring = P[1:-1, None, :] + rb[1:-1, None, None] * (cs[None, :, None] * Av[1:-1, None, :] + sn[None, :, None] * Nv[1:-1, None, :])
    tail = P[0] + T[0] * W.TAIL_INSET
    head = P[-1] - T[-1] * W.HEAD_INSET
    return np.concatenate([tail[None, :], ring.reshape(-1, 3), head[None, :]], 0)


def worm_verts(sp, k, rings, around, bulge=True):
    P, A = centerline(sp, k, rings)
    u = np.arange(rings + 1) / rings
    tau = 2 * np.pi * k / FRAMES
    rb = np.interp(u, W.PROFILE_U, W.PROFILE_R)
    rb = rb * (1.0 + 0.16 * np.sin(2 * np.pi * (u * WORM_LEN / W.CRAWL_PERI_LEN) - sp["m"] * tau + sp["ph"]))
    rb[0] = rb[-1] = 0.0
    return tube(P, A, rb, around)


def knot_verts(specs, k, rings, around):
    return np.concatenate([worm_verts(sp, k, rings, around) for sp in specs], 0)


def knot_topology(n, rings, around):
    f, uv, nv = W.topology(rings, around, mandibles=False)
    faces = np.concatenate([f + i * nv for i in range(n)], 0)
    uvs = np.concatenate([uv] * n, 0)
    return faces, uvs, nv


# =========================================================================== contact analysis
def analyse(specs, frames):
    """Contacts between the centre lines of the worms (40 samples each) at the given frames."""
    n = len(specs)
    mean_deg = []
    min_deg = []
    connected = True
    deep = []
    pairs = 0
    lost = set()
    for k in frames:
        pts = np.stack([centerline(sp, k, 40)[0] for sp in specs], 0)            # (n, 41, 3)
        flat = pts.reshape(-1, 3)
        d = np.linalg.norm(flat[:, None, :] - flat[None, :, :], axis=2).reshape(n, 41, n, 41)
        dmin = d.min(axis=(1, 3))                                                  # (n, n) closest approach of two worms
        np.fill_diagonal(dmin, 1e9)
        adj = dmin <= CONTACT
        deg = adj.sum(1)
        mean_deg.append(deg.mean())
        min_deg.append(deg)
        # connectivity
        seen = {0}
        stack = [0]
        while stack:
            i = stack.pop()
            for j in np.nonzero(adj[i])[0]:
                if int(j) not in seen:
                    seen.add(int(j))
                    stack.append(int(j))
        connected = connected and len(seen) == n
        lost.update(set(range(n)) - seen)
        dd = d.copy()
        for i in range(n):
            dd[i, :, i, :] = 1e9
        deep.append(float((dd.min(axis=(2, 3)) < DEEP).mean()))                    # share of the samples buried in another worm
        pairs += int(np.triu(adj, 1).sum())
    return {"mean_deg": float(np.mean(mean_deg)), "min_deg": np.min(np.stack(min_deg, 0), axis=0), "connected": connected,
            "deep": float(np.mean(deep)), "pairs": pairs / len(frames),
            "lost": lost}


def build_knot(size, variant):
    """First seed whose knot is one connected tangle (every worm touches another one, every frame), dense, and not too buried.
    A coil or stray that does not touch the tangle is redrawn alone (up to 40 times) before the seed is given up."""
    last = None
    lo, hi = config.KNOT_EXTENT[size]
    lo = lo * 1.12                      # the validator checks every frame; the generator looks at frames 0 and 6 only
    for attempt in range(400):
        rng = np.random.default_rng([config.SEED, 7001, ord(size), ord(variant), attempt])
        try:
            specs = make_worms(rng, size, variant)
        except RuntimeError as e:
            last = str(e)
            continue
        need = np.array([1 if sp["kind"] == "stray" else 2 for sp in specs])
        st = None
        for repair in range(41):
            st = analyse(specs, (0, 3, 6, 9))
            bad = set(np.nonzero(st["min_deg"] < need)[0].tolist()) | st["lost"]
            if not bad:
                break
            free = [i for i in bad if specs[i]["gen"] is not None]
            if not free:
                break
            for i in free:
                specs[i]["curve"] = specs[i]["gen"](rng)
        ext = np.concatenate([knot_verts(specs, k, K_RINGS, K_AROUND) for k in (0, 6)], 0)
        side = float((ext.max(0) - ext.min(0)).max())
        ok = (st["connected"] and (st["min_deg"] >= need).all() and st["mean_deg"] >= MIN_MEAN_DEG[size]
              and st["deep"] < 0.18 and lo <= side <= hi)
        last = "attempt %d: connected %s, mean deg %.2f, min deg %d, deep %.2f, side %.3f" % (
            attempt, st["connected"], st["mean_deg"], st["min_deg"].min(), st["deep"], side)
        if ok:
            st["attempt"] = attempt
            return specs, st
    raise RuntimeError("knot %s_%s: no acceptable tangle in 400 attempts (%s)" % (size, variant, last))


# =========================================================================== previews
def setup_scene(build, bg=(0.2, 0.2, 0.22)):
    C.reset_scene()
    C.setup_world(bg, 1.0)
    return C.make_pbr_material("worm", os.path.join(build, "worm_albedo.png"), os.path.join(build, "worm_normal.png"),
                               roughness=0.2, normal_strength=1.0, specular=0.6,
                               gloss_png=os.path.join(build, "worm_gloss.png"), closest=True)


def add_mesh(name, verts, faces, uvs, mat):
    o = C.make_object(name, verts, faces, uvs)
    o.data.materials.append(mat)
    return o


def rot_matrix(yaw, tx, tz):
    cy, sy, cx, sx, cz, sz = np.cos(yaw), np.sin(yaw), np.cos(tx), np.sin(tx), np.cos(tz), np.sin(tz)
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return ry @ rx @ rz


def render_knot_previews(build, prev, data):
    """data: key 's_a' -> dict(frames (LOD0 list of verts), faces, uvs)."""
    # S, M, L side by side, variants a (front row) and b (back row), 3/4 view, neutral background
    mat = setup_scene(build)
    xs = {"s": -0.30, "m": 0.0, "l": 0.36}
    for key, d in data.items():
        v = d["frames"][0]
        size, var = key.split("_")
        z = 0.0 if var == "a" else -0.40
        y0 = -v[:, 1].min()
        add_mesh("k_" + key, v + np.array([xs[size], y0, z]), d["faces"], d["uvs"], mat)
    C.add_ground(4.0, (0.30, 0.30, 0.32))
    C.add_scale_bar((-0.5, 0.0, 0.32), 1.0, 0.1, height=0.004, depth=0.012)
    C.add_light("AREA", C.gltf_to_blender((0.8, 1.6, 1.4)), 90, size=1.5, target=C.gltf_to_blender((0.0, 0.08, -0.2)))
    C.add_light("AREA", C.gltf_to_blender((-1.2, 0.9, -0.2)), 35, size=1.5, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.08, -0.2)))
    C.add_camera(C.gltf_to_blender((0.12, 0.70, 1.10)), C.gltf_to_blender((0.03, 0.06, -0.20)), lens=36)
    C.configure_render(1600, 800, 48)
    C.render_to(os.path.join(prev, "knots_sml_34.png"))

    # M knot close-up
    mat = setup_scene(build)
    d = data["m_a"]
    v = d["frames"][3]
    add_mesh("k", v + np.array([0.0, -v[:, 1].min(), 0.0]), d["faces"], d["uvs"], mat)
    C.add_ground(2.0, (0.30, 0.30, 0.32))
    C.add_light("AREA", C.gltf_to_blender((0.5, 0.9, 0.7)), 22, size=0.8, target=C.gltf_to_blender((0.0, 0.08, 0.0)))
    C.add_light("AREA", C.gltf_to_blender((-0.7, 0.5, -0.2)), 8, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.08, 0.0)))
    C.add_camera(C.gltf_to_blender((0.26, 0.30, 0.42)), C.gltf_to_blender((0.0, 0.08, 0.0)), lens=55)
    C.configure_render(1200, 800, 64)
    C.render_to(os.path.join(prev, "knot_m_closeup.png"))


def dome_instances(data, radius=1.0, seed=11):
    """About 60 knots in a low dome of radius ~1 m: a ground layer of 40 (Vogel spiral) and a second layer of 20 on top. Returns
    (verts, faces, uvs) per instance."""
    rng = np.random.default_rng([config.SEED, seed])
    kinds1 = ["l"] * 12 + ["m"] * 16 + ["s"] * 12
    kinds2 = ["m"] * 10 + ["s"] * 10
    rng.shuffle(kinds1)
    rng.shuffle(kinds2)
    golden = np.pi * (3.0 - np.sqrt(5.0))
    out = []
    for layer, (kinds, rad) in enumerate(((kinds1, 0.97 * radius), (kinds2, 0.62 * radius))):
        n = len(kinds)
        for i, kind in enumerate(kinds):
            r = rad * np.sqrt((i + 0.5) / n) + rng.normal(0, 0.03)
            a = i * golden + rng.normal(0, 0.15)
            x, z = r * np.cos(a), r * np.sin(a)
            key = "%s_%s" % (kind, "ab"[int(rng.integers(2))])
            d = data[key]
            v = d["frames"][int(rng.integers(FRAMES))]
            R = rot_matrix(rng.uniform(0, 2 * np.pi), rng.normal(0, 0.25), rng.normal(0, 0.25))
            v = v @ R.T
            rr = min(r / (0.97 * radius), 1.0)
            lift = (0.005 + 0.03 * np.sqrt(max(0.0, 1 - rr * rr))) if layer == 0 else (0.06 + 0.10 * np.sqrt(max(0.0, 1 - (r / (0.7 * radius)) ** 2)))
            v = v + np.array([x, -np.percentile(v[:, 1], 4) + lift, z])      # the loose tips may sink a little into the ground
            out.append((v, d["faces"], d["uvs"]))
    return out


def render_dome(build, prev, data):
    """The composition of the plan (r = 1 m), and the same 60 knots packed into r = 0.7 m (what a smaller heap would look like)."""
    for radius, tag in ((1.0, ""), (0.7, "_r07")):
        inst = dome_instances(data, radius)
        print("dome r=%.1f: %d knots, %d triangles" % (radius, len(inst), sum(len(f) for _, f, _ in inst)))
        for name, cam, tgt, lens in (("knots_dome%s_34.png" % tag, (1.5, 1.35, 2.3), (0.0, 0.12, 0.0), 42),
                                     ("knots_dome%s_low.png" % tag, (0.0, 0.16, 2.9), (0.0, 0.14, 0.0), 40)):
            mat = setup_scene(build)
            for i, (v, f, uv) in enumerate(inst):
                add_mesh("d%d" % i, v, f, uv, mat)
            C.add_ground(8.0, (0.30, 0.30, 0.32))
            C.add_scale_bar((-0.5, 0.0, 1.3), 1.0, 0.1, height=0.006, depth=0.02)
            C.add_light("AREA", C.gltf_to_blender((1.8, 3.0, 2.2)), 330, size=3.0, target=C.gltf_to_blender((0.0, 0.1, 0.0)))
            C.add_light("AREA", C.gltf_to_blender((-2.5, 1.4, -1.5)), 90, size=3.0, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.1, 0.0)))
            C.add_camera(C.gltf_to_blender(cam), C.gltf_to_blender(tgt), lens=lens)
            C.configure_render(1600, 900, 32)
            C.render_to(os.path.join(prev, name))


def render_gif(build, prev, d, gif_name="knot_m_swarm.gif"):
    """The 12 frames of one knot, looped (83 ms = 12 fps), static camera."""
    gif_dir = C.ensure_dir(os.path.join(prev, "_gif_knot"))
    for k in range(FRAMES):
        mat = setup_scene(build)
        v = d["frames"][k]
        add_mesh("k", v + np.array([0.0, d["y0"], 0.0]), d["faces"], d["uvs"], mat)
        C.add_ground(2.0, (0.30, 0.30, 0.32))
        C.add_light("AREA", C.gltf_to_blender((0.5, 0.9, 0.7)), 22, size=0.8, target=C.gltf_to_blender((0.0, 0.08, 0.0)))
        C.add_light("AREA", C.gltf_to_blender((-0.7, 0.5, -0.2)), 8, size=0.8, color=(0.7, 0.8, 1.0), target=C.gltf_to_blender((0.0, 0.08, 0.0)))
        C.add_camera(C.gltf_to_blender((0.26, 0.30, 0.42)), C.gltf_to_blender((0.0, 0.08, 0.0)), lens=55)
        C.configure_render(640, 480, 24)
        C.render_to(os.path.join(gif_dir, "f%02d.png" % k))
    script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "make_gif.py")
    try:
        subprocess.run(["py", "-3", script, gif_dir, os.path.join(prev, gif_name), "83"], check=True)
    except Exception as e:  # Pillow lives in the system Python, not in Blender's
        print("GIF not assembled (%s): frames stay in %s" % (e, gif_dir))


# =========================================================================== main
def main():
    t0 = time.time()
    args = C.parse_args()
    out = C.ensure_dir(args["build"])
    prev = C.ensure_dir(args["previews"])
    import bpy
    C.reset_scene()
    data = {}
    for size in "sml":
        for var in "ab":
            key = "%s_%s" % (size, var)
            specs, st = build_knot(size, var)
            n = len(specs)
            assert n == config.KNOT_WORMS[size], (key, n)
            f0 = [knot_verts(specs, k, K_RINGS, K_AROUND) for k in range(FRAMES)]
            f1 = [knot_verts(specs, 2 * j, K1_RINGS, K1_AROUND) for j in range(FRAMES // 2)]
            c = np.concatenate(f0, 0).mean(0)
            f0 = [v - c for v in f0]
            f1 = [v - c for v in f1]
            faces0, uvs0, nv0 = knot_topology(n, K_RINGS, K_AROUND)
            faces1, uvs1, nv1 = knot_topology(n, K1_RINGS, K1_AROUND)
            # winding check on a straight worm (a tangle fools the centroid test)
            line = np.stack([np.zeros(K_RINGS + 1), np.zeros(K_RINGS + 1), np.linspace(0, WORM_LEN, K_RINGS + 1)], 1)
            rb = np.interp(np.arange(K_RINGS + 1) / K_RINGS, W.PROFILE_U, W.PROFILE_R)
            C.check_outward(tube(line, np.tile([1.0, 0.0, 0.0], (K_RINGS + 1, 1)), rb, K_AROUND), W.topology(K_RINGS, K_AROUND, False)[0], key)
            ext = np.concatenate(f0, 0)
            print("knot_%s: %d worms, LOD0 %d tris / LOD1 %d tris, extent %s, contacts: mean %.2f per worm (min %d), %.1f touching pairs, "
                  "%.0f %% buried samples, attempt %d" % (key, n, len(faces0), len(faces1), np.round(ext.max(0) - ext.min(0), 3),
                                                       st["mean_deg"], st["min_deg"].min(), st["pairs"], 100 * st["deep"], st["attempt"]))
            obj = C.make_object("knot_" + key, f0[0], faces0, uvs0)
            for k, v in enumerate(f0):
                C.set_positions(obj, v)
                C.export_glb(obj, os.path.join(out, "knot_%s_%02d.glb" % (key, k)))
            obj1 = C.make_object("knot_%s_lod1" % key, f1[0], faces1, uvs1)
            for j, v in enumerate(f1):
                C.set_positions(obj1, v)
                C.export_glb(obj1, os.path.join(out, "knot_%s_lod1_%02d.glb" % (key, j)))
            data[key] = {"frames": f0, "faces": faces0, "uvs": uvs0, "y0": -ext[:, 1].min()}
    render_knot_previews(out, prev, data)
    render_dome(out, prev, data)
    render_gif(out, prev, data["m_a"])
    C.stats("make_knots", t0)


if __name__ == "__main__":
    main()
