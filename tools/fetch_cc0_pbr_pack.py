#!/usr/bin/env python3
"""
Downloads verified CC0 Public Domain PBR textures and African Highland Pure-Sky HDRIs
from Poly Haven (https://polyhaven.com, CC0 1.0) AND AmbientCG (https://ambientcg.com, CC0 1.0)
into KenyaStreetSim/Assets/KenyaStreetSim/Textures/CC0_PBR/.
"""

import io
import json
import os
import urllib.request
import zipfile

UA = {"User-Agent": "KenyaStreetSim-CC0-Fetcher/2.0"}
OUT_DIR = os.path.abspath(
    os.path.join(
        os.path.dirname(__file__),
        "..",
        "KenyaStreetSim",
        "Assets",
        "KenyaStreetSim",
        "Textures",
        "CC0_PBR",
    )
)
os.makedirs(OUT_DIR, exist_ok=True)


def get_json(url: str):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=25) as r:
        return json.loads(r.read().decode("utf-8"))


def download(url: str, dst: str):
    if os.path.exists(dst) and os.path.getsize(dst) > 10000:
        print(f"  [CACHED] {os.path.basename(dst)} ({os.path.getsize(dst)/1024:.0f} KB)")
        return True
    try:
        req = urllib.request.Request(url, headers=UA)
        with urllib.request.urlopen(req, timeout=60) as r:
            data = r.read()
        with open(dst, "wb") as f:
            f.write(data)
        print(f"  [SAVED]  {os.path.basename(dst)} ({len(data)/1024:.0f} KB)")
        return True
    except Exception as e:
        print(f"  [WARN] Failed {os.path.basename(dst)}: {e}")
        return False


def pick_tex_url(files_json, channel_names, res="2k"):
    for ch in channel_names:
        if ch in files_json:
            res_map = files_json[ch].get(res) or files_json[ch].get("1k")
            if not res_map:
                continue
            for fmt in ("jpg", "png"):
                if fmt in res_map and "url" in res_map[fmt]:
                    return res_map[fmt]["url"], fmt
    return None, None


def download_ambientcg_pack(asset_id: str, out_prefix: str, res="1K-JPG"):
    """Downloads a verified CC0 ZIP pack from AmbientCG and extracts Color + NormalGL."""
    alb_dst = os.path.join(OUT_DIR, f"{out_prefix}_Albedo.jpg")
    nrm_dst = os.path.join(OUT_DIR, f"{out_prefix}_Normal.jpg")
    if os.path.exists(alb_dst) and os.path.exists(nrm_dst):
        print(f"  [CACHED] AmbientCG {asset_id} -> {out_prefix}")
        return
    url = f"https://ambientcg.com/get?file={asset_id}_{res}.zip"
    print(f"\nAmbientCG CC0: {asset_id} ({res})")
    try:
        req = urllib.request.Request(url, headers=UA)
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read()
        with zipfile.ZipFile(io.BytesIO(raw)) as zf:
            for name in zf.namelist():
                low = name.lower()
                if low.endswith("_color.jpg"):
                    with open(alb_dst, "wb") as f:
                        f.write(zf.read(name))
                    print(f"  [SAVED]  {os.path.basename(alb_dst)}")
                elif low.endswith("_normalgl.jpg"):
                    with open(nrm_dst, "wb") as f:
                        f.write(zf.read(name))
                    print(f"  [SAVED]  {os.path.basename(nrm_dst)}")
    except Exception as e:
        print(f"  [WARN] AmbientCG {asset_id} skipped: {e}")


HDRIS = [
    ("kloofendal_48d_partly_cloudy_puresky", "Sky_KenyaDay_2k.hdr"),
    ("qwantani_sunset_puresky", "Sky_KenyaSunset_2k.hdr"),
    ("qwantani_night_puresky", "Sky_KenyaNight_2k.hdr"),
]

POLYHAVEN_TEXTURES = [
    ("asphalt_02", "CC0_Asphalt", "2k"),
    ("interlocking_concrete_pavers", "CC0_Cabro", "2k"),
    ("grey_plaster", "CC0_Plaster", "2k"),
    ("corrugated_iron", "CC0_Mabati", "1k"),
    ("aerial_grass_rock", "CC0_SavannaTerrain", "2k"),
    ("bark_brown_02", "CC0_Bark", "1k"),
    ("red_laterite_soil_stones", "CC0_KenyaRedMurram", "1k"),
    ("stone_wall_04", "CC0_KenyaStoneWall", "1k"),
    ("wood_planks_dirt", "CC0_KenyaMarketWood", "1k"),
    ("forest_leaves_02", "CC0_KenyaForestFloor", "1k"),
]

AMBIENTCG_TEXTURES = [
    ("Asphalt012", "ACG_KenyaHighwayTarmac", "1K-JPG"),
    ("Ground054",  "ACG_KenyaHighlandGrass", "1K-JPG"),
    ("CorrugatedSteel005", "ACG_KenyaMabatiRoof", "1K-JPG"),
]


def main():
    print(f"Fetching Poly Haven + AmbientCG CC0 PBR Library -> {OUT_DIR}")

    for asset_id, out_name in HDRIS:
        dst = os.path.join(OUT_DIR, out_name)
        if os.path.exists(dst) and os.path.getsize(dst) > 10000:
            print(f"  [CACHED] {out_name}")
            continue
        print(f"\nPoly Haven HDRI: {asset_id}")
        try:
            info = get_json(f"https://api.polyhaven.com/files/{asset_id}")
            url = info.get("hdri", {}).get("2k", {}).get("hdr", {}).get("url")
            if url:
                download(url, dst)
        except Exception as e:
            print(f"  [WARN] HDRI {asset_id}: {e}")

    for asset_id, prefix, res in POLYHAVEN_TEXTURES:
        alb_test = os.path.join(OUT_DIR, f"{prefix}_Albedo.jpg")
        nrm_test = os.path.join(OUT_DIR, f"{prefix}_Normal.jpg")
        if os.path.exists(alb_test) and os.path.exists(nrm_test):
            print(f"  [CACHED] Poly Haven {prefix}")
            continue
        print(f"\nPoly Haven PBR: {asset_id} ({res})")
        try:
            info = get_json(f"https://api.polyhaven.com/files/{asset_id}")
            diff_url, diff_ext = pick_tex_url(info, ["Diffuse", "col", "albedo"], res=res)
            norm_url, norm_ext = pick_tex_url(info, ["nor_gl", "Normal"], res=res)
            if diff_url:
                download(diff_url, os.path.join(OUT_DIR, f"{prefix}_Albedo.{diff_ext}"))
            if norm_url:
                download(norm_url, os.path.join(OUT_DIR, f"{prefix}_Normal.{norm_ext}"))
        except Exception as e:
            print(f"  [WARN] Poly Haven {asset_id}: {e}")

    for asset_id, prefix, res in AMBIENTCG_TEXTURES:
        download_ambientcg_pack(asset_id, prefix, res)

    print("\nPoly Haven + AmbientCG CC0 PBR texture library sync complete!")


if __name__ == "__main__":
    main()
