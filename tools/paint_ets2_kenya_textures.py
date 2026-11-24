"""
Blender 5.3 + NumPy ETS2-Class Texture Studio Script (Zero external pip packages required!):
  1. Composites Poly Haven CC0_Asphalt + AmbientCG ACG_KenyaHighwayTarmac + Poly Haven CC0_KenyaRedMurram
     with dual-lane ETS2 tire-wear tracks, subtle oil strips & dusty red Kenyan verge edges
     -> Assets/KenyaStreetSim/Textures/CC0_PBR/ETS2_KenyaTarmac_Albedo.jpg (1024x1024)
  2. Generates a crisp 2048x1024 editable Savanna Line Kenyan Coach Livery template PNG
     -> KenyaStreetSim/ArtStudio/SavannaLine_Coach_Livery_Template_2048x1024.png (ready to edit in Krita / GIMP!)
"""

import os
import bpy
import numpy as np

_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
REPO_ROOT = os.path.abspath(os.path.join(_script_dir, "..")) if _script_dir else r"C:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
CC0_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "Textures", "CC0_PBR")
ART_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "ArtStudio")
os.makedirs(ART_DIR, exist_ok=True)


def load_rgba_np(path, target_w, target_h):
    img = bpy.data.images.load(path, check_existing=False)
    if img.size[0] != target_w or img.size[1] != target_h:
        img.scale(target_w, target_h)
    arr = np.empty(target_w * target_h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape((target_h, target_w, 4))


def save_np_image(arr, out_path, fmt='JPEG'):
    h, w, _ = arr.shape
    img = bpy.data.images.new("OutTex", width=w, height=h, alpha=(fmt == 'PNG'))
    img.pixels.foreach_set(np.clip(arr, 0.0, 1.0).ravel())
    img.filepath_raw = out_path
    img.file_format = fmt
    img.save()
    bpy.data.images.remove(img)
    print(f"[SAVED] {out_path} ({os.path.getsize(out_path)/1024:.0f} KB)")


def build_ets2_weathered_tarmac():
    base_p = os.path.join(CC0_DIR, "CC0_Asphalt_Albedo.jpg")
    acg_p = os.path.join(CC0_DIR, "ACG_KenyaHighwayTarmac_Albedo.jpg")
    mur_p = os.path.join(CC0_DIR, "CC0_KenyaRedMurram_Albedo.jpg")
    out_p = os.path.join(CC0_DIR, "ETS2_KenyaTarmac_Albedo.jpg")
    if not os.path.isfile(base_p):
        print("[SKIP] Missing CC0_Asphalt_Albedo.jpg")
        return

    sz = 1024
    base = load_rgba_np(base_p, sz, sz)
    if os.path.isfile(acg_p):
        acg = load_rgba_np(acg_p, sz, sz)
        base[:, :, :3] = base[:, :, :3] * 0.65 + acg[:, :, :3] * 0.35

    # Deepen contrast slightly for rich sunlit Kenyan bitumen look
    rgb = base[:, :, :3]
    rgb = np.clip((rgb - 0.5) * 1.18 + 0.44, 0.04, 0.92)

    # Smooth Gaussian ETS2 dual-lane tire-wear tracks + faint center oil streaks across U (width)!
    u = np.linspace(0.0, 1.0, sz, dtype=np.float32)[None, :]  # (1, W)
    wear = np.zeros_like(u)
    for center in (0.18, 0.34, 0.66, 0.82):
        wear += 0.24 * np.exp(-((u - center) / 0.048) ** 2)
    for center in (0.26, 0.74):
        wear += 0.14 * np.exp(-((u - center) / 0.028) ** 2)
    rgb *= (1.0 - wear[:, :, None])

    # Subtle dusty red Kenyan murram verge bleed along outer 6% left & right edges!
    if os.path.isfile(mur_p):
        mur = load_rgba_np(mur_p, sz, sz)[:, :, :3]
        edge_dist = np.minimum(u, 1.0 - u)
        blend = np.clip(1.0 - edge_dist / 0.065, 0.0, 1.0) ** 1.9 * 0.48
        rgb = rgb * (1.0 - blend[:, :, None]) + mur * blend[:, :, None]

    out = np.ones((sz, sz, 4), dtype=np.float32)
    out[:, :, :3] = rgb
    save_np_image(out, out_p, fmt='JPEG')


def build_editable_sacco_livery():
    out_p = os.path.join(ART_DIR, "SavannaLine_Coach_Livery_Template_2048x1024.png")
    w, h = 2048, 1024
    arr = np.ones((h, w, 4), dtype=np.float32)
    # Pearl white upper coach body
    arr[:, :, 0] = 0.95
    arr[:, :, 1] = 0.96
    arr[:, :, 2] = 0.98

    y = np.arange(h, dtype=np.float32)[:, None]  # Blender image Y=0 is bottom!
    x = np.arange(w, dtype=np.float32)[None, :]
    slope = y - (x / w) * 95.0

    # Lower Kenyan Crimson Skirt
    skirt = slope < 360.0
    arr[skirt, :3] = (0.76, 0.10, 0.12)
    # Sweeping Kenyan Flag Ribbon (Emerald -> White -> Black -> Gold)
    arr[(slope >= 360.0) & (slope < 425.0), :3] = (0.06, 0.46, 0.22)
    arr[(slope >= 425.0) & (slope < 445.0), :3] = (0.98, 0.98, 0.99)
    arr[(slope >= 445.0) & (slope < 510.0), :3] = (0.08, 0.09, 0.10)
    arr[(slope >= 510.0) & (slope < 532.0), :3] = (0.95, 0.72, 0.08)

    # Dark panoramic window belt
    win_band = (y[:, 0] >= 590.0) & (y[:, 0] <= 885.0)
    arr[win_band, :, :3] = (0.09, 0.11, 0.15)
    for i in range(7):
        x0, x1 = 120 + i * 260, 345 + i * 260
        pane = (y >= 615.0) & (y <= 860.0) & (x >= x0) & (x <= x1)
        arr[pane, :3] = (0.16, 0.24, 0.32)

    save_np_image(arr, out_p, fmt='PNG')


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_ets2_weathered_tarmac()
    build_editable_sacco_livery()
