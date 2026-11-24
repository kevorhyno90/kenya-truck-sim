"""
Builds a ready-to-use Blender 5.3 Asset Library (Kenya_CC0_PBR_Library.blend) pre-loaded with all
downloaded Poly Haven & AmbientCG CC0 PBR materials (Albedo + Normal Maps) marked as Blender Assets!
"""

import os
import bpy

_script_dir = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else ""
REPO_ROOT = os.path.abspath(os.path.join(_script_dir, "..")) if _script_dir else r"C:\Users\USER\.gemini\antigravity\scratch\kenya-truck-sim"
CC0_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "Assets", "KenyaStreetSim", "Textures", "CC0_PBR")
STUDIO_DIR = os.path.join(REPO_ROOT, "KenyaStreetSim", "BlenderStudio")
OUT_BLEND = os.path.join(STUDIO_DIR, "Kenya_CC0_PBR_Library.blend")

MATERIALS = [
    ("PH_Asphalt_Highway",      "CC0_Asphalt",            0.78, 0.0),
    ("ACG_Kenya_Tarmac",        "ACG_KenyaHighwayTarmac", 0.74, 0.0),
    ("PH_Kenya_Red_Murram",     "CC0_KenyaRedMurram",     0.90, 0.0),
    ("PH_Cabro_Pavers",         "CC0_Cabro",              0.80, 0.0),
    ("PH_Town_Plaster",         "CC0_Plaster",            0.82, 0.0),
    ("PH_Stone_Wall_Masonry",   "CC0_KenyaStoneWall",     0.85, 0.0),
    ("PH_Corrugated_Mabati",    "CC0_Mabati",             0.42, 0.65),
    ("ACG_Corrugated_Steel",    "ACG_KenyaMabatiRoof",    0.38, 0.72),
    ("PH_Market_Timber_Wood",   "CC0_KenyaMarketWood",    0.84, 0.0),
    ("PH_Acacia_Tree_Bark",     "CC0_Bark",               0.88, 0.0),
    ("PH_Savanna_Grass_Rock",   "CC0_SavannaTerrain",     0.88, 0.0),
    ("ACG_Highland_Grass",      "ACG_KenyaHighlandGrass", 0.85, 0.0),
    ("PH_Forest_Floor_Leaves",  "CC0_KenyaForestFloor",   0.86, 0.0),
]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(STUDIO_DIR, exist_ok=True)
    created = 0

    for mat_name, prefix, rough, metal in MATERIALS:
        alb_path = os.path.join(CC0_DIR, f"{prefix}_Albedo.jpg")
        nrm_path = os.path.join(CC0_DIR, f"{prefix}_Normal.jpg")
        if not os.path.isfile(alb_path):
            continue

        mat = bpy.data.materials.new(name=mat_name)
        mat.use_nodes = True
        nt = mat.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal

        alb_node = nt.nodes.new("ShaderNodeTexImage")
        alb_node.image = bpy.data.images.load(alb_path, check_existing=True)
        nt.links.new(alb_node.outputs["Color"], bsdf.inputs["Base Color"])

        if os.path.isfile(nrm_path):
            nrm_node = nt.nodes.new("ShaderNodeTexImage")
            nrm_node.image = bpy.data.images.load(nrm_path, check_existing=True)
            nrm_node.image.colorspace_settings.name = 'Non-Color'
            bump = nt.nodes.new("ShaderNodeNormalMap")
            bump.inputs["Strength"].default_value = 1.0
            nt.links.new(nrm_node.outputs["Color"], bump.inputs["Color"])
            nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])

        mat.asset_mark()
        mat.use_fake_user = True
        created += 1

    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print(f"[PBR LIBRARY] Saved {created} Poly Haven + AmbientCG Blender 5.3 Asset Materials -> {OUT_BLEND}")


if __name__ == "__main__":
    main()
