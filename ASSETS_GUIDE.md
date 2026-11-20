<!-- markdownlint-disable MD013 -->
# 3D Vehicle Asset & Real Model Guide 🇰🇪

This guide explains how to get, convert, and load real, high-poly 3D models into **Kenya Bus & Truck Simulator (Safari Hauler)**.

---

## 🚀 1. What Just Got Upgraded in the Simulator

The graphics and rendering pipeline have been completely overhauled to bridge the gap toward **Euro Truck Simulator 2**:

1. **PBR Lighting & Real-Time Environment Reflections**:
   - Upgraded to **ACES Filmic Tone Mapping** with dynamic PBR environment reflections.
   - Vehicle metallic car paint, chrome grilles, Alcoa rims, and glass now reflect the sky and horizon in real time instead of looking flat.

2. **Kenyan Highway Overhaul (A104 Corridor)**:
   - Tarmac asphalt highway with solid white shoulder lines and broken yellow center markings.
   - Dual-sided **Kenyan metal W-Beam crash guardrails (*chuma za barabara*)**.
   - Kilometer destination milestone signs (`A104 NAIROBI / NAKURU`).
   - Distant **Great Rift Valley mountain ridge silhouettes**.

3. **Cockpit View (`C` key) with Rotating 3D Steering Wheel**:
   - Inside the Kenyan Right-Hand Drive (RHD) cabin, you now see the instrument binnacle and an authentic **3D steering wheel that rotates in real time** when you press `A` and `D`.

4. **Live `.GLB` / `.GLTF` 3D Model Importer**:
   - Integrated **`THREE.GLTFLoader`**.
   - You can now **drag and drop any `.glb` or `.gltf` 3D vehicle file** directly into the game or select it via the Fleet Garage (`V` key).
   - The engine automatically normalizes the model's scale, aligns it with the road, and enables environment reflections and steering.

---

## 📦 2. How to Get Free ETS2-Quality 3D Models

You can find high-detail Scania buses and Isuzu trucks in standard 3D formats on the web:

### A. Sketchfab (Filter by "Downloadable" & "glTF / GLB")

- **Scania Touring / Marcopolo G7 / Scania Buses**: Search for "Scania Bus" or "Coach Bus" and check the **Free Downloadable** box.
- **Isuzu FRR / Elf / Forward Trucks**: Search for "Isuzu Truck" or "Box Truck".
- **Format**: Choose `.glb` or `.gltf`. If only `.fbx` or `.obj` is available, open it in Blender (free) and click **File ➔ Export ➔ glTF 2.0 (.glb)**.

### B. Converting Euro Truck Simulator 2 Mods (`.scs`) to `.glb`

If you have ETS2 bus/truck mods installed on your PC:

1. Use **ETS2 ConverterPIX** or Blender with the **SCS Blender Tools** plugin.
2. Import the `.pmd` / `.pmg` model from the ETS2 mod archive into Blender.
3. Export the model as a `.glb` binary file.
4. Drag and drop that `.glb` straight into our game window!

---

## 🎮 3. How to Test Your Model in the Game

1. Open the game in your browser at [http://localhost:8080](http://localhost:8080).
2. Press **V** to open the Fleet Garage.
3. Click **"Import Real 3D Model (.GLB / .GLTF)"** and choose your file (or simply drag and drop the `.glb` file anywhere on the browser window).
4. Hit **Hit the Road ➔** and drive with full physics, suspension, and engine audio!
