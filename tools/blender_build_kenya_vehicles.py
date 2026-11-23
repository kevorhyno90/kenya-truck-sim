"""
Kenya Street Sim — Blender 5.3 Professional Automotive 3D Vehicle Studio (Path A: 100% Owned)
v3 Studio Grade:
  - Genuine automotive bmesh lofting + crisp multi-segment beveling + INSET RECESSED WINDOWS
    (real painted A/B/C pillars, black rubber window gaskets & roof rails)
  - Boolean circular wheel-arch cutouts + fender flares (SUV / Pickup)
  - Deep-dish 3D 5/6-spoke alloy wheels (open hollow barrel, dark recess, brake rotor & caliper,
    chrome hubcap — merged into 1 clean multi-material mesh per wheel for 100% smooth Unity spinning!)
  - Windshield wipers, door handles, fog lights, Canter cab wind deflector & solid mirror brackets,
    Matatu PSV destination board & bold chrome bullbar, exhaust tips & Kenyan plates!
  - Exact Unity +Z forward alignment (180-deg Z export pre-rotation for Blender -> glTF -> Unity glTFast!)

Outputs 6 100%-owned Unity-ready .glb vehicles directly into ImportedModels/RealTraffic/
plus editable master showroom: KenyaStreetSim/BlenderStudio/Kenya_Owned_Commercial_Traffic_Pack.blend
"""

import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector, Euler, Matrix

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
UNITY_TRAFFIC_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "ImportedModels", "RealTraffic")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
PREVIEW_PNG = sys.argv[-1] if (len(sys.argv) > 1 and sys.argv[-1].lower().endswith(".png")) else os.path.join(STUDIO_DIR, "kenya_fleet_showroom.png")

os.makedirs(UNITY_TRAFFIC_DIR, exist_ok=True)
os.makedirs(STUDIO_DIR, exist_ok=True)


# ==============================================================================
# 1. AUTOMOTIVE PBR MATERIALS (Clearcoat Metallic Paint, Glossy Glass, Chrome)
# ==============================================================================
def make_mat(name, base_col, metallic=0.0, roughness=0.4, coat=0.0, emission=None, emission_strength=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name=name)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*base_col, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = 0.05
    if emission is not None and emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def init_materials():
    return {
        "pearl_white":  make_mat("Paint_PearlWhite",  (0.90, 0.92, 0.94), metallic=0.18, roughness=0.18, coat=1.0),
        "silver":       make_mat("Paint_Silver",      (0.64, 0.68, 0.74), metallic=0.72, roughness=0.20, coat=1.0),
        "navy":         make_mat("Paint_MidnightNavy",(0.05, 0.12, 0.28), metallic=0.55, roughness=0.18, coat=1.0),
        "maroon":       make_mat("Paint_WineMaroon",  (0.42, 0.05, 0.08), metallic=0.48, roughness=0.18, coat=1.0),
        "gunmetal":     make_mat("Paint_Gunmetal",    (0.15, 0.18, 0.22), metallic=0.68, roughness=0.20, coat=1.0),
        "canter_blue":  make_mat("Paint_CanterBlue",  (0.08, 0.28, 0.62), metallic=0.35, roughness=0.22, coat=0.9),
        "cargo_white":  make_mat("Cargo_BoxWhite",    (0.84, 0.86, 0.89), metallic=0.10, roughness=0.36, coat=0.2),
        "psv_yellow":   make_mat("Matatu_PSV_Yellow", (0.98, 0.76, 0.02), metallic=0.08, roughness=0.24, coat=0.8),
        "matatu_teal":  make_mat("Matatu_LowerTeal",  (0.04, 0.36, 0.44), metallic=0.28, roughness=0.22, coat=0.9),
        "glass":        make_mat("Auto_TintedGlass",  (0.03, 0.06, 0.09), metallic=0.32, roughness=0.04, coat=1.0),
        "black_trim":   make_mat("Auto_BlackTrim",    (0.055, 0.06, 0.07), metallic=0.10, roughness=0.48),
        "wheel_cavity": make_mat("Auto_WheelCavity",  (0.015, 0.016, 0.02), metallic=0.10, roughness=0.75),
        "grille_dark":  make_mat("Auto_GrilleDark",   (0.022, 0.025, 0.030), metallic=0.22, roughness=0.42),
        "chrome":       make_mat("Auto_Chrome",       (0.88, 0.91, 0.94), metallic=0.98, roughness=0.07),
        "alloy":        make_mat("Auto_AlloyRim",     (0.76, 0.80, 0.85), metallic=0.92, roughness=0.15),
        "tire":         make_mat("Auto_TireRubber",   (0.036, 0.038, 0.044), metallic=0.02, roughness=0.82),
        "brake_disc":   make_mat("Auto_BrakeDisc",    (0.18, 0.19, 0.22), metallic=0.80, roughness=0.45),
        "caliper_red":  make_mat("Auto_BrakeCaliper", (0.68, 0.06, 0.06), metallic=0.30, roughness=0.32),
        "headlight":    make_mat("Auto_Headlight",    (0.98, 0.96, 0.88), metallic=0.12, roughness=0.06, emission=(1.0, 0.95, 0.82), emission_strength=2.2),
        "foglight":     make_mat("Auto_Foglight",     (0.95, 0.92, 0.82), metallic=0.10, roughness=0.08, emission=(0.95, 0.90, 0.72), emission_strength=1.1),
        "indicator":    make_mat("Auto_IndicatorAmb", (0.96, 0.50, 0.04), metallic=0.08, roughness=0.14, emission=(1.0, 0.52, 0.04), emission_strength=1.4),
        "taillight":    make_mat("Auto_TaillightRed", (0.86, 0.04, 0.04), metallic=0.08, roughness=0.12, emission=(0.95, 0.05, 0.04), emission_strength=2.2),
        "plate_yellow": make_mat("Kenya_PlateYellow", (0.96, 0.78, 0.08), metallic=0.05, roughness=0.30),
        "reflect_red":  make_mat("Kenya_ReflectRed",  (0.92, 0.08, 0.06), metallic=0.10, roughness=0.18, emission=(0.8, 0.05, 0.04), emission_strength=0.6),
        "dest_amber":   make_mat("Matatu_DestBoard",  (0.98, 0.68, 0.06), metallic=0.05, roughness=0.18, emission=(1.0, 0.68, 0.08), emission_strength=1.5),
    }


# ==============================================================================
# 2. TRUE AUTOMOTIVE LOFT + INSET RECESSED WINDOWS + CRISP BEVEL + WHEEL ARCHES
# ==============================================================================
def link_obj(obj, parent, col):
    col.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def smooth_obj(obj, angle_deg=34.0):
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


