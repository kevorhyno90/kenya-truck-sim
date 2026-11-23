"""
Kenya Street Sim — Blender 5.3 Heavy Articulated Kenyan Transit Semi-Trailer Truck (Path A: 100% Owned)
Builds a realistic 16.5m Northern Corridor / Kisumu / Nakuru-Eldoret-Malaba 18-wheeler container trailer:
  - Heavy red/white high-roof cab-over prime mover tractor with 3D RHD cab interior, sleeper bunk,
    chrome bullbar, roof air fairing, chrome vertical exhaust stacks, west-coast mirrors & KDX 882T plates
  - 12.2m 40-foot ribbed shipping container flatbed trailer ('TRANS-AFRICA CARGO') with kingpin fifth wheel,
    landing legs, side underrun rails, Kenyan diagonal reflective chevrons, 80 KPH sticker & 10 heavy wheels!
"""

import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector, Euler

_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
_candidate_root = os.path.abspath(os.path.join(_script_dir, "..")) if _script_dir else ""
REPO_ROOT = _candidate_root if os.path.isdir(os.path.join(_candidate_root, "KenyaStreetSim")) else r"C:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
OWNED_FLEET_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "ImportedModels", "OwnedFleet")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
TEX_DIR = os.path.join(STUDIO_DIR, "Textures")

os.makedirs(OWNED_FLEET_DIR, exist_ok=True)
_tools_dir = os.path.join(REPO_ROOT, "tools")
if _tools_dir not in sys.path:
    sys.path.insert(0, _tools_dir)

import kenya_auto_textures
import blender_build_playable_fleet as pf


