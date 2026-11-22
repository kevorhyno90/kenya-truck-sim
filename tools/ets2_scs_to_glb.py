#!/usr/bin/env python3
"""
KenyaStreetSim — Universal ETS2 (.scs / .pmg / .pmd / .mat / .tobj / .dds) to glTF 2.0 (.glb) Converter
---------------------------------------------------------------------------------------------------------
Converts unlocked Euro Truck Simulator 2 (.scs ZIP) vehicle/prop mods directly into Unity-ready
glTF 2.0 Binary (.glb) models with embedded PNG PBR textures, transparent glass, skipped shadow boxes,
and preserved ETS2 wheel/mirror locators.

Pure Python stdlib + NumPy (no Blender or external DLLs required).
Supports SCS PMG v0x14 (and v0x15), PMD v4, MAT, TOBJ, and DDS (DXT1, DXT3, DXT5, BGRA8888, BGR888).
"""

import argparse
import json
import os
import re
import struct
import sys
import zipfile
import zlib
import numpy as np


# ---------------------------------------------------------------------------
# SCS 64-bit Token Decoder (base-38)
# ---------------------------------------------------------------------------
SCS_TOKEN_CHARS = "\x000123456789abcdefghijklmnopqrstuvwxyz_"


def decode_scs_token(val: int) -> str:
    out = []
    while val > 0:
        out.append(SCS_TOKEN_CHARS[val % 38])
        val //= 38
    return "".join(out)


# ---------------------------------------------------------------------------
# Virtual Mod Filesystem (reads transparently from a folder OR a .scs ZIP)
# ---------------------------------------------------------------------------
class ModFS:
    def __init__(self, source_path: str):
        self.source_path = source_path
        self.is_zip = os.path.isfile(source_path)
        self.zf = None
        self.zip_map = {}

        if self.is_zip:
            with open(source_path, "rb") as f:
                magic = f.read(4)
            if magic == b"SCS#":
                raise RuntimeError(
                    f"'{os.path.basename(source_path)}' uses SCS HashFS (SCS#) archive format, "
                    "not a standard unlocked ZIP .scs mod. Please use an unlocked .scs mod or unpack it first with SCS Extractor."
                )
            if not zipfile.is_zipfile(source_path):
                raise RuntimeError(
                    f"'{os.path.basename(source_path)}' is locked or not a standard ZIP-based .scs archive."
                )
            self.zf = zipfile.ZipFile(source_path, "r")
            for info in self.zf.infolist():
                if not info.is_dir():
                    norm = info.filename.replace("\\", "/").lstrip("/").lower()
                    self.zip_map[norm] = info.filename
        elif not os.path.isdir(source_path):
            raise RuntimeError(f"Source path not found: {source_path}")

    def close(self):
        if self.zf is not None:
            self.zf.close()
            self.zf = None

    def list_pmgs(self):
        results = []
        if self.is_zip:
            for norm, orig in self.zip_map.items():
                if norm.endswith(".pmg"):
                    size = self.zf.getinfo(orig).file_size
                    results.append((norm, size))
        else:
            for root, _, files in os.walk(self.source_path):
                for fn in files:
                    if fn.lower().endswith(".pmg"):
                        full = os.path.join(root, fn)
                        rel = os.path.relpath(full, self.source_path).replace("\\", "/")
                        results.append((rel, os.path.getsize(full)))
        results.sort(key=lambda x: x[1], reverse=True)
        return results

    def read_bytes(self, vpath: str):
        norm = vpath.replace("\\", "/").lstrip("/")
        if self.is_zip:
            orig = self.zip_map.get(norm.lower())
            if orig is None:
                return None
            return self.zf.read(orig)
        full = os.path.join(self.source_path, norm.replace("/", os.sep))
        if not os.path.isfile(full):
            return None
        with open(full, "rb") as f:
            return f.read()


# ---------------------------------------------------------------------------
# Fast Vectorized NumPy DDS (DXT1 / DXT3 / DXT5 / RGBA / BGRA) -> PNG Encoder
# ---------------------------------------------------------------------------
def _decode_rgb565(c: np.ndarray):
    r = ((c >> 11) & 0x1F).astype(np.uint16)
    g = ((c >> 5) & 0x3F).astype(np.uint16)
    b = (c & 0x1F).astype(np.uint16)
    r = ((r << 3) | (r >> 2)).astype(np.uint8)
    g = ((g << 2) | (g >> 4)).astype(np.uint8)
    b = ((b << 3) | (b >> 2)).astype(np.uint8)
    return r, g, b


