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
    "mass_boil": (8, 8, True),
    "nest_idle": (1, 0, False),
}

# name -> (width, height, linear)
TEXTURES = {
    "worm_albedo": (128, 128, False),
    "worm_normal": (128, 128, True),
    "worm_gloss": (128, 128, True),      # RGBA: RGB = 0, alpha = lustre (the only texture with alpha)
    "mass_albedo": (1024, 1024, False),
    "mass_normal": (1024, 1024, True),
    "nest_albedo": (1024, 1024, False),
    "nest_normal": (1024, 1024, True),
}

# animation prefix -> (min, max) triangles per frame
TRI_BUDGET = {
    "worm": (600, 900),
    "mass": (1500, 2500),
    "nest": (1700, 2300),
}

WORM_TEXTURE_MAX = 128               # worm_* textures follow the vanilla creatures (dump section 1)
WORM_LENGTH = (0.38, 0.42)          # extent along Z of crawl/sleep frames
NEST_FOOTPRINT = (0.85, 0.95)       # X and Z extent
NEST_HEIGHT = (0.45, 0.55)
DOME_RADIUS = (0.95, 1.08)          # X and Z half-extent
