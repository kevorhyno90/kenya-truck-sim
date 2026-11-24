"""
Kenya Street Sim — Blender 5.3 Photoreal Kenyan World Architecture & Roadside Kit (Path A: 100% Owned)
Exports 8 engine-agnostic .glb assets (compatible with Unity 6, Godot 4, and Unreal Engine 5)
into KenyaStreetSim/Assets/KenyaStreetSim/ImportedModels/KenyaWorldKit/:
  1. Kenya_Shop_Duka_2Story.glb      — Authentic Kenyan 2-story town shop (duka veranda, shutters, balconies, pitched mabati roof, KenTank)
  2. Kenya_Plaza_3Story.glb          — Authentic Kenyan 3-story county plaza (terracotta plaster, arched colonnade, tinted windows, rooftop tanks)
  3. Kenya_PetrolStation_Modern.glb  — Highway service station (chamfered canopy, 4 curved fuel pumps, bollards, Express Mart shop, price totem)
  4. Kenya_Market_Cluster.glb        — Open-air Kenyan roadside market (eucalyptus timber poles, sloped canvas tarps, 3D bananas/tomatoes/sacks)
  5. Kenya_BusStage_Terminal.glb     — Kenyan coach terminal & parcel booking shelter (arched steel trusses, glazed windbreak, timber benches)
  6. Kenya_Roundabout_Monument.glb   — Sculpted Kenyan county roundabout monument (stone planter, masonry pedestal, Kenyan shield & spears, flagpoles)
  7. Kenya_Highland_Pine.glb         — Realistic jagged branching highland cypress/pine tree (Mau/Timboroa/Kericho — replaces smooth cones!)
  8. Kenya_Highway_Guardrail.glb     — 12m galvanized W-beam steel crash barrier with I-beam posts & retro-reflective chevrons
"""

import math
import os
import random
import bpy
import bmesh
from mathutils import Vector, Euler, Matrix