def _decode_dxt_color_blocks(color_bytes: np.ndarray, is_dxt1: bool):
    """color_bytes shape: (num_blocks, 8) uint8 -> returns (num_blocks, 16, 4) uint8 RGBA."""
    c0 = color_bytes[:, 0].astype(np.uint16) | (color_bytes[:, 1].astype(np.uint16) << 8)
    c1 = color_bytes[:, 2].astype(np.uint16) | (color_bytes[:, 3].astype(np.uint16) << 8)
    r0, g0, b0 = _decode_rgb565(c0)
    r1, g1, b1 = _decode_rgb565(c1)

    n = color_bytes.shape[0]
    pal = np.zeros((n, 4, 4), dtype=np.uint8)
    pal[:, 0, 0], pal[:, 0, 1], pal[:, 0, 2], pal[:, 0, 3] = r0, g0, b0, 255
    pal[:, 1, 0], pal[:, 1, 1], pal[:, 1, 2], pal[:, 1, 3] = r1, g1, b1, 255

    r0i, g0i, b0i = r0.astype(np.int32), g0.astype(np.int32), b0.astype(np.int32)
    r1i, g1i, b1i = r1.astype(np.int32), g1.astype(np.int32), b1.astype(np.int32)

    if is_dxt1:
        gt = c0 > c1
        pal[:, 2, 0] = np.where(gt, (2 * r0i + r1i) // 3, (r0i + r1i) // 2).astype(np.uint8)
        pal[:, 2, 1] = np.where(gt, (2 * g0i + g1i) // 3, (g0i + g1i) // 2).astype(np.uint8)
        pal[:, 2, 2] = np.where(gt, (2 * b0i + b1i) // 3, (b0i + b1i) // 2).astype(np.uint8)
        pal[:, 2, 3] = 255

        pal[:, 3, 0] = np.where(gt, (r0i + 2 * r1i) // 3, 0).astype(np.uint8)
        pal[:, 3, 1] = np.where(gt, (g0i + 2 * g1i) // 3, 0).astype(np.uint8)
        pal[:, 3, 2] = np.where(gt, (b0i + 2 * b1i) // 3, 0).astype(np.uint8)
        pal[:, 3, 3] = np.where(gt, 255, 0).astype(np.uint8)
    else:
        pal[:, 2, 0] = ((2 * r0i + r1i) // 3).astype(np.uint8)
        pal[:, 2, 1] = ((2 * g0i + g1i) // 3).astype(np.uint8)
        pal[:, 2, 2] = ((2 * b0i + b1i) // 3).astype(np.uint8)
        pal[:, 2, 3] = 255
        pal[:, 3, 0] = ((r0i + 2 * r1i) // 3).astype(np.uint8)
        pal[:, 3, 1] = ((g0i + 2 * g1i) // 3).astype(np.uint8)
        pal[:, 3, 2] = ((b0i + 2 * b1i) // 3).astype(np.uint8)
        pal[:, 3, 3] = 255

    bits = (
        color_bytes[:, 4].astype(np.uint32)
        | (color_bytes[:, 5].astype(np.uint32) << 8)
        | (color_bytes[:, 6].astype(np.uint32) << 16)
        | (color_bytes[:, 7].astype(np.uint32) << 24)
    )
    shifts = np.arange(0, 32, 2, dtype=np.uint32)[None, :]
    idxs = (bits[:, None] >> shifts) & 0x3  # (n, 16)
    row_idx = np.arange(n)[:, None]
    return pal[row_idx, idxs]  # (n, 16, 4)


def decode_dds_to_rgba(raw: bytes):
    if len(raw) < 128 or raw[:4] != b"DDS ":
        return None
    h, w = struct.unpack("<II", raw[12:20])
    pf_flags = struct.unpack("<I", raw[80:84])[0]
    fourcc = raw[84:88]
    rgb_bits = struct.unpack("<I", raw[88:92])[0]
    r_mask, g_mask, b_mask, a_mask = struct.unpack("<4I", raw[92:108])

    data_off = 128
    if fourcc == b"DX10":
        # Minimal DX10 header skip
        data_off = 148

    payload = raw[data_off:]
    bw = max(1, (w + 3) // 4)
    bh = max(1, (h + 3) // 4)
    nb = bw * bh

    if (pf_flags & 0x4) and fourcc in (b"DXT1", b"DXT3", b"DXT5"):
        if fourcc == b"DXT1":
            need = nb * 8
            if len(payload) < need:
                return None
            blocks = np.frombuffer(payload[:need], dtype=np.uint8).reshape(nb, 8)
            px = _decode_dxt_color_blocks(blocks, is_dxt1=True)
        elif fourcc == b"DXT3":
            need = nb * 16
            if len(payload) < need:
                return None
            blocks = np.frombuffer(payload[:need], dtype=np.uint8).reshape(nb, 16)
            px = _decode_dxt_color_blocks(blocks[:, 8:16], is_dxt1=False)
            ab = blocks[:, 0:8]
            a_bits = np.zeros((nb, 16), dtype=np.uint8)
            for k in range(8):
                a_bits[:, 2 * k] = (ab[:, k] & 0x0F) * 17
                a_bits[:, 2 * k + 1] = ((ab[:, k] >> 4) & 0x0F) * 17
            px[:, :, 3] = a_bits
        else:  # DXT5
            need = nb * 16
            if len(payload) < need:
                return None
            blocks = np.frombuffer(payload[:need], dtype=np.uint8).reshape(nb, 16)
            px = _decode_dxt_color_blocks(blocks[:, 8:16], is_dxt1=False)
            a0 = blocks[:, 0].astype(np.int32)
            a1 = blocks[:, 1].astype(np.int32)
            apal = np.zeros((nb, 8), dtype=np.uint8)
            apal[:, 0] = a0
            apal[:, 1] = a1
            gt = a0 > a1
            apal[:, 2] = np.where(gt, (6 * a0 + 1 * a1) // 7, (4 * a0 + 1 * a1) // 5)
            apal[:, 3] = np.where(gt, (5 * a0 + 2 * a1) // 7, (3 * a0 + 2 * a1) // 5)
            apal[:, 4] = np.where(gt, (4 * a0 + 3 * a1) // 7, (2 * a0 + 3 * a1) // 5)
            apal[:, 5] = np.where(gt, (3 * a0 + 4 * a1) // 7, (1 * a0 + 4 * a1) // 5)
            apal[:, 6] = np.where(gt, (2 * a0 + 5 * a1) // 7, 0)
            apal[:, 7] = np.where(gt, (1 * a0 + 6 * a1) // 7, 255)

            abits = (
                blocks[:, 2].astype(np.uint64)
                | (blocks[:, 3].astype(np.uint64) << 8)
                | (blocks[:, 4].astype(np.uint64) << 16)
                | (blocks[:, 5].astype(np.uint64) << 24)
                | (blocks[:, 6].astype(np.uint64) << 32)
                | (blocks[:, 7].astype(np.uint64) << 40)
            )
            ashifts = np.arange(0, 48, 3, dtype=np.uint64)[None, :]
            aidx = ((abits[:, None] >> ashifts) & 0x7).astype(np.int32)
            px[:, :, 3] = apal[np.arange(nb)[:, None], aidx]

        img = px.reshape(bh, bw, 4, 4, 4).transpose(0, 2, 1, 3, 4).reshape(bh * 4, bw * 4, 4)
        return img[:h, :w, :]

    # Uncompressed RGB/RGBA/BGRA
    if rgb_bits == 32 and len(payload) >= w * h * 4:
        arr = np.frombuffer(payload[: w * h * 4], dtype=np.uint8).reshape(h, w, 4).copy()
        if r_mask == 0x00FF0000:  # BGRA -> RGBA
            arr = arr[:, :, [2, 1, 0, 3]]
        if a_mask == 0:
            arr[:, :, 3] = 255
        return arr
    if rgb_bits == 24 and len(payload) >= w * h * 3:
        arr = np.frombuffer(payload[: w * h * 3], dtype=np.uint8).reshape(h, w, 3)
        out = np.full((h, w, 4), 255, dtype=np.uint8)
        if r_mask == 0x00FF0000:
            out[:, :, :3] = arr[:, :, [2, 1, 0]]
        else:
            out[:, :, :3] = arr
        return out
    return None


def downsample_rgba(img: np.ndarray, max_dim: int) -> np.ndarray:
    while img.shape[0] > max_dim or img.shape[1] > max_dim:
        h, w = img.shape[0] // 2, img.shape[1] // 2
        if h < 1 or w < 1:
            break
        trimmed = img[: h * 2, : w * 2, :].astype(np.uint16)
        img = ((trimmed[0::2, 0::2] + trimmed[1::2, 0::2] + trimmed[0::2, 1::2] + trimmed[1::2, 1::2]) >> 2).astype(
            np.uint8
        )
    return img


def encode_png(rgba: np.ndarray, drop_alpha: bool = False) -> bytes:
    h, w, _ = rgba.shape
    if drop_alpha:
        pixels = np.ascontiguousarray(rgba[:, :, :3])
        color_type = 2  # RGB
        stride = w * 3
    else:
        pixels = np.ascontiguousarray(rgba)
        color_type = 6  # RGBA
        stride = w * 4

    # Prepend filter byte 0 (None) to each scanline
    rows = pixels.reshape(h, stride)
    filtered = np.empty((h, stride + 1), dtype=np.uint8)
    filtered[:, 0] = 0
    filtered[:, 1:] = rows
    comp = zlib.compress(filtered.tobytes(), level=6)

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", w, h, 8, color_type, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", comp) + chunk(b"IEND", b"")


# ---------------------------------------------------------------------------
# SCS PMD v4, MAT, TOBJ, PMG v0x14/v0x15 Parsers
# ---------------------------------------------------------------------------
def parse_tobj_dds_path(tobj_bytes: bytes):
    if not tobj_bytes or len(tobj_bytes) < 50:
        return None
    # Standard SCS TOBJ binary: uint32 path length at offset 40, ASCII path starts at 48
    path_len = struct.unpack("<I", tobj_bytes[40:44])[0]
    if 48 + path_len <= len(tobj_bytes) and path_len > 4:
        return tobj_bytes[48 : 48 + path_len].decode("ascii", "ignore").strip("\x00")
    # Fallback text TOBJ
    txt = tobj_bytes.decode("ascii", "ignore")
    m = re.search(r"([/\w\-\.]+\.dds)", txt, re.IGNORECASE)
    return m.group(1) if m else None


def parse_pmd_v4(pmd_bytes: bytes, look_index: int = 0):
    ver, mat_cnt, look_cnt, piece_cnt, var_cnt, part_cnt, attr_cnt, attr_val_size, mat_block_size = struct.unpack(
        "<9I", pmd_bytes[:36]
    )
    if ver != 4:
        raise RuntimeError(f"Unsupported PMD version {ver} (expected v4)")
    mat_str_off = struct.unpack("<I", pmd_bytes[60:64])[0]
    raw_strs = pmd_bytes[mat_str_off : mat_str_off + mat_block_size].split(b"\x00")
    all_mats = [s.decode("ascii", "ignore") for s in raw_strs if s]

    look_clamped = max(0, min(look_index, max(0, look_cnt - 1)))
    start = look_clamped * mat_cnt
    end = start + mat_cnt
    if end <= len(all_mats):
        return all_mats[start:end]
    return all_mats[:mat_cnt]


def parse_mat_info(mod_fs: ModFS, mat_vpath: str):
    raw = mod_fs.read_bytes(mat_vpath)
    info = {
        "vpath": mat_vpath,
        "shader": "eut2.dif.spec.rfx",
        "base_dds": None,
        "diffuse": [1.0, 1.0, 1.0],
        "shininess": 40.0,
        "skip": False,
        "is_glass": False,
        "is_overlay": False,
        "is_emissive": False,
        "is_paint": False,
    }
    if not raw:
        return info

    txt = raw.decode("utf-8", "ignore")
    m_sh = re.search(r'material\s*:\s*"([^"]+)"', txt)
    if m_sh:
        info["shader"] = m_sh.group(1).lower()

    sh = info["shader"]
    if "shadowonly" in sh or "fakeshadow" in sh or "ui_shadow" in sh:
        info["skip"] = True
        return info
    if "glass" in sh:
        info["is_glass"] = True
    if ".over" in sh or "decal" in sh:
        info["is_overlay"] = True
    if ".lum" in sh or "lamp" in sh:
        info["is_emissive"] = True
    if "truckpaint" in sh:
        info["is_paint"] = True

    m_diff = re.search(r"diffuse\s*:\s*\{\s*([0-9eE\.\+\-]+)\s*,\s*([0-9eE\.\+\-]+)\s*,\s*([0-9eE\.\+\-]+)\s*\}", txt)
    if m_diff:
        info["diffuse"] = [min(1.0, max(0.0, float(m_diff.group(k)))) for k in (1, 2, 3)]

    m_shin = re.search(r"shininess\s*:\s*([0-9eE\.\+\-]+)", txt)
    if m_shin:
        info["shininess"] = float(m_shin.group(1))

    # Extract texture[0] (texture_base)
    tex_matches = re.findall(r'texture\[(\d+)\]\s*:\s*"([^"]+)"', txt)
    if tex_matches:
        tobj_vpath = tex_matches[0][1]
        tobj_bytes = mod_fs.read_bytes(tobj_vpath)
        if tobj_bytes:
            info["base_dds"] = parse_tobj_dds_path(tobj_bytes)
        elif tobj_vpath.lower().endswith(".tobj"):
            # Try direct .dds guess in same folder
            guess = tobj_vpath[:-5] + ".dds"
            if mod_fs.read_bytes(guess) is not None:
                info["base_dds"] = guess

    return info


def parse_pmg(pmg_bytes: bytes):
    sig = struct.unpack("<I", pmg_bytes[:4])[0]
    ver = sig & 0xFF
    magic = pmg_bytes[1:4]
    if magic != b"gmP":
        raise RuntimeError(f"Invalid PMG magic: {pmg_bytes[:4]!r}")
    if ver not in (0x14, 0x15):
        raise RuntimeError(f"Unsupported PMG version 0x{ver:02x} (supported: 0x14, 0x15)")

    piece_cnt, part_cnt, bone_cnt, weight_cnt, loc_cnt = struct.unpack("<5I", pmg_bytes[4:24])
    offs = struct.unpack("<10I", pmg_bytes[64:104])
    bone_off, part_off, loc_off, piece_off, str_off, str_size, vert_off, vert_size, idx_off, idx_size = offs

    # Locators (44 bytes each)
    locators = []
    for i in range(loc_cnt):
        o = loc_off + i * 44
        tok = struct.unpack("<Q", pmg_bytes[o : o + 8])[0]
        pos = struct.unpack("<3f", pmg_bytes[o + 8 : o + 20])
        scale = struct.unpack("<f", pmg_bytes[o + 20 : o + 24])[0]
        name = decode_scs_token(tok)
        locators.append({"name": name, "pos": pos, "scale": scale})

    # Pieces (100 bytes each in 0x14 & 0x15)
    pieces = []
    for i in range(piece_cnt):
        o = piece_off + i * 100
        edges, verts, uv_mask, uv_cnt, mat_idx = struct.unpack("<2Ii2I", pmg_bytes[o : o + 20])
        stride = struct.unpack("<I", pmg_bytes[o + 60 : o + 64])[0]
        pos_o, nrm_o, uv_o, rgba_o, rgba2_o, tan_o, bi_o, bw_o, p_idx_o = struct.unpack("<9i", pmg_bytes[o + 64 : o + 100])
        if edges == 0 or verts == 0:
            continue

        # Read interleaved vertex buffer via NumPy strided view
        buf = np.frombuffer(pmg_bytes, dtype=np.uint8, count=verts * stride, offset=pos_o).reshape(verts, stride)

        pos = np.frombuffer(np.ascontiguousarray(buf[:, 0:12]), dtype="<f4").reshape(verts, 3).copy()
        nrm_rel = nrm_o - pos_o
        if nrm_o >= 0 and 0 <= nrm_rel <= stride - 12:
            nrm = np.frombuffer(np.ascontiguousarray(buf[:, nrm_rel : nrm_rel + 12]), dtype="<f4").reshape(verts, 3).copy()
        else:
            nrm = np.tile(np.array([0.0, 1.0, 0.0], dtype=np.float32), (verts, 1))

        uv_rel = uv_o - pos_o
        if uv_o >= 0 and uv_cnt > 0 and 0 <= uv_rel <= stride - 8:
            uv = np.frombuffer(np.ascontiguousarray(buf[:, uv_rel : uv_rel + 8]), dtype="<f4").reshape(verts, 2).copy()
        else:
            uv = np.zeros((verts, 2), dtype=np.float32)

        # Indices: uint16 triangles (3 indices per triangle)
        indices = np.frombuffer(pmg_bytes, dtype="<u2", count=edges, offset=p_idx_o).copy()

        # Rotate 180 deg around Y so ETS2 front (-Z) aligns cleanly with glTF/Unity front (+Z)
        # (-x, +y, -z) has det = +1, so CCW triangle winding is preserved!
        pos[:, 0] = -pos[:, 0]
        pos[:, 2] = -pos[:, 2]
        nrm[:, 0] = -nrm[:, 0]
        nrm[:, 2] = -nrm[:, 2]
        # Normalize normals safely
        nlen = np.linalg.norm(nrm, axis=1, keepdims=True)
        nrm = np.where(nlen > 1e-6, nrm / np.maximum(nlen, 1e-6), np.array([0.0, 1.0, 0.0], dtype=np.float32))

        pieces.append(
            {
                "piece_index": i,
                "mat_idx": mat_idx,
                "pos": pos,
                "nrm": nrm,
                "uv": uv,
                "indices": indices,
            }
        )

    return pieces, locators


# ---------------------------------------------------------------------------
# glTF 2.0 Binary (.glb) Writer
# ---------------------------------------------------------------------------
def build_glb(
    mod_fs: ModFS,
    pmg_vpath: str,
    out_glb_path: str,
    look_index: int = 0,
    max_tex: int = 1024,
    flip_v: bool = False,
):
    pmg_bytes = mod_fs.read_bytes(pmg_vpath)
    if not pmg_bytes:
        raise RuntimeError(f"PMG not found: {pmg_vpath}")

    pmd_vpath = pmg_vpath[:-4] + ".pmd"
    pmd_bytes = mod_fs.read_bytes(pmd_vpath)
    mat_vpaths = parse_pmd_v4(pmd_bytes, look_index=look_index) if pmd_bytes else []

    pieces, locators = parse_pmg(pmg_bytes)
    mat_infos = [parse_mat_info(mod_fs, mp) for mp in mat_vpaths]

    bin_chunks = bytearray()
    buffer_views = []
    accessors = []
    images = []
    textures = []
    materials = []

    def append_bin(raw_bytes: bytes, target=None) -> int:
        # 4-byte align
        while len(bin_chunks) % 4 != 0:
            bin_chunks.append(0)
        offset = len(bin_chunks)
        bin_chunks.extend(raw_bytes)
        bv = {"buffer": 0, "byteOffset": offset, "byteLength": len(raw_bytes)}
        if target is not None:
            bv["target"] = target
        idx = len(buffer_views)
        buffer_views.append(bv)
        return idx

    # Deduplicate decoded DDS -> PNG textures
    tex_cache = {}

    def get_texture_index(dds_vpath: str, keep_alpha: bool):
        if not dds_vpath:
            return None
        key = (dds_vpath.lower(), keep_alpha)
        if key in tex_cache:
            return tex_cache[key]
        raw_dds = mod_fs.read_bytes(dds_vpath)
        if not raw_dds:
            tex_cache[key] = None
            return None
        rgba = decode_dds_to_rgba(raw_dds)
        if rgba is None:
            tex_cache[key] = None
            return None
        rgba = downsample_rgba(rgba, max_tex)
        png_bytes = encode_png(rgba, drop_alpha=not keep_alpha)
        bv_idx = append_bin(png_bytes)
        img_idx = len(images)
        images.append({"bufferView": bv_idx, "mimeType": "image/png", "name": os.path.basename(dds_vpath)})
        tex_idx = len(textures)
        textures.append({"source": img_idx, "sampler": 0})
        tex_cache[key] = tex_idx
        return tex_idx

    # Build glTF materials matching PMD indices
    gltf_mat_map = {}
    for mi, info in enumerate(mat_infos):
        if info["skip"]:
            gltf_mat_map[mi] = None
            continue

        mat_name = f"ETS2_Mat_{mi:02d}_{os.path.basename(info['base_dds']) if info['base_dds'] else info['shader']}"
        if info["is_glass"]:
            gltf_mat = {
                "name": mat_name,
                "pbrMetallicRoughness": {
                    "baseColorFactor": [0.08, 0.13, 0.17, 0.28],
                    "metallicFactor": 0.15,
                    "roughnessFactor": 0.06,
                },
                "alphaMode": "BLEND",
                "doubleSided": True,
            }
        else:
            keep_alpha = info["is_overlay"]
            tex_idx = get_texture_index(info["base_dds"], keep_alpha=keep_alpha)
            diff = info["diffuse"]
            # SCS truckpaint uses diffuse as paint tint; ensure vivid bright body instead of black
            if info["is_paint"] and sum(diff) < 0.3:
                diff = [0.95, 0.95, 0.96]
            roughness = float(np.clip(1.0 - (info["shininess"] / 150.0), 0.14, 0.85))
            metallic = 0.22 if info["is_paint"] else (0.35 if "add.env" in info["shader"] else 0.08)

            pbr = {
                "baseColorFactor": [diff[0], diff[1], diff[2], 1.0],
                "metallicFactor": metallic,
                "roughnessFactor": roughness,
            }
            if tex_idx is not None:
                pbr["baseColorTexture"] = {"index": tex_idx}

            gltf_mat = {
                "name": mat_name,
                "pbrMetallicRoughness": pbr,
                "doubleSided": True,
            }
            if info["is_overlay"] and tex_idx is not None:
                gltf_mat["alphaMode"] = "BLEND"
            if info["is_emissive"] and tex_idx is None:
                gltf_mat["emissiveFactor"] = [0.6, 0.75, 0.9]

        gltf_mat_map[mi] = len(materials)
        materials.append(gltf_mat)

    # Fallback default material
    default_mat_idx = len(materials)
    materials.append(
        {
            "name": "ETS2_DefaultPBR",
            "pbrMetallicRoughness": {"baseColorFactor": [0.85, 0.85, 0.88, 1.0], "metallicFactor": 0.15, "roughnessFactor": 0.45},
            "doubleSided": True,
        }
    )

    primitives = []
    total_verts = 0
    total_tris = 0
    all_min = np.array([1e9, 1e9, 1e9], dtype=np.float32)
    all_max = np.array([-1e9, -1e9, -1e9], dtype=np.float32)

    for pc in pieces:
        mi = pc["mat_idx"]
        if mi in gltf_mat_map and gltf_mat_map[mi] is None:
            continue  # Skip shadowonly / fakeshadow planes cleanly

        pos = np.ascontiguousarray(pc["pos"], dtype=np.float32)
        nrm = np.ascontiguousarray(pc["nrm"], dtype=np.float32)
        uv = np.ascontiguousarray(pc["uv"], dtype=np.float32)
        if flip_v:
            uv = uv.copy()
            uv[:, 1] = 1.0 - uv[:, 1]
        idx = np.ascontiguousarray(pc["indices"], dtype=np.uint16)

        pmin = pos.min(axis=0)
        pmax = pos.max(axis=0)
        all_min = np.minimum(all_min, pmin)
        all_max = np.maximum(all_max, pmax)

        pos_bv = append_bin(pos.tobytes(), target=34962)
        nrm_bv = append_bin(nrm.tobytes(), target=34962)
        uv_bv = append_bin(uv.tobytes(), target=34962)
        idx_bv = append_bin(idx.tobytes(), target=34963)

        pos_acc = len(accessors)
        accessors.append(
            {
                "bufferView": pos_bv,
                "componentType": 5126,
                "count": len(pos),
                "type": "VEC3",
                "min": [float(x) for x in pmin],
                "max": [float(x) for x in pmax],
            }
        )
        nrm_acc = len(accessors)
        accessors.append({"bufferView": nrm_bv, "componentType": 5126, "count": len(nrm), "type": "VEC3"})
        uv_acc = len(accessors)
        accessors.append({"bufferView": uv_bv, "componentType": 5126, "count": len(uv), "type": "VEC2"})
        idx_acc = len(accessors)
        accessors.append({"bufferView": idx_bv, "componentType": 5123, "count": len(idx), "type": "SCALAR"})

        mat_gltf = gltf_mat_map.get(mi, default_mat_idx)
        primitives.append(
            {
                "attributes": {"POSITION": pos_acc, "NORMAL": nrm_acc, "TEXCOORD_0": uv_acc},
                "indices": idx_acc,
                "material": mat_gltf,
                "mode": 4,
            }
        )
        total_verts += len(pos)
        total_tris += len(idx) // 3

    # Build scene nodes: Root mesh + ETS2 wheel/mirror/cab locators (rotated 180° around Y to match +Z forward)
    nodes = [{"name": os.path.splitext(os.path.basename(pmg_vpath))[0], "mesh": 0, "children": []}]
    wheel_locs = []
    for loc in locators:
        nm = loc["name"]
        if any(k in nm for k in ("wheel_", "mirror", "swheel", "cabin", "headlight")):
            lx, ly, lz = loc["pos"]
            pos_z_fwd = [-float(lx), float(ly), -float(lz)]
            n_idx = len(nodes)
            nodes.append({"name": f"ETS2_LOC_{nm}", "translation": pos_z_fwd})
            nodes[0]["children"].append(n_idx)
            if nm.startswith("wheel_"):
                wheel_locs.append({"name": nm, "pos": pos_z_fwd})

    if not nodes[0]["children"]:
        del nodes[0]["children"]

    while len(bin_chunks) % 4 != 0:
        bin_chunks.append(0)

    gltf = {
        "asset": {"version": "2.0", "generator": "KenyaStreetSim ETS2 SCS-to-GLB Converter"},
        "scene": 0,
        "scenes": [{"name": "ETS2_Vehicle", "nodes": [0]}],
        "nodes": nodes,
        "meshes": [{"name": "ETS2_VehicleMesh", "primitives": primitives}],
        "materials": materials,
        "textures": textures,
        "images": images,
        "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}],
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [{"byteLength": len(bin_chunks)}],
    }

    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4 != 0:
        json_bytes += b" "

    total_len = 12 + 8 + len(json_bytes) + 8 + len(bin_chunks)
    os.makedirs(os.path.dirname(os.path.abspath(out_glb_path)), exist_ok=True)
    with open(out_glb_path, "wb") as f:
        f.write(struct.pack("<3I", 0x46546C67, 2, total_len))
        f.write(struct.pack("<II", len(json_bytes), 0x4E4F534A))
        f.write(json_bytes)
        f.write(struct.pack("<II", len(bin_chunks), 0x004E4942))
        f.write(bin_chunks)

    return {
        "output": out_glb_path,
        "size_mb": round(os.path.getsize(out_glb_path) / (1024 * 1024), 2),
        "pieces": len(primitives),
        "vertices": total_verts,
        "triangles": total_tris,
        "textures": len(images),
        "bounds_min": [round(float(x), 3) for x in all_min],
        "bounds_max": [round(float(x), 3) for x in all_max],
        "wheel_locators": wheel_locs,
    }


def main():
    ap = argparse.ArgumentParser(description="Convert unlocked ETS2 .scs vehicle mod (.pmg/.pmd/.dds) to glTF 2.0 (.glb)")
    ap.add_argument("source", help="Path to unlocked .scs ZIP file OR unpacked ETS2 mod folder")
    ap.add_argument("--list", action="store_true", help="List all .pmg models inside the mod")
    ap.add_argument("--pmg", default="", help="Relative path to .pmg inside mod (auto-selects largest vehicle .pmg if omitted)")
    ap.add_argument("--out", default="", help="Output .glb file path")
    ap.add_argument("--look", type=int, default=0, help="PMD look index (default 0)")
    ap.add_argument("--max-tex", type=int, default=1024, help="Max texture dimension in pixels (default 1024)")
    ap.add_argument("--flip-v", action="store_true", help="Flip V texture coordinates (1 - v)")
    args = ap.parse_args()

    mod_fs = ModFS(args.source)
    try:
        pmgs = mod_fs.list_pmgs()
        if args.list:
            print(json.dumps([{"pmg": p, "bytes": s} for p, s in pmgs], indent=2))
            return

        target_pmg = args.pmg
        if not target_pmg:
            # Prefer exterior vehicle .pmg (excluding interior/mirror/flare/shadow/wheel)
            candidates = [
                (p, s)
                for p, s in pmgs
                if "vehicle/" in p.lower()
                and not any(x in p.lower() for x in ("interior", "shadow", "flare", "wiper", "anim", "mirror", "steering"))
            ]
            if not candidates:
                candidates = pmgs
            if not candidates:
                raise RuntimeError("No .pmg 3D models found in ETS2 mod!")
            target_pmg = candidates[0][0]

        out_path = args.out
        if not out_path:
            base_name = os.path.splitext(os.path.basename(target_pmg))[0]
            out_path = os.path.abspath(f"{base_name}_ETS2.glb")

        report = build_glb(
            mod_fs,
            target_pmg,
            out_path,
            look_index=args.look,
            max_tex=args.max_tex,
            flip_v=args.flip_v,
        )
        print(json.dumps(report, indent=2))
    finally:
        mod_fs.close()


if __name__ == "__main__":
    main()
