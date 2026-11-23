#!/usr/bin/env python3
"""
Downloads verified CC0 Public Domain PBR textures and African Highland Pure-Sky HDRIs
from Poly Haven (https://polyhaven.com, CC0 1.0) into KenyaStreetSim/Assets/KenyaStreetSim/Textures/CC0_PBR/.
"""

import json
import os
import urllib.request

UA = {"User-Agent": "KenyaStreetSim-CC0-Fetcher/1.0"}
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
        return
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r:
        data = r.read()
    with open(dst, "wb") as f:
        f.write(data)
    print(f"  [SAVED]  {os.path.basename(dst)} ({len(data)/1024:.0f} KB)")


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


HDRIS = [
    ("kloofendal_48d_partly_cloudy_puresky", "Sky_KenyaDay_2k.hdr"),
    ("qwantani_sunset_puresky", "Sky_KenyaSunset_2k.hdr"),
    ("qwantani_night_puresky", "Sky_KenyaNight_2k.hdr"),
]

TEXTURES = [
    ("asphalt_02", "CC0_Asphalt", "2k"),
    ("interlocking_concrete_pavers", "CC0_Cabro", "2k"),
    ("grey_plaster", "CC0_Plaster", "2k"),
    ("corrugated_iron", "CC0_Mabati", "1k"),
    ("aerial_grass_rock", "CC0_SavannaTerrain", "2k"),
    ("bark_brown_02", "CC0_Bark", "1k"),
]


def main():
    print(f"Downloading CC0 Poly Haven assets into: {OUT_DIR}")

    for asset_id, out_name in HDRIS:
        print(f"\nHDRI: {asset_id}")
        info = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        hdri_2k = info.get("hdri", {}).get("2k", {})
        url = hdri_2k.get("hdr", {}).get("url")
        if url:
            download(url, os.path.join(OUT_DIR, out_name))
        else:
            print(f"  [WARN] No 2k HDR found for {asset_id}")

    for asset_id, prefix, res in TEXTURES:
        print(f"\nTexture: {asset_id} ({res})")
        info = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        diff_url, diff_ext = pick_tex_url(info, ["Diffuse", "col", "albedo"], res=res)
        norm_url, norm_ext = pick_tex_url(info, ["nor_gl", "Normal"], res=res)
        if diff_url:
            download(diff_url, os.path.join(OUT_DIR, f"{prefix}_Albedo.{diff_ext}"))
        if norm_url:
            download(norm_url, os.path.join(OUT_DIR, f"{prefix}_Normal.{norm_ext}"))

    print("\nAll CC0 PBR assets downloaded cleanly!")


if __name__ == "__main__":
    main()
