"""
Kenya Street Sim — Blender 5.3 Flagship Playable Commercial Fleet Studio (Path A: 100% Owned)
Builds TWO studio-grade, 100%-owned playable Kenyan hero vehicles from scratch:
  1. Savanna_Express_Coach_2026.glb (13.2m Tri-Axle 6x2 Luxury Kenyan Touring Coach)
     - Swept aerodynamic body, inset panoramic windows, glossy Savanna Burgundy/Pearl/Gold livery
     - Full 3D walk-in passenger cabin (10 rows of 2+2 seats, overhead luggage racks, ceiling LEDs,
       entry stairwell & RHD driver cockpit!)
     - Articulated left passenger door, roof HVAC pod, glowing amber LED route board, printed KDX 057M
       Kenyan plates, swept twin-arm mirrors & 6 deep-dish Alcoa chrome wheels!
  2. Savanna_Parcel_Lorry_FRR.glb (8.35m Heavy Kenyan FRR-Class Commercial Parcel Carrier)
     - Sculpted Highland Cobalt Blue cab-over truck with full 3D RHD cab interior, wind deflector,
       4-bar chrome grille, solid west-coast mirrors, amber roof clearance lights & boarding steps
     - Ribbed aluminum express freight box with SAVANNA PARCEL banners, rear swing doors w/ chrome
       cam-lock rods, red/yellow Kenyan reflective chevrons, 80 KPH sticker, printed KDX 409G plates,
       diesel tank, air brake tanks, side guards, mudflaps & 4 heavy commercial wheels!

Outputs:
  - KenyaStreetSim/Assets/KenyaStreetSim/ImportedModels/OwnedFleet/Savanna_Express_Coach_2026.glb
  - KenyaStreetSim/Assets/KenyaStreetSim/ImportedModels/OwnedFleet/Savanna_Parcel_Lorry_FRR.glb
  - KenyaStreetSim/BlenderStudio/Kenya_Owned_Playable_Fleet_Studio.blend (Scripting Tab + Material Preview!)
"""

import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector, Euler, Matrix

_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
_candidate_root = os.path.abspath(os.path.join(_script_dir, "..")) if _script_dir else ""
REPO_ROOT = _candidate_root if os.path.isdir(os.path.join(_candidate_root, "KenyaStreetSim")) else r"C:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
OWNED_FLEET_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "ImportedModels", "OwnedFleet")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
TEX_DIR = os.path.join(STUDIO_DIR, "Textures")
PREVIEW_PNG = sys.argv[-1] if (len(sys.argv) > 1 and sys.argv[-1].lower().endswith(".png")) else os.path.join(STUDIO_DIR, "kenya_playable_fleet_showroom.png")

os.makedirs(OWNED_FLEET_DIR, exist_ok=True)
os.makedirs(STUDIO_DIR, exist_ok=True)

_tools_dir = os.path.join(REPO_ROOT, "tools")
if _tools_dir not in sys.path:
    sys.path.insert(0, _tools_dir)
import kenya_auto_textures


# ==============================================================================
# 1. MATERIALS & TEXTURE HELPERS
# ==============================================================================
def make_mat(name, base_col, metallic=0.0, roughness=0.4, coat=0.0, emission=None, emission_strength=0.0, alpha=1.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name=name)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base_col, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Alpha" in bsdf.inputs and alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        if hasattr(mat, "blend_method"):
            mat.blend_method = 'BLEND'
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = 'BLENDED'
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = 0.05
    if emission is not None and emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def make_textured_mat(name, png_path, roughness=0.26, metallic=0.04, emission_strength=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name=name)
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic

    tex_node = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(png_path, check_existing=True)
    try:
        img.pack()
    except Exception:
        pass
    tex_node.image = img
    nt.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
    if emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
        nt.links.new(tex_node.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def init_materials():
    tex = kenya_auto_textures.ensure_all_textures(TEX_DIR)
    mats = {
        "coach_pearl":    make_mat("Paint_CoachPearl",     (0.92, 0.93, 0.95), metallic=0.16, roughness=0.16, coat=1.0),
        "coach_burgundy": make_mat("Paint_CoachBurgundy",  (0.46, 0.045, 0.095), metallic=0.52, roughness=0.16, coat=1.0),
        "coach_gold":     make_mat("Paint_CoachGold",      (0.92, 0.70, 0.14), metallic=0.72, roughness=0.20, coat=0.9),
        "coach_skirt":    make_mat("Paint_CoachSkirt",     (0.14, 0.15, 0.18), metallic=0.35, roughness=0.28, coat=0.6),
        "lorry_blue":     make_mat("Paint_LorryCobalt",    (0.06, 0.24, 0.58), metallic=0.45, roughness=0.18, coat=1.0),
        "cargo_white":    make_mat("Cargo_FreightWhite",   (0.88, 0.90, 0.93), metallic=0.12, roughness=0.32, coat=0.3),
        "psv_yellow":     make_mat("Safety_YellowStripe",  (0.98, 0.78, 0.04), metallic=0.06, roughness=0.22, coat=0.6),
        # Crystal clear front windshield (alpha 0.22) + sleek tinted side panoramic glass (alpha 0.54)!
        "windshield":     make_mat("Glass_CrystalWindshield", (0.14, 0.22, 0.28), metallic=0.12, roughness=0.04, coat=1.0, alpha=0.22),
        "side_glass":     make_mat("Glass_PanoramicTint",  (0.04, 0.08, 0.12), metallic=0.24, roughness=0.04, coat=1.0, alpha=0.54),
        "black_trim":     make_mat("Trim_BlackRubber",     (0.05, 0.055, 0.065), metallic=0.08, roughness=0.50),
        "grille_dark":    make_mat("Trim_GrilleMesh",      (0.02, 0.024, 0.03), metallic=0.22, roughness=0.42),
        "chrome":         make_mat("Metal_Chrome",         (0.90, 0.92, 0.95), metallic=0.98, roughness=0.06),
        "alcoa":          make_mat("Metal_AlcoaAlloy",     (0.82, 0.85, 0.89), metallic=0.95, roughness=0.12),
        "steel_rim":      make_mat("Metal_CommercialSteel",(0.68, 0.72, 0.77), metallic=0.88, roughness=0.22),
        "tire":           make_mat("Rubber_HeavyTire",     (0.035, 0.037, 0.043), metallic=0.02, roughness=0.84),
        "wheel_cavity":   make_mat("Wheel_DarkHub",        (0.014, 0.015, 0.02), metallic=0.10, roughness=0.75),
        "brake_disc":     make_mat("Metal_BrakeDrum",      (0.20, 0.21, 0.24), metallic=0.80, roughness=0.44),
        "headlight":      make_mat("Light_HeadLED",        (0.98, 0.97, 0.90), metallic=0.10, roughness=0.06, emission=(1.0, 0.96, 0.86), emission_strength=2.4),
        "drl_white":      make_mat("Light_DRLWhite",       (0.95, 0.98, 1.00), metallic=0.05, roughness=0.08, emission=(0.94, 0.98, 1.00), emission_strength=2.0),
        "foglight":       make_mat("Light_FogLamp",        (0.96, 0.92, 0.80), metallic=0.10, roughness=0.08, emission=(0.96, 0.90, 0.74), emission_strength=1.4),
        "indicator":      make_mat("Light_AmberTurn",      (0.98, 0.52, 0.04), metallic=0.08, roughness=0.14, emission=(1.0, 0.54, 0.04), emission_strength=1.6),
        "taillight":      make_mat("Light_TailRedLED",     (0.88, 0.04, 0.04), metallic=0.08, roughness=0.12, emission=(0.96, 0.05, 0.04), emission_strength=2.4),
        "cabin_floor":    make_mat("Cabin_FloorDeck",      (0.12, 0.13, 0.15), metallic=0.04, roughness=0.76),
        "cabin_aisle":    make_mat("Cabin_CarpetRed",      (0.36, 0.06, 0.08), metallic=0.02, roughness=0.86),
        "cabin_dash":     make_mat("Cabin_DashDark",       (0.07, 0.075, 0.088), metallic=0.06, roughness=0.58),
        "seat_coach":     make_mat("Cabin_SeatBurgundy",   (0.42, 0.07, 0.12), metallic=0.02, roughness=0.76),
        "seat_headrest":  make_mat("Cabin_HeadrestCream",  (0.84, 0.80, 0.72), metallic=0.02, roughness=0.72),
        "seat_truck":     make_mat("Cabin_TruckSeat",      (0.16, 0.17, 0.20), metallic=0.02, roughness=0.78),
        "cabin_led":      make_mat("Cabin_CeilingLED",     (0.98, 0.94, 0.82), metallic=0.0, roughness=0.15, emission=(1.0, 0.94, 0.80), emission_strength=1.6),
        # Crisp Printed Textures!
        "plate_coach":    make_textured_mat("PlateTex_Coach",    tex["plate_coach"],   roughness=0.22),
        "plate_lorry":    make_textured_mat("PlateTex_Lorry",    tex["plate_lorry"],   roughness=0.22),
        "led_coach":      make_textured_mat("LEDTex_Coach",      tex["led_coach"],     roughness=0.12, emission_strength=2.1),
        "banner_coach":   make_textured_mat("Banner_Coach",      tex["banner_coach"],  roughness=0.22),
        "banner_parcel":  make_textured_mat("Banner_Parcel",     tex["banner_parcel"], roughness=0.24),
        "chevron":        make_textured_mat("Chevron_Safety",    tex["chevron"],       roughness=0.18, emission_strength=0.45),
        "speed_80":       make_textured_mat("Sticker_80KPH",     tex["speed_80"],      roughness=0.25),
    }
    return mats


# ==============================================================================
# 2. GEOMETRY & AUTOMOTIVE WHEEL BUILDERS
# ==============================================================================
def link_obj(obj, parent, col):
    col.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def smooth_obj(obj, angle_deg=36.0):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    try:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle_deg))
    except Exception:
        bpy.ops.object.shade_smooth()
    obj.select_set(False)