def build_kenya_semitrailer(mats, col):
    root = bpy.data.objects.new("Kenya_SemiTrailer_Transit", None)
    pf.link_obj(root, None, col)

    # Extra materials for Heavy Transit Prime Mover & 40ft Shipping Container
    mat_cab_red = pf.make_mat("Trailer_CabCrimson", (0.58, 0.06, 0.08), metallic=0.45, roughness=0.18, coat=1.0)
    mat_cont_blue = pf.make_mat("Trailer_ContainerBlue", (0.08, 0.26, 0.48), metallic=0.25, roughness=0.42, coat=0.2)
    mat_chassis = mats["black_trim"]

    tex_plate = kenya_auto_textures.make_kenya_plate_png(os.path.join(TEX_DIR, "Plate_KDX882T.png"), "KDX 882T", yellow=True)
    tex_banner = kenya_auto_textures.make_side_banner_png(
        os.path.join(TEX_DIR, "Livery_TransAfrica_Trailer.png"),
        "TRANS-AFRICA LOGISTICS", "MOMBASA - NAIROBI - ELDORET - KAMPALA",
        (16, 52, 98), (242, 184, 24), text_col=(246, 248, 252)
    )
    mat_plate = pf.make_textured_mat("PlateTex_Trailer", tex_plate, roughness=0.22)
    mat_banner = pf.make_textured_mat("Banner_Trailer", tex_banner, roughness=0.30)

    w_rad = 0.52
    # 1. HEAVY SLEEPER CAB-OVER TRACTOR (y = +4.25 -> +7.65)
    cab_cutouts = [(-1.20, 6.20, w_rad, w_rad + 0.09, 0.56), (1.20, 6.20, w_rad, w_rad + 0.09, 0.56)]
    cab_slices = [
        ( 4.65, 1.12, 0.78, 1.22, 1.38, 1.22, 1.98, 1.12, 3.22), # sleeper back wall
        ( 7.05, 1.14, 0.78, 1.22, 1.38, 1.21, 1.96, 1.10, 3.18), # roof brow
        ( 7.50, 1.10, 0.80, 1.18, 1.38, 1.16, 1.90, 1.08, 1.94), # windshield cowl
        ( 7.68, 1.02, 0.86, 1.12, 1.36, 1.10, 1.80, 1.02, 1.84), # front fascia
    ]
    pf.build_lofted_shell(
        "Tractor_Cab", cab_slices, mat_cab_red, mats, root, col,
        side_win_spans=(0,), windshield_span=1, wheel_cutouts=cab_cutouts, bevel_offset=0.06,
    )
    # Aerodynamic roof wind fairing wedge + roof sun visor + amber marker lights + twin chrome exhaust stacks!
    pf.create_wind_deflector_wedge("TractorWindFairing", y_back=4.68, y_front=6.88, z_base=3.18, z_top_back=3.92, z_top_front=3.30, half_w_back=1.14, half_w_front=1.04, mat=mat_cab_red, parent=root, col=col)
    pf.create_beveled_box("CabSunVisor", (0, 7.22, 3.14), (2.18, 0.34, 0.14), mats["black_trim"], root, col, rot_deg=(12, 0, 0), bevel_w=0.02)
    for mx in (-0.72, 0.0, 0.72):
        pf.create_beveled_box(f"CabRoofLamp_{mx:.2f}", (mx, 7.04, 3.26), (0.14, 0.10, 0.06), mats["indicator"], root, col, bevel_w=0.01)
    for side in (-1.0, 1.0):
        pf.create_cylinder(f"ChromeStack_{side:.0f}", (side * 1.16, 4.52, 2.55), 0.075, 2.60, 14, mats["chrome"], root, col)
        pf.create_cylinder(f"WestMirPost_{side:.0f}", (side * 1.40, 7.05, 2.42), 0.024, 0.84, 10, mats["black_trim"], root, col)
        pf.create_cylinder(f"WestMirTop_{side:.0f}",  (side * 1.26, 7.05, 2.82), 0.020, 0.32, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        pf.create_cylinder(f"WestMirBot_{side:.0f}",  (side * 1.26, 7.05, 2.02), 0.020, 0.32, 10, mats["black_trim"], root, col, rot_deg=(0, 90, 0))
        pf.create_beveled_box(f"WestMirHead_{side:.0f}", (side * 1.45, 7.09, 2.42), (0.09, 0.17, 0.50), mats["black_trim"], root, col, bevel_w=0.018)
        pf.create_beveled_box(f"HeadL_{side:.0f}", (side * 0.88, 7.78, 0.90), (0.36, 0.06, 0.16), mats["headlight"], root, col, bevel_w=0.018)
        pf.create_beveled_box(f"IndF_{side:.0f}",  (side * 1.10, 7.76, 0.90), (0.12, 0.06, 0.16), mats["indicator"], root, col, bevel_w=0.014)

    # 3D RHD Tractor Interior (visible behind windshield!)
    pf.create_beveled_box("TractorDash", (0, 7.06, 1.88), (2.00, 0.58, 0.36), mats["cabin_dash"], root, col, bevel_w=0.04)
    pf.create_cylinder("TractorSteerRim", (0.56, 6.62, 2.08), 0.22, 0.04, 18, mats["black_trim"], root, col, rot_deg=(68, 0, 0))
    for sx in (-0.56, 0.56):
        pf.create_beveled_box(f"TractorSeat_{sx:.2f}", (sx, 6.00, 2.02), (0.48, 0.14, 0.78), mats["seat_truck"], root, col, rot_deg=(-8, 0, 0), bevel_w=0.03)

    # Heavy Grille + Bumper + Bullbar + Front Plate
    pf.create_beveled_box("TractorGrille", (0, 7.66, 1.46), (1.84, 0.08, 0.52), mats["grille_dark"], root, col, bevel_w=0.02)
    for gz in (1.28, 1.40, 1.52, 1.64):
        pf.create_beveled_box(f"GrilleFin_{gz:.2f}", (0, 7.70, gz), (1.72, 0.035, 0.04), mats["chrome"], root, col, bevel_w=0.008)
    pf.create_beveled_box("TractorBumper", (0, 7.66, 0.86), (2.40, 0.28, 0.36), mat_chassis, root, col, bevel_w=0.04)
    pf.create_beveled_box("PlateFront", (0, 7.81, 0.84), (0.58, 0.022, 0.15), mat_chassis, root, col, bevel_w=0.004)
    pf.create_decal_quad("PlateFrontTex", (0, 7.826, 0.84), 0.56, 0.14, '+Y', mat_plate, root, col)

    # 2. TRACTOR CHASSIS, FUEL TANKS, FIFTH WHEEL & 12.2m 40FT CONTAINER SEMI-TRAILER (y = -8.10 -> +4.10)!
    for sx in (-0.54, 0.54):
        pf.create_beveled_box(f"TractorRail_{sx:.2f}", (sx, 4.20, 0.86), (0.16, 6.40, 0.26), mat_chassis, root, col, bevel_w=0.01)
        pf.create_beveled_box(f"TrailerRail_{sx:.2f}", (sx, -2.00, 1.10), (0.16, 12.10, 0.26), mat_chassis, root, col, bevel_w=0.01)
    for side in (-1.0, 1.0):
        pf.create_cylinder(f"DieselTank_{side:.0f}", (side * 0.96, 4.10, 0.82), 0.30, 1.65, 16, mats["alcoa"], root, col, rot_deg=(90, 0, 0), bevel_w=0.025)
        pf.create_beveled_box(f"TrailerGuard_{side:.0f}", (side * 1.18, -1.00, 0.72), (0.06, 5.80, 0.24), mats["steel_rim"], root, col, bevel_w=0.01)
        pf.create_beveled_box(f"LandingLeg_{side:.0f}",   (side * 0.82,  1.90, 0.66), (0.14, 0.14, 0.82), mat_chassis, root, col, bevel_w=0.01)

    # 40ft Shipping Container Box + Corrugated Side Ribs + TRANS-AFRICA LOGISTICS Banners!
    pf.create_beveled_box("TrailerFlatbed",   (0, -2.00, 1.28), (2.48, 12.24, 0.16), mat_chassis, root, col, bevel_w=0.02)
    pf.create_beveled_box("Container40ft",    (0, -2.00, 2.66), (2.44, 12.16, 2.62), mat_cont_blue, root, col, bevel_w=0.04)
    pf.create_beveled_box("ContainerYellow",  (0, -2.00, 1.38), (2.47, 12.12, 0.14), mats["psv_yellow"], root, col, bevel_w=0.01)
    for ry in (-7.2, -5.8, -4.4, -3.0, -1.6, -0.2, 1.2, 2.6, 3.6):
        pf.create_beveled_box(f"ContRib_{ry:.1f}", (0, ry, 2.66), (2.47, 0.08, 2.48), mat_cont_blue, root, col, bevel_w=0.01)
    for side in (-1.0, 1.0):
        pf.create_decal_quad(f"TrailerBanner_{side:.0f}", (side * 1.244, -2.00, 2.74), 8.20, 1.40, '-X' if side < 0 else '+X', mat_banner, root, col)

    # Rear Container Doors + Cam-Lock Rods + Chevron Bar + 80 KPH + Rear Lights & Plate!
    for rx in (-0.74, -0.26, 0.26, 0.74):
        pf.create_cylinder(f"RearLockRod_{rx:.2f}", (rx, -8.10, 2.66), 0.020, 2.44, 10, mats["chrome"], root, col)
    pf.create_decal_quad("RearChevronTex", (0, -8.12, 1.36), 2.38, 0.22, '-Y', mats["chevron"], root, col)
    pf.create_decal_quad("Rear80KphTex",   (0.82, -8.11, 3.40), 0.42, 0.42, '-Y', mats["speed_80"], root, col)
    pf.create_beveled_box("RearBumper",    (0, -8.12, 0.64), (2.36, 0.20, 0.22), mat_chassis, root, col, bevel_w=0.025)
    pf.create_beveled_box("PlateRear",     (0, -8.23, 0.64), (0.58, 0.022, 0.15), mat_chassis, root, col, bevel_w=0.004)
    pf.create_decal_quad("PlateRearTex",   (0, -8.246, 0.64), 0.56, 0.14, '-Y', mat_plate, root, col)
    for side in (-1.0, 1.0):
        pf.create_beveled_box(f"Tail_{side:.0f}", (side * 0.96, -8.18, 0.86), (0.36, 0.08, 0.18), mats["taillight"], root, col, bevel_w=0.018)
        pf.create_beveled_box(f"IndR_{side:.0f}", (side * 1.18, -8.18, 0.86), (0.12, 0.08, 0.18), mats["indicator"], root, col, bevel_w=0.014)

    # 10 Heavy Commercial Wheels (Steer front + tandem tractor drive + tri-axle trailer!)
    wheel_axles = [
        ("FL", "FR",  6.20, False),
        ("TL1", "TR1", 2.75, True),
        ("TL2", "TR2", 1.35, True),
        ("RL1", "RR1", -5.20, True),
        ("RL2", "RR2", -6.60, True),
    ]
    for nl, nr, wy, is_dual in wheel_axles:
        w_w = 0.48 if is_dual else 0.32
        pf.build_commercial_wheel(f"Wheel_{nl}", (-1.08, wy, w_rad), w_rad, w_w, mats["steel_rim"], mats, root, col, is_left=True,  is_dual_rear=is_dual, num_lugs=10)
        pf.build_commercial_wheel(f"Wheel_{nr}", ( 1.08, wy, w_rad), w_rad, w_w, mats["steel_rim"], mats, root, col, is_left=False, is_dual_rear=is_dual, num_lugs=10)

    return root


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = pf.init_materials()
    col = bpy.context.scene.collection
    trailer = build_kenya_semitrailer(mats, col)
    pf.export_hero_glb(trailer, "Kenya_SemiTrailer_Transit.glb")


if __name__ == "__main__":
    main()
