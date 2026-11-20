"""
Generates the exact 3D Model of the ENA Coach KDK 057M (MAN Master Apollo)
in Wavefront OBJ + MTL format (universal standard accepted by Sketchfab, SketchUp, Blender, etc.)
"""
import math
import os

base_dir = r"c:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
obj_path = os.path.join(base_dir, "assets", "models", "ena_coach_kdk057m.obj")
mtl_path = os.path.join(base_dir, "assets", "models", "ena_coach_kdk057m.mtl")

mtl_content = """# ENA Coach KDK 057M MAN Master Apollo Material Library
# Universal format accepted by Sketchfab, SketchUp, Blender, Maya

newmtl BodyWhite
Ka 0.2 0.2 0.2
Kd 0.96 0.97 0.98
Ks 0.6 0.6 0.6
Ns 75.0
illum 2

newmtl SkirtRed
Ka 0.2 0.05 0.05
Kd 0.86 0.15 0.15
Ks 0.4 0.4 0.4
Ns 50.0
illum 2

newmtl AccentBlack
Ka 0.05 0.05 0.05
Kd 0.06 0.08 0.12
Ks 0.3 0.3 0.3
Ns 30.0
illum 2

newmtl ForeheadBlack
Ka 0.02 0.02 0.02
Kd 0.05 0.05 0.06
Ks 0.8 0.8 0.8
Ns 90.0
illum 2

newmtl WindshieldGlass
Ka 0.05 0.08 0.1
Kd 0.08 0.14 0.22
Ks 0.95 0.95 0.95
Ns 120.0
d 0.65
illum 4

newmtl WindowTint
Ka 0.02 0.03 0.04
Kd 0.05 0.07 0.1
Ks 0.8 0.8 0.8
Ns 80.0
d 0.85
illum 4

newmtl MirrorWhite
Ka 0.2 0.2 0.2
Kd 0.98 0.98 0.98
Ks 0.7 0.7 0.7
Ns 85.0
illum 2

newmtl MirrorGlass
Ka 0.1 0.1 0.1
Kd 0.8 0.85 0.9
Ks 0.98 0.98 0.98
Ns 150.0
illum 3

newmtl ChromeGrille
Ka 0.3 0.3 0.3
Kd 0.85 0.85 0.88
Ks 0.95 0.95 0.95
Ns 140.0
illum 3

newmtl AlcoaRims
Ka 0.3 0.3 0.3
Kd 0.88 0.88 0.92
Ks 0.9 0.9 0.9
Ns 120.0
illum 3

newmtl TireRubber
Ka 0.05 0.05 0.05
Kd 0.12 0.12 0.14
Ks 0.08 0.08 0.08
Ns 15.0
illum 2

newmtl OrangeLogo
Ka 0.2 0.1 0.0
Kd 0.98 0.45 0.05
Ks 0.5 0.5 0.5
Ns 60.0
illum 2

newmtl LicensePlate
Ka 0.2 0.2 0.2
Kd 0.95 0.95 0.92
Ks 0.2 0.2 0.2
Ns 20.0
illum 2
"""

with open(mtl_path, "w", encoding="utf-8") as f:
    f.write(mtl_content)

print(f"Written: {mtl_path}")

vertices = []
faces = []

def add_vertex(x, y, z):
    vertices.append((x, y, z))
    return len(vertices)

def add_quad(mat, v1, v2, v3, v4):
    faces.append((mat, [v1, v2, v3, v4]))

def add_box(mat, x, y, z, w, h, l):
    hw, hh, hl = w / 2, h / 2, l / 2
    v1 = add_vertex(x - hw, y - hh, z - hl)
    v2 = add_vertex(x + hw, y - hh, z - hl)
    v3 = add_vertex(x + hw, y + hh, z - hl)
    v4 = add_vertex(x - hw, y + hh, z - hl)
    v5 = add_vertex(x - hw, y - hh, z + hl)
    v6 = add_vertex(x + hw, y - hh, z + hl)
    v7 = add_vertex(x + hw, y + hh, z + hl)
    v8 = add_vertex(x - hw, y + hh, z + hl)

    add_quad(mat, v1, v2, v3, v4) # back
    add_quad(mat, v6, v5, v8, v7) # front
    add_quad(mat, v5, v1, v4, v8) # left
    add_quad(mat, v2, v6, v7, v3) # right
    add_quad(mat, v4, v3, v7, v8) # top
    add_quad(mat, v5, v6, v2, v1) # bottom

def add_cylinder(mat, cx, cy, cz, radius, length, axis="z", segments=16):
    hr = radius
    hl = length / 2
    circle1 = []
    circle2 = []
    for i in range(segments):
        ang = (i / segments) * 2 * math.pi
        cos_a = math.cos(ang)
        sin_a = math.sin(ang)
        if axis == "z":
            p1 = add_vertex(cx + cos_a * hr, cy + sin_a * hr, cz - hl)
            p2 = add_vertex(cx + cos_a * hr, cy + sin_a * hr, cz + hl)
        elif axis == "x":
            p1 = add_vertex(cx - hl, cy + cos_a * hr, cz + sin_a * hr)
            p2 = add_vertex(cx + hl, cy + cos_a * hr, cz + sin_a * hr)
        elif axis == "y":
            p1 = add_vertex(cx + cos_a * hr, cy - hl, cz + sin_a * hr)
            p2 = add_vertex(cx + cos_a * hr, cy + hl, cz + sin_a * hr)
        circle1.append(p1)
        circle2.append(p2)
    
    for i in range(segments):
        next_i = (i + 1) % segments
        add_quad(mat, circle1[i], circle2[i], circle2[next_i], circle1[next_i])

