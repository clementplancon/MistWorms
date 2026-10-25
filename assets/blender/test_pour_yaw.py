"""Out-of-game check of the poured worms of a heap that turns (parity with MassLayoutTests.PouredWormsHoldStillWhileTheHeapTurns):
the median world speed of the worms lying on the ground must at least halve when MassFrameParams.yawRate / speed are given.
Run: python assets/blender/test_pour_yaw.py"""
import math
import sys

import numpy as np

import config
import mass_layout as ml

V = 2.4
N = 50
TURN = 0.15          # turns per second toward the left
DT = 1.0 / 30.0
SECONDS = 6.0


def run(given_rate, anchored=False):
    S = ml.Slots(8)
    r = ml.radius_of(N)
    rp = float(ml.r_place(N, np.float32(r)))
    lt_ref = ml.lt_of(rp, 1.0)
    tread, lob, boil, odo = 0.2, [0.0] * ml.K.LobeMax, 0.3, 0.0
    cx = cz = 0.0
    prev, streak, speeds = {}, {}, []
    for f in range(int(SECONDS / DT)):
        time = f * DT
        psi = TURN * time * 2.0 * math.pi
        cx += V * math.cos(psi) * DT
        cz += V * math.sin(psi) * DT
        c0, s0 = math.cos(psi), math.sin(psi)
        P = ml.MassFrameParams(r=r, n=N, sigma=1.0, t=time % config.MASS_LAYOUT["ClockPeriod"], tread=tread, lobeTread=lob, boil=boil,
                               odoFlux=odo, odoBorder=odo, yawRate=given_rate, speed=V,
                               frame=(cx, cz, c0, s0, -s0, c0, psi / (2.0 * math.pi)) if anchored else None)
        outs = ml.evaluate(P, S)
        lobes = ml.compute_lobes(P, S, np.float32(rp))
        c, s = math.cos(psi), math.sin(psi)
        for o in outs:
            pour = o.role == ml.ROLE_FLUX and o.visible and o.anim == ml.ANIM_IDLE and abs(float(o.pos[1]) - config.MASS_LAYOUT["FluxLift"]) < 1e-4
            w = (cx + c * float(o.pos[0]) - s * float(o.pos[2]), cz + s * float(o.pos[0]) + c * float(o.pos[2]))
            if time > 1.0 and pour and streak.get(o.i, 0) >= 3 and o.i in prev:
                speeds.append(math.hypot(w[0] - prev[o.i][0], w[1] - prev[o.i][1]) / DT)
            prev[o.i] = w
            streak[o.i] = streak.get(o.i, 0) + 1 if pour else 0
        for j in range(ml.K.LobeMax):
            lob[j] += V * DT / float(lobes[j].lt)
        tread += V * DT / lt_ref
        boil += 0.0
        odo += V * DT * config.MASS_LAYOUT["CrawlFramesPerM"]
    speeds.sort()
    return speeds[len(speeds) // 2], len(speeds)


without, n0 = run(0.0)
with_, n1 = run(TURN)
anch, n2 = run(0.0, anchored=True)
print("anchored in the world: %.3f m/s (%d samples)" % (anch, n2))
print("median world speed of the poured worms: %.3f m/s without (%d samples), %.3f m/s with the correction (%d samples)" % (without, n0, with_, n1))
sys.exit(0 if n1 > 50 and with_ < 0.5 * without and with_ < 0.3 and n2 > 50 and anch < 0.02 else 1)
