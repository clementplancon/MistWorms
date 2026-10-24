"""Validates assets/build against the asset contract and writes assets/build/manifest.json.

Plain Python 3 (no bpy, no numpy): parses the .glb files itself, exactly like the runtime loader will.
Usage: py -3 assets/blender/validate_assets.py [--repo-root <dir>]
Exit code 1 if any rule is violated (manifest.json is then NOT written).
"""
import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import config  # noqa: E402

FORBIDDEN_KEYS = ("materials", "images", "textures", "skins", "animations", "extensionsUsed",
                  "extensionsRequired", "samplers")
COMPONENT = {5126: ("f", 4), 5123: ("H", 2), 5125: ("I", 4)}
TYPE_COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}


class Violation(Exception):
    pass


def read_accessor(js, bin_data, index, expect_type, allowed_components):
    acc = js["accessors"][index]
    if acc["type"] != expect_type:
        raise Violation("accessor %d type %s, expected %s" % (index, acc["type"], expect_type))
    if acc["componentType"] not in allowed_components:
        raise Violation("accessor %d componentType %d not allowed" % (index, acc["componentType"]))
    if acc.get("sparse"):
        raise Violation("sparse accessors are not supported")
    fmt, size = COMPONENT[acc["componentType"]]
    n = TYPE_COUNT[expect_type]
    view = js["bufferViews"][acc["bufferView"]]
    if view.get("buffer", 0) != 0:
        raise Violation("only buffer 0 is supported")
    stride = view.get("byteStride", n * size)
    base = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    out = []
    for i in range(acc["count"]):
        out.append(struct.unpack_from("<" + fmt * n, bin_data, base + i * stride))
    return out