# 1. Main Coach Body (Upper Pearl White)
add_box("BodyWhite", 0, 2.65, 0.2, 2.68, 2.1, 13.6)

# 2. Lower Red Skirt Band (Matching ENA Coach photo)
add_box("SkirtRed", 0, 1.05, 0.2, 2.70, 0.85, 13.6)

# 3. Slim Black Accent Line
add_box("AccentBlack", 0, 1.5, 0.2, 2.71, 0.08, 13.62)

# 4. Aerodynamic Front Cap & Bumper (Rounded Taper)
add_box("BodyWhite", 0, 1.45, 7.05, 2.62, 1.25, 0.75)
add_box("SkirtRed", 0, 0.78, 7.06, 2.64, 0.35, 0.76)

# 5. Gloss Black Forehead / Brow
add_box("ForeheadBlack", 0, 3.65, 6.85, 2.64, 0.42, 0.45)

# Orange ENA Logo Badge on Front Forehead
add_box("OrangeLogo", 0.65, 3.68, 7.08, 0.55, 0.24, 0.04)

# 6. Tinted Panoramic Split Windshield
add_box("WindshieldGlass", 0, 2.65, 7.02, 2.58, 1.7, 0.12)
# Destination Board "NAIROBI" behind windshield
add_box("LicensePlate", 0.62, 2.05, 7.04, 0.65, 0.22, 0.03)

# 7. Chrome MAN Horizontal V-Grille
add_box("ChromeGrille", 0, 1.35, 7.45, 1.75, 0.62, 0.08)
for gy in [-0.18, -0.06, 0.06, 0.18]:
    add_box("ChromeGrille", 0, 1.35 + gy, 7.5, 1.65, 0.035, 0.04)
# MAN chrome emblem
add_box("ChromeGrille", 0, 1.72, 7.46, 0.6, 0.12, 0.05)

# 8. Kenyan License Plate KDK 057M
add_box("LicensePlate", 0, 0.82, 7.47, 0.58, 0.18, 0.04)

# 9. Sweeping Headlamp Clusters
for side in [-1, 1]:
    add_box("WindshieldGlass", side * 1.05, 1.25, 7.42, 0.42, 0.22, 0.12)
    add_box("ChromeGrille", side * 1.05, 1.12, 7.44, 0.42, 0.03, 0.08)
    add_box("BodyWhite", side * 0.95, 0.78, 7.45, 0.24, 0.12, 0.06)

# 10. Distinctive Curved White Elephant-Ear Mirrors (Matching Photo!)
for side in [-1, 1]:
    p_roof = (side * 1.35, 3.82, 6.4)
    p_sweep = (side * 1.76, 3.52, 6.85)
    p_mid = (side * 1.82, 3.05, 6.9)
    p_low = (side * 1.68, 2.52, 6.75)
    
    add_cylinder("MirrorWhite", p_roof[0], p_roof[1], p_roof[2], 0.05, 0.4, axis="x", segments=10)
    add_cylinder("MirrorWhite", p_sweep[0], p_sweep[1], p_sweep[2], 0.05, 0.5, axis="y", segments=10)
    add_cylinder("MirrorWhite", p_mid[0], p_mid[1], p_mid[2], 0.05, 0.5, axis="y", segments=10)
    add_cylinder("MirrorWhite", p_low[0], p_low[1], p_low[2], 0.05, 0.4, axis="z", segments=10)
    
    add_box("MirrorWhite", side * 1.68, 2.52, 6.75, 0.18, 0.72, 0.28)
    add_box("MirrorGlass", side * 1.68, 2.52, 6.6, 0.15, 0.65, 0.02)

# 11. Tinted Passenger Windows with Top Sliding Vents
add_box("WindowTint", 0, 2.9, 0.2, 2.72, 1.15, 11.8)

# 12. Roof AC Unit & Spoiler
add_box("BodyWhite", 0, 3.92, 0.5, 1.85, 0.45, 4.2)
add_box("BodyWhite", 0, 3.82, -6.5, 2.68, 0.35, 0.7)

# 13. Wheels & Axles (6 Wheels: 1 Steer Axle + Dual Rear Multi-Axles)
wheel_z = [4.8, -3.2, -4.7]
for wz in wheel_z:
    for side in [-1, 1]:
        wx = side * 1.32
        add_cylinder("TireRubber", wx, 0.65, wz, 0.65, 0.38, axis="x", segments=20)
        add_cylinder("AlcoaRims", wx + (side * 0.02), 0.65, wz, 0.42, 0.39, axis="x", segments=16)
        add_cylinder("ChromeGrille", wx + (side * 0.06), 0.65, wz, 0.18, 0.41, axis="x", segments=12)

# Write OBJ file
with open(obj_path, "w", encoding="utf-8") as f:
    f.write("# ENA Coach KDK 057M MAN Master Apollo 3D Model\n")
    f.write("# Universal 3D OBJ Format for Sketchfab, SketchUp, Blender\n")
    f.write(f"mtllib {os.path.basename(mtl_path)}\n\n")
    
    for v in vertices:
        f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
    
    current_mat = None
    for mat, v_list in faces:
        if mat != current_mat:
            current_mat = mat
            f.write(f"\nusemtl {mat}\n")
        f.write(f"f {' '.join(str(idx) for idx in v_list)}\n")

print(f"Generated OBJ model with {len(vertices)} vertices and {len(faces)} faces at: {obj_path}")