def create_fender_flare(name, loc, radius, thickness, depth, mat, parent, col):
    """Creates a crisp semi-circular black rubber 4x4 wheel-arch fender flare."""
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    segs = 18
    r_in = radius
    r_out = radius + thickness
    hd = depth * 0.5
    rings = []
    for i in range(segs + 1):
        ang = math.radians(-10.0 + (200.0 * i / segs))
        cy, cz = math.cos(ang), math.sin(ang)
        pts = [
            (-hd, cy * r_in,  cz * r_in),
            ( hd, cy * r_in,  cz * r_in),
            ( hd, cy * r_out, cz * r_out),
            (-hd, cy * r_out, cz * r_out),
        ]
        rings.append([bm.verts.new(Vector(p)) for p in pts])
    for i in range(segs):
        r0, r1 = rings[i], rings[i + 1]
        for k in range(4):
            kn = (k + 1) % 4
            bm.faces.new((r0[k], r0[kn], r1[kn], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(loc)
    obj.data.materials.append(mat)
    link_obj(obj, parent, col)
    smooth_obj(obj, 42.0)
    return obj


def build_automotive_body(
    name,
    slices,
    paint_mat,
    mats,
    parent,
    col,
    side_win_spans=(),
    windshield_span=None,
    rear_glass_span=None,
    rear_cap_glass=False,
    belt_stripe_mat=None,
    lower_skirt_mat=None,
    wheel_cutouts=None,
):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()

    # Material slots: 0=paint, 1=glass, 2=black_trim, 3=belt_stripe, 4=lower_skirt
    slot_mats = [paint_mat, mats["glass"], mats["black_trim"]]
    if belt_stripe_mat is not None:
        slot_mats.append(belt_stripe_mat)
    if lower_skirt_mat is not None:
        slot_mats.append(lower_skirt_mat)

    rings = []
    for (y, hws, zs, hwm, zm, hwb, zb, hwr, zr) in slices:
        pts = [
            (-hws, y, zs),
            (-hwm, y, zm),
            (-hwb, y, zb),
            (-hwr, y, zr),
            ( hwr, y, zr),
            ( hwb, y, zb),
            ( hwm, y, zm),
            ( hws, y, zs),
        ]
        rings.append([bm.verts.new(Vector(p)) for p in pts])

    side_glass_faces = []
    front_glass_faces = []
    rear_glass_faces = []

    ring_n = 8
    for s in range(len(rings) - 1):
        r0 = rings[s]
        r1 = rings[s + 1]
        for i in range(ring_n):
            inext = (i + 1) % ring_n
            f = bm.faces.new((r0[i], r0[inext], r1[inext], r1[i]))
            f.material_index = 0

            if belt_stripe_mat is not None and i in (1, 5):
                f.material_index = 3
            if lower_skirt_mat is not None and i in (0, 6):
                f.material_index = 4 if belt_stripe_mat is not None else 3

            if s in side_win_spans and i in (2, 4):
                side_glass_faces.append(f)
            if windshield_span is not None and s == windshield_span and i == 3:
                front_glass_faces.append(f)
            if rear_glass_span is not None and s == rear_glass_span and i == 3:
                rear_glass_faces.append(f)

    if rear_cap_glass:
        r0 = rings[0]
        bm.faces.new((r0[0], r0[7], r0[6], r0[5], r0[2], r0[1])).material_index = 0
        f_rw = bm.faces.new((r0[2], r0[5], r0[4], r0[3]))
        f_rw.material_index = 0
        rear_glass_faces.append(f_rw)
    else:
        bm.faces.new(list(reversed(rings[0]))).material_index = 0

    bm.faces.new(rings[-1]).material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    def inset_window_group(faces, frame_thick, recess_depth):
        if not faces:
            return
        bmesh.ops.inset_individual(bm, faces=faces, thickness=frame_thick, depth=0.0, use_even_offset=True)
        res2 = bmesh.ops.inset_individual(bm, faces=faces, thickness=0.018, depth=-recess_depth, use_even_offset=True)
        for sf in res2.get("faces", []):
            sf.material_index = 2
        for gf in faces:
            gf.material_index = 1

    inset_window_group(side_glass_faces,  frame_thick=0.068, recess_depth=0.022)
    inset_window_group(front_glass_faces, frame_thick=0.072, recess_depth=0.022)
    inset_window_group(rear_glass_faces,  frame_thick=0.072, recess_depth=0.020)

    outer_edges = [
        e for e in bm.edges
        if len(e.link_faces) == 2
        and e.link_faces[0].material_index == 0 and e.link_faces[1].material_index == 0
        and e.calc_face_angle(0.0) > math.radians(18.0)
    ]
    if outer_edges:
        bmesh.ops.bevel(bm, geom=outer_edges, offset=0.048, segments=3, affect='EDGES', clamp_overlap=True)

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


def build_realistic_wheel(name, loc, radius, width, spokes, mats, parent, col, is_left=True):
    """
    Builds a SINGLE unified multi-material deep-dish 3D alloy wheel mesh centered at (0,0,0)
    with origin at exact axle center `loc`!
      Slot 0: Tire rubber (profiled rounded tread & sidewall)
      Slot 1: Alloy rim lip & 5/6 recessed 3D star spokes
      Slot 2: Dark inner wheel cavity backing (makes spokes pop sharply!)
      Slot 3: Metallic brake rotor disc
      Slot 4: Chrome center hubcap
      Slot 5: Brake caliper accent
    """
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm = bmesh.new()
    out_sign = -1.0 if is_left else 1.0
    hw = width * 0.5
    rim_r = radius * 0.62
    segs = 32

    def add_lathe_profile(profile_pts, mat_idx, closed_loop=True):
        rings = []
        for s in range(segs):
            ang = (s / segs) * math.pi * 2.0
            cy, cz = math.cos(ang), math.sin(ang)
            rings.append([bm.verts.new(Vector((px, cy * pr, cz * pr))) for (px, pr) in profile_pts])
        pn = len(profile_pts)
        p_count = pn if closed_loop else (pn - 1)
        for s in range(segs):
            sn = (s + 1) % segs
            for p in range(p_count):
                pnxt = (p + 1) % pn
                f = bm.faces.new((rings[s][p], rings[sn][p], rings[sn][pnxt], rings[s][pnxt]))
                f.material_index = mat_idx

    # 1. Profiled rubber tire (slot 0)
    tire_profile = [
        (-hw * 0.84, rim_r * 0.96),
        (-hw * 0.98, radius * 0.78),
        (-hw * 1.00, radius * 0.92),
        (-hw * 0.80, radius),
        ( hw * 0.80, radius),
        ( hw * 1.00, radius * 0.92),
        ( hw * 0.98, radius * 0.78),
        ( hw * 0.84, rim_r * 0.96),
    ]
    add_lathe_profile(tire_profile, mat_idx=0, closed_loop=True)

    # 2. Hollow outer alloy rim lip ring (slot 1) — bright outer lip, dark recessed inner barrel!
    lip_in = rim_r * 0.86
    lip_out = rim_r * 1.01
    rim_lip_profile = [
        (out_sign * hw * 0.86, lip_out),
        (out_sign * hw * 0.92, (lip_out + lip_in) * 0.5),
        (out_sign * hw * 0.84, lip_in),
        (out_sign * hw * 0.52, lip_in * 0.97),
        (out_sign * hw * 0.52, lip_out),
    ]
    add_lathe_profile(rim_lip_profile, mat_idx=1, closed_loop=True)

    # Dark inner rim barrel cavity (slot 2) behind the spokes for high-contrast 3D depth!
    inner_barrel_profile = [
        (out_sign * hw * 0.54, lip_in * 0.98),
        (-out_sign * hw * 0.82, lip_in * 0.96),
        (-out_sign * hw * 0.84, lip_out),
        (out_sign * hw * 0.54, lip_out),
    ]
    add_lathe_profile(inner_barrel_profile, mat_idx=2, closed_loop=True)

    # Helper to append a transformed primitive directly into this wheel's bmesh with material slot
    def add_cyl_part(x_center, r, depth, cyl_segs, mat_idx):
        ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=cyl_segs, radius1=r, radius2=r, depth=depth)
        rot_m = Matrix.Rotation(math.radians(90.0), 4, 'Y')
        tr_m = Matrix.Translation(Vector((x_center, 0.0, 0.0)))
        bmesh.ops.transform(bm, matrix=tr_m @ rot_m, verts=ret["verts"])
        for f in set(f for v in ret["verts"] for f in v.link_faces):
            f.material_index = mat_idx

    def add_box_part(center, size, rot_x_rad, mat_idx):
        ret = bmesh.ops.create_cube(bm, size=1.0)
        sc_m = Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        rx_m = Matrix.Rotation(rot_x_rad, 4, 'X')
        tr_m = Matrix.Translation(Vector(center))
        bmesh.ops.transform(bm, matrix=tr_m @ rx_m @ sc_m, verts=ret["verts"])
        for f in set(f for v in ret["verts"] for f in v.link_faces):
            f.material_index = mat_idx

    # 3. Dark inner wheel backing disc (slot 2) — recessed deep behind brake rotor for dramatic contrast!
    add_cyl_part(-out_sign * hw * 0.42, lip_in * 0.98, 0.03, 24, mat_idx=2)

    # 4. Metallic brake rotor disc (slot 3) & brake caliper (slot 5) behind the spokes!
    add_cyl_part(-out_sign * hw * 0.14, radius * 0.38, 0.028, 22, mat_idx=3)
    add_box_part((out_sign * hw * 0.02, 0.0, radius * 0.32), (0.045, radius * 0.22, radius * 0.14), 0.0, mat_idx=5)

    # 5. Crisp 5/6-spoke 3D alloy star spokes sitting proudly in front of the dark cavity! (slot 1)
    spoke_x = out_sign * hw * 0.64
    for sp in range(spokes):
        ang = (sp / spokes) * math.pi * 2.0
        spoke_len = lip_in * 0.90
        r_mid = spoke_len * 0.50
        add_box_part(
            (spoke_x, math.cos(ang) * r_mid, math.sin(ang) * r_mid),
            (width * 0.14, spoke_len, radius * 0.095),
            ang,
            mat_idx=1,
        )

    # 6. Center alloy hub boss + chrome center cap (slot 1 & slot 4)
    add_cyl_part(out_sign * hw * 0.62, radius * 0.18, width * 0.18, 18, mat_idx=1)
    add_cyl_part(out_sign * hw * 0.72, radius * 0.11, 0.030, 16, mat_idx=4)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    wheel_obj = bpy.data.objects.new(name, mesh)
    wheel_obj.location = Vector(loc)
    for m in (mats["tire"], mats["alloy"], mats["wheel_cavity"], mats["brake_disc"], mats["chrome"], mats["caliper_red"]):
        wheel_obj.data.materials.append(m)
    link_obj(wheel_obj, parent, col)
    smooth_obj(wheel_obj, 42.0)
    return wheel_obj


def add_wheel_set(parent, col, half_track, front_y, rear_y, radius, width, spokes, mats):
    build_realistic_wheel("Wheel_FL", (-half_track, front_y, radius), radius, width, spokes, mats, parent, col, is_left=True)
    build_realistic_wheel("Wheel_FR", ( half_track, front_y, radius), radius, width, spokes, mats, parent, col, is_left=False)
    build_realistic_wheel("Wheel_RL", (-half_track, rear_y,  radius), radius, width, spokes, mats, parent, col, is_left=True)
    build_realistic_wheel("Wheel_RR", ( half_track, rear_y,  radius), radius, width, spokes, mats, parent, col, is_left=False)


def add_wipers_handles_exhaust(parent, col, half_w, cowl_y, cowl_z, handle_ys, handle_z, rear_y, exhaust_z, handle_mat, mats):
    """Adds dual front windshield wipers, left/right door handles, and chrome rear exhaust pipe."""
    # Dual front windshield wipers resting at windshield cowl
    for wx in (-half_w * 0.36, half_w * 0.24):
        create_beveled_box(f"WiperArm_{wx:.2f}", (wx, cowl_y + 0.02, cowl_z + 0.02), (half_w * 0.58, 0.024, 0.024), mats["black_trim"], parent, col, rot_deg=(0, 0, -6), bevel_w=0.004)
    # Crisp horizontal door handles on both sides
    for hy in handle_ys:
        for side in (-1.0, 1.0):
            create_beveled_box(f"Handle_{side:.0f}_{hy:.2f}", (side * (half_w + 0.015), hy, handle_z), (0.04, 0.14, 0.036), handle_mat, parent, col, bevel_w=0.008)
    # Chrome rear exhaust tailpipe (right-rear underneath bumper)
    create_cylinder("ExhaustTip", (half_w * 0.56, rear_y - 0.04, exhaust_z), 0.038, 0.22, 12, mats["chrome"], parent, col, rot_deg=(90, 0, 0))


def add_mirrors_lights_plates(parent, col, half_w, mir_y, mir_z, front_y, rear_y, head_z, tail_z, plate_f_z, plate_r_z, mir_mat, mats, mir_scale=1.0, fog_z=None):
    for side in (-1.0, 1.0):
        create_beveled_box(f"MirArm_{side}", (side * (half_w + 0.05 * mir_scale), mir_y, mir_z), (0.14 * mir_scale, 0.045, 0.045), mats["black_trim"], parent, col, bevel_w=0.008)
        create_beveled_box(f"MirCap_{side}", (side * (half_w + 0.14 * mir_scale), mir_y - 0.02, mir_z + 0.02), (0.16 * mir_scale, 0.10 * mir_scale, 0.12 * mir_scale), mir_mat, parent, col, bevel_w=0.022)
        create_beveled_box(f"HeadL_{side}",  (side * half_w * 0.66, front_y, head_z), (0.30, 0.07, 0.14), mats["headlight"], parent, col, bevel_w=0.02)
        create_beveled_box(f"IndF_{side}",   (side * half_w * 0.86, front_y - 0.04, head_z), (0.10, 0.07, 0.13), mats["indicator"], parent, col, bevel_w=0.015)
        create_beveled_box(f"Tail_{side}",   (side * half_w * 0.72, rear_y, tail_z), (0.26, 0.07, 0.22), mats["taillight"], parent, col, bevel_w=0.02)
        if fog_z is not None:
            create_beveled_box(f"FogL_{side}", (side * half_w * 0.70, front_y + 0.02, fog_z), (0.14, 0.05, 0.07), mats["foglight"], parent, col, bevel_w=0.012)
    create_beveled_box("PlateFront", (0, front_y + 0.03, plate_f_z), (0.48, 0.025, 0.12), mats["plate_yellow"], parent, col, bevel_w=0.004)
    create_beveled_box("PlateRear",  (0, rear_y - 0.03,  plate_r_z), (0.48, 0.025, 0.12), mats["plate_yellow"], parent, col, bevel_w=0.004)


# ==============================================================================
# 3. THE 6 STUDIO-GRADE 100%-OWNED KENYAN VEHICLES
# ==============================================================================
def build_kenya_matatu_14seater(mats, col):
    """Iconic Nairobi 14-Seater PSV Matatu with painted yellow PSV belt, destination board & bold chrome bullbar."""
    root = bpy.data.objects.new("Kenya_Matatu_14Seater_PSV", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.37, 1.54, -1.48
    cutouts = [(-0.96, f_y, w_rad, w_rad + 0.07, 0.44), (0.96, f_y, w_rad, w_rad + 0.07, 0.44),
               (-0.96, r_y, w_rad, w_rad + 0.07, 0.44), (0.96, r_y, w_rad, w_rad + 0.07, 0.44)]
    slices = [
        (-2.50, 0.90, 0.34, 0.95, 0.92, 0.95, 1.24, 0.84, 2.12), # 0: rear
        (-1.25, 0.92, 0.32, 0.97, 0.92, 0.97, 1.24, 0.86, 2.16), # 1: rear pillar
        ( 0.05, 0.92, 0.32, 0.97, 0.92, 0.97, 1.24, 0.86, 2.16), # 2: mid pillar
        ( 1.15, 0.92, 0.32, 0.97, 0.92, 0.97, 1.24, 0.85, 2.14), # 3: B-pillar
        ( 1.85, 0.91, 0.32, 0.96, 0.92, 0.96, 1.22, 0.84, 2.08), # 4: roof brow
        ( 2.38, 0.88, 0.34, 0.93, 0.90, 0.92, 1.18, 0.86, 1.22), # 5: nose cowl
        ( 2.52, 0.82, 0.38, 0.88, 0.86, 0.86, 1.06, 0.80, 1.08), # 6: front grille fascia
    ]
    build_automotive_body(
        "Matatu_Body", slices, mats["pearl_white"], mats, root, col,
        side_win_spans=(0, 1, 2, 3), windshield_span=4, rear_cap_glass=True,
        belt_stripe_mat=mats["psv_yellow"], lower_skirt_mat=mats["matatu_teal"],
        wheel_cutouts=cutouts,
    )
    # PSV Front Destination Route Signbox on Roof Brow + Roof Air Vent Hatch
    create_beveled_box("PsvDestHousing", (0, 1.82, 2.16), (1.06, 0.24, 0.16), mats["black_trim"], root, col, rot_deg=(12, 0, 0), bevel_w=0.025)
    create_beveled_box("PsvDestGlow",    (0, 1.93, 2.16), (0.94, 0.04, 0.10), mats["dest_amber"], root, col, rot_deg=(12, 0, 0), bevel_w=0.008)
    create_beveled_box("RoofVentHatch",  (0, -0.42, 2.18), (0.76, 0.62, 0.07), mats["black_trim"], root, col, bevel_w=0.02)

    create_beveled_box("FrontGrille", (0, 2.51, 0.88), (1.14, 0.06, 0.24), mats["grille_dark"], root, col, bevel_w=0.015)
    create_beveled_box("GrilleChrome", (0, 2.54, 0.90), (1.06, 0.03, 0.05), mats["chrome"], root, col, bevel_w=0.008)
    create_beveled_box("FrontBumper", (0, 2.48, 0.44), (1.84, 0.20, 0.22), mats["black_trim"], root, col, bevel_w=0.04)
    create_beveled_box("RearBumper",  (0, -2.48, 0.44), (1.84, 0.18, 0.22), mats["black_trim"], root, col, bevel_w=0.04)
    # Bold Proud Kenyan Matte-Black & Chrome Bullbar (clearly pops against white nose!)
    create_cylinder("BullbarTop", (0, 2.66, 0.84), 0.034, 1.48, 14, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
    create_cylinder("BullbarBot", (0, 2.66, 0.56), 0.034, 1.48, 14, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
    for sx in (-0.44, 0.44):
        create_cylinder(f"BullbarPost_{sx}", (sx, 2.64, 0.70), 0.032, 0.42, 12, mats["black_trim"], root, col)

    add_mirrors_lights_plates(root, col, 0.94, 1.92, 1.38, 2.50, -2.51, 0.90, 0.96, 0.46, 0.64, mats["black_trim"], mats, mir_scale=1.15, fog_z=0.45)
    add_wipers_handles_exhaust(root, col, 0.96, 2.38, 1.24, (1.42, 0.50), 1.12, -2.50, 0.30, mats["black_trim"], mats)
    add_wheel_set(root, col, 0.91, f_y, r_y, w_rad, 0.23, 5, mats)
    return root


def build_kenya_probox_wagon(mats, col):
    """Iconic Kenyan Everyday Station Wagon / Probox-Class Estate Car."""
    root = bpy.data.objects.new("Kenya_Probox_Wagon", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.33, 1.36, -1.32
    cutouts = [(-0.86, f_y, w_rad, w_rad + 0.06, 0.40), (0.86, f_y, w_rad, w_rad + 0.06, 0.40),
               (-0.86, r_y, w_rad, w_rad + 0.06, 0.40), (0.86, r_y, w_rad, w_rad + 0.06, 0.40)]
    slices = [
        (-2.12, 0.80, 0.30, 0.86, 0.62, 0.85, 0.98, 0.72, 1.48), # 0: rear hatch
        (-1.18, 0.83, 0.26, 0.88, 0.60, 0.87, 0.99, 0.73, 1.52), # 1: C-pillar
        (-0.22, 0.84, 0.26, 0.88, 0.60, 0.87, 0.99, 0.73, 1.52), # 2: B-pillar
        ( 0.52, 0.84, 0.26, 0.88, 0.60, 0.86, 0.97, 0.71, 1.49), # 3: A-pillar roof brow
        ( 1.06, 0.83, 0.26, 0.87, 0.60, 0.86, 0.95, 0.78, 0.98), # 4: windshield cowl
        ( 1.92, 0.80, 0.28, 0.84, 0.58, 0.82, 0.88, 0.72, 0.90), # 5: front hood nose
        ( 2.12, 0.74, 0.34, 0.79, 0.54, 0.77, 0.76, 0.66, 0.78), # 6: front bumper fascia
    ]
    build_automotive_body(
        "Probox_Body", slices, mats["silver"], mats, root, col,
        side_win_spans=(0, 1, 2), windshield_span=3, rear_cap_glass=True,
        wheel_cutouts=cutouts,
    )
    for sx in (-0.60, 0.60):
        create_beveled_box(f"RoofRail_{sx}", (sx, -0.78, 1.56), (0.044, 1.94, 0.055), mats["black_trim"], root, col, bevel_w=0.01)
    create_beveled_box("FrontGrille", (0, 2.11, 0.72), (0.96, 0.06, 0.18), mats["grille_dark"], root, col, bevel_w=0.015)
    create_beveled_box("GrilleChrome", (0, 2.14, 0.74), (0.88, 0.03, 0.04), mats["chrome"], root, col, bevel_w=0.005)
    create_beveled_box("FrontBumper", (0, 2.09, 0.39), (1.66, 0.18, 0.20), mats["black_trim"], root, col, bevel_w=0.035)
    create_beveled_box("RearBumper",  (0, -2.10, 0.40), (1.66, 0.18, 0.20), mats["black_trim"], root, col, bevel_w=0.035)
    create_beveled_box("RoofAntenna", (0, -1.68, 1.56), (0.04, 0.14, 0.06), mats["black_trim"], root, col, bevel_w=0.012)

    add_mirrors_lights_plates(root, col, 0.85, 0.78, 1.05, 2.11, -2.12, 0.76, 0.84, 0.40, 0.56, mats["silver"], mats, mir_scale=1.05, fog_z=0.40)
    add_wipers_handles_exhaust(root, col, 0.87, 1.08, 1.00, (0.04, -0.82), 0.90, -2.11, 0.26, mats["black_trim"], mats)
    add_wheel_set(root, col, 0.82, f_y, r_y, w_rad, 0.20, 5, mats)
    return root


def build_kenya_prado_suv(mats, col):
    """Kenyan Executive 4x4 Highland SUV (Prado / Land Cruiser-Class) with Chrome Vertical Grille, Fender Flares & Spare Cover."""
    root = bpy.data.objects.new("Kenya_Prado_Highland_SUV", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.42, 1.48, -1.46
    cutouts = [(-0.96, f_y, w_rad, w_rad + 0.08, 0.48), (0.96, f_y, w_rad, w_rad + 0.08, 0.48),
               (-0.96, r_y, w_rad, w_rad + 0.08, 0.48), (0.96, r_y, w_rad, w_rad + 0.08, 0.48)]
    slices = [
        (-2.34, 0.88, 0.44, 0.96, 0.78, 0.95, 1.20, 0.80, 1.84), # 0: rear tailgate
        (-1.32, 0.91, 0.40, 0.98, 0.76, 0.97, 1.22, 0.82, 1.88), # 1: C-pillar
        (-0.26, 0.92, 0.40, 0.98, 0.76, 0.97, 1.22, 0.82, 1.88), # 2: B-pillar
        ( 0.54, 0.92, 0.40, 0.98, 0.76, 0.96, 1.20, 0.80, 1.84), # 3: A-pillar roof brow
        ( 1.12, 0.91, 0.40, 0.97, 0.76, 0.95, 1.18, 0.86, 1.24), # 4: hood cowl
        ( 2.08, 0.88, 0.42, 0.94, 0.76, 0.92, 1.14, 0.82, 1.18), # 5: front hood nose
        ( 2.34, 0.82, 0.48, 0.88, 0.76, 0.86, 1.00, 0.76, 1.04), # 6: grille nose
    ]
    build_automotive_body(
        "Prado_Body", slices, mats["navy"], mats, root, col,
        side_win_spans=(0, 1, 2), windshield_span=3, rear_cap_glass=True,
        wheel_cutouts=cutouts,
    )
    for sx in (-0.68, 0.68):
        create_beveled_box(f"RoofRail_{sx}", (sx, -0.86, 1.92), (0.055, 2.38, 0.06), mats["chrome"], root, col, bevel_w=0.01)
        create_beveled_box(f"SideStep_{sx}", (sx * 1.44, 0.0, 0.42), (0.20, 1.88, 0.06), mats["black_trim"], root, col, bevel_w=0.015)

    # 4x4 Black Rubber Wheel-Arch Fender Flares
    for side in (-1.0, 1.0):
        create_fender_flare(f"FlareF_{side:.0f}", (side * 0.97, f_y, w_rad), w_rad + 0.05, 0.065, 0.10, mats["black_trim"], root, col)
        create_fender_flare(f"FlareR_{side:.0f}", (side * 0.97, r_y, w_rad), w_rad + 0.05, 0.065, 0.10, mats["black_trim"], root, col)

    # Bold Vertical-Slat Chrome Prado Grille + Silver Skid Plate + Rear Spare Wheel Cover!
    create_beveled_box("PradoGrilleFrame", (0, 2.33, 0.92), (1.02, 0.08, 0.34), mats["grille_dark"], root, col, bevel_w=0.02)
    for gx in (-0.34, -0.17, 0.0, 0.17, 0.34):
        create_beveled_box(f"GrilleSlat_{gx}", (gx, 2.37, 0.92), (0.042, 0.04, 0.30), mats["chrome"], root, col, bevel_w=0.006)
    create_beveled_box("FrontSkidPlate", (0, 2.32, 0.52), (1.28, 0.22, 0.20), mats["alloy"], root, col, bevel_w=0.03)
    create_cylinder("RearSpareCover", (0, -2.44, 1.04), 0.38, 0.20, 24, mats["navy"], root, col, rot_deg=(90, 0, 0), bevel_w=0.03)
    create_cylinder("RearSpareChrome", (0, -2.46, 1.04), 0.36, 0.16, 24, mats["chrome"], root, col, rot_deg=(90, 0, 0))

    add_mirrors_lights_plates(root, col, 0.95, 0.80, 1.28, 2.33, -2.35, 0.98, 1.12, 0.55, 0.62, mats["navy"], mats, mir_scale=1.28, fog_z=0.60)
    add_wipers_handles_exhaust(root, col, 0.97, 1.14, 1.26, (0.08, -0.88), 1.10, -2.34, 0.38, mats["chrome"], mats)
    add_wheel_set(root, col, 0.93, f_y, r_y, w_rad, 0.26, 6, mats)
    return root


def build_kenya_hilux_pickup(mats, col):
    """Kenyan 4x4 Double-Cab Pickup (Hilux / D-Max-Class) with Chrome Sports Rollbar & 4x4 Fender Flares."""
    root = bpy.data.objects.new("Kenya_Hilux_DoubleCab_Pickup", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.42, 1.62, -1.56
    cutouts = [(-0.94, f_y, w_rad, w_rad + 0.08, 0.46), (0.94, f_y, w_rad, w_rad + 0.08, 0.46),
               (-0.94, r_y, w_rad, w_rad + 0.08, 0.46), (0.94, r_y, w_rad, w_rad + 0.08, 0.46)]
    slices = [
        (-2.52, 0.86, 0.46, 0.94, 0.78, 0.93, 1.16, 0.88, 1.18), # 0: rear tailgate
        (-0.84, 0.88, 0.42, 0.96, 0.76, 0.95, 1.18, 0.88, 1.20), # 1: front of bed
        (-0.68, 0.90, 0.42, 0.96, 0.76, 0.95, 1.18, 0.78, 1.78), # 2: rear cab roof
        ( 0.04, 0.90, 0.42, 0.96, 0.76, 0.95, 1.18, 0.78, 1.78), # 3: B-pillar
        ( 0.70, 0.90, 0.42, 0.96, 0.76, 0.95, 1.16, 0.76, 1.76), # 4: A-pillar roof brow
        ( 1.28, 0.89, 0.42, 0.95, 0.76, 0.94, 1.14, 0.84, 1.22), # 5: hood cowl
        ( 2.24, 0.86, 0.44, 0.92, 0.76, 0.90, 1.08, 0.78, 1.14), # 6: front hood nose
        ( 2.52, 0.78, 0.50, 0.86, 0.74, 0.84, 0.96, 0.72, 1.00), # 7: front grille fascia
    ]
    build_automotive_body(
        "Pickup_Body", slices, mats["maroon"], mats, root, col,
        side_win_spans=(2, 3), windshield_span=4, rear_glass_span=1,
        wheel_cutouts=cutouts,
    )
    # 4x4 Black Fender Flares + Side Steps
    for side in (-1.0, 1.0):
        create_fender_flare(f"FlareF_{side:.0f}", (side * 0.95, f_y, w_rad), w_rad + 0.05, 0.065, 0.10, mats["black_trim"], root, col)
        create_fender_flare(f"FlareR_{side:.0f}", (side * 0.95, r_y, w_rad), w_rad + 0.05, 0.065, 0.10, mats["black_trim"], root, col)
        create_beveled_box(f"PickupStep_{side:.0f}", (side * 0.98, 0.02, 0.42), (0.18, 1.62, 0.06), mats["black_trim"], root, col, bevel_w=0.015)

    create_beveled_box("CargoBedLiner", (0, -1.68, 1.16), (1.62, 1.60, 0.08), mats["black_trim"], root, col, bevel_w=0.015)
    create_cylinder("RollbarTop", (0, -0.84, 1.74), 0.038, 1.54, 14, mats["chrome"], root, col, rot_deg=(0, 90, 0))
    for sx in (-0.75, 0.75):
        create_cylinder(f"RollbarLeg_{sx}", (sx, -0.84, 1.46), 0.038, 0.56, 14, mats["chrome"], root, col)
        create_cylinder(f"RollbarBrace_{sx}", (sx, -1.16, 1.46), 0.034, 0.76, 12, mats["chrome"], root, col, rot_deg=(42, 0, 0))
    create_beveled_box("PickupGrille", (0, 2.51, 0.92), (1.02, 0.08, 0.28), mats["grille_dark"], root, col, bevel_w=0.02)
    create_beveled_box("GrilleChrome", (0, 2.54, 0.94), (0.94, 0.04, 0.07), mats["chrome"], root, col, bevel_w=0.01)
    create_beveled_box("FrontBumper",  (0, 2.50, 0.54), (1.80, 0.22, 0.22), mats["black_trim"], root, col, bevel_w=0.035)
    create_beveled_box("RearBumper",   (0, -2.53, 0.56), (1.80, 0.20, 0.18), mats["chrome"], root, col, bevel_w=0.03)

    add_mirrors_lights_plates(root, col, 0.93, 0.98, 1.24, 2.51, -2.53, 0.94, 0.96, 0.55, 0.62, mats["chrome"], mats, mir_scale=1.22, fog_z=0.58)
    add_wipers_handles_exhaust(root, col, 0.95, 1.30, 1.24, (0.28, -0.44), 1.08, -2.52, 0.40, mats["chrome"], mats)
    add_wheel_set(root, col, 0.91, f_y, r_y, w_rad, 0.26, 6, mats)
    return root


def build_kenya_saloon_sedan(mats, col):
    """Kenyan Modern Sleek 3-Box Highway Sedan (Axio / Premio / Taxi-Class)."""
    root = bpy.data.objects.new("Kenya_Saloon_Taxi_Sedan", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.33, 1.38, -1.36
    cutouts = [(-0.88, f_y, w_rad, w_rad + 0.06, 0.40), (0.88, f_y, w_rad, w_rad + 0.06, 0.40),
               (-0.88, r_y, w_rad, w_rad + 0.06, 0.40), (0.88, r_y, w_rad, w_rad + 0.06, 0.40)]
    slices = [
        (-2.22, 0.76, 0.34, 0.84, 0.60, 0.82, 0.92, 0.72, 0.95), # 0: rear bumper/trunk
        (-1.58, 0.82, 0.28, 0.88, 0.58, 0.87, 0.96, 0.78, 1.00), # 1: base of rear window
        (-1.02, 0.84, 0.26, 0.89, 0.56, 0.88, 0.96, 0.68, 1.42), # 2: C-pillar roof
        (-0.28, 0.85, 0.26, 0.90, 0.56, 0.88, 0.96, 0.68, 1.43), # 3: B-pillar roof
        ( 0.38, 0.85, 0.26, 0.90, 0.56, 0.88, 0.94, 0.67, 1.41), # 4: A-pillar roof brow
        ( 1.02, 0.84, 0.26, 0.89, 0.56, 0.87, 0.90, 0.78, 0.94), # 5: hood cowl
        ( 1.96, 0.80, 0.28, 0.85, 0.54, 0.83, 0.82, 0.72, 0.85), # 6: front hood nose
        ( 2.22, 0.72, 0.32, 0.78, 0.50, 0.76, 0.68, 0.64, 0.71), # 7: front bumper fascia
    ]
    build_automotive_body(
        "Sedan_Body", slices, mats["gunmetal"], mats, root, col,
        side_win_spans=(2, 3), windshield_span=4, rear_glass_span=1,
        wheel_cutouts=cutouts,
    )
    create_beveled_box("TrunkLipSpoiler", (0, -2.14, 0.98), (1.34, 0.11, 0.04), mats["black_trim"], root, col, bevel_w=0.01)
    create_beveled_box("SharkFinAnt",     (0, -0.82, 1.45), (0.04, 0.16, 0.07), mats["black_trim"], root, col, bevel_w=0.014)
    create_beveled_box("SedanGrille",     (0, 2.20, 0.62), (0.98, 0.06, 0.16), mats["grille_dark"], root, col, bevel_w=0.015)
    create_beveled_box("SedanChromeBar",  (0, 2.23, 0.66), (0.90, 0.03, 0.035), mats["chrome"], root, col, bevel_w=0.005)
    create_beveled_box("LowerIntake",     (0, 2.19, 0.38), (1.36, 0.14, 0.14), mats["black_trim"], root, col, bevel_w=0.025)

    add_mirrors_lights_plates(root, col, 0.86, 0.72, 1.00, 2.21, -2.21, 0.68, 0.80, 0.40, 0.58, mats["gunmetal"], mats, mir_scale=1.05, fog_z=0.39)
    add_wipers_handles_exhaust(root, col, 0.88, 1.04, 0.96, (-0.02, -0.78), 0.88, -2.21, 0.27, mats["chrome"], mats)
    add_wheel_set(root, col, 0.83, f_y, r_y, w_rad, 0.21, 5, mats)
    return root


def build_kenya_canter_truck(mats, col):
    """Kenyan Commercial Cab-Over Cargo Lorry (Canter / NQR-Class) with Solid Mirror Arms, Wind Deflector & Slatted Grille."""
    root = bpy.data.objects.new("Kenya_Canter_Commercial_Truck", None)
    link_obj(root, None, col)

    w_rad, f_y, r_y = 0.45, 2.25, -1.55
    cutouts = [(-1.02, f_y, w_rad, w_rad + 0.09, 0.50), (1.02, f_y, w_rad, w_rad + 0.09, 0.50)]
    cab_slices = [
        ( 1.22, 0.96, 0.64, 1.04, 1.12, 1.04, 1.56, 0.96, 2.46), # 0: cab back wall
        ( 2.68, 0.98, 0.64, 1.04, 1.12, 1.04, 1.56, 0.94, 2.44), # 1: roof brow
        ( 3.06, 0.95, 0.66, 1.02, 1.12, 1.00, 1.50, 0.94, 1.56), # 2: bottom of windshield
        ( 3.16, 0.88, 0.72, 0.96, 1.12, 0.94, 1.44, 0.88, 1.48), # 3: front grille fascia
    ]
    build_automotive_body(
        "Canter_Cab", cab_slices, mats["canter_blue"], mats, root, col,
        side_win_spans=(0,), windshield_span=1,
        wheel_cutouts=cutouts,
    )
    # Aerodynamic Cab Roof Wind Deflector fairing into Freight Box!
    create_beveled_box("CabWindDeflector", (0, 1.92, 2.74), (1.88, 1.38, 0.62), mats["cargo_white"], root, col, rot_deg=(24, 0, 0), bevel_w=0.06)

    # Recessed Dark Truck Grille with 3 Crisp Chrome Louver Bars!
    create_beveled_box("CabGrilleDark", (0, 3.14, 1.22), (1.56, 0.07, 0.34), mats["grille_dark"], root, col, bevel_w=0.02)
    for gz in (1.12, 1.22, 1.32):
        create_beveled_box(f"CabGrilleBar_{gz:.2f}", (0, 3.18, gz), (1.44, 0.03, 0.038), mats["chrome"], root, col, bevel_w=0.008)
    create_beveled_box("CabBumper", (0, 3.15, 0.70), (2.06, 0.22, 0.28), mats["black_trim"], root, col, bevel_w=0.035)

    for sx in (-0.52, 0.52):
        create_beveled_box(f"ChassisRail_{sx}", (sx, -0.55, 0.72), (0.14, 5.20, 0.22), mats["black_trim"], root, col, bevel_w=0.01)
    create_cylinder("DieselTank", (-0.86, 0.22, 0.68), 0.23, 1.12, 16, mats["alloy"], root, col, rot_deg=(90, 0, 0), bevel_w=0.02)

    # Side Underrun Safety Rails + Rear Mudflaps
    for side in (-1.0, 1.0):
        create_beveled_box(f"SideGuard_{side:.0f}", (side * 1.02, 0.16, 0.54), (0.06, 2.28, 0.14), mats["alloy"], root, col, bevel_w=0.01)
        create_beveled_box(f"Mudflap_{side:.0f}",   (side * 0.96, r_y - 0.56, 0.40), (0.44, 0.04, 0.50), mats["black_trim"], root, col, bevel_w=0.008)

    create_beveled_box("FreightCargoBox", (0, -1.06, 2.04), (2.24, 4.22, 2.26), mats["cargo_white"], root, col, bevel_w=0.05)
    create_beveled_box("CargoRoofTrim",   (0, -1.06, 3.18), (2.28, 4.28, 0.08), mats["alloy"], root, col, bevel_w=0.02)
    create_beveled_box("SafetyStripeYel", (0, -1.06, 1.02), (2.27, 4.18, 0.14), mats["psv_yellow"], root, col, bevel_w=0.01)
    create_beveled_box("SafetyStripeRed", (0, -3.19, 1.08), (2.14, 0.04, 0.16), mats["reflect_red"], root, col, bevel_w=0.005)
    for ry in (-2.7, -2.0, -1.3, -0.6, 0.1, 0.8):
        create_beveled_box(f"CargoRib_{ry}", (0, ry, 2.04), (2.27, 0.055, 2.10), mats["alloy"], root, col, bevel_w=0.008)

    # Solidly Mounted Truck Side Mirrors with Top & Bottom Horizontal Mounting Brackets into Cab A-Pillar!
    for side in (-1.0, 1.0):
        create_cylinder(f"TruckMirPost_{side}",   (side * 1.20, 2.74, 1.94), 0.020, 0.62, 10, mats["black_trim"], root, col)
        create_cylinder(f"TruckMirTopArm_{side}", (side * 1.08, 2.74, 2.24), 0.018, 0.28, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        create_cylinder(f"TruckMirBotArm_{side}", (side * 1.08, 2.74, 1.64), 0.018, 0.28, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        create_beveled_box(f"TruckMirHead_{side}", (side * 1.24, 2.78, 1.94), (0.08, 0.14, 0.38), mats["black_trim"], root, col, bevel_w=0.015)

    for side in (-1.0, 1.0):
        create_beveled_box(f"HeadL_{side}", (side * 0.72, 3.16, 0.84), (0.28, 0.07, 0.14), mats["headlight"], root, col, bevel_w=0.02)
        create_beveled_box(f"IndF_{side}",  (side * 0.90, 3.12, 0.84), (0.10, 0.07, 0.13), mats["indicator"], root, col, bevel_w=0.015)
        create_beveled_box(f"Tail_{side}",  (side * 0.84, -3.20, 0.86), (0.26, 0.07, 0.22), mats["taillight"], root, col, bevel_w=0.02)
    create_beveled_box("PlateFront", (0, 3.24, 0.68), (0.48, 0.025, 0.12), mats["plate_yellow"], root, col, bevel_w=0.004)
    create_beveled_box("PlateRear",  (0, -3.21, 0.72), (0.48, 0.025, 0.12), mats["plate_yellow"], root, col, bevel_w=0.004)

    add_wipers_handles_exhaust(root, col, 1.02, 3.06, 1.56, (1.66,), 1.44, -3.16, 0.44, mats["black_trim"], mats)

    build_realistic_wheel("Wheel_FL", (-0.98, f_y, w_rad), w_rad, 0.28, 6, mats, root, col, is_left=True)
    build_realistic_wheel("Wheel_FR", ( 0.98, f_y, w_rad), w_rad, 0.28, 6, mats, root, col, is_left=False)
    build_realistic_wheel("Wheel_RL", (-0.96, r_y, w_rad), w_rad, 0.42, 6, mats, root, col, is_left=True)
    build_realistic_wheel("Wheel_RR", ( 0.96, r_y, w_rad), w_rad, 0.42, 6, mats, root, col, is_left=False)
    return root


# ==============================================================================
# 4. EXPORT GLBs TO UNITY (+Z FORWARD!) + SAVE EDITABLE .BLEND + RENDER PREVIEW
# ==============================================================================
def select_hierarchy(root_obj):
    bpy.ops.object.select_all(action='DESELECT')
    def walk(o):
        o.select_set(True)
        for ch in o.children:
            walk(ch)
    walk(root_obj)
    bpy.context.view_layer.objects.active = root_obj


def export_vehicle_glb(root_obj, filename):
    saved_loc = root_obj.location.copy()
    saved_rot = root_obj.rotation_euler.copy()

    # Rotate 180 deg around Z during export so Blender +Y forward -> glTF +Z -> Unity glTFast +Z FORWARD!
    root_obj.location = Vector((0.0, 0.0, 0.0))
    root_obj.rotation_euler = Euler((0.0, 0.0, math.pi), 'XYZ')
    bpy.context.view_layer.update()
    select_hierarchy(root_obj)
    out_path = os.path.join(UNITY_TRAFFIC_DIR, filename)
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
    print(f"EXPORTED GLB (+Z Unity Forward): {filename} ({os.path.getsize(out_path) // 1024} KB)")


def setup_showroom_and_render(vehicles):
    positions = [
        (-5.0,  3.2, 0.0),
        ( 0.0,  3.2, 0.0),
        ( 5.0,  3.2, 0.0),
        (-5.0, -3.4, 0.0),
        ( 0.0, -3.4, 0.0),
        ( 5.2, -3.4, 0.0),
    ]
    for v, pos in zip(vehicles, positions):
        v.location = Vector(pos)
        v.rotation_euler = Euler((0.0, 0.0, math.radians(-22.0)), 'XYZ')

    bpy.ops.mesh.primitive_plane_add(size=46.0, location=(0, 0, 0))
    floor = bpy.context.active_object
    floor.name = "Showroom_Tarmac"
    floor.data.materials.append(make_mat("Showroom_Floor", (0.14, 0.15, 0.17), metallic=0.05, roughness=0.40))

    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.58, 0.74, 0.94, 1.0)
        bg.inputs[1].default_value = 0.85

    sun_data = bpy.data.lights.new("SunKey", type='SUN')
    sun_data.energy = 3.8
    sun_obj = bpy.data.objects.new("SunKey", sun_data)
    sun_obj.rotation_euler = Euler((math.radians(48), math.radians(15), math.radians(-38)), 'XYZ')
    bpy.context.scene.collection.objects.link(sun_obj)

    cam_data = bpy.data.cameras.new("ShowroomCam")
    cam_data.lens = 42.0
    cam_obj = bpy.data.objects.new("ShowroomCam", cam_data)
    cam_obj.location = Vector((11.5, 13.0, 6.2))
    direction = Vector((0.0, 0.0, 1.05)) - cam_obj.location
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj

    blend_path = os.path.join(STUDIO_DIR, "Kenya_Owned_Commercial_Traffic_Pack.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"SAVED MASTER BLEND: {blend_path}")

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PNG
    scene.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED PREVIEW: {PREVIEW_PNG}")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = init_materials()
    col = bpy.context.scene.collection

    builders = [
        ("Kenya_Matatu_14Seater_PSV.glb",     build_kenya_matatu_14seater),
        ("Kenya_Probox_Wagon.glb",            build_kenya_probox_wagon),
        ("Kenya_Prado_Highland_SUV.glb",      build_kenya_prado_suv),
        ("Kenya_Hilux_DoubleCab_Pickup.glb",  build_kenya_hilux_pickup),
        ("Kenya_Saloon_Taxi_Sedan.glb",       build_kenya_saloon_sedan),
        ("Kenya_Canter_Commercial_Truck.glb", build_kenya_canter_truck),
    ]

    built_roots = []
    for fname, fn in builders:
        root = fn(mats, col)
        export_vehicle_glb(root, fname)
        built_roots.append(root)

    setup_showroom_and_render(built_roots)


if __name__ == "__main__":
    main()