def create_beveled_box(name, loc, size, mat, parent, col, rot_deg=(0, 0, 0), bevel_w=0.02, bevel_segs=2):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel_w > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel_w, min(size) * 0.36), segments=bevel_segs, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(loc)
    obj.rotation_euler = Euler([math.radians(a) for a in rot_deg], 'XYZ')
    obj.data.materials.append(mat)
    link_obj(obj, parent, col)
    smooth_obj(obj, 38.0)
    return obj


def create_cylinder(name, loc, radius, depth, segs, mat, parent, col, rot_deg=(0, 0, 0), bevel_w=0.0):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius, radius2=radius, depth=depth)
    if bevel_w > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel_w, segments=2, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(loc)
    obj.rotation_euler = Euler([math.radians(a) for a in rot_deg], 'XYZ')
    obj.data.materials.append(mat)
    link_obj(obj, parent, col)
    smooth_obj(obj, 42.0)
    return obj


def create_wind_deflector_wedge(name, y_back, y_front, z_base, z_top_back, z_top_front, half_w_back, half_w_front, mat, parent, col):
    """Creates a true aerodynamic sloped roof fairing wedge sitting 100% flush on the cab roof (zero window clipping!)."""
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    v0 = bm.verts.new(Vector((-half_w_back,  y_back,  z_base)))
    v1 = bm.verts.new(Vector(( half_w_back,  y_back,  z_base)))
    v2 = bm.verts.new(Vector(( half_w_front, y_front, z_base)))
    v3 = bm.verts.new(Vector((-half_w_front, y_front, z_base)))
    v4 = bm.verts.new(Vector((-half_w_back,  y_back,  z_top_back)))
    v5 = bm.verts.new(Vector(( half_w_back,  y_back,  z_top_back)))
    v6 = bm.verts.new(Vector(( half_w_front, y_front, z_top_front)))
    v7 = bm.verts.new(Vector((-half_w_front, y_front, z_top_front)))
    bm.faces.new((v0, v1, v2, v3))
    bm.faces.new((v4, v7, v6, v5))
    bm.faces.new((v0, v4, v5, v1))
    bm.faces.new((v3, v2, v6, v7))
    bm.faces.new((v0, v3, v7, v4))
    bm.faces.new((v1, v5, v6, v2))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.055, segments=3, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(mat)
    link_obj(obj, parent, col)
    smooth_obj(obj, 42.0)
    return obj


def create_decal_quad(name, loc, width, height, facing, mat, parent, col, rot_deg=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    hw, hh = width * 0.5, height * 0.5
    if facing == '+Y':
        pts = [( hw, 0, -hh), (-hw, 0, -hh), (-hw, 0,  hh), ( hw, 0,  hh)]
    elif facing == '-Y':
        pts = [(-hw, 0, -hh), ( hw, 0, -hh), ( hw, 0,  hh), (-hw, 0,  hh)]
    elif facing == '-X':
        pts = [(0,  hw, -hh), (0, -hw, -hh), (0, -hw,  hh), (0,  hw,  hh)]
    else:
        pts = [(0, -hw, -hh), (0,  hw, -hh), (0,  hw,  hh), (0, -hw,  hh)]
    uvs = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]
    verts = [bm.verts.new(Vector(p)) for p in pts]
    face = bm.faces.new(verts)
    for loop, uv in zip(face.loops, uvs):
        loop[uv_layer].uv = uv
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(loc)
    obj.rotation_euler = Euler([math.radians(a) for a in rot_deg], 'XYZ')
    obj.data.materials.append(mat)
    link_obj(obj, parent, col)
    return obj


