"""
Kenya Street Sim — Blender 5.3 Photoreal Roadside Realism, Wildlife & ETS2 Cockpit Pack (Path A: 100% Owned)
Exports 8 engine-agnostic .glb assets into KenyaStreetSim/Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/:
  1. Kenya_Cockpit_Accessories_Pack.glb — ETS2 Kenyan bus cockpit details (hanging Kenyan flag pennant + wooden prayer beads, thermos flask, dash coin tray, gear boot, handbrake valve, sun visor trim)
  2. Kenya_BodaBoda_Rider.glb           — Iconic Kenyan Boxer/TVS boda-boda motorbike + rider in neon lime reflective safety vest & helmet
  3. Kenya_Stone_Bridge_Culvert.glb     — Authentic KeNHA Kenyan river bridge/culvert parapet walls with black/white hazard chevrons & steel rails
  4. Kenya_Rift_Boulder_Cliff.glb       — Stratified Rift Valley volcanic cliff & boulder outcrop (CC0 stone textured)
  5. Kenya_Grass_Tuft_Cluster.glb       — Sculpted 3D wild savanna/highland roadside grass & bush cluster (batched into vegetation chunks)
  6. Kenya_Zebra_Sculpted.glb           — Sculpted Blender Plains Zebra with bold black/white stripe body, arched neck, mane & hooves
  7. Kenya_Giraffe_Sculpted.glb         — Sculpted Blender Masai Giraffe with towering neck, chestnut patches & ossicones
  8. Kenya_Elephant_Sculpted.glb        — Sculpted Blender Tsavo Red-Dusted African Elephant with flared ears, curved trunk & ivory tusks
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
OUT_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "ImportedModels", "KenyaRealismPack")
CC0_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "Textures", "CC0_PBR")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
PREVIEW_PNG = r"C:\Users\USER\.gemini\antigravity\brain\1f115163-e25f-4fe7-8681-12dc36b17000\scratch\blender_realism_pack_preview.png"

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(STUDIO_DIR, exist_ok=True)
os.makedirs(os.path.dirname(PREVIEW_PNG), exist_ok=True)

_IMG_CACHE = {}


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _IMG_CACHE.clear()


def link_obj(obj, parent, col):
    col.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


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
             emission=None, emission_strength=0.0, tex_file=None, uv_scale=(1.0, 1.0), coat=0.0):
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
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (uv_scale[0], uv_scale[1], 1.0)
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = img
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = (min(1.0, base_col[0] * 1.15), min(1.0, base_col[1] * 1.15), min(1.0, base_col[2] * 1.15), 1.0)
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
        nt.links.new(mp.outputs["Vector"], tn.inputs["Vector"])
        nt.links.new(tn.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (base_col[0], base_col[1], base_col[2], 1.0)

    if emission is not None and emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def init_materials():
    m = {}
    # Kenyan flag & cockpit accessories
    m["kenya_black"]  = make_mat("KR_KenyaBlack",  (0.07, 0.07, 0.08), roughness=0.45)
    m["kenya_red"]    = make_mat("KR_KenyaRed",    (0.76, 0.09, 0.11), roughness=0.40)
    m["kenya_green"]  = make_mat("KR_KenyaGreen",  (0.06, 0.44, 0.19), roughness=0.42)
    m["kenya_white"]  = make_mat("KR_KenyaWhite",  (0.95, 0.96, 0.97), roughness=0.35)
    m["gold_cord"]    = make_mat("KR_GoldCord",    (0.88, 0.68, 0.18), metallic=0.65, roughness=0.32)
    m["wood_bead"]    = make_mat("KR_WoodBead",    (0.45, 0.24, 0.11), roughness=0.38, coat=0.4)
    m["leather_dark"] = make_mat("KR_LeatherDark", (0.14, 0.13, 0.13), roughness=0.52)
    m["chrome"]       = make_mat("KR_Chrome",      (0.85, 0.87, 0.90), metallic=0.92, roughness=0.18)
    m["thermos_red"]  = make_mat("KR_ThermosRed",  (0.68, 0.12, 0.12), metallic=0.48, roughness=0.28, coat=0.7)
    m["led_amber"]    = make_mat("KR_LedAmber",    (1.00, 0.72, 0.14), roughness=0.22, emission=(1.00, 0.72, 0.14), emission_strength=2.2)

    # Boda-boda motorbike & rider
    m["boda_red"]     = make_mat("KR_BodaRed",     (0.80, 0.08, 0.08), metallic=0.35, roughness=0.24, coat=0.8)
    m["tire_rubber"]  = make_mat("KR_TireRubber",  (0.09, 0.09, 0.10), roughness=0.82)
    m["vest_neon"]    = make_mat("KR_VestNeon",    (0.68, 0.98, 0.10), roughness=0.38, emission=(0.45, 0.72, 0.05), emission_strength=0.45)
    m["skin_brown"]   = make_mat("KR_SkinBrown",   (0.28, 0.16, 0.10), roughness=0.62)
    m["jeans_blue"]   = make_mat("KR_JeansBlue",   (0.14, 0.22, 0.36), roughness=0.85)
    m["jacket_dark"]  = make_mat("KR_JacketDark",  (0.12, 0.13, 0.16), roughness=0.68)
    m["headlamp"]     = make_mat("KR_Headlamp",    (1.00, 0.96, 0.84), roughness=0.15, emission=(1.00, 0.94, 0.78), emission_strength=2.4)

    # Bridge stone, Rift boulders & 3D grass
    m["stone_bridge"] = make_mat("KR_StoneBridge", (0.64, 0.61, 0.56), roughness=0.84, tex_file="CC0_KenyaStoneWall_Albedo.jpg", uv_scale=(2.5, 1.5))
    m["rift_rock"]    = make_mat("KR_RiftRock",    (0.46, 0.40, 0.35), roughness=0.90, tex_file="CC0_KenyaStoneWall_Albedo.jpg", uv_scale=(1.6, 1.6))
    m["galv_rail"]    = make_mat("KR_GalvRail",    (0.72, 0.74, 0.77), metallic=0.82, roughness=0.30)
    m["grass_blade"]  = make_mat("KR_GrassBlade",  (0.26, 0.44, 0.15), roughness=0.78)
    m["grass_dry"]    = make_mat("KR_GrassDry",    (0.48, 0.44, 0.20), roughness=0.82)

    # Sculpted Kenyan Wildlife
    m["zebra_white"]  = make_mat("KR_ZebraWhite",  (0.94, 0.93, 0.89), roughness=0.65)
    m["zebra_black"]  = make_mat("KR_ZebraBlack",  (0.08, 0.08, 0.09), roughness=0.62)
    m["giraffe_gold"] = make_mat("KR_GiraffeGold", (0.86, 0.64, 0.26), roughness=0.68)
    m["giraffe_spot"] = make_mat("KR_GiraffeSpot", (0.36, 0.18, 0.07), roughness=0.72)
    m["elephant_red"] = make_mat("KR_ElephantRed", (0.47, 0.37, 0.32), roughness=0.88)  # Authentic Tsavo red-earth dusted grey!
    m["ivory"]        = make_mat("KR_Ivory",       (0.96, 0.92, 0.80), roughness=0.32, coat=0.3)
    return m


def apply_box_uv(bm, scale=0.45):
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


def add_box(name, loc, size, mat, parent, col, rot_deg=(0, 0, 0), bevel_w=0.015):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel_w > 0.0:
        bw = min(bevel_w, min(size) * 0.25)
        if bw > 0.001:
            bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bw, segments=1, affect='EDGES', clamp_overlap=True)
    apply_box_uv(bm)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    obj.data.materials.append(mat)
    return link_obj(obj, parent, col)


def add_cyl(name, loc, r1, r2, depth, segs, mat, parent, col, rot_deg=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    apply_box_uv(bm)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return link_obj(obj, parent, col)


def add_ellipsoid(name, loc, scale_xyz, mat, parent, col, rot_deg=(0, 0, 0), subdiv=2):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=0.5)
    bmesh.ops.scale(bm, vec=Vector(scale_xyz), verts=bm.verts)
    apply_box_uv(bm)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = loc
    obj.rotation_euler = Euler(tuple(math.radians(a) for a in rot_deg), 'XYZ')
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return link_obj(obj, parent, col)


# ==============================================================================
# 1. ETS2 KENYAN COACH COCKPIT ACCESSORIES PACK (Origin = cockpitRig center!)
# ==============================================================================
def build_cockpit_accessories(mats, col):
    root = bpy.data.objects.new("Kenya_Cockpit_Accessories_Pack", None)
    link_obj(root, None, col)

    # A) Hanging Kenyan National Flag Pennant + Wooden Beads Pivot (sways dynamically in Unity!)
    pendant = bpy.data.objects.new("PhysicsPendantPivot", None)
    pendant.location = (0.04, 0.08, 0.50)  # Top-center windshield mirror mount in cockpitRig!
    link_obj(pendant, root, col)
    add_cyl("MirrorMountStem", (0.0, 0.0, 0.02), 0.012, 0.012, 0.08, 8, mats["kenya_black"], pendant, col)
    add_box("InteriorRearMirror", (0.0, 0.0, -0.02), (0.24, 0.03, 0.075), mats["kenya_black"], pendant, col, bevel_w=0.008)
    # Braided gold hanging cord
    add_cyl("FlagCord", (0.0, 0.0, -0.09), 0.004, 0.004, 0.11, 6, mats["gold_cord"], pendant, col)
    # Kenyan Flag Pennant (Black / White / Red / White / Green horizontal bands + Maasai Shield!)
    add_box("PennantBar",    (0.0, 0.0, -0.145), (0.125, 0.010, 0.010), mats["gold_cord"],   pendant, col, bevel_w=0.002)
    add_box("Band_Black",    (0.0, 0.0, -0.172), (0.110, 0.008, 0.038), mats["kenya_black"], pendant, col, bevel_w=0.002)
    add_box("Band_White1",   (0.0, 0.0, -0.194), (0.110, 0.008, 0.008), mats["kenya_white"], pendant, col, bevel_w=0.001)
    add_box("Band_Red",      (0.0, 0.0, -0.218), (0.110, 0.008, 0.040), mats["kenya_red"],   pendant, col, bevel_w=0.002)
    add_box("Band_White2",   (0.0, 0.0, -0.242), (0.110, 0.008, 0.008), mats["kenya_white"], pendant, col, bevel_w=0.001)
    add_box("Band_Green",    (0.0, 0.0, -0.265), (0.102, 0.008, 0.038), mats["kenya_green"], pendant, col, bevel_w=0.002)
    add_ellipsoid("MiniShield", (0.0, 0.0, -0.218), (0.036, 0.016, 0.068), mats["kenya_red"], pendant, col, subdiv=1)
    add_cyl("GoldTassel",    (0.0, 0.0, -0.300), 0.012, 0.005, 0.036, 8, mats["gold_cord"], pendant, col)
    # Hanging Wooden Beads loop beside pennant
    for bi in range(9):
        ang = (bi / 8.0) * math.pi
        bx = -0.055 + math.cos(ang) * 0.032
        bz = -0.085 - math.sin(ang) * 0.088
        add_ellipsoid(f"Bead_{bi}", (bx, 0.005, bz), (0.014, 0.014, 0.014), mats["wood_bead"], pendant, col, subdiv=1)

    # B) Driver's Red Kenyan Chai Thermos Flask + Stainless Lid + Dash Coin Tray on Left Dash!
    add_box("DashMatPad",     (-0.22, 0.10, -0.34), (0.34, 0.24, 0.014), mats["leather_dark"], root, col, bevel_w=0.004)
    add_cyl("ChaiThermos",    (-0.28, 0.12, -0.23), 0.044, 0.042, 0.20, 14, mats["thermos_red"], root, col)
    add_cyl("ThermosCap",     (-0.28, 0.12, -0.11), 0.043, 0.038, 0.05, 14, mats["chrome"],      root, col)
    add_box("CoinTray",       (-0.14, 0.08, -0.32), (0.14, 0.11, 0.025), mats["kenya_black"],  root, col, bevel_w=0.005)
    for ci in range(4):
        add_cyl(f"ShillingCoin_{ci}", (-0.16 + ci * 0.018, 0.08, -0.305), 0.011, 0.011, 0.005, 10, mats["gold_cord"], root, col)

    # C) Sculpted Leather Handbrake Air-Valve & Gearshift Boot beside Driver!
    add_cyl("GearBoot",       (0.08, -0.18, -0.44), 0.058, 0.025, 0.12, 10, mats["leather_dark"], root, col)
    add_cyl("GearStick",      (0.08, -0.18, -0.32), 0.010, 0.010, 0.18, 8,  mats["chrome"],       root, col, rot_deg=(-8, 6, 0))
    add_ellipsoid("GearKnob", (0.095, -0.16, -0.23), (0.046, 0.046, 0.056), mats["kenya_black"],  root, col, subdiv=1)
    add_box("ParkValveBase",  (0.18, -0.14, -0.38), (0.06, 0.09, 0.05), mats["kenya_black"],      root, col, bevel_w=0.008)
    add_box("ParkValveLever", (0.18, -0.12, -0.33), (0.028, 0.06, 0.024), mats["led_amber"],      root, col, rot_deg=(24, 0, 0), bevel_w=0.005)

    # D) Overhead Driver Sun Visor + Left/Right Chrome Entry Grab-Handles!
    add_box("DriverSunVisor", (0.38, 0.05, 0.56), (0.48, 0.16, 0.022), mats["leather_dark"], root, col, rot_deg=(28, 0, 0), bevel_w=0.006)
    add_cyl("GrabHandleR",    (0.86, 0.02, 0.12), 0.018, 0.018, 0.44, 10, mats["chrome"], root, col, rot_deg=(12, 0, 0))
    add_cyl("GrabHandleL",    (-0.78, 0.02, 0.12), 0.018, 0.018, 0.44, 10, mats["chrome"], root, col, rot_deg=(12, 0, 0))
    return root


# ==============================================================================
# 2. ICONIC KENYAN BODA-BODA MOTORBIKE + RIDER IN REFLECTIVE LIME SAFETY VEST!
# ==============================================================================
def build_boda_boda_rider(mats, col):
    root = bpy.data.objects.new("Kenya_BodaBoda_Rider", None)
    link_obj(root, None, col)

    # Front & Rear Spoked Wheels (+Y is forward -> +Z in Unity!)
    for wy, name in ((0.64, "FrontWheel"), (-0.64, "RearWheel")):
        add_cyl(name + "_Tire", (0.0, wy, 0.33), 0.33, 0.33, 0.10, 16, mats["tire_rubber"], root, col, rot_deg=(0, 90, 0))
        add_cyl(name + "_Rim",  (0.0, wy, 0.33), 0.22, 0.22, 0.11, 14, mats["chrome"],      root, col, rot_deg=(0, 90, 0))

    # Boxer / TVS Red Frame, Engine Block, Chrome Exhaust, Fuel Tank, Seat & Rear Carrier Rack
    add_box("EngineBlock",  (0.0,  0.02, 0.42), (0.28, 0.44, 0.34), mats["chrome"],      root, col, bevel_w=0.03)
    add_cyl("ExhaustPipe",  (0.18, -0.32, 0.36), 0.042, 0.048, 0.85, 10, mats["chrome"], root, col, rot_deg=(84, 0, 0))
    add_ellipsoid("FuelTank", (0.0, 0.24, 0.78), (0.30, 0.56, 0.26), mats["boda_red"],   root, col, subdiv=2)
    add_box("LongSeat",     (0.0, -0.26, 0.77), (0.28, 0.72, 0.14), mats["leather_dark"], root, col, bevel_w=0.04)
    add_box("RearRack",     (0.0, -0.70, 0.78), (0.34, 0.34, 0.05), mats["chrome"],      root, col, bevel_w=0.01)
    # Front Fork, Handlebars, Round Headlamp & Mudguard
    for side in (-1, 1):
        add_cyl(f"Fork_{side}", (side * 0.09, 0.55, 0.62), 0.022, 0.022, 0.66, 8, mats["chrome"], root, col, rot_deg=(18, 0, 0))
    add_cyl("Handlebar",    (0.0, 0.44, 0.94), 0.018, 0.018, 0.66, 8, mats["chrome"], root, col, rot_deg=(0, 90, 0))
    add_cyl("Headlamp",     (0.0, 0.58, 0.88), 0.095, 0.085, 0.12, 12, mats["headlamp"], root, col, rot_deg=(90, 0, 0))
    add_box("FrontFender",  (0.0, 0.64, 0.65), (0.14, 0.38, 0.05), mats["boda_red"],  root, col, rot_deg=(-12, 0, 0), bevel_w=0.015)

    # Kenyan Boda-Boda Rider wearing Neon Lime Reflective Vest & Helmet!
    add_box("RiderTorso",   (0.0, -0.14, 1.16), (0.38, 0.24, 0.52), mats["jacket_dark"], root, col, rot_deg=(-12, 0, 0), bevel_w=0.05)
    add_box("ReflectVest",  (0.0, -0.14, 1.17), (0.40, 0.26, 0.42), mats["vest_neon"],   root, col, rot_deg=(-12, 0, 0), bevel_w=0.04)
    add_ellipsoid("Helmet", (0.0, -0.08, 1.54), (0.25, 0.29, 0.26), mats["kenya_black"], root, col, subdiv=2)
    add_box("Visor",        (0.0,  0.04, 1.54), (0.20, 0.08, 0.09), mats["chrome"],      root, col, bevel_w=0.01)
    for side in (-1, 1):
        add_cyl(f"Arm_{side}",   (side * 0.24, 0.15, 1.12), 0.055, 0.048, 0.54, 8, mats["jacket_dark"], root, col, rot_deg=(62, 0, -side * 10))
        add_cyl(f"Thigh_{side}", (side * 0.18, -0.05, 0.78), 0.078, 0.068, 0.42, 8, mats["jeans_blue"], root, col, rot_deg=(68, 0, side * 12))
        add_cyl(f"Shin_{side}",  (side * 0.21,  0.05, 0.52), 0.062, 0.052, 0.40, 8, mats["jeans_blue"], root, col, rot_deg=(-12, 0, 0))
    return root


# ==============================================================================
# 3. KENHA KENYAN HIGHWAY STONE & CONCRETE RIVER BRIDGE PARAPET (spans road!)
# ==============================================================================
def build_stone_bridge_culvert(mats, col):
    root = bpy.data.objects.new("Kenya_Stone_Bridge_Culvert", None)
    link_obj(root, None, col)

    half_span_x = 9.4  # Clear width 18.8m so 2-lane & climbing-lane roads pass cleanly with ZERO obstruction!
    bridge_len_y = 22.0
    for side in (-1, 1):
        sx = side * half_span_x
        # Dressed stone masonry lower abutment + concrete parapet wall
        add_box(f"Abutment_{side}",   (sx, 0.0, -0.60), (1.35, bridge_len_y, 2.20), mats["stone_bridge"], root, col, bevel_w=0.05)
        add_box(f"ParapetWall_{side}", (sx, 0.0,  0.65), (0.68, bridge_len_y, 1.15), mats["stone_bridge"], root, col, bevel_w=0.04)
        add_box(f"ParapetCap_{side}",  (sx, 0.0,  1.28), (0.82, bridge_len_y + 0.3, 0.16), mats["kenya_white"], root, col, bevel_w=0.025)
        # Galvanized top safety pipe railing
        add_cyl(f"TopRail_{side}",     (sx, 0.0,  1.62), 0.045, 0.045, bridge_len_y, 10, mats["galv_rail"], root, col, rot_deg=(90, 0, 0))
        for py in (-10.0, -5.0, 0.0, 5.0, 10.0):
            add_box(f"RailPost_{side}_{py:.0f}", (sx, py, 1.45), (0.10, 0.10, 0.36), mats["galv_rail"], root, col, bevel_w=0.01)
        # Authentic Kenyan Black & White Painted Bridge End Hazard Piers!
        for end_s in (-1, 1):
            ey = end_s * (bridge_len_y * 0.5 + 0.35)
            add_box(f"EndPierB1_{side}_{end_s}", (sx, ey, 0.45), (0.95, 0.95, 0.55), mats["kenya_black"], root, col, bevel_w=0.03)
            add_box(f"EndPierW1_{side}_{end_s}", (sx, ey, 0.98), (0.95, 0.95, 0.52), mats["kenya_white"], root, col, bevel_w=0.03)
            add_box(f"EndPierB2_{side}_{end_s}", (sx, ey, 1.48), (0.95, 0.95, 0.48), mats["kenya_black"], root, col, bevel_w=0.03)
    return root


# ==============================================================================
# 4. RIFT VALLEY VOLCANIC ROCK & BOULDER CLIFF OUTCROP
# ==============================================================================
def build_rift_boulder_cliff(mats, col):
    root = bpy.data.objects.new("Kenya_Rift_Boulder_Cliff", None)
    link_obj(root, None, col)

    rng = random.Random(254771)
    mesh = bpy.data.meshes.new("RiftCliffCluster_Mesh")
    bm = bmesh.new()
    rocks = [
        ((0.0, 0.0, 1.6),  (4.4, 3.6, 2.4)),
        ((-2.6, 1.4, 1.0), (2.8, 2.4, 1.7)),
        ((2.8, -1.0, 1.1), (3.1, 2.5, 1.8)),
        ((0.8, 2.2, 0.7),  (2.2, 2.0, 1.3)),
        ((-1.4, -2.0, 0.8),(2.4, 1.9, 1.4)),
    ]
    for loc, sc in rocks:
        m = Matrix.Translation(loc) @ Matrix.Diagonal((sc[0], sc[1], sc[2], 1.0))
        ret = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.95, matrix=m)
        for v in ret["verts"]:
            # Angular stratified basalt/volcanic facets
            v.co.x += rng.uniform(-0.32, 0.32)
            v.co.y += rng.uniform(-0.32, 0.32)
            v.co.z = max(-0.2, v.co.z + rng.uniform(-0.26, 0.26))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    apply_box_uv(bm, 0.28)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("VolcanicOutcrop", mesh)
    obj.data.materials.append(mats["rift_rock"])
    link_obj(obj, root, col)
    return root


# ==============================================================================
# 5. SCULPTED 3D ROADSIDE SAVANNA / HIGHLAND GRASS & BUSH CLUSTER
# ==============================================================================
def build_grass_tuft_cluster(mats, col):
    root = bpy.data.objects.new("Kenya_Grass_Tuft_Cluster", None)
    link_obj(root, None, col)

    rng = random.Random(254555)
    mesh = bpy.data.meshes.new("GrassTuft_Mesh")
    bm = bmesh.new()
    # 14 radiating arched grass blades forming a lush 1.5m wide x 0.75m tall roadside clump!
    for i in range(14):
        ang = (i / 14.0) * math.pi * 2.0 + rng.uniform(-0.18, 0.18)
        dist = rng.uniform(0.05, 0.52)
        cx, cy = math.cos(ang) * dist, math.sin(ang) * dist
        h = rng.uniform(0.42, 0.82)
        w = rng.uniform(0.07, 0.12)
        lean = rng.uniform(0.14, 0.34)
        dx, dy = math.cos(ang), math.sin(ang)
        px, py = -dy * w, dx * w
        v0 = bm.verts.new((cx - px, cy - py, 0.0))
        v1 = bm.verts.new((cx + px, cy + py, 0.0))
        v2 = bm.verts.new((cx + dx * lean * 0.45 + px * 0.6, cy + dy * lean * 0.45 + py * 0.6, h * 0.55))
        v3 = bm.verts.new((cx + dx * lean * 0.45 - px * 0.6, cy + dy * lean * 0.45 - py * 0.6, h * 0.55))
        vt = bm.verts.new((cx + dx * lean, cy + dy * lean, h))
        bm.faces.new((v0, v1, v2, v3))
        bm.faces.new((v3, v2, vt))
        # Separate thin back-face vertices offset slightly so visible from both sides cleanly!
        off = Vector((dx * 0.003, dy * 0.003, 0.0))
        b0 = bm.verts.new(v0.co + off)
        b1 = bm.verts.new(v1.co + off)
        b2 = bm.verts.new(v2.co + off)
        b3 = bm.verts.new(v3.co + off)
        bt = bm.verts.new(vt.co + off)
        bm.faces.new((b3, b2, b1, b0))
        bm.faces.new((bt, b2, b3))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    apply_box_uv(bm, 1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("GrassCluster", mesh)
    obj.data.materials.append(mats["grass_blade"])
    link_obj(obj, root, col)
    return root


# ==============================================================================
# 6. SCULPTED BLENDER PLAINS ZEBRA (Organic barrel torso, stripes, arched neck, mane & hooves!)
# ==============================================================================
def build_zebra_sculpted(mats, col):
    root = bpy.data.objects.new("Kenya_Zebra_Sculpted", None)
    link_obj(root, None, col)

    # Organic barrel torso + 5 bold black Kenyan zebra stripe bands
    add_ellipsoid("Torso", (0.0, 0.0, 1.24), (0.64, 1.72, 0.70), mats["zebra_white"], root, col, subdiv=2)
    for sy in (-0.52, -0.26, 0.0, 0.26, 0.52):
        add_ellipsoid(f"Stripe_{sy:.2f}", (0.0, sy, 1.24), (0.66, 0.13, 0.72), mats["zebra_black"], root, col, subdiv=1)
    # Arched neck, striped mane, sculpted head, muzzle & ears (+Y forward)
    add_cyl("Neck", (0.0, 0.74, 1.62), 0.22, 0.15, 0.72, 10, mats["zebra_white"], root, col, rot_deg=(-36, 0, 0))
    add_box("Mane", (0.0, 0.64, 1.72), (0.08, 0.14, 0.66), mats["zebra_black"], root, col, rot_deg=(-36, 0, 0), bevel_w=0.015)
    add_ellipsoid("Head", (0.0, 1.02, 1.92), (0.24, 0.48, 0.24), mats["zebra_white"], root, col, rot_deg=(24, 0, 0), subdiv=2)
    add_ellipsoid("Muzzle", (0.0, 1.22, 1.82), (0.16, 0.20, 0.16), mats["zebra_black"], root, col, subdiv=1)
    for side in (-1, 1):
        add_cyl(f"Ear_{side}", (side * 0.09, 0.92, 2.10), 0.04, 0.015, 0.16, 6, mats["zebra_black"], root, col, rot_deg=(-12, side * 18, 0))
    # 4 Tapered Legs + Hooves + Tail
    for lx in (-0.21, 0.21):
        for ly in (0.56, -0.58):
            add_cyl(f"Leg_{lx:.2f}_{ly:.2f}", (lx, ly, 0.55), 0.065, 0.095, 1.04, 8, mats["zebra_white"], root, col)
            add_cyl(f"Hoof_{lx:.2f}_{ly:.2f}", (lx, ly, 0.06), 0.070, 0.065, 0.12, 8, mats["zebra_black"], root, col)
    add_cyl("Tail", (0.0, -0.88, 0.96), 0.03, 0.05, 0.58, 6, mats["zebra_black"], root, col, rot_deg=(18, 0, 0))
    return root


# ==============================================================================
# 7. SCULPTED BLENDER MASAI GIRAFFE (Towering neck, chestnut spots, ossicones!)
# ==============================================================================
def build_giraffe_sculpted(mats, col):
    root = bpy.data.objects.new("Kenya_Giraffe_Sculpted", None)
    link_obj(root, None, col)

    # Sloping savanna body + spotted hide reliefs
    add_ellipsoid("Torso", (0.0, 0.0, 2.15), (0.86, 1.95, 0.95), mats["giraffe_gold"], root, col, rot_deg=(14, 0, 0), subdiv=2)
    for sx, sy, sz in ((-0.38, 0.25, 2.25), (0.38, -0.15, 2.10), (-0.36, -0.35, 2.02), (0.38, 0.35, 2.28), (0.0, 0.15, 2.55)):
        add_ellipsoid(f"Spot_{sx:.2f}_{sy:.2f}", (sx, sy, sz), (0.18, 0.34, 0.30), mats["giraffe_spot"], root, col, subdiv=1)
    # Towering graceful neck + spots + head + twin ossicones!
    add_cyl("LongNeck", (0.0, 0.88, 3.65), 0.28, 0.14, 2.45, 12, mats["giraffe_gold"], root, col, rot_deg=(-14, 0, 0))
    for nz in (3.0, 3.6, 4.2):
        add_ellipsoid(f"NeckSpot_{nz:.1f}", (0.0, 0.72 + (nz - 3.0) * 0.25, nz), (0.34, 0.22, 0.24), mats["giraffe_spot"], root, col, subdiv=1)
    add_ellipsoid("Head", (0.0, 1.32, 4.90), (0.26, 0.62, 0.26), mats["giraffe_gold"], root, col, rot_deg=(18, 0, 0), subdiv=2)
    for side in (-1, 1):
        add_cyl(f"Ossicone_{side}", (side * 0.07, 1.18, 5.14), 0.025, 0.028, 0.22, 6, mats["giraffe_spot"], root, col)
        add_cyl(f"Ear_{side}",      (side * 0.16, 1.12, 5.02), 0.04, 0.015, 0.18, 6, mats["giraffe_gold"], root, col, rot_deg=(0, side * 55, 0))
    # 4 Long Slender Legs + Tail
    for lx in (-0.30, 0.30):
        for ly, lh in ((0.64, 2.15), (-0.64, 1.88)):
            add_cyl(f"Leg_{lx:.2f}_{ly:.2f}", (lx, ly, lh * 0.5), 0.075, 0.125, lh, 8, mats["giraffe_gold"], root, col)
    add_cyl("Tail", (0.0, -0.98, 1.55), 0.03, 0.05, 0.85, 6, mats["giraffe_spot"], root, col, rot_deg=(12, 0, 0))
    return root


# ==============================================================================
# 8. SCULPTED BLENDER TSAVO AFRICAN ELEPHANT (Flared ears, curved trunk & gleaming ivory tusks!)
# ==============================================================================
def build_elephant_sculpted(mats, col):
    root = bpy.data.objects.new("Kenya_Elephant_Sculpted", None)
    link_obj(root, None, col)

    # Massive organic barrel torso & domed African elephant head
    add_ellipsoid("Body", (0.0, -0.10, 2.25), (1.92, 3.15, 1.98), mats["elephant_red"], root, col, subdiv=2)
    add_ellipsoid("Head", (0.0,  1.52, 2.68), (1.28, 1.32, 1.38), mats["elephant_red"], root, col, subdiv=2)
    # Wide Flared Iconic African Elephant Ears!
    for side in (-1, 1):
        add_ellipsoid(f"Ear_{side}", (side * 0.92, 1.32, 2.68), (0.18, 0.95, 1.38), mats["elephant_red"], root, col, rot_deg=(0, side * 12, -side * 24), subdiv=2)
    # Multi-Segment Gracefully Curved Trunk!
    add_cyl("TrunkUpper", (0.0, 2.12, 2.12), 0.24, 0.30, 0.92, 10, mats["elephant_red"], root, col, rot_deg=(24, 0, 0))
    add_cyl("TrunkMid",   (0.0, 2.34, 1.36), 0.17, 0.24, 0.82, 10, mats["elephant_red"], root, col, rot_deg=(8, 0, 0))
    add_cyl("TrunkTip",   (0.0, 2.30, 0.72), 0.11, 0.17, 0.62, 10, mats["elephant_red"], root, col, rot_deg=(-22, 0, 0))
    # Twin Gleaming Curved Ivory Tusks!
    for side in (-1, 1):
        add_cyl(f"TuskBase_{side}", (side * 0.38, 2.22, 2.02), 0.085, 0.065, 0.88, 10, mats["ivory"], root, col, rot_deg=(68, side * 14, 0))
        add_cyl(f"TuskTip_{side}",  (side * 0.48, 2.84, 1.96), 0.064, 0.018, 0.74, 10, mats["ivory"], root, col, rot_deg=(96, side * 18, 0))
    # 4 Thick Pillar Legs + Tail
    for lx in (-0.64, 0.64):
        for ly in (0.86, -0.96):
            add_cyl(f"Leg_{lx:.2f}_{ly:.2f}", (lx, ly, 0.88), 0.30, 0.34, 1.76, 12, mats["elephant_red"], root, col)
    add_cyl("Tail", (0.0, -1.68, 1.55), 0.04, 0.07, 1.05, 6, mats["elephant_red"], root, col, rot_deg=(16, 0, 0))
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
    print(f"[EXPORTED] {filename} ({kb:.1f} KB)")


def render_preview(col):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PNG

    world = bpy.data.worlds.new("RealismWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.60, 0.74, 0.90, 1.0)
    bg.inputs["Strength"].default_value = 1.15

    sun = bpy.data.objects.new("SunKey", bpy.data.lights.new("SunKey", type='SUN'))
    sun.data.energy = 3.2
    sun.rotation_euler = Euler((math.radians(48), math.radians(15), math.radians(-30)), 'XYZ')
    col.objects.link(sun)

    cam = bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam"))
    cam.data.lens = 34
    cam.location = (0.0, 24.0, 7.5)
    cam.rotation_euler = (Vector((0.0, 0.0, 1.8)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    col.objects.link(cam)
    scene.camera = cam

    bpy.ops.render.render(write_still=True)
    print(f"[PREVIEW] Rendered -> {PREVIEW_PNG}")


def main():
    clear_scene()
    mats = init_materials()
    col = bpy.context.scene.collection

    builders = [
        ("Kenya_Cockpit_Accessories_Pack.glb", build_cockpit_accessories, (-10.0, 6.0, 1.2)),
        ("Kenya_BodaBoda_Rider.glb",           build_boda_boda_rider,     ( -7.0, 4.0, 0.0)),
        ("Kenya_Stone_Bridge_Culvert.glb",     build_stone_bridge_culvert,(  0.0,-8.0, 0.0)),
        ("Kenya_Rift_Boulder_Cliff.glb",       build_rift_boulder_cliff,  (  8.0, 2.0, 0.0)),
        ("Kenya_Grass_Tuft_Cluster.glb",       build_grass_tuft_cluster,  ( -4.0, 6.0, 0.0)),
        ("Kenya_Zebra_Sculpted.glb",           build_zebra_sculpted,      ( -2.0, 3.0, 0.0)),
        ("Kenya_Giraffe_Sculpted.glb",         build_giraffe_sculpted,    (  1.5, 2.5, 0.0)),
        ("Kenya_Elephant_Sculpted.glb",        build_elephant_sculpted,   (  5.5, 4.0, 0.0)),
    ]

    for fname, fn, loc in builders:
        obj = fn(mats, col)
        export_glb(obj, fname)
        obj.location = loc

    add_box("StudioFloor", (0, 0, -0.15), (60, 50, 0.3), mats["stone_bridge"], None, col, bevel_w=0.0)
    blend_path = os.path.join(STUDIO_DIR, "Kenya_RealismPack_Showroom.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[SAVED] Showroom .blend -> {blend_path}")
    render_preview(col)


if __name__ == "__main__":
    main()