_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
_candidate_root = os.path.abspath(os.path.join(_script_dir, "..")) if _script_dir else ""
REPO_ROOT = _candidate_root if os.path.isdir(os.path.join(_candidate_root, "KenyaStreetSim")) else r"C:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
OUT_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "ImportedModels", "KenyaWorldKit")
CC0_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "Textures", "CC0_PBR")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
PREVIEW_PNG = r"C:\Users\USER\.gemini\antigravity\brain\1f115163-e25f-4fe7-8681-12dc36b17000\scratch\blender_world_kit_preview.png"

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(STUDIO_DIR, exist_ok=True)
os.makedirs(os.path.dirname(PREVIEW_PNG), exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def link_obj(obj, parent, col):
    col.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


_IMG_CACHE = {}

def load_img(filename, max_dim=512):
    if filename in _IMG_CACHE:
        return _IMG_CACHE[filename]
    path = os.path.join(CC0_DIR, filename)
    if not os.path.isfile(path):
        return None
    img = bpy.data.images.load(path, check_existing=True)
    if img.size[0] > max_dim or img.size[1] > max_dim:
        img.scale(max_dim, max_dim)
    try:
        img.file_format = 'JPEG'
        img.pack()
    except Exception:
        pass
    _IMG_CACHE[filename] = img
    return img


def make_mat(name, base_col=(0.7, 0.7, 0.7), metallic=0.0, roughness=0.55,
             emission=None, emission_strength=0.0, tex_file=None, uv_scale=(1.0, 1.0),
             tint_mix=0.55, coat=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Coat Weight" in bsdf.inputs and coat > 0.0:
        bsdf.inputs["Coat Weight"].default_value = coat

    img = load_img(tex_file) if tex_file else None
    if img is not None:
        tex_coord = nt.nodes.new("ShaderNodeTexCoord")
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (uv_scale[0], uv_scale[1], 1.0)
        tex_node = nt.nodes.new("ShaderNodeTexImage")
        tex_node.image = img

        # Native glTF-compatible BaseColorTexture * BaseColorFactor!
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        # Lift tint slightly so photographed plaster/cabro stays sun-bright in Unity ACES!
        lift = 1.14
        mix.inputs[7].default_value = (min(1.0, base_col[0] * lift), min(1.0, base_col[1] * lift), min(1.0, base_col[2] * lift), 1.0)

        nt.links.new(tex_coord.outputs["UV"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex_node.inputs["Vector"])
        nt.links.new(tex_node.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (base_col[0], base_col[1], base_col[2], 1.0)

    if emission is not None and emission_strength > 0.0:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def init_materials():
    m = {}
    # Kenyan architectural plasters with real CC0 plaster texture!
    m["plaster_cream"]      = make_mat("KW_PlasterCream",      (0.86, 0.80, 0.68), roughness=0.78, tex_file="CC0_Plaster_Albedo.jpg", uv_scale=(2.4, 2.4), tint_mix=0.72)
    m["plaster_ochre"]      = make_mat("KW_PlasterOchre",      (0.82, 0.64, 0.42), roughness=0.76, tex_file="CC0_Plaster_Albedo.jpg", uv_scale=(2.6, 2.6), tint_mix=0.78)
    m["plaster_terracotta"] = make_mat("KW_PlasterTerracotta", (0.72, 0.41, 0.30), roughness=0.78, tex_file="CC0_Plaster_Albedo.jpg", uv_scale=(2.6, 2.6), tint_mix=0.80)
    m["plaster_sage"]       = make_mat("KW_PlasterSage",       (0.68, 0.74, 0.66), roughness=0.76, tex_file="CC0_Plaster_Albedo.jpg", uv_scale=(2.2, 2.2), tint_mix=0.74)
    m["concrete"]           = make_mat("KW_Concrete",          (0.60, 0.59, 0.56), roughness=0.82, tex_file="CC0_Plaster_Albedo.jpg", uv_scale=(2.0, 2.0), tint_mix=0.60)
    m["concrete_dark"]      = make_mat("KW_ConcreteDark",      (0.32, 0.32, 0.33), roughness=0.86, tex_file="CC0_Cabro_Albedo.jpg",   uv_scale=(2.5, 2.5), tint_mix=0.55)
    m["cabro_pad"]          = make_mat("KW_CabroPad",          (0.66, 0.63, 0.58), roughness=0.78, tex_file="CC0_Cabro_Albedo.jpg",   uv_scale=(4.0, 5.0), tint_mix=0.45)
    m["stone_masonry"]      = make_mat("KW_StoneMasonry",      (0.58, 0.54, 0.47), roughness=0.85, tex_file="CC0_Cabro_Albedo.jpg",   uv_scale=(3.0, 2.0), tint_mix=0.55)

    # Corrugated Mabati roofs & awnings
    m["mabati_red"]         = make_mat("KW_MabatiRed",         (0.56, 0.16, 0.12), metallic=0.35, roughness=0.48, tex_file="CC0_Mabati_Albedo.jpg", uv_scale=(4.0, 2.5), tint_mix=0.85)
    m["mabati_blue"]        = make_mat("KW_MabatiBlue",        (0.12, 0.30, 0.56), metallic=0.35, roughness=0.46, tex_file="CC0_Mabati_Albedo.jpg", uv_scale=(4.0, 2.5), tint_mix=0.85)
    m["mabati_galv"]        = make_mat("KW_MabatiGalv",        (0.62, 0.65, 0.68), metallic=0.68, roughness=0.38, tex_file="CC0_Mabati_Albedo.jpg", uv_scale=(4.0, 2.0), tint_mix=0.55)
    m["shutter_steel"]      = make_mat("KW_ShutterSteel",      (0.42, 0.46, 0.50), metallic=0.55, roughness=0.46, tex_file="CC0_Mabati_Albedo.jpg", uv_scale=(2.0, 3.0), tint_mix=0.65)

    # Glass, frames, metals, wood, plastics
    m["glass_tint"]         = make_mat("KW_GlassTint",         (0.10, 0.18, 0.26), metallic=0.22, roughness=0.08, coat=1.0)
    m["glass_shop"]         = make_mat("KW_GlassShopWarm",     (0.18, 0.25, 0.30), metallic=0.18, roughness=0.10, emission=(0.95, 0.80, 0.52), emission_strength=0.22, coat=1.0)
    m["alu_frame"]          = make_mat("KW_AluFrame",          (0.20, 0.22, 0.25), metallic=0.72, roughness=0.35)
    m["galv_steel"]         = make_mat("KW_GalvSteel",         (0.68, 0.71, 0.74), metallic=0.82, roughness=0.32)
    m["dark_steel"]         = make_mat("KW_DarkSteel",         (0.16, 0.18, 0.20), metallic=0.75, roughness=0.42)
    m["gold_brass"]         = make_mat("KW_GoldBrass",         (0.85, 0.64, 0.18), metallic=0.86, roughness=0.26)
    m["tank_black"]         = make_mat("KW_KenTankBlack",      (0.08, 0.09, 0.10), metallic=0.05, roughness=0.38)
    m["tank_blue"]          = make_mat("KW_KenTankBlue",       (0.08, 0.32, 0.64), metallic=0.05, roughness=0.36)
    m["white_clean"]        = make_mat("KW_WhiteClean",        (0.92, 0.93, 0.94), metallic=0.08, roughness=0.34, coat=0.3)
    m["safety_yellow"]      = make_mat("KW_SafetyYellow",      (0.94, 0.74, 0.08), metallic=0.12, roughness=0.36)
    m["brand_red"]          = make_mat("KW_BrandRed",          (0.80, 0.12, 0.12), metallic=0.16, roughness=0.32, coat=0.4)
    m["brand_green"]        = make_mat("KW_BrandGreen",        (0.07, 0.48, 0.24), metallic=0.14, roughness=0.34, coat=0.3)
    m["brand_blue"]         = make_mat("KW_BrandBlue",         (0.08, 0.28, 0.64), metallic=0.15, roughness=0.35, coat=0.3)
    m["led_warm"]           = make_mat("KW_LedWarm",           (1.00, 0.95, 0.84), roughness=0.20, emission=(1.00, 0.92, 0.72), emission_strength=2.6)
    m["led_amber"]          = make_mat("KW_LedAmber",          (0.98, 0.72, 0.14), roughness=0.20, emission=(1.00, 0.74, 0.18), emission_strength=2.2)
    m["sign_board_dark"]    = make_mat("KW_SignBoardDark",     (0.08, 0.16, 0.34), metallic=0.15, roughness=0.40)

    # Wood, bark, market tarps, produce & foliage
    m["timber_wood"]        = make_mat("KW_TimberWood",        (0.46, 0.31, 0.18), roughness=0.82, tex_file="CC0_Bark_Albedo.jpg", uv_scale=(1.5, 3.0), tint_mix=0.72)
    m["bark_pine"]          = make_mat("KW_BarkPine",          (0.38, 0.26, 0.18), roughness=0.88, tex_file="CC0_Bark_Albedo.jpg", uv_scale=(1.6, 4.0), tint_mix=0.68)
    m["pine_dark"]          = make_mat("KW_PineNeedleDark",    (0.09, 0.24, 0.11), roughness=0.78)
    m["pine_mid"]           = make_mat("KW_PineNeedleMid",     (0.14, 0.32, 0.14), roughness=0.76)
    m["pine_tip"]           = make_mat("KW_PineNeedleTip",     (0.20, 0.39, 0.17), roughness=0.74)
    m["hedge_green"]        = make_mat("KW_HedgeGreen",        (0.14, 0.38, 0.15), roughness=0.78)
    m["flower_red"]         = make_mat("KW_FlowerRed",         (0.86, 0.18, 0.20), roughness=0.68)
    m["flower_gold"]        = make_mat("KW_FlowerGold",        (0.95, 0.76, 0.12), roughness=0.68)

    m["tarp_blue"]          = make_mat("KW_TarpBlue",          (0.10, 0.38, 0.76), roughness=0.68)
    m["tarp_red"]           = make_mat("KW_TarpRed",           (0.78, 0.18, 0.14), roughness=0.68)
    m["tarp_amber"]         = make_mat("KW_TarpAmber",         (0.90, 0.62, 0.12), roughness=0.68)
    m["tarp_green"]         = make_mat("KW_TarpGreen",         (0.12, 0.54, 0.28), roughness=0.68)
    m["banana_green"]       = make_mat("KW_BananaGreen",       (0.44, 0.68, 0.18), roughness=0.52)
    m["tomato_red"]         = make_mat("KW_TomatoRed",         (0.84, 0.16, 0.10), roughness=0.40)
    m["sack_sisal"]         = make_mat("KW_SackSisal",         (0.70, 0.58, 0.40), roughness=0.90)
    m["refl_red"]           = make_mat("KW_ReflRed",           (0.90, 0.12, 0.10), roughness=0.25, emission=(0.85, 0.10, 0.08), emission_strength=0.6)
    m["refl_white"]         = make_mat("KW_ReflWhite",         (0.95, 0.96, 0.98), roughness=0.22, emission=(0.90, 0.92, 0.96), emission_strength=0.4)
    return m


def apply_box_uv(bm, scale=0.35):
    uv_layer = bm.loops.layers.uv.verify()
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in face.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                u, v = co.x * scale, co.y * scale
            elif ax >= ay:
                u, v = co.y * scale, co.z * scale
            else:
                u, v = co.x * scale, co.z * scale
            loop[uv_layer].uv = (u, v)


def add_box(name, loc, size, mat, parent, col, rot_deg=(0, 0, 0), bevel_w=0.025, uv_scale=0.32):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel_w > 0.0:
        max_b = min(size[0], size[1], size[2]) * 0.28
        bw = min(bevel_w, max_b)
        if bw > 0.002:
            bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bw, segments=1, affect='EDGES', clamp_overlap=True)
    apply_box_uv(bm, uv_scale)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    obj.data.materials.append(mat)
    return link_obj(obj, parent, col)


def add_cyl(name, loc, radius_bot, radius_top, depth, segs, mat, parent, col, rot_deg=(0, 0, 0), uv_scale=0.45):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius_bot, radius2=radius_top, depth=depth)
    apply_box_uv(bm, uv_scale)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return link_obj(obj, parent, col)


def add_hip_roof(name, loc, width_x, depth_y, height_z, ridge_shrink, mat, parent, col, uv_scale=0.35):
    """Creates a realistic Kenyan pitched hip mabati roof with overhang eaves."""
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    hx, hy = width_x * 0.5, depth_y * 0.5
    rx = max(0.2, hx - ridge_shrink)
    ry = max(0.15, hy - ridge_shrink * 0.85)
    # Bottom eaves vertices
    v0 = bm.verts.new((-hx, -hy, 0.0))
    v1 = bm.verts.new(( hx, -hy, 0.0))
    v2 = bm.verts.new(( hx,  hy, 0.0))
    v3 = bm.verts.new((-hx,  hy, 0.0))
    # Top ridge vertices
    t0 = bm.verts.new((-rx, -ry * 0.15, height_z))
    t1 = bm.verts.new(( rx, -ry * 0.15, height_z))
    t2 = bm.verts.new(( rx,  ry * 0.15, height_z))
    t3 = bm.verts.new((-rx,  ry * 0.15, height_z))
    bm.faces.new((v0, v1, t1, t0))
    bm.faces.new((v1, v2, t2, t1))
    bm.faces.new((v2, v3, t3, t2))
    bm.faces.new((v3, v0, t0, t3))
    bm.faces.new((t0, t1, t2, t3))
    bm.faces.new((v3, v2, v1, v0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    apply_box_uv(bm, uv_scale)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.data.materials.append(mat)
    return link_obj(obj, parent, col)


def add_kentank(name, loc, mat_tank, mat_stand, parent, col):
    """Kenyan rooftop cylindrical ribbed plastic water tank (KenTank / Roto) on stand."""
    add_box(f"{name}_Stand", (loc[0], loc[1], loc[2] + 0.18), (1.45, 1.45, 0.36), mat_stand, parent, col, bevel_w=0.02)
    add_cyl(f"{name}_Body",  (loc[0], loc[1], loc[2] + 1.15), 0.72, 0.72, 1.62, 16, mat_tank, parent, col)
    for rz in (0.68, 1.15, 1.62):
        add_cyl(f"{name}_Rib_{rz:.2f}", (loc[0], loc[1], loc[2] + rz), 0.76, 0.76, 0.10, 16, mat_tank, parent, col)
    add_cyl(f"{name}_Lid",   (loc[0], loc[1], loc[2] + 2.02), 0.24, 0.22, 0.14, 12, mat_tank, parent, col)


# ==============================================================================
# 1. AUTHENTIC KENYAN 2-STORY DUKA & APARTMENT BLOCK (14m wide x 10.6m deep, front = -Y -> Unity +Z!)
# Note: glTF export_yup maps Blender (X, Y, Z) -> Unity (-X, Z, Y); we align front face toward +Y
# so in Unity local +Z is the shopfront!
# ==============================================================================
def build_kenya_shop_duka_2story(mats, col):
    root = bpy.data.objects.new("Kenya_Shop_Duka_2Story", None)
    link_obj(root, None, col)

    W, D = 13.8, 10.4
    front_y = D * 0.5  # +5.2 (becomes +Z in Unity!)

    # Concrete plinth & veranda walkway
    add_box("Plinth", root_loc := (0, 0.35, 0.22), (W + 0.4, D + 1.1, 0.44), mats["concrete"], root, col, bevel_w=0.03)
    # Recessed Ground-Floor Shop Core (creates genuine covered Kenyan veranda colonnade!)
    add_box("GroundCore", (0, -0.55, 2.15), (W - 0.1, D - 1.3, 3.55), mats["plaster_cream"], root, col, bevel_w=0.04)

    # 5 Front Veranda Columns + Horizontal Fascia Beam
    for cx in (-6.45, -3.25, 0.0, 3.25, 6.45):
        add_box(f"VerandaCol_{cx:.1f}", (cx, front_y + 0.25, 2.05), (0.42, 0.42, 3.55), mats["concrete"], root, col, bevel_w=0.03)
        add_box(f"ColBase_{cx:.1f}",    (cx, front_y + 0.25, 0.58), (0.54, 0.54, 0.30), mats["concrete_dark"], root, col, bevel_w=0.02)
    add_box("SignFasciaBeam", (0, front_y + 0.28, 3.92), (W + 0.3, 0.58, 0.78), mats["sign_board_dark"], root, col, bevel_w=0.04)
    add_box("AwningSloped",   (0, front_y + 1.05, 3.38), (W + 0.1, 1.55, 0.10), mats["mabati_blue"], root, col, rot_deg=(-14, 0, 0), bevel_w=0.015)

    # Ground-floor Kenyan shopfront bays: glass display + rolled steel shutters + timber doors
    for bx in (-4.8, -1.6, 1.6, 4.8):
        add_box(f"ShopFrame_{bx:.1f}",   (bx, front_y - 1.16, 1.85), (2.75, 0.18, 2.85), mats["alu_frame"], root, col, bevel_w=0.015)
        if abs(bx) > 3.0:
            add_box(f"ShopGlass_{bx:.1f}",   (bx, front_y - 1.12, 1.85), (2.48, 0.08, 2.58), mats["glass_shop"], root, col, bevel_w=0.005)
            add_box(f"ShutterBox_{bx:.1f}",  (bx, front_y - 1.02, 3.18), (2.78, 0.32, 0.34), mats["shutter_steel"], root, col, bevel_w=0.02)
        else:
            add_box(f"ShutterClosed_{bx:.1f}", (bx, front_y - 1.12, 2.02), (2.48, 0.10, 2.55), mats["shutter_steel"], root, col, bevel_w=0.01)

    # Upper Floor Apartment Block (recessed front balcony + overhang slab!)
    add_box("UpperSlab",   (0, 0.15, 4.30), (W + 0.45, D + 0.85, 0.28), mats["concrete"], root, col, bevel_w=0.03)
    add_box("UpperCore",   (0, -0.35, 5.95), (W, D - 0.8, 3.15), mats["plaster_ochre"], root, col, bevel_w=0.04)
    # Recessed upper balcony railing (horizontal bars + steel balusters)
    add_box("BalconyRailTop", (0, front_y + 0.45, 5.38), (W + 0.15, 0.07, 0.07), mats["dark_steel"], root, col, bevel_w=0.01)
    add_box("BalconyRailMid", (0, front_y + 0.45, 4.92), (W + 0.15, 0.05, 0.05), mats["dark_steel"], root, col, bevel_w=0.008)
    for rx in (-6.6, -4.4, -2.2, 0.0, 2.2, 4.4, 6.6):
        add_box(f"BalcPost_{rx:.1f}", (rx, front_y + 0.45, 4.92), (0.06, 0.06, 0.96), mats["dark_steel"], root, col, bevel_w=0.008)

    # Upper windows with concrete lintels, sills & mullions (front + sides!)
    for wx in (-4.6, -1.55, 1.55, 4.6):
        add_box(f"WinLintel_{wx:.1f}", (wx, front_y - 0.68, 6.72), (1.96, 0.22, 0.16), mats["concrete"], root, col, bevel_w=0.015)
        add_box(f"WinSill_{wx:.1f}",   (wx, front_y - 0.66, 5.18), (1.96, 0.26, 0.14), mats["concrete"], root, col, bevel_w=0.015)
        add_box(f"WinFrame_{wx:.1f}",  (wx, front_y - 0.72, 5.95), (1.76, 0.14, 1.44), mats["alu_frame"], root, col, bevel_w=0.012)
        add_box(f"WinGlass_{wx:.1f}",  (wx, front_y - 0.70, 5.95), (1.56, 0.08, 1.26), mats["glass_tint"], root, col, bevel_w=0.005)
        add_box(f"WinMull_{wx:.1f}",   (wx, front_y - 0.68, 5.95), (0.07, 0.12, 1.32), mats["alu_frame"], root, col, bevel_w=0.005)
    # AC condenser box mounted on upper wall
    add_box("AC_Unit", (-6.0, front_y - 0.45, 5.35), (0.78, 0.34, 0.56), mats["white_clean"], root, col, bevel_w=0.02)

    # Pitched Corrugated Mabati Hip Roof with Generous Overhang + Eaves Fascia + KenTank on rear flat slab!
    add_box("EavesFascia", (0, -0.15, 7.55), (W + 1.15, D + 0.65, 0.22), mats["timber_wood"], root, col, bevel_w=0.02)
    add_hip_roof("MabatiHipRoof", (0, -0.15, 7.64), W + 1.35, D + 0.85, 2.25, 2.8, mats["mabati_red"], root, col)
    add_kentank("RoofKenTank", (4.2, -3.2, 7.65), mats["tank_black"], mats["dark_steel"], root, col)
    return root


# ==============================================================================
# 2. AUTHENTIC KENYAN 3-STORY COUNTY COMMERCIAL PLAZA (15.0m wide x 10.8m deep)
# ==============================================================================
def build_kenya_plaza_3story(mats, col):
    root = bpy.data.objects.new("Kenya_Plaza_3Story", None)
    link_obj(root, None, col)

    W, D = 14.8, 10.6
    front_y = D * 0.5  # +5.3

    # Plinth + Ground-floor deep arcade
    add_box("PlazaPlinth", (0, 0.2, 0.24), (W + 0.4, D + 0.9, 0.48), mats["concrete_dark"], root, col, bevel_w=0.03)
    add_box("PlazaCore",   (0, -0.45, 5.45), (W, D - 0.9, 10.1), mats["plaster_terracotta"], root, col, bevel_w=0.05)

    # Ground-floor colonnade pillars + glowing bank/supermarket windows
    for cx in (-6.9, -3.45, 0.0, 3.45, 6.9):
        add_box(f"ArcadeCol_{cx:.1f}", (cx, front_y + 0.22, 2.15), (0.50, 0.50, 3.65), mats["plaster_cream"], root, col, bevel_w=0.03)
    for bx in (-5.15, -1.72, 1.72, 5.15):
        add_box(f"ArcadeGlass_{bx:.1f}", (bx, front_y - 0.88, 1.95), (2.95, 0.14, 2.95), mats["glass_shop"], root, col, bevel_w=0.01)
        add_box(f"ArcadeMull_{bx:.1f}",  (bx, front_y - 0.82, 1.95), (0.09, 0.16, 2.95), mats["alu_frame"], root, col, bevel_w=0.008)

    # Horizontal floor slabs & upper commercial curtain windows (Floors 2 & 3)
    for f_idx, fy in enumerate((4.05, 7.25)):
        add_box(f"FloorBand_{f_idx}", (0, 0.05, fy), (W + 0.55, D + 0.65, 0.34), mats["plaster_cream"], root, col, bevel_w=0.03)
        add_box(f"PlazaRail_{f_idx}", (0, front_y + 0.30, fy + 0.98), (W + 0.2, 0.07, 0.07), mats["dark_steel"], root, col, bevel_w=0.01)
        for wx in (-5.2, -1.75, 1.75, 5.2):
            add_box(f"PWinFrame_{f_idx}_{wx:.1f}", (wx, front_y - 0.86, fy + 1.65), (2.75, 0.20, 2.05), mats["alu_frame"], root, col, bevel_w=0.015)
            add_box(f"PWinGlass_{f_idx}_{wx:.1f}", (wx, front_y - 0.80, fy + 1.65), (2.52, 0.08, 1.84), mats["glass_tint"], root, col, bevel_w=0.005)
            add_box(f"PWinVert_{f_idx}_{wx:.1f}",  (wx, front_y - 0.78, fy + 1.65), (0.08, 0.14, 1.88), mats["alu_frame"], root, col, bevel_w=0.005)

    # Parapet crown, rooftop stair penthouse, dual blue & black KenTanks, and Safaricom-style telecom mast!
    add_box("ParapetFront", (0, front_y - 0.10, 10.95), (W + 0.45, 0.45, 1.15), mats["plaster_cream"], root, col, bevel_w=0.04)
    add_box("ParapetCap",   (0, front_y - 0.10, 11.58), (W + 0.65, 0.62, 0.16), mats["mabati_blue"], root, col, bevel_w=0.02)
    add_box("RoofPenthouse", (-3.5, -1.8, 11.45), (4.4, 3.8, 2.15), mats["plaster_sage"], root, col, bevel_w=0.04)
    add_hip_roof("PentRoof", (-3.5, -1.8, 12.50), 4.9, 4.3, 0.95, 1.1, mats["mabati_blue"], root, col)
    add_kentank("PlazaTank1", (3.6, -1.2, 10.50), mats["tank_blue"],  mats["dark_steel"], root, col)
    add_kentank("PlazaTank2", (5.5, -1.2, 10.50), mats["tank_black"], mats["dark_steel"], root, col)
    add_cyl("RoofMast", (-5.8, -3.5, 13.0), 0.12, 0.06, 5.0, 10, mats["galv_steel"], root, col)
    return root


# ==============================================================================
# 3. PHOTOREAL KENYAN HIGHWAY PETROL STATION (Matches Unity footprint: forecourt X=-2, Z=0 -> Blender X, Y)
# ==============================================================================
def build_kenya_petrol_station(mats, col):
    root = bpy.data.objects.new("Kenya_PetrolStation_Modern", None)
    link_obj(root, None, col)

    # Raised interlocking cabro forecourt apron with chamfered curbs
    add_box("ForecourtSlab", (-1.8, 0.0, 0.10), (19.6, 28.5, 0.20), mats["cabro_pad"], root, col, bevel_w=0.04)

    # Sculpted modern overhanging canopy (beveled white fascia + bold crimson/amber Kenyan energy stripe!)
    add_box("CanopyDeck",   (-1.5, 0.0, 5.45), (11.6, 18.8, 0.42), mats["white_clean"], root, col, bevel_w=0.06)
    add_box("CanopyFascia", (-1.5, 0.0, 5.82), (11.9, 19.1, 0.56), mats["brand_red"], root, col, bevel_w=0.08)
    add_box("CanopyAccent", (-1.5, 0.0, 5.36), (11.95, 19.15, 0.18), mats["safety_yellow"], root, col, bevel_w=0.03)
    # Recessed LED soffit canopy downlights
    for ly in (-6.0, -2.0, 2.0, 6.0):
        for lx in (-4.0, 1.0):
            add_box(f"SoffitLED_{lx:.0f}_{ly:.0f}", (lx, ly, 5.22), (1.35, 0.95, 0.06), mats["led_warm"], root, col, bevel_w=0.01)

    # 4 Structural Canopy Columns with Safety Yellow Bumper Guards
    for cx in (-4.6, 1.6):
        for cy in (-6.2, 6.2):
            add_cyl(f"ColShaft_{cx:.0f}_{cy:.0f}",  (cx, cy, 2.72), 0.30, 0.28, 5.20, 14, mats["white_clean"], root, col)
            add_cyl(f"ColGuard_{cx:.0f}_{cy:.0f}",  (cx, cy, 0.58), 0.36, 0.36, 0.96, 14, mats["safety_yellow"], root, col)

    # 2 Raised Concrete Pump Islands + 4 Detailed Multi-Nozzle Fuel Dispensers + Steel Crash Bollards!
    for idx, iy in enumerate((-4.2, 4.2)):
        add_box(f"Island_{idx}", (-1.5, iy, 0.24), (1.75, 4.6, 0.26), mats["concrete_dark"], root, col, bevel_w=0.04)
        for end_s in (-1, 1):
            add_cyl(f"Bollard_{idx}_{end_s}", (-1.5, iy + end_s * 2.1, 0.72), 0.09, 0.09, 0.95, 12, mats["safety_yellow"], root, col)
            py = iy + end_s * 1.15
            add_box(f"PumpBase_{idx}_{end_s}",   (-1.5, py, 0.85), (0.86, 0.58, 1.02), mats["white_clean"], root, col, bevel_w=0.04)
            add_box(f"PumpHead_{idx}_{end_s}",   (-1.5, py, 1.62), (0.90, 0.62, 0.56), mats["brand_red"], root, col, bevel_w=0.05)
            add_box(f"PumpScreen_{idx}_{end_s}", (-1.5, py, 1.62), (0.94, 0.42, 0.24), mats["led_amber"], root, col, bevel_w=0.01)
            add_cyl(f"PumpHose_{idx}_{end_s}",   (-1.5, py + end_s * 0.34, 1.18), 0.025, 0.025, 0.95, 8, mats["dark_steel"], root, col)

    # Rear Modern Express Mart / Cafe Building + Overhang + Glass Doors + Rooftop ACs
    add_box("MartBody",    (7.0, 0.0, 2.25), (6.4, 18.8, 4.30), mats["white_clean"], root, col, bevel_w=0.06)
    add_box("MartCrown",   (7.0, 0.0, 4.30), (6.65, 19.0, 0.62), mats["brand_red"], root, col, bevel_w=0.05)
    add_box("MartAwning",  (3.3, 0.0, 3.45), (1.35, 16.5, 0.14), mats["dark_steel"], root, col, bevel_w=0.02)
    add_box("MartGlass",   (3.78, 0.0, 1.82), (0.14, 14.2, 2.75), mats["glass_shop"], root, col, bevel_w=0.01)
    for my in (-5.0, -2.5, 0.0, 2.5, 5.0):
        add_box(f"MartMull_{my:.1f}", (3.72, my, 1.82), (0.14, 0.09, 2.80), mats["alu_frame"], root, col, bevel_w=0.008)
    add_box("MartAC1", (7.4, -4.5, 4.82), (1.1, 1.6, 0.68), mats["galv_steel"], root, col, bevel_w=0.03)
    add_box("MartAC2", (7.4,  4.5, 4.82), (1.1, 1.6, 0.68), mats["galv_steel"], root, col, bevel_w=0.03)

    # Tall Illuminated Highway Price Totem!
    add_box("TotemBase",   (-9.3, -12.2, 3.85), (1.65, 0.50, 7.65), mats["dark_steel"], root, col, bevel_w=0.05)
    add_box("TotemCrown",  (-9.3, -12.2, 6.95), (1.82, 0.58, 1.55), mats["brand_red"], root, col, bevel_w=0.06)
    add_box("TotemPanel1", (-9.3, -12.2, 5.30), (1.42, 0.54, 0.62), mats["led_amber"], root, col, bevel_w=0.02)
    add_box("TotemPanel2", (-9.3, -12.2, 4.45), (1.42, 0.54, 0.62), mats["led_warm"], root, col, bevel_w=0.02)
    return root


# ==============================================================================
# 4. AUTHENTIC KENYAN ROADSIDE OPEN-AIR MARKET CLUSTER (4 eucalyptus timber stalls + produce!)
# ==============================================================================
def build_kenya_market_cluster(mats, col):
    root = bpy.data.objects.new("Kenya_Market_Cluster", None)
    link_obj(root, None, col)

    tarps = [mats["tarp_blue"], mats["tarp_red"], mats["tarp_amber"], mats["tarp_green"]]
    offsets = [(-6.2, 0.5, 4), (-2.1, -0.4, -3), (2.1, 0.4, 5), (6.2, -0.5, -4)]

    for i, (ox, oy, yaw) in enumerate(offsets):
        stall = bpy.data.objects.new(f"MarketStall_{i}", None)
        stall.location = (ox, oy, 0.0)
        stall.rotation_euler = Euler((0, 0, math.radians(yaw)), 'XYZ')
        link_obj(stall, root, col)

        # 4 Rough-cut tapered eucalyptus wooden poles + cross rafters
        for px, py, h in ((-1.45, -1.05, 2.55), (1.45, -1.05, 2.55), (-1.45, 1.05, 2.15), (1.45, 1.05, 2.15)):
            add_cyl(f"Pole_{i}_{px:.1f}_{py:.1f}", (px, py, h * 0.5), 0.065, 0.052, h, 8, mats["timber_wood"], stall, col)
        add_cyl(f"BeamBack_{i}",  (0, -1.05, 2.48), 0.05, 0.05, 3.25, 8, mats["timber_wood"], stall, col, rot_deg=(0, 90, 0))
        add_cyl(f"BeamFront_{i}", (0,  1.05, 2.10), 0.05, 0.05, 3.25, 8, mats["timber_wood"], stall, col, rot_deg=(0, 90, 0))

        # Sloped draped canvas tarp roof + hanging valance flap
        add_box(f"TarpRoof_{i}", (0, 0.0, 2.34), (3.45, 2.68, 0.07), tarps[i % len(tarps)], stall, col, rot_deg=(-10, 0, 0), bevel_w=0.015)
        add_box(f"TarpFlap_{i}", (0, 1.32, 1.98), (3.42, 0.05, 0.30), tarps[i % len(tarps)], stall, col, bevel_w=0.01)

        # Slatted timber produce counter + crates of green bananas, red tomatoes & plump sisal maize sacks!
        add_box(f"TableTop_{i}",  (0, 0.25, 0.84), (2.75, 1.25, 0.10), mats["timber_wood"], stall, col, bevel_w=0.015)
        for lx in (-1.2, 1.2):
            for ly in (-0.25, 0.75):
                add_box(f"TLeg_{i}_{lx:.1f}_{ly:.1f}", (lx, ly, 0.42), (0.09, 0.09, 0.84), mats["timber_wood"], stall, col, bevel_w=0.01)

        # Crates & sculpted produce mounds
        add_box(f"CrateA_{i}", (-0.78, 0.25, 1.02), (0.86, 0.78, 0.26), mats["timber_wood"], stall, col, bevel_w=0.015)
        add_cyl(f"Bananas_{i}", (-0.78, 0.25, 1.20), 0.36, 0.26, 0.22, 10, mats["banana_green"], stall, col)
        add_box(f"CrateB_{i}", (0.14, 0.25, 1.02), (0.82, 0.76, 0.24), mats["tarp_blue"], stall, col, bevel_w=0.015)
        add_cyl(f"Tomatoes_{i}", (0.14, 0.25, 1.18), 0.34, 0.24, 0.20, 10, mats["tomato_red"], stall, col)
        # 2 Standing plump woven sisal maize/potato sacks on the ground
        add_cyl(f"Sack1_{i}", (1.05, 0.48, 0.48), 0.30, 0.22, 0.94, 12, mats["sack_sisal"], stall, col, rot_deg=(4, -6, 0))
        add_cyl(f"Sack2_{i}", (0.95, -0.35, 0.44), 0.28, 0.20, 0.86, 12, mats["sack_sisal"], stall, col, rot_deg=(-5, 4, 0))
    return root


# ==============================================================================
# 5. AUTHENTIC KENYAN COACH BUS STAGE TERMINAL SHELTER & PARCEL KIOSK
# ==============================================================================
def build_kenya_bus_stage_terminal(mats, col):
    root = bpy.data.objects.new("Kenya_BusStage_Terminal", None)
    link_obj(root, None, col)

    # Raised tiled commuter platform
    add_box("StagePlatform", (0.0, 0.0, 0.10), (3.4, 10.6, 0.20), mats["cabro_pad"], root, col, bevel_w=0.03)
    # 3 Arched steel portal frames
    for sy in (-4.4, 0.0, 4.4):
        add_box(f"RearPost_{sy:.1f}",  (-1.35, sy, 1.62), (0.18, 0.18, 3.15), mats["brand_blue"], root, col, bevel_w=0.02)
        add_box(f"FrontPost_{sy:.1f}", ( 1.25, sy, 1.72), (0.16, 0.16, 3.35), mats["brand_blue"], root, col, bevel_w=0.02)
        add_box(f"TrussArm_{sy:.1f}",  (-0.05, sy, 3.24), (3.10, 0.16, 0.18), mats["brand_blue"], root, col, rot_deg=(0, -6, 0), bevel_w=0.02)
    # Curved / pitched corrugated mabati canopy + branded red front fascia + LED strip
    add_box("ShelterCanopy", (-0.05, 0.0, 3.40), (3.55, 10.4, 0.14), mats["mabati_galv"], root, col, rot_deg=(0, -6, 0), bevel_w=0.02)
    add_box("ShelterFascia", ( 1.62, 0.0, 3.28), (0.16, 10.45, 0.56), mats["brand_red"], root, col, bevel_w=0.02)
    add_box("ShelterLED",    ( 0.40, 0.0, 3.18), (0.35, 8.8, 0.06), mats["led_warm"], root, col, bevel_w=0.01)

    # Glazed windbreak rear wall + enclosed Kenyan SACCO parcel/ticket booking kiosk on one end!
    add_box("WindbreakFrame", (-1.35, 1.2, 1.55), (0.12, 6.2, 2.55), mats["alu_frame"], root, col, bevel_w=0.015)
    add_box("WindbreakGlass", (-1.35, 1.2, 1.55), (0.06, 5.8, 2.25), mats["glass_tint"], root, col, bevel_w=0.005)
    # Ticket & parcel office booth
    add_box("BookingKiosk",   (-0.35, -3.5, 1.48), (2.25, 2.55, 2.75), mats["plaster_cream"], root, col, bevel_w=0.03)
    add_box("KioskCounter",   ( 0.85, -3.5, 1.05), (0.45, 1.65, 0.12), mats["timber_wood"], root, col, bevel_w=0.015)
    add_box("KioskWindow",    ( 0.78, -3.5, 1.68), (0.10, 1.55, 1.05), mats["glass_shop"], root, col, bevel_w=0.01)
    # Slatted wooden commuter waiting benches
    for by in (0.4, 3.2):
        add_box(f"BenchSeat_{by:.1f}", (-0.70, by, 0.58), (0.58, 2.15, 0.09), mats["timber_wood"], root, col, bevel_w=0.015)
        add_box(f"BenchBack_{by:.1f}", (-0.98, by, 0.94), (0.09, 2.15, 0.34), mats["timber_wood"], root, col, bevel_w=0.015)
        for leg_y in (by - 0.85, by + 0.85):
            add_box(f"BLeg_{by:.1f}_{leg_y:.1f}", (-0.70, leg_y, 0.32), (0.52, 0.08, 0.48), mats["dark_steel"], root, col, bevel_w=0.01)
    return root


# ==============================================================================
# 6. ICONIC KENYAN COUNTY ROUNDABOUT MONUMENT (Stone planter + Shield & Spears + Flagpoles!)
# ==============================================================================
def build_kenya_roundabout_monument(mats, col):
    root = bpy.data.objects.new("Kenya_Roundabout_Monument", None)
    link_obj(root, None, col)

    # Stepped octagonal stone masonry planter & manicured hedge ring
    add_cyl("OuterCurb",   (0, 0, 0.38), 5.6, 5.4, 0.76, 8, mats["stone_masonry"], root, col)
    add_cyl("HedgeRing",   (0, 0, 0.86), 4.7, 4.4, 0.58, 12, mats["hedge_green"], root, col)
    add_cyl("InnerStep",   (0, 0, 1.25), 3.1, 2.8, 0.85, 8, mats["concrete"], root, col)

    # Radial bougainvillea / marigold flower clusters around the stone planter
    for i in range(8):
        ang = i * (math.pi * 2.0 / 8.0)
        fx, fy = math.cos(ang) * 4.25, math.sin(ang) * 4.25
        add_cyl(f"Flower_{i}", (fx, fy, 1.18), 0.58, 0.42, 0.36, 8, mats["flower_red"] if i % 2 == 0 else mats["flower_gold"], root, col)

    # Rising sculpted concrete monument obelisk with Kenyan black/red/green relief bands
    add_cyl("PlinthCore",   (0, 0, 2.55), 1.75, 1.38, 1.95, 8, mats["stone_masonry"], root, col)
    add_cyl("BandBlack",    (0, 0, 4.05), 1.28, 1.12, 1.10, 8, mats["dark_steel"], root, col)
    add_cyl("BandRed",      (0, 0, 5.15), 1.08, 0.94, 1.10, 8, mats["brand_red"], root, col)
    add_cyl("BandGreen",    (0, 0, 6.25), 0.90, 0.76, 1.10, 8, mats["brand_green"], root, col)

    # Ornate Golden Kenyan Maasai Shield & Crossed Spears Crown!
    add_cyl("ShieldOval",   (0, 0, 7.65), 0.76, 0.62, 1.95, 12, mats["brand_red"], root, col)
    add_box("ShieldRim",    (0, 0, 7.65), (1.35, 0.34, 2.10), mats["gold_brass"], root, col, bevel_w=0.06)
    add_cyl("SpearL",       (0, 0, 7.85), 0.045, 0.035, 3.65, 8, mats["gold_brass"], root, col, rot_deg=(0, 32, 0))
    add_cyl("SpearR",       (0, 0, 7.85), 0.045, 0.035, 3.65, 8, mats["gold_brass"], root, col, rot_deg=(0, -32, 0))
    add_cyl("CrownFinial",  (0, 0, 9.15), 0.36, 0.06, 0.95, 10, mats["gold_brass"], root, col)

    # 4 Subtle architectural uplights & 2 flanking flagpoles
    for side in (-1, 1):
        add_cyl(f"FlagPole_{side}", (side * 3.4, 0, 4.1), 0.06, 0.04, 7.4, 8, mats["galv_steel"], root, col)
        add_box(f"FlagCloth_{side}", (side * 3.4 + 0.65, 0, 7.25), (1.28, 0.05, 0.76), mats["brand_green"] if side > 0 else mats["brand_red"], root, col, bevel_w=0.01)
    return root


# ==============================================================================
# 7. REALISTIC MULTI-TIERED KENYAN HIGHLAND PINE / CYPRESS (Replaces smooth primitive cones!)
# ==============================================================================
def build_kenya_highland_pine(mats, col):
    root = bpy.data.objects.new("Kenya_Highland_Pine", None)
    link_obj(root, None, col)

    rng = random.Random(254909)
    # Tapered slightly curved furrowed-bark trunk + buttress roots
    add_cyl("TrunkLower", (0.0, 0.0, 3.2), 0.42, 0.25, 6.4, 10, mats["bark_pine"], root, col)
    add_cyl("TrunkUpper", (0.06, -0.04, 8.2), 0.25, 0.07, 4.8, 8, mats["bark_pine"], root, col, rot_deg=(1.5, 2.0, 0))
    for r_i in range(4):
        ra = r_i * (math.pi * 0.5) + 0.35
        add_cyl(f"Root_{r_i}", (math.cos(ra) * 0.45, math.sin(ra) * 0.45, 0.35), 0.18, 0.08, 1.1, 6, mats["bark_pine"], root, col, rot_deg=(math.sin(ra) * 48, -math.cos(ra) * 48, 0))

    # 6 Staggered Irregular Whorls of Drooping Sculpted Branch Boughs & Jagged Foliage Clusters!
    foliage_mats = [mats["pine_dark"], mats["pine_mid"], mats["pine_tip"]]
    tiers = [
        (3.4, 2.75, 7, 1.55, 0.85),
        (5.0, 2.35, 7, 1.38, 0.80),
        (6.5, 1.95, 6, 1.22, 0.75),
        (7.9, 1.52, 6, 1.05, 0.68),
        (9.2, 1.08, 5, 0.86, 0.62),
        (10.3, 0.65, 4, 0.66, 0.56),
    ]
    mesh = bpy.data.meshes.new("PineFoliage_Mesh")
    bm = bmesh.new()
    for t_idx, (tz, rad, count, b_len, b_thick) in enumerate(tiers):
        for bi in range(count):
            ang = (bi / count) * math.pi * 2.0 + t_idx * 0.62 + rng.uniform(-0.22, 0.22)
            dist = rad * rng.uniform(0.62, 0.96)
            bx = math.cos(ang) * dist
            by = math.sin(ang) * dist
            bz = tz + rng.uniform(-0.30, 0.24)
            # Create jagged drooping flattened organic icosphere cluster for each branch fan!
            mat_xform = (
                Matrix.Translation((bx, by, bz))
                @ Euler((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), ang), 'XYZ').to_matrix().to_4x4()
                @ Matrix.Diagonal((b_len * rng.uniform(0.85, 1.15), b_len * 0.68, b_thick * rng.uniform(0.78, 1.12), 1.0))
            )
            ret = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.82, matrix=mat_xform)
            for v in ret["verts"]:
                v.co.x += rng.uniform(-0.14, 0.14)
                v.co.y += rng.uniform(-0.14, 0.14)
                v.co.z += rng.uniform(-0.12, 0.12)
    # Crown spire tuft
    ret_top = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.85, matrix=Matrix.Translation((0, 0, 11.2)) @ Matrix.Diagonal((0.75, 0.75, 1.35, 1.0)))
    for v in ret_top["verts"]:
        v.co.x += rng.uniform(-0.08, 0.08)
        v.co.y += rng.uniform(-0.08, 0.08)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    apply_box_uv(bm, 0.6)
    bm.to_mesh(mesh)
    bm.free()
    fol_obj = bpy.data.objects.new("PineCanopyBoughs", mesh)
    fol_obj.data.materials.append(foliage_mats[1])
    link_obj(fol_obj, root, col)
    return root


# ==============================================================================
# 8. AUTHENTIC 12m GALVANIZED W-BEAM STEEL HIGHWAY GUARDRAIL WITH REFLECTORS!
# ==============================================================================
def build_kenya_highway_guardrail(mats, col):
    root = bpy.data.objects.new("Kenya_Highway_Guardrail", None)
    link_obj(root, None, col)

    length = 12.0  # Spans Y = -6.0 -> +6.0 (becomes Z = -6..+6 in Unity!)
    # Sculpted corrugated W-Beam double-rib profile
    add_box("WBeamTopRib",    ( 0.04, 0.0, 0.78), (0.08, length, 0.12), mats["galv_steel"], root, col, bevel_w=0.015)
    add_box("WBeamCenterWeb", (-0.02, 0.0, 0.68), (0.05, length, 0.14), mats["galv_steel"], root, col, bevel_w=0.010)
    add_box("WBeamBotRib",    ( 0.04, 0.0, 0.58), (0.08, length, 0.12), mats["galv_steel"], root, col, bevel_w=0.015)

    # 7 Heavy Steel I-Beam Posts + Spacer Blocks + Retro-Reflective Red/White Delineators!
    for i in range(7):
        py = -5.8 + i * (11.6 / 6.0)
        add_box(f"Post_{i}",       (-0.10, py, 0.45), (0.11, 0.09, 0.90), mats["dark_steel"], root, col, bevel_w=0.01)
        add_box(f"Spacer_{i}",     (-0.04, py, 0.68), (0.10, 0.12, 0.24), mats["galv_steel"], root, col, bevel_w=0.01)
        if i % 2 == 1:
            add_box(f"ReflPost_{i}", (-0.10, py, 0.98), (0.06, 0.08, 0.22), mats["white_clean"], root, col, bevel_w=0.008)
            add_box(f"ReflRed_{i}",  (-0.06, py, 1.02), (0.02, 0.07, 0.10), mats["refl_red"], root, col, bevel_w=0.004)
    return root


def select_hierarchy(root_obj):
    bpy.ops.object.select_all(action='DESELECT')
    stack = [root_obj]
    while stack:
        o = stack.pop()
        o.select_set(True)
        for ch in o.children:
            stack.append(ch)
    bpy.context.view_layer.objects.active = root_obj


def export_glb(root_obj, filename):
    old_loc = root_obj.location.copy()
    root_obj.location = (0.0, 0.0, 0.0)
    select_hierarchy(root_obj)
    out_path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials='EXPORT',
        export_image_format='JPEG',
        export_image_quality=82,
    )
    root_obj.location = old_loc
    kb = os.path.getsize(out_path) / 1024.0
    print(f"[EXPORTED] {filename} ({kb:.1f} KB) -> {out_path}")