def parse_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    if len(data) < 20:
        raise Violation("file too small")
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2:
        raise Violation("not a binary glTF 2.0 file")
    if length != len(data):
        raise Violation("header length %d != file size %d" % (length, len(data)))
    off = 12
    js = None
    bin_data = None
    while off + 8 <= len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        body = data[off + 8: off + 8 + clen]
        if ctype == 0x4E4F534A:
            js = json.loads(body.decode("utf8"))
        elif ctype == 0x004E4942 and bin_data is None:
            bin_data = body
        off += 8 + clen
        off = (off + 3) & ~3
    if js is None or bin_data is None:
        raise Violation("missing JSON or BIN chunk")
    for key in FORBIDDEN_KEYS:
        if js.get(key):
            raise Violation("forbidden glTF section '%s'" % key)
    if len(js.get("meshes", [])) != 1:
        raise Violation("expected exactly 1 mesh, found %d" % len(js.get("meshes", [])))
    prims = js["meshes"][0]["primitives"]
    if len(prims) != 1:
        raise Violation("expected exactly 1 primitive, found %d" % len(prims))
    p = prims[0]
    if p.get("mode", 4) != 4:
        raise Violation("primitive mode %s is not triangles" % p.get("mode"))
    if "material" in p:
        raise Violation("primitive references a material")
    attrs = p["attributes"]
    for need in ("POSITION", "NORMAL", "TEXCOORD_0"):
        if need not in attrs:
            raise Violation("missing attribute " + need)
    if "indices" not in p:
        raise Violation("missing indices")
    pos = read_accessor(js, bin_data, attrs["POSITION"], "VEC3", (5126,))
    nor = read_accessor(js, bin_data, attrs["NORMAL"], "VEC3", (5126,))
    uvs = read_accessor(js, bin_data, attrs["TEXCOORD_0"], "VEC2", (5126,))
    idx = [t[0] for t in read_accessor(js, bin_data, p["indices"], "SCALAR", (5123, 5125))]
    if not (len(pos) == len(nor) == len(uvs)):
        raise Violation("attribute counts differ (%d/%d/%d)" % (len(pos), len(nor), len(uvs)))
    if len(idx) % 3 != 0 or not idx:
        raise Violation("index count %d is not a positive multiple of 3" % len(idx))
    if max(idx) >= len(pos):
        raise Violation("index out of range")
    for v in pos:
        for c in v:
            if c != c or abs(c) > 100:
                raise Violation("bad position value")
    bad_normals = sum(1 for n in nor if abs((n[0] ** 2 + n[1] ** 2 + n[2] ** 2) ** 0.5 - 1.0) > 0.05)
    if bad_normals > 0:
        raise Violation("%d non-unit normals" % bad_normals)
    for u in uvs:
        if not (-0.001 <= u[0] <= 1.001 and -0.001 <= u[1] <= 1.001):
            raise Violation("uv outside [0,1]: %s" % (u,))
    mins = [min(v[i] for v in pos) for i in range(3)]
    maxs = [max(v[i] for v in pos) for i in range(3)]
    return {"vertices": len(pos), "triangles": len(idx) // 3, "indices": tuple(idx), "uvs": tuple(uvs),
            "min": mins, "max": maxs}


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(33)
    if head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        raise Violation("not a PNG")
    w, h, depth, ctype = struct.unpack(">IIBB", head[16:26])
    if depth != 8 or ctype not in (2, 6):
        raise Violation("PNG must be 8-bit RGB or RGBA (depth %d, colour type %d)" % (depth, ctype))
    return w, h


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    argv = sys.argv[1:]
    if "--repo-root" in argv:
        root = argv[argv.index("--repo-root") + 1]
    if "--" in argv:  # tolerate the blender-style separator
        argv = argv[argv.index("--") + 1:]
        if "--repo-root" in argv:
            root = argv[argv.index("--repo-root") + 1]
    build = os.path.join(root, "assets", "build")
    errors = []
    meshes = []
    info = {}

    for anim, (frames, fps, loop) in config.ANIMATIONS.items():
        names = [anim] if anim == "nest_idle" else ["%s_%02d" % (anim, i) for i in range(frames)]
        prefix = anim.split("_")[0]
        lo, hi = config.TRI_BUDGET[prefix]
        first = None
        for name in names:
            path = os.path.join(build, name + ".glb")
            if not os.path.isfile(path):
                errors.append("%s: file missing" % name)
                continue
            try:
                m = parse_glb(path)
            except (Violation, KeyError, ValueError, struct.error) as e:
                errors.append("%s: %s" % (name, e))
                continue
            info[name] = m
            if not lo <= m["triangles"] <= hi:
                errors.append("%s: %d triangles outside budget %d-%d" % (name, m["triangles"], lo, hi))
            if first is None:
                first = (name, m)
            else:
                f0 = first[1]
                if m["vertices"] != f0["vertices"] or m["indices"] != f0["indices"]:
                    errors.append("%s: topology differs from %s (vertices %d vs %d)" %
                                  (name, first[0], m["vertices"], f0["vertices"]))
                if m["uvs"] != f0["uvs"]:
                    errors.append("%s: UVs differ from %s" % (name, first[0]))
            meshes.append({"name": name, "file": name + ".glb", "vertices": m["vertices"],
                           "triangles": m["triangles"],
                           "boundsMin": [round(x, 5) for x in m["min"]],
                           "boundsMax": [round(x, 5) for x in m["max"]]})

    # bounds plausibility
    for name, m in info.items():
        mn, mx = m["min"], m["max"]
        ext = [mx[i] - mn[i] for i in range(3)]
        if name.startswith(("worm_crawl", "worm_sleep")):
            if not config.WORM_LENGTH[0] <= ext[2] <= config.WORM_LENGTH[1]:
                errors.append("%s: length along Z %.3f outside %s" % (name, ext[2], config.WORM_LENGTH))
            if mn[1] < -0.015 or ext[0] > 0.2:
                errors.append("%s: not resting on the ground / too wide (minY %.3f, width %.3f)" % (name, mn[1], ext[0]))
        if name.startswith("worm_"):
            if abs((mn[2] + mx[2]) / 2) > 0.2:
                errors.append("%s: origin far from the body" % name)
        if name == "worm_rear_05" and not 0.24 <= mx[1] <= 0.32:
            errors.append("worm_rear_05: height %.3f, spec says ~0.28" % mx[1])
        if name == "nest_idle":
            lo, hi = config.NEST_FOOTPRINT
            if not (lo <= ext[0] <= hi and lo <= ext[2] <= hi):
                errors.append("nest: footprint %.3f x %.3f outside %s" % (ext[0], ext[2], config.NEST_FOOTPRINT))
            if not config.NEST_HEIGHT[0] <= ext[1] <= config.NEST_HEIGHT[1]:
                errors.append("nest: height %.3f outside %s" % (ext[1], config.NEST_HEIGHT))
            if abs(mn[1]) > 0.03:
                errors.append("nest: origin not at ground level (minY %.3f)" % mn[1])
        if name.startswith("mass_boil"):
            rx, rz = ext[0] / 2, ext[2] / 2
            lo, hi = config.DOME_RADIUS
            if not (lo <= rx <= hi and lo <= rz <= hi):
                errors.append("%s: dome radius %.3f/%.3f outside %s" % (name, rx, rz, config.DOME_RADIUS))
            if not 0.95 <= mx[1] <= 1.15 or mn[1] < -0.06:
                errors.append("%s: dome height range %.3f..%.3f unexpected" % (name, mn[1], mx[1]))

    textures = []
    for name, (w, h, linear) in config.TEXTURES.items():
        path = os.path.join(build, name + ".png")
        if not os.path.isfile(path):
            errors.append("%s: file missing" % name)
            continue
        try:
            tw, th = png_size(path)
        except (Violation, struct.error) as e:
            errors.append("%s: %s" % (name, e))
            continue
        if (tw, th) != (w, h):
            errors.append("%s: size %dx%d, expected %dx%d" % (name, tw, th, w, h))
        textures.append({"name": name, "file": name + ".png", "width": tw, "height": th, "linear": linear})

    if errors:
        print("VALIDATION FAILED (%d problem(s)):" % len(errors))
        for e in errors:
            print("  - " + e)
        return 1

    manifest = {
        "version": 1,
        "generator": config.GENERATOR,
        "seed": config.SEED,
        "animations": {a: {"frames": f, "fps": fps, "loop": loop} for a, (f, fps, loop) in config.ANIMATIONS.items()},
        "meshes": meshes,
        "textures": textures,
    }
    with open(os.path.join(build, "manifest.json"), "w", encoding="utf8", newline="\n") as fh:
        json.dump(manifest, fh, indent=1)
        fh.write("\n")

    print("Validation OK: %d meshes, %d textures -> assets/build/manifest.json" % (len(meshes), len(textures)))
    groups = {}
    for m in meshes:
        groups.setdefault(m["name"].rsplit("_", 1)[0] if m["name"] != "nest_idle" else "nest_idle", []).append(m["triangles"])
    for g, tris in groups.items():
        print("  %-12s %2d frame(s)  %d vertices-frame0  %d triangles" %
              (g, len(tris), next(m["vertices"] for m in meshes if m["name"].startswith(g)), tris[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
