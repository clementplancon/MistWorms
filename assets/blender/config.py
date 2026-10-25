"""Shared constants for the Blender scripts and validate_assets.py (pure Python: no bpy import here)."""

SEED = 20261024
GENERATOR = "Blender 4.5.5 + assets/blender/*.py"

# name -> (frames, fps, loop). Order matters: it is the order in manifest.json.
ANIMATIONS = {
    "worm_crawl": (16, 12, True),
    "worm_idle": (8, 6, True),
    "worm_rear": (6, 10, False),
    "worm_strike": (4, 16, False),
    "worm_sleep": (4, 4, True),
    "worm_coil": (12, 10, True),          # plan 4B-3 pass A: coiled worm of the heap bodies
    "worm_crawl_lod1": (8, 6, True),      # LOD1: frame j = pose 2j of the LOD0 loop, half the rate
    "worm_idle_lod1": (4, 3, True),
    "worm_coil_lod1": (6, 5, True),
    "mass_boil": (8, 8, True),            # legacy dome, removed in pass D
    "mass_core_t1": (1, 0, False),        # heap core, same geometry, UV x {1, 1.8, 3}
    "mass_core_t2": (1, 0, False),
    "mass_core_t3": (1, 0, False),
    "nest_idle": (1, 0, False),
}

# name -> (width, height, linear)
TEXTURES = {
    "worm_albedo": (128, 128, False),
    "worm_normal": (128, 128, True),
    "worm_gloss": (128, 128, True),      # RGBA: RGB = 0, alpha = lustre (the only texture with alpha)
    "mass_albedo": (1024, 1024, False),   # legacy dome, removed in pass D
    "mass_normal": (1024, 1024, True),
    "mass_core_albedo": (256, 256, False),
    "mass_core_normal": (256, 256, True),
    "mass_core_gloss": (256, 256, True),  # RGBA: alpha = lustre (every *_gloss carries alpha)
    "nest_albedo": (1024, 1024, False),
    "nest_normal": (1024, 1024, True),
}

# mesh prefix -> (min, max) triangles per frame. The key is chosen per mesh by suffix, then longest prefix:
# any name containing "_lod1" uses "worm_lod1"; otherwise the longest key the name starts with.
TRI_BUDGET = {
    "worm": (600, 900),
    "worm_lod1": (200, 300),
    "mass": (1500, 2500),                 # legacy dome
    "mass_core": (1200, 1800),
    "nest": (1700, 2300),
}

CORE_TILES = {"mass_core_t1": 1.0, "mass_core_t2": 1.8, "mass_core_t3": 3.0}   # UV multiplier of each core mesh

WORM_TEXTURE_MAX = 128               # worm_* textures follow the vanilla creatures (dump section 1)
WORM_LENGTH = (0.38, 0.42)          # extent along Z of crawl/sleep frames
WORM_LENGTH_LOD1 = (0.36, 0.42)     # LOD1 has no mandibles (about 0.02 m shorter)
NEST_FOOTPRINT = (0.85, 0.95)       # X and Z extent
NEST_HEIGHT = (0.45, 0.55)
DOME_RADIUS = (0.95, 1.08)          # X and Z half-extent

# Coil (worm_coil_*): lengths in metres, angles in radians unless noted
COIL_EXTENT_XZ_MAX = 0.30
COIL_HEIGHT_MAX = 0.10
COIL_RING_MIN_DIST = 0.056              # min distance between centres of non-adjacent rings
CORE_HALF_EXTENT = (0.95, 1.12)         # mass_core_t*: XZ half extent and y max
CORE_Y_MIN = -0.06