def render_studio_preview(col):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PNG

    world = bpy.data.worlds.new("KenyaStudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.58, 0.72, 0.88, 1.0)
    bg.inputs["Strength"].default_value = 1.1

    sun_data = bpy.data.lights.new("SunKey", type='SUN')
    sun_data.energy = 3.2
    sun_obj = bpy.data.objects.new("SunKey", sun_data)
    sun_obj.rotation_euler = Euler((math.radians(46), math.radians(15), math.radians(-35)), 'XYZ')
    col.objects.link(sun_obj)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 28
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    cam_obj.location = (-2.0, 56.0, 18.0)
    direction = Vector((-3.0, 4.0, 3.8)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    col.objects.link(cam_obj)
    scene.camera = cam_obj

    bpy.ops.render.render(write_still=True)
    print(f"[PREVIEW] Rendered Blender 5.3 World Kit showcase -> {PREVIEW_PNG}")


def main():
    clear_scene()
    mats = init_materials()
    col = bpy.context.scene.collection

    builders = [
        ("Kenya_Shop_Duka_2Story.glb",      build_kenya_shop_duka_2story,   (-26.0,  0.0, 0.0)),
        ("Kenya_Plaza_3Story.glb",          build_kenya_plaza_3story,       ( -9.0,  0.0, 0.0)),
        ("Kenya_PetrolStation_Modern.glb",  build_kenya_petrol_station,     ( 16.0, -4.0, 0.0)),
        ("Kenya_Market_Cluster.glb",        build_kenya_market_cluster,     (-18.0, 16.0, 0.0)),
        ("Kenya_BusStage_Terminal.glb",     build_kenya_bus_stage_terminal, (  0.0, 16.0, 0.0)),
        ("Kenya_Roundabout_Monument.glb",   build_kenya_roundabout_monument,( 13.0, 16.0, 0.0)),
        ("Kenya_Highland_Pine.glb",         build_kenya_highland_pine,      (-30.0, 15.0, 0.0)),
        ("Kenya_Highway_Guardrail.glb",     build_kenya_highway_guardrail,  ( -6.0, 24.0, 0.0)),
    ]

    for fname, fn, show_loc in builders:
        obj = fn(mats, col)
        export_glb(obj, fname)
        obj.location = show_loc

    # Ground plane for showroom preview
    add_box("ShowroomGround", (0, 8, -0.15), (95, 70, 0.3), mats["cabro_pad"], None, col, bevel_w=0.0)
    blend_path = os.path.join(STUDIO_DIR, "Kenya_WorldKit_Showroom.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[SAVED] Showroom .blend -> {blend_path}")
    render_studio_preview(col)


if __name__ == "__main__":
    main()