def build_lofted_shell(
    name,
    slices,
    paint_mat,
    mats,
    parent,
    col,
    side_win_spans=(),
    windshield_span=None,
    door_cutout_span_left=None,
    belt_mat=None,
    skirt_mat=None,
    wheel_cutouts=None,
    bevel_offset=0.06,
):
    """
    Lofts a crisp multi-material commercial vehicle shell (+Y forward, +Z up) with:
      - Separate Crystal Windshield material (slot 1) & Tinted Panoramic Side Windows (slot 2)
      - Painted A/B/C pillars & black rubber window gaskets (slot 3)
      - Optional belt accent (slot 4) & lower luggage skirt (slot 5)
      - Optional real left-side door bay recess (for articulated opening passenger door!)
      - Boolean circular wheel-arch wells!
    """
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()

    slot_mats = [paint_mat, mats["windshield"], mats["side_glass"], mats["black_trim"]]
    belt_idx = 0
    skirt_idx = 0
    if belt_mat is not None:
        belt_idx = len(slot_mats)
        slot_mats.append(belt_mat)
    if skirt_mat is not None:
        skirt_idx = len(slot_mats)
        slot_mats.append(skirt_mat)

    rings = []
    for (y, hws, zs, hwm, zm, hwb, zb, hwr, zr) in slices:
        pts = [
            (-hws, y, zs), (-hwm, y, zm), (-hwb, y, zb), (-hwr, y, zr),
            ( hwr, y, zr), ( hwb, y, zb), ( hwm, y, zm), ( hws, y, zs),
        ]
        rings.append([bm.verts.new(Vector(p)) for p in pts])

    side_glass_faces = []
    front_glass_faces = []
    door_open_faces = []

    for s in range(len(rings) - 1):
        r0, r1 = rings[s], rings[s + 1]
        for i in range(8):
            inext = (i + 1) % 8
            f = bm.faces.new((r0[i], r0[inext], r1[inext], r1[i]))
            f.material_index = 0
            if belt_mat is not None and i in (1, 5):
                f.material_index = belt_idx
            if skirt_mat is not None and i in (0, 6):
                f.material_index = skirt_idx

            # Optional open left door bay for playable Touring Coach (right side i==4 still gets flush inset driver window!)
            if door_cutout_span_left is not None and s == door_cutout_span_left and i in (0, 1, 2):
                door_open_faces.append(f)
                continue
            if door_cutout_span_left is not None and s == door_cutout_span_left and i == 4:
                side_glass_faces.append(f)
                continue

            if s in side_win_spans and i in (2, 4):
                side_glass_faces.append(f)
            if windshield_span is not None and s == windshield_span and i == 3:
                front_glass_faces.append(f)

    bm.faces.new(list(reversed(rings[0]))).material_index = 0
    bm.faces.new(rings[-1]).material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # Remove left-side passenger door bay faces cleanly so passengers walk straight into the illuminated stairwell!
    if door_open_faces:
        bmesh.ops.delete(bm, geom=door_open_faces, context='FACES_ONLY')

    def inset_windows(faces, glass_slot, frame_thick, recess):
        if not faces:
            return
        bmesh.ops.inset_individual(bm, faces=faces, thickness=frame_thick, depth=0.0, use_even_offset=True)
        res2 = bmesh.ops.inset_individual(bm, faces=faces, thickness=0.022, depth=-recess, use_even_offset=True)
        for sf in res2.get("faces", []):
            sf.material_index = 3
        for gf in faces:
            gf.material_index = glass_slot

    inset_windows(side_glass_faces,  glass_slot=2, frame_thick=0.075, recess=0.026)
    inset_windows(front_glass_faces, glass_slot=1, frame_thick=0.085, recess=0.028)

    outer_edges = [
        e for e in bm.edges
        if len(e.link_faces) == 2
        and e.link_faces[0].material_index in (0, belt_idx, skirt_idx)
        and e.link_faces[1].material_index in (0, belt_idx, skirt_idx)
        and e.calc_face_angle(0.0) > math.radians(18.0)
    ]
    if outer_edges:
        bmesh.ops.bevel(bm, geom=outer_edges, offset=bevel_offset, segments=3, affect='EDGES', clamp_overlap=True)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    for m in slot_mats:
        obj.data.materials.append(m)
    link_obj(obj, parent, col)

    if wheel_cutouts:
        cut_objs = []
        for idx, (wx, wy, wz, wrad, wdepth) in enumerate(wheel_cutouts):
            c_mesh = bpy.data.meshes.new(f"Cut_{idx}")
            cbm = bmesh.new()
            bmesh.ops.create_cone(cbm, cap_ends=True, cap_tris=False, segments=28, radius1=wrad, radius2=wrad, depth=wdepth)
            cbm.to_mesh(c_mesh)
            cbm.free()
            c_obj = bpy.data.objects.new(f"Cut_{idx}", c_mesh)
            c_obj.location = Vector((wx, wy, wz))
            c_obj.rotation_euler = Euler((0.0, math.radians(90.0), 0.0), 'XYZ')
            col.objects.link(c_obj)
            cut_objs.append(c_obj)

            bmod = obj.modifiers.new(f"WheelArch_{idx}", 'BOOLEAN')
            bmod.operation = 'DIFFERENCE'
            bmod.object = c_obj
            try:
                bmod.solver = 'EXACT'
            except Exception:
                pass

        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        for mod in list(obj.modifiers):
            try:
                bpy.ops.object.modifier_apply(modifier=mod.name)
            except Exception as e:
                print(f"  Note applying {mod.name} on {name}: {e}")
        obj.select_set(False)
        for c_obj in cut_objs:
            bpy.data.objects.remove(c_obj, do_unlink=True)

    smooth_obj(obj, 36.0)
    return obj


def build_commercial_wheel(name, loc, radius, width, rim_mat, mats, parent, col, is_left=True, is_dual_rear=False, num_lugs=10):
    """
    Heavy commercial Alcoa / Steel deep-dish coach & truck wheel (SINGLE unified mesh centered at axle!).
    Features rounded heavy highway tire tread, polished outer lip, deep recessed dark drum cavity,
    10-spoke/lug ventilation pattern, hub flange & chrome lug nut ring!
    """
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    out_sign = -1.0 if is_left else 1.0
    hw = width * 0.5
    rim_r = radius * 0.58
    segs = 32

    def add_lathe(profile_pts, mat_idx):
        rings = []
        for s in range(segs):
            ang = (s / segs) * math.pi * 2.0
            cy, cz = math.cos(ang), math.sin(ang)
            rings.append([bm.verts.new(Vector((px, cy * pr, cz * pr))) for (px, pr) in profile_pts])
        pn = len(profile_pts)
        for s in range(segs):
            sn = (s + 1) % segs
            for p in range(pn):
                pnxt = (p + 1) % pn
                f = bm.faces.new((rings[s][p], rings[sn][p], rings[sn][pnxt], rings[s][pnxt]))
                f.material_index = mat_idx

    # Heavy commercial tire (single or wide twin-groove dual rear tire!)
    if is_dual_rear:
        tire_profile = [
            (-hw * 0.90, rim_r * 0.96), (-hw * 1.00, radius * 0.80), (-hw * 0.96, radius),
            (-hw * 0.14, radius), (-hw * 0.06, radius * 0.86), ( hw * 0.06, radius * 0.86),
            ( hw * 0.14, radius), ( hw * 0.96, radius), ( hw * 1.00, radius * 0.80), ( hw * 0.90, rim_r * 0.96),
        ]
    else:
        tire_profile = [
            (-hw * 0.86, rim_r * 0.96), (-hw * 0.98, radius * 0.78), (-hw * 1.00, radius * 0.92),
            (-hw * 0.82, radius), ( hw * 0.82, radius), ( hw * 1.00, radius * 0.92),
            ( hw * 0.98, radius * 0.78), ( hw * 0.86, rim_r * 0.96),
        ]
    add_lathe(tire_profile, mat_idx=0)

    # Outer polished Alcoa/Steel lip + dark deep dish inner barrel
    lip_in = rim_r * 0.84
    lip_out = rim_r * 1.01
    rim_lip = [
        (out_sign * hw * 0.86, lip_out),
        (out_sign * hw * 0.94, (lip_out + lip_in) * 0.5),
        (out_sign * hw * 0.84, lip_in),
        (out_sign * hw * 0.42, lip_in * 0.96),
        (out_sign * hw * 0.42, lip_out),
    ]
    add_lathe(rim_lip, mat_idx=1)

    inner_barrel = [
        (out_sign * hw * 0.44, lip_in * 0.97),
        (-out_sign * hw * 0.86, lip_in * 0.95),
        (-out_sign * hw * 0.88, lip_out),
        (out_sign * hw * 0.44, lip_out),
    ]
    add_lathe(inner_barrel, mat_idx=2)

    def add_cyl(x_c, r, depth, csegs, mat_idx):
        ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=csegs, radius1=r, radius2=r, depth=depth)
        rot_m = Matrix.Rotation(math.radians(90.0), 4, 'Y')
        tr_m = Matrix.Translation(Vector((x_c, 0.0, 0.0)))
        bmesh.ops.transform(bm, matrix=tr_m @ rot_m, verts=ret["verts"])
        for f in set(f for v in ret["verts"] for f in v.link_faces):
            f.material_index = mat_idx

    def add_box(center, size, rot_x, mat_idx):
        ret = bmesh.ops.create_cube(bm, size=1.0)
        sc_m = Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        rx_m = Matrix.Rotation(rot_x, 4, 'X')
        tr_m = Matrix.Translation(Vector(center))
        bmesh.ops.transform(bm, matrix=tr_m @ rx_m @ sc_m, verts=ret["verts"])
        for f in set(f for v in ret["verts"] for f in v.link_faces):
            f.material_index = mat_idx

    # Deep dark backing + brake drum + Alcoa face webs + protruding axle hub + lug nuts!
    dish_depth = -out_sign * hw * 0.12 if is_dual_rear else out_sign * hw * 0.48
    add_cyl(-out_sign * hw * 0.46, lip_in * 0.98, 0.04, 24, mat_idx=2)
    add_cyl(dish_depth - out_sign * 0.05, radius * 0.36, 0.04, 22, mat_idx=3)

    # Heavy commercial spider spokes (6 or 8 crisp Alcoa ribs around dark ventilation windows!)
    spokes = 8 if num_lugs >= 10 else 6
    for sp in range(spokes):
        ang = (sp / spokes) * math.pi * 2.0
        slen = lip_in * 0.90
        rm = slen * 0.50
        add_box((dish_depth, math.cos(ang) * rm, math.sin(ang) * rm), (width * 0.12, slen, radius * 0.115), ang, mat_idx=1)

    # Center commercial axle hub & chrome lug-nut ring!
    add_cyl(dish_depth + out_sign * 0.05, radius * 0.25, width * 0.22, 20, mat_idx=1)
    add_cyl(dish_depth + out_sign * 0.12, radius * 0.13, 0.08, 18, mat_idx=4)
    for lg in range(num_lugs):
        ang = (lg / num_lugs) * math.pi * 2.0
        lr = radius * 0.195
        add_box((dish_depth + out_sign * 0.11, math.cos(ang) * lr, math.sin(ang) * lr), (0.04, 0.026, 0.026), ang, mat_idx=4)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(loc)
    for m in (mats["tire"], rim_mat, mats["wheel_cavity"], mats["brake_disc"], mats["chrome"]):
        obj.data.materials.append(m)
    link_obj(obj, parent, col)
    smooth_obj(obj, 42.0)
    return obj