# ---------------------------------------------------------------------------------------------------------------
# MASS_LAYOUT: the constants of the heap layout (plan 4B-3 section 3.1). SINGLE SOURCE FOR THE PYTHON REFERENCE
# (mass_layout.py); src/MistWorms/Mass/MassLayoutConst.cs must carry the same values (a copy goes to manifest.json).
# Angles are in degrees here (converted by mass_layout.py); times in seconds; lengths in metres.
# ---------------------------------------------------------------------------------------------------------------
MASS_LAYOUT = {
    # roles and seed
    "SlotCount": 768, "BodyFraction": 0.60, "FluxFraction": 0.90, "RoleAlpha": 0.6180340, "PrioAlpha": 0.7548777,
    "BorderMinN": 6,
    # revision 3 (user): NO core. The heap is only worms, filling the VOLUME of an organic "amoeba" envelope made of lobes.
    # lobes: K = clamp(1 + n / LobeDiv, 1, LobeMax) (continuous: lobe j separates from lobe 0 with weight clamp01(K - j))
    "LobeMax": 6, "LobeDiv": 40.0, "LobeRadiusK": 0.95, "LobeRadiusCap": 0.95, "HeightFrac": 0.4,
    "LobeAspectMin": 0.7, "LobeAspectMax": 1.45, "LobeHeightMin": 0.8, "LobeHeightMax": 1.25,
    "LobePulse": 0.22, "LobeHeightPulse": 0.18, "LobePeriods": [3.0, 4.0, 5.0, 6.0, 8.0],
    "LobeBaseFrac": 0.75, "LobeDriftFrac": 0.45, "Lobe0DriftFrac": 0.12, "LobePsiSwing": 0.2, "LobePsiPeriod": 8.0,
    # envelope of the whole heap (border ring, footprint) and motion
    "StretchFront": 0.20, "StretchBack": 0.10, "SideNarrow": 0.05, "FrontDrop": 0.30, "StreamHeightFactor": 0.4,
    "CatBias": 0.45, "CatAmp": 0.25, "CatRate": 0.5, "CatStretch": 0.35,
    # volume filling: rho = 1 - RhoSpan * h^2 (denser near the surface); bodies plunge by BodyDiveFrac of their radius
    "RhoSpan": 0.65, "BodyDiveFrac": 0.45, "BodyDivePeriods": [3.0, 4.0, 5.0, 6.0],
    "BodyDriftPeriods": [-60.0, -120.0, 120.0, 60.0],      # seconds per turn of the swirl inside a lobe
    "BodyRollMax": 55.0, "BodyPitchMax": 35.0, "BodyHeadingBias": 0.4, "BodyHeadingSwing": 0.6,
    "CoilPhaseStep": 4.7, "CoilHalfHeight": 0.04, "OccludeDot": -0.15, "Lod0FaceDot": 0.35, "InnerCull": 0.8,
    # flux (crawl on the surface of the lobe)
    "EmergeEnd": 0.12, "SurfaceEnd": 0.80, "FluxLift": 0.015, "BoilSpeed": 0.30, "BoilTref": 4.5,
    "BoilKs": [0.75, 5.0 / 6.0, 1.0, 7.0 / 6.0, 1.25], "BoilLenMax": 1.2, "BoilLenMin": 0.45, "BoilTwistMax": 0.5,
    "EmergeDepthFrac": -0.6, "HideFrac": 0.4, "EmergePitch": 55.0, "DivePitch": -50.0, "TreadLaneFan": 40.0, "LaneConverge": 0.5,
    "PourAhead": 0.25, "TreadSurfaceFrac": 0.68, "TreadPerimeter": 2.84, "TreadStretch": 0.1, "TreadLtScale": 0.9, "FluxRollMax": 8.0,
    "PourIdleMu": 0.84, "PourEnd": 0.96, "PourHideMargin": 0.2, "CrawlFramesPerM": 5, "CrawlPhaseStep": 6.1, "IdlePhaseStep": 3.3,
    "StreamFileSqueeze": 0.85,
    # border (scouts around the heap) -- pseudopods are now a lobe
    "BorderReachMin": 0.25, "BorderReachMax": 0.50, "BorderTuck": 0.92, "BorderLoopWidth": 0.15,
    "BorderPeriods": [4.0, 6.0], "BorderPauseFrac": 0.2, "BorderSpeed": 0.35, "FrontCompress": 0.65,
    "StragglerFrac": 0.2, "BorderAheadExtra": 0.25,
    # pseudopod: an ARM of worms leaves the heap toward the target: ArmFrac of the places are recruited onto a conveyor along the arm,
    # a few (ArmTipFrac) rear up at the tip. Length = min(ArmMax, D - ArmMargin - ArmStart * r), width ArmWidth tapering to ArmTaper.
    "PseudoRange": 6.0, "PseudoMoveMin": 0.3, "PseudoRamp": 0.4, "TargetRefresh": 0.5,
    "ArmFrac": 0.25, "ArmStart": 0.6, "ArmMax": 2.0, "ArmMargin": 0.3, "ArmWidth": 0.4, "ArmTaper": 0.6, "ArmThick": 0.14,
    "ArmWave": 0.18, "ArmWaveK": 1.3, "ArmWavePer": 3.0, "ArmFlow": 0.12, "ArmTipFrac": 0.015, "ArmTipPitch": 70.0,
    # engulf: EngulfLobe becomes a wide low BASE heap at the target's feet (BaseFrac of the places move into it); ColFrac of the places climb
    # the target as a tapering sheath (RBottom -> RTop, bulges rising, uneven top, strays that stick out, a part that slides down and back up);
    # the rest of the heap stays grouped beside. Fractions apply at engulf = 1 and grow with it.
    "EngulfLobe": 2, "BaseRadius": 1.0, "BaseHeight": 0.35, "EngulfHeight": 1.8, "ColFrac": 0.50, "BaseFrac": 0.40, "RecruitSoft": 0.1,
    "EngulfTurns": 2.2, "EngulfSpin": 0.125, "EngulfRBottom": 0.55, "EngulfRTop": 0.35, "EngulfRMinSheath": 0.33, "EngulfJitter": 0.12,
    "EngulfBulges": 2.5, "EngulfBulgeAmp": 0.08, "EngulfRise": 0.125, "EngulfTopWobble": 0.10,
    "EngulfSlideFrac": 0.35, "EngulfSlideAmp": 0.45, "EngulfSlidePeriods": [2.0, 3.0, 4.0], "EngulfOutFrac": 0.15, "EngulfOutReach": 0.30,
    "BaseOutFrac": 0.12, "BaseSpread": 0.2, "BaseTiltK": 0.25, "EngulfAcrossFrac": 0.30, "EngulfAcrossPitch": 10.0, "EngulfPitchMin": 45.0, "EngulfPitchMax": 85.0,
    # heap worm scale: base = lerp(1.0, 1.7, clamp01((r - 0.6) / (2.44 - 0.6))), x (1 +- 0.1) per worm; border capped
    "HeapScaleR0": 1.0, "HeapScaleR1": 1.0, "HeapScaleRMin": 0.6, "HeapScaleRMax": 2.44, "ScaleJitter": 0.08, "BorderScaleCap": 1.15,
    "TangleR0": 0.15, "TangleK": 0.04, "TangleFullN": 3, "TangleSpan": 17,
    # displayed worms V = min(SlotCount, round(n * k(n))), k = lerp(DensityK0, DensityK1, clamp01((n - DensityN0) / (DensityN1 - DensityN0)))
    "DensityK0": 6.0, "DensityK1": 3.84, "DensityN0": 10, "DensityN1": 200,
    # clock
    "ClockPeriod": 120.0,
    # draw selection (Emit): bodies are ALWAYS LOD1 meshes; flux/border LOD0 up to 12 m
    "BodyLod1Mesh": True,
    "Lod0Cap": 600, "Lod1Cap": 240, "Lod2Cap": 80, "HeapLod0GlobalCap": 1000,
}
