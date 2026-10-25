"""Reference implementation of the heap layout (REVISION 3: organic lobes, no core, volume filling, engulf): the Python original of
`MassLayout.Evaluate` (plan 4B-3, passes A4 and C).

THE C# PORT (src/MistWorms/Mass/MassLayout.cs, pass C) IS WRITTEN FROM THIS FILE, LINE BY LINE. Rules that make that direct:

* Pure Python + numpy scalars, NO bpy. Everything is `np.float32` (C# `float`); integers are Python ints (C# `uint`/`int`).
  Every numeric literal goes through `_()` (C# `1.0f`); never mix a Python number with a float32 (NumPy 1.x would silently
  promote to float64). The same code gives the same bits under NumPy 1.26 (Blender) and NumPy 2.x.
* Randomness: `lowbias32` hash only (`hash_u32`, `hash01`). Trigonometry: only `SIN_LUT` (256 entries, linear interpolation,
  argument in TURNS) in the per-frame code; `math.sin` is used only to build the table and `math.cos` nowhere.
  `sqrt` is one per Surface() call, `1/sqrt` one per normal. No `atan2`, no `pow` per frame.
* Constants: `config.MASS_LAYOUT` (copied in `manifest.json` and in `MassLayoutConst.cs`). Angles there are in degrees; this
  file converts them to TURNS once (`K.*_t`). One turn = 360 degrees.
* The reference is stateless: the boil line of a flux worm is a pure function of (seed, slot, cycle c). The C# keeps `cycle[i]`
  as a cache and recomputes the line only when `floor(lambda)` changes; the result must be identical.

COORDINATES. Local frame of the heap, origin on the ground under the centre: +X = forward (direction of travel), +Y = up,
+Z = LEFT. Angles of the ground plane are in turns from +X toward +Z. With Unity's world (X right, Y up, Z forward) the map
local -> world is  x*Fm + y*Um + z*Lm  (Fm forward, Lm = -right): a proper rotation, so per worm `R = cross(U, F)` works in both.
A worm instance has columns  R*s, U*s, F*s, pos  (mesh +X -> R, +Y -> U, +Z -> F, scale s); Unity's mesh X flip is the loader's.

ANIMS (WormAnim in WormRenderer.cs): Crawl 0, Rear 1, Strike 2, Sleep 3, Idle 4, Coil 5 (new). Frames: crawl 16, idle 8, coil 12.
ROLES: 0 body (coil under the surface), 1 flux (crawl on the surface, belt of the treadmill), 2 border (loops on the ground).

Time `t` is the 120 s clock (`ClockPeriod`); every periodic term here has an integer number of cycles in 120 s. The distance
terms (`tread`, `boil`, `odoFlux`, `odoBorder`) are integrators kept by the caller (see MassFrameParams below).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402

F32 = np.float32


def _(x):
    return np.float32(x)


# ---------------------------------------------------------------------------------------------------- constants
class _K:
    """MASS_LAYOUT as float32 (angles degrees -> turns, `*_t`)."""


K = _K()
for _k, _v in config.MASS_LAYOUT.items():
    if isinstance(_v, list):
        setattr(K, _k, [_(x) if isinstance(x, float) else x for x in _v])
    elif isinstance(_v, int):
        setattr(K, _k, _v)
    else:
        setattr(K, _k, _(_v))
for _k in ("BodyRollMax", "BodyPitchMax", "EmergePitch", "DivePitch", "TreadLaneFan", "FluxRollMax", "ArmTipPitch", "EngulfAcrossPitch", "EngulfPitchMin", "EngulfPitchMax"):
    setattr(K, _k + "_t", _(config.MASS_LAYOUT[_k]) / _(360.0))
K.DRIFT_RATE = [_(1.0) / x for x in K.BodyDriftPeriods]       # turns per second (signed)
K.TWO_PI = _(2.0 * math.pi)
K.GOLDEN_TURN = _(0.3819660112501051)                          # 1 - 1/phi, boil azimuth step per cycle

ROLE_BODY, ROLE_FLUX, ROLE_BORDER = 0, 1, 2
ANIM_CRAWL, ANIM_REAR, ANIM_STRIKE, ANIM_SLEEP, ANIM_IDLE, ANIM_COIL = 0, 1, 2, 3, 4, 5
CRAWL_FRAMES, IDLE_FRAMES, COIL_FRAMES = 16, 8, 12

SIN_LUT = [_(math.sin(2.0 * math.pi * i / 256.0)) for i in range(256)] + [_(0.0)]   # 257 entries: index 256 = wrap


# ---------------------------------------------------------------------------------------------------- hash
M32 = 0xFFFFFFFF


def lowbias32(x):
    x &= M32
    x ^= x >> 16
    x = (x * 0x7FEB352D) & M32
    x ^= x >> 15
    x = (x * 0x846CA68B) & M32
    x ^= x >> 16
    return x


def hash_u32(seed, a, b):
    """H(seed, a, b): two chained lowbias32 rounds. Arguments are 32-bit unsigned integers."""
    h = lowbias32((seed & M32) ^ lowbias32((a + 0x9E3779B9) & M32))
    return lowbias32(h ^ lowbias32((b + 0x85EBCA6B) & M32))


def hash01(seed, a, b):
    """[0,1) with 24 bits: exact in float32."""
    return _(hash_u32(seed, a, b) >> 8) * _(1.0 / 16777216.0)


def seed_from_ids(uid_user, uid_id, instance_id=None):
    """Seed of a heap: H(uid ^ uid>>32) ^ H(id * 0x9E3779B9); without a ZDO, GetInstanceID. (Documented; the golden uses 12345.)"""
    if instance_id is not None:
        return lowbias32(instance_id)
    u = (uid_user ^ (uid_user >> 32)) & M32
    return lowbias32(u) ^ lowbias32((uid_id * 0x9E3779B9) & M32)


# ---------------------------------------------------------------------------------------------------- scalar helpers
def frac(x):
    return x - np.floor(x)


def lerp(a, b, t):
    return a + (b - a) * t


def clamp01(x):
    return _(0.0) if x < _(0.0) else (_(1.0) if x > _(1.0) else x)


def smooth01(x):
    x = clamp01(x)
    return x * x * (_(3.0) - _(2.0) * x)


def sin_t(turns):
    """sin(2 pi turns) from the 256 entry table, linear interpolation; period 1."""
    x = frac(turns) * _(256.0)
    if x >= _(256.0):                                # frac() can round up to exactly 1.0
        x = x - _(256.0)
    i = int(np.floor(x))
    fr = x - _(i)
    a = SIN_LUT[i]
    return a + (SIN_LUT[i + 1] - a) * fr


def cos_t(turns):
    return sin_t(turns + _(0.25))


def rsqrt(x):
    return _(1.0) / np.sqrt(x)


# ---------------------------------------------------------------------------------------------------- vectors (tuples of float32)
def v3(x, y, z):
    return (_(x), _(y), _(z))


def vadd(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vmul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def vdot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def vcross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def vlerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def vnorm(a, fallback):
    l2 = vdot(a, a)
    if l2 < _(1e-10):
        return fallback
    return vmul(a, rsqrt(l2))


UP = v3(0, 1, 0)
FWD = v3(1, 0, 0)


# ---------------------------------------------------------------------------------------------------- inputs / outputs
class MassFrameParams:
    """Inputs of one frame (the C# struct MassFrameParams). `r` is the smoothed displayed radius r(n). Local frame: +X forward, +Y up, +Z left."""

    def __init__(self, r, n, sigma=0.0, streamK=0.0, ribbonW=0.0, ribbonL=0.0, t=0.0,
                 tread=0.0, boil=0.0, odoFlux=0.0, odoBorder=0.0,
                 targetTurns=0.0, targetDist=-1.0, pseudoRamp=0.0, engulf=0.0, lobeTread=None,
                 blocked=(False, False, False, False), camL=None, yawRate=0.0, speed=0.0,
                 frame=None):
        self.r = _(r)
        self.n = int(n)
        self.sigma = _(sigma)                 # smoothed speed ratio v / MoveSpeed, 0..1
        self.streamK = _(streamK)             # 0 free form .. 1 ribbon (lobes aligned in the corridor)
        self.ribbonW = _(ribbonW)
        self.ribbonL = _(ribbonL)
        self.t = _(t)                         # clock, seconds in [0, ClockPeriod)
        self.tread = _(tread)                 # integral of v / Lt(r): caterpillar phase of the lobes (the caller folds it modulo 2)
        # per-lobe treadmill phase: integral of v / Lt_j (the lobe's own path length, which pulses), kept by the caller
        # (MassLayout.AdvanceLobeTread in C#): a ratio applied to a cumulative phase would jump when the lobe size changes
        self.lobeTread = tuple(_(x) for x in (lobeTread if lobeTread is not None else [0.0] * K.LobeMax))
        self.boil = _(boil)                   # integral of (1 - sigma) / BoilTref
        self.odoFlux = _(odoFlux)             # crawl odometers (frames)
        self.odoBorder = _(odoBorder)
        self.targetTurns = _(targetTurns)     # target heading in the local frame (turns from +X toward +Z)
        self.targetDist = _(targetDist)       # target distance from the heap centre, < 0 when there is none
        self.pseudoRamp = _(pseudoRamp)       # 0..1 (PseudoRamp seconds ramp kept by the caller)
        self.engulf = _(engulf)               # 0..1: a lobe centres on the target and climbs around it
        self.blocked = tuple(bool(b) for b in blocked)   # 4 ground quadrants blocked by a wall (border worms stay inside)
        self.camL = camL                      # unit vector toward the camera in the local frame (inner-place culling), None = off
        self.yawRate = _(yawRate)             # turns per second the heap turns toward its left: the poured worms are turned back to hold still in the world
        self.speed = _(speed)                 # m/s of the heap
        # world frame of the heap (rootX, rootZ, fwdX, fwdZ, leftX, leftZ, yaw in turns), None = the poured worms are not anchored in the world
        self.frame = tuple(_(x) for x in frame) if frame is not None else None


class SlotOut:
    __slots__ = ("i", "visible", "role", "anim", "frame", "pos", "fwd", "up", "scale", "prio", "lobe", "lod0")


def r_place(n, r):
    """Placement radius: small heaps are tangles, r_place = lerp(min(r, R0 + K n), r, clamp01((n - 3) / TangleSpan))."""
    k = clamp01(_(n - K.TangleFullN) / _(K.TangleSpan))
    small = r if r < K.TangleR0 + K.TangleK * _(n) else K.TangleR0 + K.TangleK * _(n)
    return lerp(small, r, k)


# ---------------------------------------------------------------------------------------------------- slots (HeapSlots, SoA)
class Slots:
    """Per-slot and per-lobe constants drawn from the seed; alloc once per heap. Lists here, arrays in C#."""

    def __init__(self, seed, count=None):
        self.seed = seed & M32
        count = K.SlotCount if count is None else count
        self.count = count
        keys = ("role", "prio", "lobe", "dx", "dy", "dz", "rho0", "rateClass", "diveClass", "divePh", "psi0", "roll", "pitch",
                "phiB", "reach", "lam0", "boilK", "animPhase", "lane", "lane2", "scale", "borderClass", "straggler",
                "boilPhi", "rollFlux", "borderRank", "thr", "hv", "hr", "hph", "eSlide", "eSlidePh", "eOut", "ePit", "thr2", "as0", "ahz", "ahy")
        for k in keys:
            setattr(self, k, [None] * count)
        # a worm poured on the ground lies still in the WORLD: where it touched down (world x, z), the heading of the heap then (turns), its cycle
        self.pourWx = [_(0.0)] * count
        self.pourWz = [_(0.0)] * count
        self.pourYaw = [_(0.0)] * count
        self.pourCyc = [None] * count
        h0 = hash01(self.seed, 0, 0)
        h1 = hash01(self.seed, 0, 1)
        border_rank = 0
        for i in range(count):
            q = frac(_(i) * K.RoleAlpha + h0)
            role = ROLE_BODY if q < K.BodyFraction else (ROLE_FLUX if q < K.FluxFraction else ROLE_BORDER)
            self.role[i] = role
            self.prio[i] = frac(_(i) * K.PrioAlpha + h1)
            self.borderRank[i] = border_rank
            if role == ROLE_BORDER:
                border_rank += 1

            def h(k):
                return hash01(self.seed, i + 1, k)
            u = h(2)                                         # unit hemisphere direction, Y = u uniform
            rho = np.sqrt(_(1.0) - u * u)
            ph = h(3)
            self.dx[i] = rho * cos_t(ph)
            self.dy[i] = u
            self.dz[i] = rho * sin_t(ph)
            hh = h(30)
            self.rho0[i] = _(1.0) - K.RhoSpan * hh * hh      # radius fraction inside the lobe: denser near the surface
            self.rateClass[i] = int(np.floor(h(4) * _(4.0)))
            self.diveClass[i] = int(np.floor(h(5) * _(4.0)))
            self.divePh[i] = h(8)
            self.psi0[i] = h(9)
            self.roll[i] = (h(10) * _(2.0) - _(1.0)) * K.BodyRollMax_t
            self.pitch[i] = (h(11) * _(2.0) - _(1.0)) * K.BodyPitchMax_t
            self.phiB[i] = h(12)
            self.reach[i] = lerp(K.BorderReachMin, K.BorderReachMax, h(13))
            self.lam0[i] = h(14)
            self.boilK[i] = min(int(np.floor(h(15) * _(5.0))), 4)
            self.animPhase[i] = h(16)
            self.lane[i] = (h(17) * _(2.0) - _(1.0)) * K.TreadLaneFan_t
            self.lane2[i] = self.lane[i] * K.LaneConverge
            self.scale[i] = h(19) * _(2.0) - _(1.0)          # scale jitter in [-1, 1)
            self.borderClass[i] = min(int(np.floor(h(20) * _(2.0))), 1)
            self.straggler[i] = h(21) < K.StragglerFrac
            self.boilPhi[i] = h(22)
            self.rollFlux[i] = (h(23) * _(2.0) - _(1.0)) * K.FluxRollMax_t
            self.lobe[i] = min(int(np.floor(h(25) * _(K.LobeMax))), K.LobeMax - 1)
            self.thr[i] = h(26)                              # engulf recruitment threshold
            self.hv[i] = h(27)                               # height along the spiral
            self.hr[i] = h(28)                               # radius jitter on the spiral
            self.hph[i] = h(29)                              # phase on the spiral
            self.eSlide[i] = h(31)                           # slides down and back up when < EngulfSlideFrac
            self.eSlidePh[i] = h(32)
            self.eOut[i] = h(33)                             # sticks out of the sheath when < EngulfOutFrac
            self.ePit[i] = h(34)                             # pitch class: across when < EngulfAcrossFrac, else vertical-ish
            self.thr2[i] = h(36)                             # arm recruitment threshold
            self.as0[i] = h(37)                              # position on the arm conveyor
            self.ahz[i] = h(38)                              # lateral position in the arm
            self.ahy[i] = h(39)                              # height in the arm
        # lobes
        keys = ("lBeta", "lAspS", "lAspI", "lHf", "lPsi0", "lPsiPh", "lCatPh", "lRPh", "lHPh", "lRPer", "lHPer")
        for k in keys:
            setattr(self, k, [None] * K.LobeMax)
        self.lPh = [[None] * 4 for _j in range(K.LobeMax)]
        self.lPer = [[None] * 4 for _j in range(K.LobeMax)]
        for j in range(K.LobeMax):
            def g(k):
                return hash01(self.seed, 2000 + j, k)
            self.lBeta[j] = frac(g(0) + _(j) * K.GOLDEN_TURN)
            asp = lerp(K.LobeAspectMin, K.LobeAspectMax, g(1))
            self.lAspS[j] = np.sqrt(asp)
            self.lAspI[j] = _(1.0) / np.sqrt(asp)
            self.lHf[j] = lerp(K.LobeHeightMin, K.LobeHeightMax, g(2))
            self.lPsi0[j] = g(3)
            self.lPsiPh[j] = g(4)
            self.lCatPh[j] = g(5)
            self.lRPh[j] = g(6)
            self.lHPh[j] = g(7)
            self.lRPer[j] = min(int(np.floor(g(8) * _(5.0))), 4)
            self.lHPer[j] = min(int(np.floor(g(9) * _(5.0))), 4)
            for m in range(4):
                self.lPh[j][m] = g(10 + m)
                self.lPer[j][m] = min(int(np.floor(g(14 + m) * _(5.0))), 4)


# ---------------------------------------------------------------------------------------------------- envelope (footprint of the heap, border ring)
class Env:
    """Half-axes of an ellipsoid (metres): the whole-heap footprint (border ring, worm scale) and each lobe."""
    __slots__ = ("aF", "aB", "aS", "h", "breath", "rp", "wscale")


def envelope(P, rp):
    e = Env()
    s = P.sigma
    e.rp = rp
    e.wscale = lerp(K.HeapScaleR0, K.HeapScaleR1, clamp01((rp - K.HeapScaleRMin) / (K.HeapScaleRMax - K.HeapScaleRMin)))
    e.aF = rp * (_(1.0) + K.StretchFront * s)
    e.aB = rp * (_(1.0) + K.StretchBack * s)
    e.aS = rp * (_(1.0) - K.SideNarrow * s)
    e.h = K.HeightFrac * rp
    e.breath = _(1.0)
    k = P.streamK
    if k > _(0.0):
        if P.ribbonW > _(0.0):
            half = _(0.5) * P.ribbonW
            e.aS = lerp(e.aS, e.aS if e.aS < half else half, k)
        if P.ribbonL > _(0.0):
            e.aF = lerp(e.aF, _(0.5) * P.ribbonL, k)
            e.aB = lerp(e.aB, _(0.5) * P.ribbonL, k)
    return e


def surface(e, P, X, Z):
    """Point of an ellipsoid above the unit-disc point (X, Z) and its outward normal (FrontDrop lowers the front in motion)."""
    ax = e.aF if X > _(0.0) else e.aB
    q = _(1.0) - X * X - Z * Z
    Y = np.sqrt(q if q > _(0.0) else _(0.0))
    drop = _(1.0) - K.FrontDrop * P.sigma * (X if X > _(0.0) else _(0.0))
    pos = (X * ax, e.h * Y * drop * e.breath, Z * e.aS)
    n = vnorm((X / ax, Y / e.h, Z / e.aS), UP)
    return pos, n


def footprint_radius(e, cx, sz):
    ax = e.aF if cx > _(0.0) else e.aB
    a = cx / ax
    b = sz / e.aS
    return rsqrt(a * a + b * b)


def tangent_frame(n):
    az = vnorm((-n[2], _(0.0), n[0]), v3(0, 0, 1))
    return az, vcross(n, az)


def _blend_dir(f, n, k):
    tx = vsub(FWD, vmul(n, vdot(FWD, n)))
    tx = vnorm(tx, f)
    return vnorm(vlerp(f, tx, k), f)


def _tilt(n, f, roll_t, pitch_t):
    u = n
    r = vcross(u, f)
    cr, sr = cos_t(roll_t), sin_t(roll_t)
    u1 = vadd(vmul(u, cr), vmul(r, sr))
    cp, sp = cos_t(pitch_t), sin_t(pitch_t)
    f2 = vadd(vmul(f, cp), vmul(u1, sp))
    u2 = vsub(vmul(u1, cp), vmul(f, sp))
    return f2, u2


def worm_scale(e, S, i, role):
    s = e.wscale * (_(1.0) + K.ScaleJitter * S.scale[i])
    if role == ROLE_BORDER and s > K.BorderScaleCap:
        s = K.BorderScaleCap
    return s


# ---------------------------------------------------------------------------------------------------- lobes
class Lobe:
    __slots__ = ("cx", "cz", "rx", "ry", "rz", "psi", "c", "s", "lt", "ax", "sx", "sz", "env")


def _angle_lerp(a, b, k):
    d = b - a
    d = d - np.floor(d + _(0.5))
    return a + d * k


def compute_lobes(P, S, rp):
    """The lobes of the heap for this frame (once per frame and per heap): the organic envelope, a metaball-like sum of ellipsoids.
    K = clamp(1 + n / LobeDiv, 1, LobeMax) lobes, continuous: lobe j separates from lobe 0 with weight clamp01(K - j), so a lobe that
    does not exist yet sits on lobe 0 and the shape grows by budding. Centres drift (periods 3 to 8 s), radii and heights pulse; in motion
    the lobes align (psi -> 0) and a caterpillar phase (the tread integral) stretches the front ones and brings the rear ones up.
    EngulfLobe becomes the low wide base heap at the target's feet (the arm and the sheath are place overrides, see evaluate)."""
    t = P.t
    sg = P.sigma
    kf = _(1.0) + _(P.n) / K.LobeDiv
    kf = _(1.0) if kf < _(1.0) else (_(K.LobeMax) if kf > _(K.LobeMax) else kf)
    rl = rp * K.LobeRadiusK * rsqrt(kf)
    rl = rp * K.LobeRadiusCap if rl > rp * K.LobeRadiusCap else rl
    hb = K.HeightFrac * rp * np.sqrt(rl / rp)
    span = rp - rl
    per = K.LobePeriods
    lobes = []
    for j in range(K.LobeMax):
        L = Lobe()
        sj = clamp01(kf - _(j))
        amp = K.Lobe0DriftFrac if j == 0 else K.LobeDriftFrac
        base = K.LobeBaseFrac * span * sj
        beta = S.lBeta[j]
        a = amp * span * (_(1.0) if j == 0 else sj)
        ph, pr = S.lPh[j], S.lPer[j]
        dx = a * (_(0.7) * sin_t(t / per[pr[0]] + ph[0]) + _(0.3) * sin_t(t / per[pr[1]] + ph[1]))
        dz = a * (_(0.7) * sin_t(t / per[pr[2]] + ph[2]) + _(0.3) * sin_t(t / per[pr[3]] + ph[3]))
        cx = base * cos_t(beta) + dx
        cz = base * sin_t(beta) + dz
        cp = P.tread * K.CatRate + S.lCatPh[j]
        cx = cx + sg * span * sj * (K.CatBias * cos_t(beta) + K.CatAmp * sin_t(cp))
        cz = cz * (_(1.0) - _(0.4) * sg)
        # the slow centre: without the drift and the caterpillar swing (they move a lobe at 0.5 m/s while the poured worms must hold still)
        sxs = base * cos_t(beta) + sg * span * sj * K.CatBias * cos_t(beta)
        szs = base * sin_t(beta) * (_(1.0) - _(0.4) * sg)
        stretch = _(1.0) + sg * K.CatStretch * (_(0.5) + _(0.5) * sin_t(cp + _(0.25))) * (_(0.6) + _(0.4) * cos_t(beta))
        pulse = _(1.0) + K.LobePulse * sin_t(t / per[S.lRPer[j]] + S.lRPh[j])
        rx = rl * S.lAspS[j] * pulse * stretch
        rz = rl * S.lAspI[j] * pulse
        ry = hb * S.lHf[j] * (_(1.0) + K.LobeHeightPulse * sin_t(t / per[S.lHPer[j]] + S.lHPh[j]))
        psi = (S.lPsi0[j] + K.LobePsiSwing * sin_t(t / K.LobePsiPeriod + S.lPsiPh[j])) * (_(1.0) - sg)
        ltx, lty = rl * S.lAspS[j], hb * S.lHf[j]                       # path length of the lanes: from the UNPULSED size (a pulsing
                                                                         # Lt would make the poured worms drift in the world)
        # ribbon: lobes aligned along the corridor
        k = P.streamK
        if k > _(0.0):
            lx = ((_(j) + _(0.5)) / _(K.LobeMax) - _(0.5)) * (P.ribbonL if P.ribbonL > _(0.0) else _(2.0) * rp)
            cx = lerp(cx, lx, k)
            cz = cz * (_(1.0) - k)
            sxs = lerp(sxs, lx, k)
            szs = szs * (_(1.0) - k)
            if P.ribbonW > _(0.0):
                half = _(0.5) * P.ribbonW
                rz = lerp(rz, rz if rz < half else half, k)
            if P.ribbonL > _(0.0):
                seg = P.ribbonL / _(K.LobeMax)
                rx = lerp(rx, rx if rx > seg else seg, k)
            ry = ry * lerp(_(1.0), K.StreamHeightFactor, k)
            psi = psi * (_(1.0) - k)
        # engulf: a column around the target
        if j == K.EngulfLobe and P.engulf > _(0.0) and P.targetDist >= _(0.0):
            es = smooth01(P.engulf)
            tx, tz = P.targetDist * cos_t(P.targetTurns), P.targetDist * sin_t(P.targetTurns)
            cx = lerp(cx, tx, es)
            cz = lerp(cz, tz, es)
            sxs = lerp(sxs, tx, es)
            szs = lerp(szs, tz, es)
            rx = lerp(rx, K.BaseRadius, es)
            rz = lerp(rz, K.BaseRadius, es)
            ry = lerp(ry, K.BaseHeight, es)
            ltx, lty = rx, ry
        L.cx, L.cz, L.rx, L.ry, L.rz, L.psi = cx, cz, rx, ry, rz, psi
        L.c, L.s = cos_t(psi), sin_t(psi)
        L.ax = ltx                                                      # unpulsed forward radius: anchor of the poured worms
        L.sx, L.sz = sxs, szs
        L.lt = _(math.pi) * np.sqrt(_(0.5) * (ltx * ltx + lty * lty)) * (_(1.0) + K.TreadStretch * sg) / K.TreadSurfaceFrac * K.TreadLtScale
        e = Env()
        e.aF, e.aB, e.aS, e.h, e.breath, e.rp, e.wscale = rx, rx, rz, ry, _(1.0), rl, _(1.0)
        L.env = e
        lobes.append(L)
    return lobes


def _to_heap(L, p):
    """Lobe-local point/vector -> heap frame: rotate by the lobe's psi about the vertical axis (vectors: no translation)."""
    return (L.c * p[0] - L.s * p[2], p[1], L.s * p[0] + L.c * p[2])


def _to_heap_pt(L, p):
    return (L.cx + L.c * p[0] - L.s * p[2], p[1], L.cz + L.s * p[0] + L.c * p[2])


def _slot_out(i, S, role, visible, anim, frame, pos, fwd, up, lobe):
    o = SlotOut()
    o.i = i
    o.visible = visible
    o.role = role
    o.anim = anim
    o.frame = frame
    o.pos = pos
    o.fwd = fwd
    o.up = up
    o.scale = _(1.0)
    o.prio = S.prio[i]
    o.lobe = lobe
    o.lod0 = False
    return o


# ---------------------------------------------------------------------------------------------------- roles
def eval_body(P, S, G, L, j, i, occl):
    t = P.t
    rc = S.rateClass[i]
    turns = frac(t * K.DRIFT_RATE[rc])
    c, s = cos_t(turns), sin_t(turns)
    X, Z = S.dx[i] * c - S.dz[i] * s, S.dx[i] * s + S.dz[i] * c       # slow swirl inside the lobe
    dy = S.dy[i]
    x = frac(t / K.BodyDivePeriods[S.diveClass[i]] + S.divePh[i])
    y01 = _(0.5) - _(0.5) * cos_t(x)
    pulse = smooth01((y01 - _(0.3)) / _(0.5))
    rho = S.rho0[i] * (_(1.0) - K.BodyDiveFrac * pulse)                # plunges into the volume once per period
    wsc = worm_scale(G, S, i, ROLE_BODY)
    nl = vnorm((X / L.rx, dy / L.ry, Z / L.rz), UP)
    p = (rho * L.rx * X, rho * L.ry * dy, rho * L.rz * Z)
    p = vsub(p, vmul(nl, K.CoilHalfHeight * wsc))
    p = (p[0], p[1] if p[1] > _(0.0) else _(0.0), p[2])
    pos = _to_heap_pt(L, p)
    nn = _to_heap(L, nl)
    hidden = False
    if occl:
        dd = vdot(nn, P.camL)
        if dd < K.OccludeDot or (_(1.0) - rho) * (_(1.0) + dd) > K.InnerCull:
            hidden = True                                              # back side, or deep places facing the camera
    az, em = tangent_frame(nn)
    psi = S.psi0[i] + K.BodyHeadingSwing / K.TWO_PI * sin_t(t * _(rc + 1) / _(12.0) + S.animPhase[i])
    f = vadd(vmul(az, cos_t(psi)), vmul(em, sin_t(psi)))
    f = _blend_dir(f, nn, K.BodyHeadingBias * P.sigma)
    f, u = _tilt(nn, f, S.roll[i], S.pitch[i])
    frame = int(np.floor(t * _(10.0) + _(i) * K.CoilPhaseStep)) % COIL_FRAMES
    return _slot_out(i, S, ROLE_BODY, not hidden, ANIM_COIL, frame, pos, f, u, j)


def boil_line(S, i, c, rl):
    cm = c & 255
    hl = hash01(S.seed, i + 1, 64 + cm * 4)
    hd = hash01(S.seed, i + 1, 65 + cm * 4)
    hr = hash01(S.seed, i + 1, 66 + cm * 4)
    hb = hash01(S.seed, i + 1, 67 + cm * 4)
    az = frac(S.boilPhi[i] + _(c) * K.GOLDEN_TURN)
    rho = _(0.10) + _(0.85) * hr
    sx, sz = rho * cos_t(az), rho * sin_t(az)
    length = lerp(K.BoilLenMin, K.BoilLenMax, hl) / rl
    ex = sx + length * cos_t(hd)
    ez = sz + length * sin_t(hd)
    e2 = ex * ex + ez * ez
    if e2 > _(0.9025):
        k = _(0.95) * rsqrt(e2)
        ex, ez = ex * k, ez * k
    return sx, sz, ex, ez, (hb * _(2.0) - _(1.0)) * K.BoilTwistMax * _(0.5)


def _anchor_poured(P, S, i, cyc, pos, f, u):
    """A worm poured on the ground lies still in the world: remembered at touch-down (world point and heading of the heap), put back there until the cycle ends."""
    rx, rz, fx, fz, lx, lz, yaw = P.frame
    det = fx * lz - lx * fz
    if abs(det) < _(0.2):
        return pos, f, u
    if S.pourCyc[i] != cyc:
        S.pourWx[i] = rx + fx * pos[0] + lx * pos[2]
        S.pourWz[i] = rz + fz * pos[0] + lz * pos[2]
        S.pourYaw[i] = yaw
        S.pourCyc[i] = cyc
        return pos, f, u
    dx, dz = S.pourWx[i] - rx, S.pourWz[i] - rz
    x = (dx * lz - lx * dz) / det
    z = (fx * dz - fz * dx) / det
    turned = -(yaw - S.pourYaw[i])
    turned -= np.floor(turned + _(0.5))
    ca, sa = cos_t(turned), sin_t(turned)
    return ((x, pos[1], z), (ca * f[0] - sa * f[2], f[1], sa * f[0] + ca * f[2]), (ca * u[0] - sa * u[2], u[1], sa * u[0] + ca * u[2]))


def eval_flux(P, S, G, L, j, i):
    t = P.t
    sg = P.sigma
    e = L.env
    lam = S.lam0[i] + P.lobeTread[j] + _(K.BoilKs[S.boilK[i]]) * P.boil
    c = int(np.floor(lam))
    mu = lam - _(c)
    sx_r, sz_r, ex_r, ez_r, bow = boil_line(S, i, c, e.rp)
    l1 = S.lane[i]
    l2 = S.lane2[i]
    sx_t, sz_t = -cos_t(l1), sin_t(l1) * _(0.8)
    ex_t, ez_t = cos_t(l2), sin_t(l2) * _(0.8)
    sx, sz = lerp(sx_r, sx_t, sg), lerp(sz_r, sz_t, sg)
    ex, ez = lerp(ex_r, ex_t, sg), lerp(ez_r, ez_t, sg)
    sq = _(1.0) - K.StreamFileSqueeze * P.streamK
    sz, ez = sz * sq, ez * sq
    dx, dz = ex - sx, ez - sz
    heading_h = (dx * e.aF, _(0.0), dz * e.aS)
    anim = ANIM_CRAWL
    pitch_t = _(0.0)
    o = K.FluxLift
    exit_blend = False
    hide_d = K.HideFrac * e.h
    if mu < K.EmergeEnd:
        a = smooth01(mu / K.EmergeEnd)
        X, Z = sx, sz
        o = lerp(K.EmergeDepthFrac * e.h, K.FluxLift, a)
        pitch_t = lerp(K.EmergePitch_t, _(0.0), a)
        hidden = o < -hide_d
    elif mu < K.SurfaceEnd:
        a = (mu - K.EmergeEnd) / (K.SurfaceEnd - K.EmergeEnd)
        sw = bow * (_(1.0) - sg) * _(4.0) * a * (_(1.0) - a)
        X, Z = lerp(sx, ex, a) - dz * sw, lerp(sz, ez, a) + dx * sw
        hidden = False
    else:
        a = smooth01((mu - K.SurfaceEnd) / (_(1.0) - K.SurfaceEnd))
        X, Z = ex, ez
        o = lerp(K.FluxLift, K.EmergeDepthFrac * e.h, a)
        pitch_t = K.DivePitch_t * a
        hidden = o < -hide_d
        exit_blend = True
    r2 = X * X + Z * Z
    if r2 > _(0.9801):
        k = _(0.99) * rsqrt(r2)
        X, Z = X * k, Z * k
    pos_s, n = surface(e, P, X, Z)
    pos = vadd(pos_s, vmul(n, o))
    f = vsub(heading_h, vmul(n, vdot(heading_h, n)))
    f = vnorm(f, FWD)
    u = n
    cp, sp = cos_t(pitch_t), sin_t(pitch_t)
    f, u = vadd(vmul(f, cp), vmul(u, sp)), vsub(vmul(u, cp), vmul(f, sp))
    # anchoring to the slow centre of the lobe, in from mu = 0.5 to 0.84 (the start of the pour): gentle on the surface, complete when the worm lies still
    wpour = sg * smooth01((mu - _(0.5)) / _(0.34)) if mu > _(0.5) else _(0.0)
    if exit_blend and sg > _(0.0):
        xe = ex * L.ax
        px = xe + K.PourAhead - (mu - K.SurfaceEnd) * L.lt           # lobe-local x: motionless in the world when the lobe moves at v
        pour = (px, K.FluxLift, ez * e.aS)
        pf = vnorm((dx * e.aF, _(0.0), dz * e.aS), FWD)
        wp = sg * smooth01((mu - K.SurfaceEnd) / _(0.04))
        pos = vlerp(pos, pour, wp)
        f = vnorm(vlerp(f, pf, wp), pf)
        u = vnorm(vlerp(u, UP, wp), UP)
        u = vnorm(vsub(u, vmul(f, vdot(u, f))), UP)
        pour_hidden = px < _(0.8) * L.ax - K.PourHideMargin or mu > K.PourEnd
        hidden = pour_hidden if sg >= _(0.5) else hidden
        if mu > K.PourIdleMu and sg > _(0.5):
            anim = ANIM_IDLE
    r = vcross(u, f)
    cr, sr = cos_t(S.rollFlux[i]), sin_t(S.rollFlux[i])
    u = vadd(vmul(u, cr), vmul(r, sr))
    lod0_phase = (K.EmergeEnd <= mu < K.SurfaceEnd) or (exit_blend and sg > _(0.5) and mu >= K.SurfaceEnd + _(0.04))
    nh = _to_heap(L, n)
    pos, f, u = _to_heap_pt(L, pos), _to_heap(L, f), _to_heap(L, u)
    if wpour > _(0.0):
        # anchored to the slow centre of the lobe, not to its drifting and swinging one: the poured worm holds still in the world
        pos = (pos[0] + wpour * (L.sx - L.cx), pos[1], pos[2] + wpour * (L.sz - L.cz))
    if exit_blend and P.yawRate != _(0.0) and sg > _(0.0) and mu > K.SurfaceEnd:
        # the heap turns: a worm poured on the ground holds still in the world only if it is turned back by what the heap turned since it touched down
        tp = (mu - K.SurfaceEnd) * L.lt / max(P.speed, _(0.3))
        tp = min(tp, _(1.5))
        gate = sg * smooth01((mu - K.SurfaceEnd) / _(0.04))
        run = (mu - K.SurfaceEnd) * L.lt * gate                  # metres the straight pour model walked it back
        ang = -P.yawRate * tp * gate
        rad = -ang * _(2.0 * np.pi)                              # what the heap turned, radians (left positive)
        chord_x, chord_z = run, _(0.0)                           # its path since the touch-down is an arc, not a line: the chord in the frame of the touch-down
        if abs(rad) > _(1e-3):
            chord_x, chord_z = run * np.sin(rad) / rad, run * (_(1.0) - np.cos(rad)) / rad
        ca, sa = cos_t(ang), sin_t(ang)
        bx, bz = pos[0] + run - chord_x, pos[2] - chord_z
        pos = (ca * bx - sa * bz, pos[1], sa * bx + ca * bz)
        f = (ca * f[0] - sa * f[2], f[1], sa * f[0] + ca * f[2])
        u = (ca * u[0] - sa * u[2], u[1], sa * u[0] + ca * u[2])
    if P.frame is not None and exit_blend and anim == ANIM_IDLE and sg > _(0.5):
        pos, f, u = _anchor_poured(P, S, i, c, pos, f, u)
    if anim == ANIM_IDLE:
        frame = int(np.floor(t * _(6.0) + S.animPhase[i] * _(IDLE_FRAMES))) % IDLE_FRAMES
    else:
        frame = int(np.floor(P.odoFlux + _(i) * K.CrawlPhaseStep)) % CRAWL_FRAMES
    o = _slot_out(i, S, ROLE_FLUX, not hidden, anim, frame, pos, f, u, j)
    # LOD0 only for worms really on the surface (or poured on the ground) and facing the camera; the others are drawn with the LOD1 mesh
    o.lod0 = bool(lod0_phase and (P.camL is None or vdot(nh, P.camL) > K.Lod0FaceDot))
    return o


def _quadrant(az_turns):
    return int(np.floor(frac(az_turns) * _(4.0))) & 3


def eval_border(P, S, G, i):
    t = P.t
    sg = P.sigma
    if S.straggler[i]:
        a = _(0.5) + (S.boilPhi[i] - _(0.5)) * _(0.15)
    else:
        a0 = S.phiB[i]
        a0 = a0 - np.floor(a0 + _(0.5))
        a = a0 * (_(1.0) - K.FrontCompress * sg)
    ur = (cos_t(a), _(0.0), sin_t(a))
    ut = (-ur[2], _(0.0), ur[0])
    ell = footprint_radius(G, ur[0], ur[2])
    reach = S.reach[i]
    reach = reach + K.BorderAheadExtra * smooth01((sg - _(0.4)) / _(0.4)) * smooth01((ur[0] - _(0.3)) / _(0.4))
    rho0 = K.BorderTuck * ell
    rho1 = ell + reach
    if P.blocked[_quadrant(a)]:
        rho1 = ell
        rho0 = rho0 if rho0 < ell else ell
    tb = K.BorderPeriods[S.borderClass[i]]
    mu = frac(S.lam0[i] + t / tb)
    pf = K.BorderPauseFrac * (_(1.0) - sg)
    seg = (_(1.0) - pf) * _(0.5)
    paused = False
    if mu < seg:
        w = _(0.5) * mu / seg
    elif mu < seg + pf:
        w = _(0.5)
        paused = True
    else:
        w = _(0.5) + _(0.5) * (mu - seg - pf) / seg
    cw, sw = cos_t(w), sin_t(w)
    rad = rho0 + (rho1 - rho0) * (_(0.5) - _(0.5) * cw)
    tan = K.BorderLoopWidth * sw
    pos = (ur[0] * rad + ut[0] * tan, _(0.0), ur[2] * rad + ut[2] * tan)
    drad = (rho1 - rho0) * _(math.pi) * sw
    dtan = K.BorderLoopWidth * K.TWO_PI * cw
    hd = (ur[0] * drad + ut[0] * dtan, _(0.0), ur[2] * drad + ut[2] * dtan)
    f = vnorm(hd, ur)
    if paused:
        anim = ANIM_IDLE
        frame = int(np.floor(t * _(6.0) + S.animPhase[i] * _(IDLE_FRAMES))) % IDLE_FRAMES
    else:
        anim = ANIM_CRAWL
        frame = int(np.floor(P.odoBorder + _(i) * K.CrawlPhaseStep)) % CRAWL_FRAMES
    o = _slot_out(i, S, ROLE_BORDER, True, anim, frame, pos, f, UP, -1)
    o.lod0 = P.camL is None or (pos[0] * P.camL[0] + pos[2] * P.camL[2]) > _(0.0)     # LOD0 only on the camera side
    return o


def _spiral(P, S, i, tx, tz, es):
    """Place on the sheath around the target: height v * H (an uneven top), 2.2 turns over the height, a radius that tapers from RBottom to
    RTop with bulges that rise slowly, strays that stick out, a part that slides down and back up (2 to 4 s), mostly vertical worms."""
    t = P.t
    v = S.hv[i]
    down = False
    if S.eSlide[i] < K.EngulfSlideFrac:
        cls = min(int(np.floor(S.eSlidePh[i] * _(3.0))), 2)
        x = frac(t / K.EngulfSlidePeriods[cls] + S.eSlidePh[i] * _(7.0))
        v = v - K.EngulfSlideAmp * v * (_(0.5) - _(0.5) * cos_t(x))
        down = x < _(0.5)
    ang = S.hph[i] + K.EngulfTurns * v + t * K.EngulfSpin
    top = _(1.0) + K.EngulfTopWobble * sin_t(ang + t / _(6.0)) * v * v
    hgt = v * K.EngulfHeight * es * top
    rr = lerp(K.EngulfRBottom, K.EngulfRTop, v) * (_(1.0) + K.EngulfJitter * (S.hr[i] * _(2.0) - _(1.0)))
    rr = rr + K.EngulfBulgeAmp * (_(0.5) + _(0.5) * sin_t(v * K.EngulfBulges - t * K.EngulfRise))
    out = S.eOut[i] < K.EngulfOutFrac
    if out:
        rr = rr + K.EngulfOutReach * (_(0.5) + _(0.5) * sin_t(t / _(3.0) + S.hph[i]))
    rr = K.EngulfRMinSheath if rr < K.EngulfRMinSheath else rr
    ca, sa = cos_t(ang), sin_t(ang)
    pos = (tx + rr * ca, hgt + _(0.02), tz + rr * sa)
    tang = (-sa, _(0.0), ca)
    if S.ePit[i] < K.EngulfAcrossFrac:
        p = K.EngulfAcrossPitch_t
    else:
        p = lerp(K.EngulfPitchMin_t, K.EngulfPitchMax_t, (S.ePit[i] - K.EngulfAcrossFrac) / (_(1.0) - K.EngulfAcrossFrac))
    if down:
        p = -p
    cp, sp = cos_t(p), sin_t(p)
    f = vadd(vmul(tang, cp), vmul(UP, sp))
    radial = (ca, _(0.0), sa)
    if out:
        f = vnorm(vlerp(f, radial, _(0.5)), f)
    u = vnorm(vsub(radial, vmul(f, vdot(radial, f))), radial)
    return pos, f, u


def _base_orient(S, i, pos, L2):
    """Orientation of the worms of the base heap at the target's feet: lying down and interlaced (mostly tangential around the base centre,
    a random spread, a small pitch), only BaseOutFrac of them turned outward; no petals."""
    rx_, rz_ = pos[0] - L2.cx, pos[2] - L2.cz
    r = vnorm((rx_, _(0.0), rz_), FWD)
    sgn = _(1.0) if S.psi0[i] >= _(0.5) else _(-1.0)
    t = (-r[2] * sgn, _(0.0), r[0] * sgn)
    eps = (S.phiB[i] - _(0.5)) * K.BaseSpread
    if S.eOut[i] < K.BaseOutFrac:
        eps = _(0.25)
    f = vnorm(vadd(vmul(t, cos_t(eps)), vmul(r, sin_t(eps))), r)
    return _tilt(UP, f, S.roll[i] * K.BaseTiltK, S.pitch[i] * K.BaseTiltK)


def _arm(P, S, i, rp, ln, dcx, dcz):
    """Place on the arm conveyor toward the target: s runs 0 -> 1 (toward the tip) at ArmFlow per second, the arm undulates, narrows toward the
    tip; the first few places (smallest threshold) rear up at the tip, head toward the target. Returns (pos, f, u, hidden)."""
    t = P.t
    a0 = K.ArmStart * rp
    tip = S.thr2[i] < K.ArmTipFrac
    s = _(1.0) if tip else frac(S.as0[i] + t * K.ArmFlow)
    wave_a = s * K.ArmWaveK - t / K.ArmWavePer
    wave = K.ArmWave * s * sin_t(wave_a)
    wid = lerp(K.ArmWidth, K.ArmWidth * K.ArmTaper, s)
    lat = (S.ahz[i] - _(0.5)) * wid
    dist = a0 + s * ln
    px, pz = -dcz, dcx
    pos = (dcx * dist + px * (wave + lat), K.ArmThick * S.ahy[i] * (_(1.0) - _(0.4) * s) + _(0.03), dcz * dist + pz * (wave + lat))
    dw = K.ArmWave * (sin_t(wave_a) + s * K.ArmWaveK * K.TWO_PI * cos_t(wave_a)) / (ln if ln > _(0.2) else _(0.2))
    f = vnorm((dcx + px * dw, _(0.0), dcz + pz * dw), (dcx, _(0.0), dcz))
    u = UP
    hidden = False
    if tip:
        cp, sp = cos_t(K.ArmTipPitch_t), sin_t(K.ArmTipPitch_t)
        f = vadd(vmul(f, cp), vmul(UP, sp))
        u = vsub(vmul(UP, cp), vmul((f[0], _(0.0), f[2]), sp))
        u = vnorm(u, UP)
    else:
        hidden = s < _(0.04) or s > _(0.96)
    return pos, f, u, hidden


# ---------------------------------------------------------------------------------------------------- Evaluate
def visible_count(n, slot_count):
    """V = min(SlotCount, round(n * k(n))), k = lerp(DensityK0, DensityK1, clamp01((n - DensityN0) / (DensityN1 - DensityN0)))."""
    k = lerp(K.DensityK0, K.DensityK1, clamp01(_(n - K.DensityN0) / _(K.DensityN1 - K.DensityN0)))
    return min(slot_count, int(np.floor(_(n) * k + _(0.5))))


def evaluate(P, S):
    """MassLayout.Evaluate: the list of SlotOut for slots [0, V). `visible` = not hidden (plunged, behind, deep facing the camera, below
    the front of a lobe). The draw selection (priority threshold, cap) is `select_drawn`."""
    n = visible_count(P.n, S.count)
    rp = r_place(P.n, P.r)
    G = envelope(P, rp)
    lobes = compute_lobes(P, S, rp)
    occl = P.camL is not None
    border_ok = P.n >= K.BorderMinN
    eng = P.engulf > _(0.0) and P.targetDist >= _(0.0)
    es = tx = tz = None
    if eng:
        tx, tz = P.targetDist * cos_t(P.targetTurns), P.targetDist * sin_t(P.targetTurns)
        es = smooth01(P.engulf)
        L2 = lobes[K.EngulfLobe]
    arm = P.pseudoRamp > _(0.0) and P.targetDist >= _(0.0)
    if arm:
        a_th = P.targetTurns
        ln = K.ArmMax
        lim = P.targetDist - K.ArmMargin - K.ArmStart * rp
        ln = ln if ln < lim else lim
        ln = (ln if ln > _(0.0) else _(0.0)) * P.pseudoRamp
        dcx, dcz = cos_t(a_th), sin_t(a_th)
    out = []
    for i in range(n):
        role = S.role[i]
        if role == ROLE_BORDER and not border_ok:
            role = ROLE_FLUX
        j = S.lobe[i]
        L = lobes[j]
        if role == ROLE_BODY:
            o = eval_body(P, S, G, L, j, i, occl)
        elif role == ROLE_FLUX:
            o = eval_flux(P, S, G, L, j, i)
        else:
            o = eval_border(P, S, G, i)
        o.scale = worm_scale(G, S, i, o.role)
        if arm:
            w = smooth01((K.ArmFrac * P.pseudoRamp - S.thr2[i]) / K.RecruitSoft)
            if w > _(0.0):
                ap, af, au, ah = _arm(P, S, i, rp, ln, dcx, dcz)
                o.pos = vlerp(o.pos, ap, w)
                o.fwd = vnorm(vlerp(o.fwd, af, w), af)
                o.up = vnorm(vlerp(o.up, au, w), au)
                if w > _(0.5):
                    o.visible = not ah
                    o.prio = _(0.0)                                  # arm worms are always drawn
                    o.lod0 = True
                    o.anim = ANIM_CRAWL
                    o.frame = int(np.floor(P.odoFlux + _(i) * K.CrawlPhaseStep)) % CRAWL_FRAMES
        if eng:
            if role == ROLE_BODY and j == K.EngulfLobe:                       # the native places of the base lobe lie down too
                nf, nu = _base_orient(S, i, o.pos, L2)
                o.fwd = vnorm(vlerp(o.fwd, nf, es), nf)
                o.up = vnorm(vlerp(o.up, nu, es), nu)
            wb = smooth01(((K.ColFrac + K.BaseFrac) * P.engulf - S.thr[i]) / K.RecruitSoft)
            if wb > _(0.0):
                ob = eval_body(P, S, G, L2, K.EngulfLobe, i, False)      # a place of the base heap at the target's feet
                bf, bu = _base_orient(S, i, ob.pos, L2)
                o.pos = vlerp(o.pos, ob.pos, wb)
                o.fwd = vnorm(vlerp(o.fwd, bf, wb), bf)
                o.up = vnorm(vlerp(o.up, bu, wb), bu)
                if wb > _(0.5):
                    o.visible = True
                    o.anim, o.frame = ob.anim, ob.frame
                    o.lod0 = False                                   # base worms are bodies: LOD1 meshes
            wc = smooth01((K.ColFrac * P.engulf - S.thr[i]) / K.RecruitSoft)
            if wc > _(0.0):
                sp_pos, sp_f, sp_u = _spiral(P, S, i, tx, tz, es)
                o.pos = vlerp(o.pos, sp_pos, wc)
                o.fwd = vnorm(vlerp(o.fwd, sp_f, wc), sp_f)
                o.up = vnorm(vlerp(o.up, sp_u, wc), sp_u)
                if wc > _(0.5):
                    o.visible = True
                    o.prio = _(0.0)                                  # sheath worms are always drawn (never culled for the heap's)
                    o.lod0 = True
                    o.anim = ANIM_CRAWL
                    o.frame = int(np.floor(P.odoFlux + _(i) * K.CrawlPhaseStep)) % CRAWL_FRAMES
        if P.streamK > _(0.0) and P.ribbonW > _(0.0):
            # In the stream no worm, whatever its role (border and arm included), lies beyond the half width of the ribbon.
            zlim = (_(0.5) * P.ribbonW - _(0.08) if _(0.5) * P.ribbonW > _(0.28) else _(0.2)) + (_(1.0) - P.streamK) * (P.r + _(2.0))   # 8 cm in: the heading follows the corridor to a few degrees
            if o.pos[2] > zlim:
                o.pos = (o.pos[0], o.pos[1], zlim)
            elif o.pos[2] < -zlim:
                o.pos = (o.pos[0], o.pos[1], -zlim)
        out.append(o)
    return out


def select_drawn(outs, cap, n_slots=None):
    """Emit's selection: p = clamp01(cap / max(1, V * vhat)), vhat = visible fraction; a slot is drawn if visible and prio < p, up to cap."""
    s_count = len(outs) if n_slots is None else n_slots
    nvis = sum(1 for o in outs if o.visible)
    vhat = _(nvis) / _(max(1, s_count))
    p = clamp01(_(cap) / (_(max(1, s_count)) * vhat if vhat > _(0.0) else _(1.0)))
    drawn = []
    for o in outs:
        if not o.visible or o.prio >= p:
            continue
        drawn.append(o)
        if len(drawn) >= cap:
            break
    return drawn


def radius_of(n):
    """r(n) = R10 (n / 10)^e  (spec 4.5; BaseRadius 0.83, RadiusExponent 0.36)."""
    return float(0.83 * (n / 10.0) ** 0.36)


# ---------------------------------------------------------------------------------------------------- integrators for previews and golden
def lt_of(r, sigma):
    return float(config.MASS_LAYOUT["TreadPerimeter"] * r * (1.0 + config.MASS_LAYOUT["TreadStretch"] * sigma)
                 / config.MASS_LAYOUT["TreadSurfaceFrac"])


def integrate_tread(n, t, sigma, S, dt=1.0 / 20.0):
    """The treadmill integrators of a heap in steady state since t = 0 (no target): global tread (caterpillar) and one per lobe."""
    r = radius_of(n)
    rp = float(r_place(n, _(r)))
    lt_ref = lt_of(rp, sigma)
    v = 2.4 * sigma
    tread = 0.0
    lob = [0.0] * K.LobeMax
    steps = int(round(t / dt))
    for k in range(steps):
        P = MassFrameParams(r=r, n=n, sigma=sigma, t=(k * dt) % config.MASS_LAYOUT["ClockPeriod"], tread=tread, lobeTread=lob)
        lobes = compute_lobes(P, S, _(rp))
        for j in range(K.LobeMax):
            lob[j] += v * dt / float(lobes[j].lt)
        tread += v * dt / lt_ref
    return tread, lob


def steady_params(n, t, sigma, S=None, **kw):
    """Parameters of a heap that has been in steady state (sigma constant) since t = 0: closed forms, and a short integration for the
    treadmill phases (global and per lobe) when sigma > 0."""
    r = radius_of(n)
    v = 2.4 * sigma
    rp = float(r_place(n, _(r)))
    lt = lt_of(rp, sigma)
    d = dict(
        r=r, n=n, sigma=sigma, t=t % config.MASS_LAYOUT["ClockPeriod"],
        tread=v * t / lt + 0.123,
        lobeTread=[0.0] * K.LobeMax,
        boil=(1.0 - sigma) * t / config.MASS_LAYOUT["BoilTref"] + 0.317,
        odoFlux=(v + config.MASS_LAYOUT["BoilSpeed"] * (1.0 - sigma)) * t * config.MASS_LAYOUT["CrawlFramesPerM"],
        odoBorder=(v + config.MASS_LAYOUT["BorderSpeed"] * (1.0 - sigma)) * t * config.MASS_LAYOUT["CrawlFramesPerM"],
    )
    if sigma > 0.0:
        S = S if S is not None else Slots(GOLDEN_SEED)
        tr, lob = integrate_tread(n, t, sigma, S)
        d["tread"] = tr + 0.123
        d["lobeTread"] = lob
    d.update(kw)
    return d


# ---------------------------------------------------------------------------------------------------- golden file
GOLDEN_SEED = 12345


def _record(S, d):
    P = MassFrameParams(**d)
    outs = evaluate(P, S)
    rp = r_place(P.n, P.r)
    lobes = compute_lobes(P, S, rp)
    rec = {"params": {k: (list(v) if isinstance(v, tuple) else v) for k, v in d.items()},
           "visibleCount": len(outs), "rPlace": round(float(rp), 6),
           "lobes": [[round(float(x), 6) for x in (L.cx, L.cz, L.rx, L.ry, L.rz, L.psi)] for L in lobes],
           "slots": []}
    for o in outs[:40]:
        rec["slots"].append({
            "i": o.i, "role": int(o.role), "anim": int(o.anim), "frame": int(o.frame), "visible": bool(o.visible), "lobe": int(o.lobe),
            "pos": [round(float(x), 6) for x in o.pos], "fwd": [round(float(x), 6) for x in o.fwd],
            "up": [round(float(x), 6) for x in o.up], "scale": round(float(o.scale), 6), "lod0": bool(o.lod0)})
    return rec


def golden():
    """The parity contract with the C# port: seed 12345, n in {10, 50, 200}, t in {0, 1.7}, sigma in {0, 1}, the first 40 slots,
    plus a pseudopod case and two engulf cases."""
    cases = []
    S = Slots(GOLDEN_SEED)
    for n in (10, 50, 200):
        for t in (0.0, 1.7):
            for sigma in (0.0, 1.0):
                kw = {}
                if sigma >= 1.0:
                    kw.update(targetTurns=0.1, targetDist=5.0, pseudoRamp=1.0)
                if sigma < 1.0:
                    kw.update(blocked=(False, True, False, False))
                if t > 0.0:
                    c = np.array([0.6, 0.5, 0.62])
                    c = c / np.linalg.norm(c)
                    kw.update(camL=tuple(float(x) for x in c))
                cases.append(_record(S, steady_params(n, t, sigma, S=S, **kw)))
    cases.append(_record(S, steady_params(50, 1.7, 0.0, targetTurns=0.15, targetDist=4.0, pseudoRamp=1.0)))
    cases.append(_record(S, steady_params(50, 1.7, 0.0, targetTurns=0.1, targetDist=3.0, engulf=0.5)))
    cases.append(_record(S, steady_params(200, 1.7, 0.0, targetTurns=-0.05, targetDist=3.5, engulf=1.0)))
    lut_sum = float(sum(float(x) for x in SIN_LUT))
    return {
        "layout": "lobes-v3",
        "seed": GOLDEN_SEED,
        "tolerance_m": 1e-3,
        "note": "Local frame: +X forward, +Y up, +Z left. Angles are turns. Produced by assets/blender/mass_layout.py (float32).",
        "massLayout": config.MASS_LAYOUT,
        "hashVectors": [{"seed": s, "a": a, "b": b, "h": hash_u32(s, a, b), "h01": float(hash01(s, a, b))}
                        for (s, a, b) in ((12345, 0, 0), (12345, 0, 1), (12345, 7, 2), (0xDEADBEEF, 199, 31), (1, 1, 1))],
        "lowbias32": [{"x": x, "h": lowbias32(x)} for x in (0, 1, 12345, 0xFFFFFFFF)],
        "sinLutSum": round(lut_sum, 6),
        "slotSample": {"role": [int(S.role[i]) for i in range(40)], "prio": [round(float(S.prio[i]), 6) for i in range(40)],
                       "lobe": [int(S.lobe[i]) for i in range(40)], "rho0": [round(float(S.rho0[i]), 6) for i in range(40)],
                       "dx": [round(float(S.dx[i]), 6) for i in range(40)], "dz": [round(float(S.dz[i]), 6) for i in range(40)]},
        "cases": cases,
    }


if __name__ == "__main__":
    import json
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    out = argv[0] if argv else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "build", "mass_layout_golden.json")
    with open(out, "w", encoding="utf8", newline="\n") as fh:
        json.dump(golden(), fh, indent=1)
        fh.write("\n")
    print("golden ->", out)