# ==============================================================================
# 3. DELIVERABLE #1: FLAGSHIP PLAYABLE 100%-OWNED KENYAN TOURING COACH (13.2m 6x2)
# ==============================================================================
def build_savanna_express_coach(mats, col):
    """
    100%-Owned Flagship Kenyan Executive Luxury Touring Coach ('Savanna Express KDX 057M'):
      - 13.2m long x 2.54m wide x 3.68m tall, Tri-Axle 6x2
      - Full 3D walk-in passenger cabin (10 rows of 2+2 reclining coach seats, aisle, overhead racks,
        entry stairwell, ceiling LEDs & RHD driver cockpit!)
      - Articulated Left Passenger Door ('Coach_PassengerDoor_Pivot')
      - Roof HVAC pod, glowing amber LED route display, SAVANNA EXPRESS banners & 6 Alcoa wheels!
    """
    root = bpy.data.objects.new("Savanna_Express_Coach_2026", None)
    link_obj(root, None, col)

    w_rad = 0.52
    f_y, m_y, r_y = 3.85, -2.45, -3.95
    cutouts = [
        (-1.26, f_y, w_rad, w_rad + 0.08, 0.56), (1.26, f_y, w_rad, w_rad + 0.08, 0.56),
        (-1.26, m_y, w_rad, w_rad + 0.08, 0.56), (1.26, m_y, w_rad, w_rad + 0.08, 0.56),
        (-1.26, r_y, w_rad, w_rad + 0.08, 0.56), (1.26, r_y, w_rad, w_rad + 0.08, 0.56),
    ]

    # Stations 0..10: 0..6 = 7 panoramic side bays; 7 = front left passenger door bay (+4.80 -> +5.75);
    # 8 = swept panoramic windshield (+5.75 -> +6.48); 9 = sculpted front nose grille (+6.48 -> +6.65)!
    slices = [
        (-6.55, 1.18, 0.44, 1.25, 1.18, 1.25, 1.88, 1.14, 3.48), # 0: rear engine fascia
        (-4.85, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 1: rear bay
        (-3.20, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 2: bay 2
        (-1.55, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 3: bay 3
        ( 0.10, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 4: bay 4
        ( 1.75, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 5: bay 5
        ( 3.35, 1.22, 0.38, 1.27, 1.18, 1.27, 1.88, 1.17, 3.56), # 6: front wheel bay
        ( 4.80, 1.22, 0.38, 1.27, 1.18, 1.27, 1.86, 1.16, 3.54), # 7: front door pillar
        ( 5.75, 1.21, 0.38, 1.26, 1.18, 1.26, 1.82, 1.13, 3.46), # 8: A-pillar roof brow
        ( 6.46, 1.16, 0.42, 1.22, 1.16, 1.21, 1.62, 1.14, 1.66), # 9: windshield cowl
        ( 6.64, 1.08, 0.48, 1.14, 1.12, 1.12, 1.44, 1.06, 1.48), # 10: front bumper nose
    ]
    build_lofted_shell(
        "Coach_BodyShell", slices, mats["coach_pearl"], mats, root, col,
        side_win_spans=(0, 1, 2, 3, 4, 5, 6),
        windshield_span=8,
        door_cutout_span_left=7,
        belt_mat=mats["coach_burgundy"],
        skirt_mat=mats["coach_skirt"],
        wheel_cutouts=cutouts,
        bevel_offset=0.065,
    )

    # Metallic Gold Sweeping Accent Stripe & Crisp Side Livery Banners + Under-Floor Luggage Bay Doors!
    for side in (-1.0, 1.0):
        create_beveled_box(f"GoldAccentStripe_{side:.0f}", (side * 1.276, -0.50, 1.84), (0.025, 11.4, 0.08), mats["coach_gold"], root, col, bevel_w=0.008)
        create_decal_quad(f"CoachLiveryBanner_{side:.0f}", (side * 1.280, -0.65, 1.50), 4.60, 0.56, '-X' if side < 0 else '+X', mats["banner_coach"], root, col)
        # 3 Under-floor luggage locker doors between front and rear axles!
        for ly in (-1.20, 0.45, 2.05):
            create_beveled_box(f"LuggageDoor_{side:.0f}_{ly:.1f}", (side * 1.255, ly, 0.78), (0.04, 1.50, 0.72), mats["coach_burgundy"], root, col, bevel_w=0.015)
            create_beveled_box(f"LuggageLatch_{side:.0f}_{ly:.1f}", (side * 1.280, ly, 0.62), (0.02, 0.16, 0.045), mats["chrome"], root, col, bevel_w=0.006)

    # (Right-side driver window is now cleanly inset flush into Coach_BodyShell station span 7!)

    # Articulated Left-Side Pneumatic Passenger Entry Door (Hinged at front edge y=+5.72, x=-1.24!)
    door_pivot = bpy.data.objects.new("Coach_PassengerDoor_Pivot", None)
    door_pivot.location = Vector((-1.24, 5.72, 1.48))
    link_obj(door_pivot, root, col)
    create_beveled_box("CoachDoorFrame", (0.0, -0.45, 0.0), (0.07, 0.92, 2.12), mats["coach_burgundy"], door_pivot, col, bevel_w=0.016)
    create_beveled_box("CoachDoorGlassUpper", (-0.01, -0.45, 0.42), (0.05, 0.76, 1.06), mats["windshield"], door_pivot, col, bevel_w=0.008)
    create_beveled_box("CoachDoorGlassLower", (-0.01, -0.45, -0.58), (0.05, 0.76, 0.64), mats["windshield"], door_pivot, col, bevel_w=0.008)
    create_cylinder("CoachDoorGrabBar", (-0.05, -0.82, 0.05), 0.018, 0.62, 10, mats["chrome"], door_pivot, col)

    # FULL 3D WALK-IN LUXURY PASSENGER CABIN & KENYAN RHD DRIVER COCKPIT INSIDE!
    create_beveled_box("CabinMainFloor", (0, -0.70, 1.18), (2.28, 11.2, 0.08), mats["cabin_floor"], root, col, bevel_w=0.01)
    create_beveled_box("CabinRedAisle",  (0, -0.70, 1.225), (0.56, 10.8, 0.015), mats["cabin_aisle"], root, col, bevel_w=0.004)
    # Front left entry boarding steps inside open stairwell!
    for st_i, (sz, sx) in enumerate(((0.56, -0.96), (0.78, -0.76), (1.00, -0.54))):
        create_beveled_box(f"EntryStep_{st_i}", (sx, 5.26, sz), (0.44, 0.88, 0.08), mats["cabin_floor"], root, col, bevel_w=0.01)
    # 10 rows of 2+2 executive reclining passenger seats with cream headrests!
    for row in range(10):
        ry = 3.85 - row * 0.98
        for side in (-1.0, 1.0):
            for col_i in (0, 1):
                sx = side * (0.46 + col_i * 0.46)
                create_beveled_box(f"CoachSeatC_{row}_{side:.0f}_{col_i}", (sx, ry, 1.62), (0.42, 0.44, 0.12), mats["seat_coach"], root, col, bevel_w=0.03)
                create_beveled_box(f"CoachSeatB_{row}_{side:.0f}_{col_i}", (sx, ry - 0.18, 2.02), (0.40, 0.11, 0.68), mats["seat_coach"], root, col, rot_deg=(-10, 0, 0), bevel_w=0.03)
                create_beveled_box(f"CoachSeatH_{row}_{side:.0f}_{col_i}", (sx, ry - 0.24, 2.40), (0.30, 0.09, 0.16), mats["seat_headrest"], root, col, bevel_w=0.025)
    # Left & right overhead luggage racks + glowing ceiling LED strips!
    for side in (-1.0, 1.0):
        create_beveled_box(f"OverheadRack_{side:.0f}", (side * 0.82, -0.90, 2.98), (0.56, 10.6, 0.08), mats["cabin_dash"], root, col, bevel_w=0.02)
        create_beveled_box(f"CeilingLED_{side:.0f}",   (side * 0.42, -0.90, 3.42), (0.08, 10.2, 0.03), mats["cabin_led"], root, col, bevel_w=0.006)
    # RHD (+X) Coach Driver Cockpit + Sculpted 3D Steering Wheel + Air-Suspension Driver Seat!
    create_beveled_box("CoachDashMain",  (0.32, 5.95, 1.64), (1.55, 0.62, 0.42), mats["cabin_dash"], root, col, bevel_w=0.05)
    create_cylinder("CoachSteerCol",     (0.58, 5.66, 1.76), 0.042, 0.46, 12, mats["cabin_dash"], root, col, rot_deg=(65, 0, 0))
    create_cylinder("CoachSteerRim",     (0.58, 5.46, 1.90), 0.23, 0.042, 22, mats["black_trim"], root, col, rot_deg=(65, 0, 0))
    create_beveled_box("DriverSeatCush", (0.58, 5.02, 1.54), (0.50, 0.48, 0.14), mats["seat_coach"], root, col, bevel_w=0.03)
    create_beveled_box("DriverSeatBack", (0.58, 4.82, 1.98), (0.48, 0.12, 0.78), mats["seat_coach"], root, col, rot_deg=(-8, 0, 0), bevel_w=0.03)

    # ROOF HVAC AIR-CONDITIONING POD + TWIN ESCAPE HATCHES + GLOWING AMBER LED ROUTE DISPLAY!
    create_beveled_box("RoofHvacPod",     (0, 0.35, 3.68), (1.76, 3.40, 0.30), mats["coach_pearl"], root, col, bevel_w=0.08)
    create_beveled_box("RoofHvacGrille",  (0, 0.35, 3.83), (1.28, 2.50, 0.04), mats["grille_dark"], root, col, bevel_w=0.01)
    create_beveled_box("RoofHatchFront",  (0, 3.45, 3.59), (0.86, 0.72, 0.08), mats["black_trim"], root, col, bevel_w=0.02)
    create_beveled_box("RoofHatchRear",   (0, -3.55, 3.59), (0.86, 0.72, 0.08), mats["black_trim"], root, col, bevel_w=0.02)
    create_beveled_box("CoachLedHousing", (0, 5.96, 3.16), (2.06, 0.16, 0.30), mats["black_trim"], root, col, rot_deg=(16, 0, 0), bevel_w=0.02)
    create_decal_quad("CoachLedDisplay",  (0, 6.06, 3.16), 1.94, 0.24, '+Y', mats["led_coach"], root, col, rot_deg=(16, 0, 0))

    # MODERN TOURING COACH FRONT FASCIA (Chrome Slats, LED Projectors, DRL Strips, Swept Mirrors, Wipers, KDX 057M!)
    create_beveled_box("CoachFrontGrille", (0, 6.62, 1.10), (1.74, 0.08, 0.44), mats["grille_dark"], root, col, bevel_w=0.02)
    for gz in (0.98, 1.10, 1.22):
        create_beveled_box(f"CoachChromeFin_{gz:.2f}", (0, 6.66, gz), (1.60, 0.035, 0.042), mats["chrome"], root, col, bevel_w=0.008)
    create_beveled_box("CoachFrontBumper", (0, 6.58, 0.56), (2.38, 0.26, 0.26), mats["coach_skirt"], root, col, bevel_w=0.045)
    create_beveled_box("PlateFront",       (0, 6.72, 0.56), (0.58, 0.022, 0.15), mats["black_trim"], root, col, bevel_w=0.004)
    create_decal_quad("PlateFrontTex",     (0, 6.736, 0.56), 0.56, 0.14, '+Y', mats["plate_coach"], root, col)

    for side in (-1.0, 1.0):
        # Twin Projector Headlights + Sweeping DRL Bar + Lower Fog Lamp
        create_beveled_box(f"HeadL_{side:.0f}",  (side * 0.96, 6.60, 1.06), (0.38, 0.08, 0.16), mats["headlight"], root, col, bevel_w=0.02)
        create_beveled_box(f"DRL_{side:.0f}",    (side * 0.96, 6.63, 1.17), (0.42, 0.04, 0.035), mats["drl_white"], root, col, bevel_w=0.006)
        create_beveled_box(f"IndF_{side:.0f}",   (side * 1.15, 6.52, 1.06), (0.12, 0.08, 0.16), mats["indicator"], root, col, bevel_w=0.015)
        create_beveled_box(f"FogF_{side:.0f}",   (side * 0.90, 6.66, 0.58), (0.20, 0.06, 0.09), mats["foglight"], root, col, bevel_w=0.015)
        # Aerodynamic Swept Forward Coach Mirrors (iconic touring bus rabbit-ear arms!)
        create_beveled_box(f"CoachMirArm_{side:.0f}",  (side * 1.34, 6.46, 2.22), (0.24, 0.52, 0.07), mats["black_trim"], root, col, rot_deg=(0, 0, side * -18), bevel_w=0.016)
        create_beveled_box(f"CoachMirHead_{side:.0f}", (side * 1.48, 6.70, 2.02), (0.14, 0.18, 0.46), mats["coach_burgundy"], root, col, bevel_w=0.026)
        # Dual resting heavy coach windshield wipers
        create_beveled_box(f"WiperArm_{side:.0f}", (side * 0.44, 6.48, 1.69), (0.86, 0.032, 0.032), mats["black_trim"], root, col, rot_deg=(0, 0, side * -4), bevel_w=0.006)
        # Tall vertical rear LED taillight towers + amber turn signals + dual chrome exhausts!
        create_beveled_box(f"Tail_{side:.0f}",    (side * 1.06, -6.57, 1.38), (0.24, 0.08, 0.62), mats["taillight"], root, col, bevel_w=0.02)
        create_beveled_box(f"IndR_{side:.0f}",    (side * 1.06, -6.57, 0.96), (0.24, 0.08, 0.16), mats["indicator"], root, col, bevel_w=0.015)
        create_cylinder(f"Exhaust_{side:.0f}",    (side * 0.66, -6.60, 0.42), 0.055, 0.30, 14, mats["chrome"], root, col, rot_deg=(90, 0, 0))

    # Rear engine vents, high-mount brake light, Kenyan safety chevron bar, 80 KPH sticker & KDX 057M plate!
    create_beveled_box("RearEngineGrille", (0, -6.56, 1.24), (1.42, 0.06, 0.58), mats["grille_dark"], root, col, bevel_w=0.015)
    create_beveled_box("RearTopBrake",     (0, -6.54, 3.36), (0.82, 0.06, 0.10), mats["taillight"], root, col, bevel_w=0.01)
    create_decal_quad("RearChevronTex",    (0, -6.59, 0.56), 2.16, 0.18, '-Y', mats["chevron"], root, col)
    create_decal_quad("Rear80KphTex",      (0.76, -6.58, 2.30), 0.38, 0.38, '-Y', mats["speed_80"], root, col)
    create_beveled_box("PlateRear",        (0, -6.59, 0.78), (0.58, 0.022, 0.15), mats["black_trim"], root, col, bevel_w=0.004)
    create_decal_quad("PlateRearTex",      (0, -6.606, 0.78), 0.56, 0.14, '-Y', mats["plate_coach"], root, col)

    # 6 Deep-Dish Alcoa Chrome Commercial Coach Wheels (Tri-Axle 6x2!)
    build_commercial_wheel("Wheel_FL", (-1.06, f_y, w_rad), w_rad, 0.32, mats["alcoa"], mats, root, col, is_left=True,  is_dual_rear=False, num_lugs=10)
    build_commercial_wheel("Wheel_FR", ( 1.06, f_y, w_rad), w_rad, 0.32, mats["alcoa"], mats, root, col, is_left=False, is_dual_rear=False, num_lugs=10)
    build_commercial_wheel("Wheel_ML", (-1.04, m_y, w_rad), w_rad, 0.48, mats["alcoa"], mats, root, col, is_left=True,  is_dual_rear=True,  num_lugs=10)
    build_commercial_wheel("Wheel_MR", ( 1.04, m_y, w_rad), w_rad, 0.48, mats["alcoa"], mats, root, col, is_left=False, is_dual_rear=True,  num_lugs=10)
    build_commercial_wheel("Wheel_RL", (-1.06, r_y, w_rad), w_rad, 0.34, mats["alcoa"], mats, root, col, is_left=True,  is_dual_rear=False, num_lugs=10)
    build_commercial_wheel("Wheel_RR", ( 1.06, r_y, w_rad), w_rad, 0.34, mats["alcoa"], mats, root, col, is_left=False, is_dual_rear=False, num_lugs=10)
    return root


# ==============================================================================
# 4. DELIVERABLE #2: PLAYABLE 100%-OWNED KENYAN COMMERCIAL PARCEL LORRY (8.35m FRR)
# ==============================================================================
def build_savanna_parcel_lorry(mats, col):
    """
    100%-Owned Playable Heavy Kenyan Commercial Parcel & Freight Lorry ('Savanna Parcel KDX 409G'):
      - 8.35m long x 2.40m wide x 3.46m tall (FRR / NQR Heavy Parcel Carrier)
      - Sculpted Highland Cobalt Blue cab-over truck w/ full 3D RHD cab interior, wind deflector,
        4-bar chrome grille, solid west-coast + curb mirrors, roof amber clearance lights & cab steps
      - Ribbed freight box w/ SAVANNA PARCEL side banners, rear swing doors w/ chrome cam-lock rods,
        Kenyan reflective chevron bar, 80 KPH sticker, printed KDX 409G plates, diesel & air tanks!
    """
    root = bpy.data.objects.new("Savanna_Parcel_Lorry_FRR", None)
    link_obj(root, None, col)

    w_rad = 0.48
    f_y, r_y = 2.85, -2.05
    cutouts = [(-1.14, f_y, w_rad, w_rad + 0.09, 0.56), (1.14, f_y, w_rad, w_rad + 0.09, 0.56)]

    cab_slices = [
        ( 1.72, 1.06, 0.72, 1.15, 1.24, 1.15, 1.72, 1.06, 2.72), # 0: cab rear wall
        ( 3.55, 1.08, 0.72, 1.16, 1.24, 1.15, 1.72, 1.04, 2.70), # 1: roof brow
        ( 4.02, 1.04, 0.74, 1.13, 1.24, 1.11, 1.66, 1.04, 1.70), # 2: windshield cowl
        ( 4.18, 0.96, 0.80, 1.06, 1.22, 1.04, 1.58, 0.98, 1.62), # 3: front grille fascia
    ]
    build_lofted_shell(
        "Lorry_CabShell", cab_slices, mats["lorry_blue"], mats, root, col,
        side_win_spans=(0,), windshield_span=1,
        wheel_cutouts=cutouts, bevel_offset=0.055,
    )

    # Full 3D RHD Commercial Truck Cab Interior (visible through crystal-clear windshield!)
    create_beveled_box("TruckDash",      (0, 3.60, 1.64), (1.92, 0.56, 0.34), mats["cabin_dash"], root, col, bevel_w=0.04)
    create_cylinder("TruckSteerCol",     (0.54, 3.34, 1.72), 0.038, 0.38, 12, mats["cabin_dash"], root, col, rot_deg=(68, 0, 0))
    create_cylinder("TruckSteerRim",     (0.54, 3.16, 1.84), 0.21, 0.038, 20, mats["black_trim"], root, col, rot_deg=(68, 0, 0))
    for sx in (-0.54, 0.0, 0.54):
        create_beveled_box(f"TruckSeatC_{sx:.2f}", (sx, 2.58, 1.46), (0.46, 0.44, 0.14), mats["seat_truck"], root, col, bevel_w=0.03)
        create_beveled_box(f"TruckSeatB_{sx:.2f}", (sx, 2.38, 1.88), (0.44, 0.12, 0.68), mats["seat_truck"], root, col, rot_deg=(-8, 0, 0), bevel_w=0.03)

    # True Aerodynamic Sloped Roof Wind Deflector Wedge sitting 100% flush on cab roof (ZERO window overlap!) + 3 Amber Clearance Lights!
    create_wind_deflector_wedge("CabWindDeflector", y_back=1.74, y_front=3.34, z_base=2.70, z_top_back=3.44, z_top_front=2.82, half_w_back=1.10, half_w_front=0.98, mat=mats["cargo_white"], parent=root, col=col)
    for mx in (-0.64, 0.0, 0.64):
        create_beveled_box(f"RoofMarker_{mx:.2f}", (mx, 3.52, 2.74), (0.14, 0.10, 0.06), mats["indicator"], root, col, bevel_w=0.01)

    # Bold 4-Bar Chrome Truck Front Grille + Heavy Steel Bumper + Fog Lamps + Tow Hooks + KDX 409G Plate!
    create_beveled_box("TruckGrilleDark", (0, 4.16, 1.32), (1.78, 0.08, 0.42), mats["grille_dark"], root, col, bevel_w=0.02)
    for gz in (1.18, 1.27, 1.36, 1.45):
        create_beveled_box(f"TruckGrilleChrome_{gz:.2f}", (0, 4.20, gz), (1.66, 0.035, 0.038), mats["chrome"], root, col, bevel_w=0.008)
    create_beveled_box("TruckFrontBumper", (0, 4.16, 0.78), (2.28, 0.26, 0.34), mats["black_trim"], root, col, bevel_w=0.04)
    create_beveled_box("PlateFront",       (0, 4.30, 0.76), (0.56, 0.022, 0.14), mats["black_trim"], root, col, bevel_w=0.004)
    create_decal_quad("PlateFrontTex",     (0, 4.316, 0.76), 0.54, 0.13, '+Y', mats["plate_lorry"], root, col)

    # Solid West-Coast Heavy Truck Mirrors + Boarding Steps + Door Grab Handles + Dual Wipers!
    for side in (-1.0, 1.0):
        create_cylinder(f"WestMirrorPost_{side:.0f}",   (side * 1.34, 3.60, 2.18), 0.022, 0.74, 10, mats["black_trim"], root, col)
        create_cylinder(f"WestMirrorTopArm_{side:.0f}", (side * 1.20, 3.60, 2.54), 0.020, 0.32, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        create_cylinder(f"WestMirrorBotArm_{side:.0f}", (side * 1.20, 3.60, 1.82), 0.020, 0.32, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        create_beveled_box(f"WestMirrorHead_{side:.0f}", (side * 1.39, 3.64, 2.18), (0.09, 0.16, 0.44), mats["black_trim"], root, col, bevel_w=0.018)
        create_beveled_box(f"CabStep_{side:.0f}",        (side * 1.12, 2.02, 0.60), (0.12, 0.42, 0.055), mats["steel_rim"], root, col, bevel_w=0.01)
        create_beveled_box(f"DoorHandle_{side:.0f}",     (side * 1.16, 2.28, 1.56), (0.05, 0.16, 0.04), mats["black_trim"], root, col, bevel_w=0.008)
        create_beveled_box(f"HeadL_{side:.0f}",          (side * 0.82, 4.29, 0.84), (0.34, 0.06, 0.15), mats["headlight"], root, col, bevel_w=0.018)
        create_beveled_box(f"IndF_{side:.0f}",           (side * 1.03, 4.28, 0.84), (0.11, 0.06, 0.15), mats["indicator"], root, col, bevel_w=0.014)
        create_beveled_box(f"FogF_{side:.0f}",           (side * 0.54, 4.29, 0.72), (0.16, 0.05, 0.08), mats["foglight"], root, col, bevel_w=0.012)
        create_beveled_box(f"WiperArm_{side:.0f}",       (side * 0.38, 4.04, 1.73), (0.72, 0.028, 0.028), mats["black_trim"], root, col, rot_deg=(0, 0, side * -5), bevel_w=0.005)

    # HEAVY STEEL CHASSIS RAILS, ALUMINUM DIESEL TANK, AIR BRAKE RESERVOIRS, SIDE GUARDS & MUDFLAPS!
    for sx in (-0.54, 0.54):
        create_beveled_box(f"ChassisRail_{sx:.2f}", (sx, -0.82, 0.78), (0.16, 6.80, 0.24), mats["black_trim"], root, col, bevel_w=0.012)
    create_cylinder("AluminumDieselTank", (-0.94, 0.42, 0.74), 0.28, 1.48, 18, mats["alcoa"], root, col, rot_deg=(90, 0, 0), bevel_w=0.025)
    create_cylinder("AirBrakeTankA",      ( 0.86, 0.64, 0.74), 0.14, 0.84, 14, mats["black_trim"], root, col, rot_deg=(90, 0, 0), bevel_w=0.015)
    create_beveled_box("BatteryBox",      ( 0.88, -0.20, 0.74), (0.36, 0.62, 0.34), mats["black_trim"], root, col, bevel_w=0.02)
    create_cylinder("ExhaustPipe",        ( 0.72, -3.90, 0.56), 0.055, 0.42, 12, mats["chrome"], root, col, rot_deg=(90, 0, 0))
    for side in (-1.0, 1.0):
        create_beveled_box(f"SideGuardTop_{side:.0f}", (side * 1.14, 0.40, 0.68), (0.06, 2.75, 0.10), mats["steel_rim"], root, col, bevel_w=0.01)
        create_beveled_box(f"SideGuardBot_{side:.0f}", (side * 1.14, 0.40, 0.50), (0.06, 2.75, 0.10), mats["steel_rim"], root, col, bevel_w=0.01)
        create_beveled_box(f"RearMudflap_{side:.0f}",  (side * 1.04, r_y - 0.62, 0.42), (0.50, 0.04, 0.56), mats["black_trim"], root, col, bevel_w=0.008)

    # TALL RIBBED EXPRESS PARCEL FREIGHT BOX + SAVANNA PARCEL BANNERS + REAR SWING DOORS & CAM-LOCK RODS!
    create_beveled_box("FreightBoxBody",  (0, -1.34, 2.24), (2.38, 5.62, 2.40), mats["cargo_white"], root, col, bevel_w=0.05)
    create_beveled_box("FreightRoofRail", (0, -1.34, 3.45), (2.42, 5.68, 0.09), mats["alcoa"], root, col, bevel_w=0.02)
    create_beveled_box("FreightFloorSub", (0, -1.34, 1.02), (2.40, 5.66, 0.12), mats["black_trim"], root, col, bevel_w=0.02)
    create_beveled_box("SideYellowStrip", (0, -1.34, 1.16), (2.41, 5.56, 0.14), mats["psv_yellow"], root, col, bevel_w=0.01)
    for ry in (-3.6, -2.8, -2.0, -1.2, -0.4, 0.4, 1.1):
        create_beveled_box(f"FreightRib_{ry:.1f}", (0, ry, 2.24), (2.40, 0.055, 2.26), mats["steel_rim"], root, col, bevel_w=0.008)
    for side in (-1.0, 1.0):
        create_decal_quad(f"ParcelBanner_{side:.0f}", (side * 1.206, -1.34, 2.38), 4.20, 1.05, '-X' if side < 0 else '+X', mats["banner_parcel"], root, col)

    # Rear Swing Cargo Doors w/ 4 Chrome Vertical Cam-Lock Rods + Rear Bumper + Chevron Bar + 80 KPH + KDX 409G!
    create_beveled_box("RearDoorSeam", (0, -4.16, 2.24), (0.04, 0.04, 2.32), mats["black_trim"], root, col, bevel_w=0.005)
    for rx in (-0.72, -0.24, 0.24, 0.72):
        create_cylinder(f"CamLockRod_{rx:.2f}", (rx, -4.18, 2.24), 0.018, 2.22, 10, mats["chrome"], root, col)
    create_beveled_box("RearUnderrunBar", (0, -4.14, 0.56), (2.26, 0.18, 0.22), mats["black_trim"], root, col, bevel_w=0.025)
    create_decal_quad("RearChevronTex",   (0, -4.175, 1.16), 2.26, 0.20, '-Y', mats["chevron"], root, col)
    create_decal_quad("Rear80KphTex",     (0.80, -4.175, 2.98), 0.36, 0.36, '-Y', mats["speed_80"], root, col)
    create_beveled_box("PlateRear",       (0, -4.24, 0.56), (0.56, 0.022, 0.14), mats["black_trim"], root, col, bevel_w=0.004)
    create_decal_quad("PlateRearTex",     (0, -4.256, 0.56), 0.54, 0.13, '-Y', mats["plate_lorry"], root, col)
    for side in (-1.0, 1.0):
        create_beveled_box(f"Tail_{side:.0f}", (side * 0.94, -4.22, 0.74), (0.34, 0.08, 0.18), mats["taillight"], root, col, bevel_w=0.018)
        create_beveled_box(f"IndR_{side:.0f}", (side * 1.14, -4.22, 0.74), (0.12, 0.08, 0.18), mats["indicator"], root, col, bevel_w=0.014)

    # 4 Heavy Commercial 6-Lug Wheels (Single Steer Front + Wide Dual Rear Drive!)
    build_commercial_wheel("Wheel_FL", (-1.04, f_y, w_rad), w_rad, 0.30, mats["steel_rim"], mats, root, col, is_left=True,  is_dual_rear=False, num_lugs=6)
    build_commercial_wheel("Wheel_FR", ( 1.04, f_y, w_rad), w_rad, 0.30, mats["steel_rim"], mats, root, col, is_left=False, is_dual_rear=False, num_lugs=6)
    build_commercial_wheel("Wheel_RL", (-1.02, r_y, w_rad), w_rad, 0.48, mats["steel_rim"], mats, root, col, is_left=True,  is_dual_rear=True,  num_lugs=6)
    build_commercial_wheel("Wheel_RR", ( 1.02, r_y, w_rad), w_rad, 0.48, mats["steel_rim"], mats, root, col, is_left=False, is_dual_rear=True,  num_lugs=6)
    return root


# ==============================================================================
# 5. EXPORT + SHOWROOM BLEND (SCRIPTING TAB + MATERIAL PREVIEW) + RENDER
# ==============================================================================
def select_hierarchy(root_obj):
    bpy.ops.object.select_all(action='DESELECT')
    def walk(o):
        o.select_set(True)
        for ch in o.children:
            walk(ch)
    walk(root_obj)
    bpy.context.view_layer.objects.active = root_obj


def export_hero_glb(root_obj, filename):
    saved_loc = root_obj.location.copy()
    saved_rot = root_obj.rotation_euler.copy()

    # Rotate 180 deg around Z during export so Blender +Y forward -> glTF +Z -> Unity glTFast +Z FORWARD!
    root_obj.location = Vector((0.0, 0.0, 0.0))
    root_obj.rotation_euler = Euler((0.0, 0.0, math.pi), 'XYZ')
    bpy.context.view_layer.update()
    select_hierarchy(root_obj)
    out_path = os.path.join(OWNED_FLEET_DIR, filename)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials='EXPORT',
    )

    root_obj.location = saved_loc
    root_obj.rotation_euler = saved_rot
    bpy.context.view_layer.update()
    print(f"EXPORTED HERO GLB (+Z Unity Forward): {filename} ({os.path.getsize(out_path) // 1024} KB)")


def setup_hero_showroom_and_render(coach, lorry):
    coach.location = Vector((-3.2, -0.4, 0.0))
    coach.rotation_euler = Euler((0.0, 0.0, math.radians(-16.0)), 'XYZ')
    lorry.location = Vector(( 3.6, 1.4, 0.0))
    lorry.rotation_euler = Euler((0.0, 0.0, math.radians(-24.0)), 'XYZ')

    bpy.ops.mesh.primitive_plane_add(size=56.0, location=(0, 0, 0))
    floor = bpy.context.active_object
    floor.name = "HeroShowroom_Tarmac"
    floor.data.materials.append(make_mat("Showroom_Floor", (0.14, 0.15, 0.17), metallic=0.05, roughness=0.40))

    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.58, 0.74, 0.94, 1.0)
        bg.inputs[1].default_value = 0.88

    sun_data = bpy.data.lights.new("SunKey", type='SUN')
    sun_data.energy = 3.8
    sun_obj = bpy.data.objects.new("SunKey", sun_data)
    sun_obj.rotation_euler = Euler((math.radians(46), math.radians(16), math.radians(-38)), 'XYZ')
    bpy.context.scene.collection.objects.link(sun_obj)

    cam_data = bpy.data.cameras.new("HeroShowroomCam")
    cam_data.lens = 38.0
    cam_obj = bpy.data.objects.new("HeroShowroomCam", cam_data)
    cam_obj.location = Vector((11.8, 13.8, 5.2))
    direction = Vector((-0.2, 0.4, 1.65)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj

    # Pre-load this generator script into Blender's Scripting Text Editor + set Material Preview!
    script_disk_path = os.path.join(REPO_ROOT, "tools", "blender_build_playable_fleet.py")
    loaded_txt = None
    if os.path.isfile(script_disk_path):
        loaded_txt = bpy.data.texts.get("blender_build_playable_fleet.py") or bpy.data.texts.load(script_disk_path)

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                for space in area.spaces:
                    if space.type == 'VIEW_3D':
                        space.shading.type = 'MATERIAL'
                        space.region_3d.view_perspective = 'CAMERA'
            elif area.type == 'TEXT_EDITOR' and loaded_txt is not None:
                for space in area.spaces:
                    if space.type == 'TEXT_EDITOR':
                        space.text = loaded_txt
                        space.show_line_numbers = True
                        space.show_syntax_highlight = True

    blend_path = os.path.join(STUDIO_DIR, "Kenya_Owned_Playable_Fleet_Studio.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"SAVED HERO PLAYABLE BLEND: {blend_path}")

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    if bpy.app.background and len(sys.argv) > 1 and sys.argv[-1].lower().endswith(".png"):
        scene.render.filepath = PREVIEW_PNG
        scene.render.image_settings.file_format = 'PNG'
        bpy.ops.render.render(write_still=True)
        print(f"RENDERED HERO SHOWROOM: {PREVIEW_PNG}")


def main():
    if bpy.app.background:
        bpy.ops.wm.read_factory_settings(use_empty=False)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    for mat in list(bpy.data.materials):
        if mat.users == 0:
            bpy.data.materials.remove(mat)

    mats = init_materials()
    col = bpy.context.scene.collection

    coach = build_savanna_express_coach(mats, col)
    export_hero_glb(coach, "Savanna_Express_Coach_2026.glb")

    lorry = build_savanna_parcel_lorry(mats, col)
    export_hero_glb(lorry, "Savanna_Parcel_Lorry_FRR.glb")

    setup_hero_showroom_and_render(coach, lorry)


if __name__ == "__main__":
    main()
