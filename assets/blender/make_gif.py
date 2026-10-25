"""Assembles the PNG frames of a folder into an animated GIF (Pillow, system Python). Usage: py -3 make_gif.py <frames dir> <out.gif> <ms per frame>"""
import os
import shutil
import sys

from PIL import Image

src, out, ms = sys.argv[1], sys.argv[2], int(sys.argv[3])
files = sorted(f for f in os.listdir(src) if f.lower().endswith(".png"))
frames = [Image.open(os.path.join(src, f)).convert("P", palette=Image.ADAPTIVE, colors=128) for f in files]
frames[0].save(out, save_all=True, append_images=frames[1:], duration=ms, loop=0, optimize=True)
shutil.rmtree(src)
print("%s: %d frames, %d KB" % (out, len(frames), os.path.getsize(out) // 1024))
