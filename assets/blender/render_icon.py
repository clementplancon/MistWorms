"""Thunderstore icon (256x256) and banner (1024x256): the shipped worm heap (core + worm instances laid out by mass_layout.py,
n = 10) + a few big worms on a dark misty ground.

Uses the exported .glb files and PNG textures from assets/build, so run it after make_worm/make_mass.
Run: blender --background --python render_icon.py
"""
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
import mass_layout as L  # noqa: E402
import make_mass_preview as MP  # noqa: E402


def import_glb(path):
    import bpy
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in bpy.data.objects:
        if o not in before and o.type != "MESH":
            bpy.data.objects.remove(o)
    return new[0]


def add_mist(world, density):
    nt = world.node_tree
    out = nt.nodes["World Output"]
    vol = nt.nodes.new("ShaderNodeVolumeScatter")
    vol.inputs["Density"].default_value = density
    vol.inputs["Anisotropy"].default_value = 0.35
    vol.inputs["Color"].default_value = (0.55, 0.65, 0.7, 1.0)
    nt.links.new(vol.outputs["Volume"], out.inputs["Volume"])


def build_scene(build_dir):
    import bpy
    C.reset_scene()
    world = C.setup_world((0.012, 0.016, 0.02), 1.0)
    try:
        add_mist(world, 0.03)
    except Exception as e:  # mist is cosmetic; never fail the build for it
        print("mist skipped:", e)
    worm_mat = C.make_pbr_material("worm", os.path.join(build_dir, "worm_albedo.png"),
                                   os.path.join(build_dir, "worm_normal.png"), roughness=0.25)
    ground_mat = C.flat_material("ground", (0.035, 0.035, 0.04), 0.7)

    # the heap: n = 10 (r = 0.83 m, as the old dome), placed by the layout reference, shipped core and coil/crawl/idle meshes
    MP._state["meshes"] = MP.load_meshes(build_dir)
    MP.setup_materials(build_dir, MP._state["meshes"])
    MP._state["keep"] = set(m.name for m in MP._state["meshes"].values())
    MP.populate(MP.heap_params(10, 0.0, 17.3), L.Slots(L.GOLDEN_SEED), 0, 120)

    # unit worms are tiny at icon scale, so the foreground ones are drawn 2.2x larger (poster licence)
    placements = [(-0.9, 0.9, 0.5, 2.2, 2), (0.15, 1.15, 2.6, 2.2, 5), (1.0, 0.7, 3.6, 2.0, 8),
                  (-1.5, 0.2, 1.1, 2.0, 11), (1.55, -0.2, 4.3, 1.9, 3), (-0.4, 1.6, 0.1, 1.9, 6)]
    for i, (x, z, ang, sc, frame) in enumerate(placements):
        w = import_glb(os.path.join(build_dir, "worm_crawl_%02d.glb" % frame))
        w.data.materials.append(worm_mat)
        w.location = (x, -z, 0.0)
        w.rotation_euler = (0, 0, ang)
        w.scale = (sc, sc, sc)

    v = np.array([[-12, 0, -12], [12, 0, -12], [12, 0, 12], [-12, 0, 12]], float)
    g = C.make_object("ground", v, np.array([[0, 3, 2], [0, 2, 1]]))
    g.data.materials.append(ground_mat)

    # lights: cold rim from behind, warm flesh-coloured key, a red glow under the dome's lip
    C.add_light("AREA", C.gltf_to_blender((-2.5, 2.0, -2.5)), 260, color=(0.55, 0.75, 1.0), size=2.5, target=C.gltf_to_blender((0, 0.5, 0)))
    C.add_light("AREA", C.gltf_to_blender((2.5, 2.2, 3.0)), 330, color=(1.0, 0.85, 0.75), size=2.5, target=C.gltf_to_blender((0, 0.4, 0)))
    C.add_light("POINT", C.gltf_to_blender((0.0, 0.25, 1.4)), 60, color=(1.0, 0.25, 0.2), size=0.4)


def main():
    t0 = time.time()
    args = C.parse_args()
    build = args["build"]
    ts = C.ensure_dir(args["thunderstore"])
    prev = C.ensure_dir(args["previews"])
    build_scene(build)
    # icon: dome centred, worms in front
    C.add_camera(C.gltf_to_blender((0.3, 1.5, 4.4)), C.gltf_to_blender((0.0, 0.35, 0.3)), lens=48)
    C.configure_render(256, 256, 64)
    C.render_to(os.path.join(ts, "icon.png"))
    # banner: wider crop, dome left of centre
    C.add_camera(C.gltf_to_blender((0.5, 1.3, 6.6)), C.gltf_to_blender((0.4, 0.3, 0.3)), lens=40)
    C.configure_render(1024, 256, 64)
    C.render_to(os.path.join(ts, "banner.png"))
    import shutil
    shutil.copyfile(os.path.join(ts, "icon.png"), os.path.join(prev, "icon.png"))
    shutil.copyfile(os.path.join(ts, "banner.png"), os.path.join(prev, "banner.png"))
    C.stats("render_icon", t0)


if __name__ == "__main__":
    main()
