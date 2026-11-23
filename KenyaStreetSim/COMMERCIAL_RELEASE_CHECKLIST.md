# Kenya Street Sim — Commercial Release Checklist (Path A: Own & Sell Your Game)

You own **100%** of your Unity 6 C# codebase, 16-town Kenyan highway network, gameplay systems, UI, and original sculpted 3D meshes. Unity Personal allows full commercial sales on **Steam, itch.io, Epic Games Store, and Google Play** up to **$200,000/year** revenue.

---

## 1. Already 100% Owned & Commercial-Safe
- **All C# Runtime & Editor Code** (`Assets/KenyaStreetSim/Scripts/` & `Editor/`)
- **16-Town Kenya Connected Highway Map, 8 Corridors, 3 Roundabouts, 4 Police Roadblocks**
- **100%-Owned Blender 5.3 Studio Kenyan Traffic Fleet (6 Original `.glb` Vehicles + Master `.blend`):**
  - Editable Blender 5.3 Showroom: `BlenderStudio/Kenya_Owned_Commercial_Traffic_Pack.blend` (reproducible via `tools/blender_build_kenya_vehicles.py`)
  - `ImportedModels/RealTraffic/Kenya_Matatu_14Seater_PSV.glb` (14-Seater PSV Matatu w/ inset recessed windows, yellow PSV belt, route destination box & bullbar)
  - `ImportedModels/RealTraffic/Kenya_Probox_Wagon.glb` (Everyday Kenyan Station Wagon w/ roof rails & 5-spoke alloy rims)
  - `ImportedModels/RealTraffic/Kenya_Prado_Highland_SUV.glb` (Executive 4x4 Highland SUV w/ vertical chrome grille, fender flares, running boards & rear spare cover)
  - `ImportedModels/RealTraffic/Kenya_Hilux_DoubleCab_Pickup.glb` (Double-Cab 4x4 Pickup w/ chrome sports rollbar, fender flares & cargo liner)
  - `ImportedModels/RealTraffic/Kenya_Saloon_Taxi_Sedan.glb` (3-Box Highway Saloon Sedan w/ recessed windows & 5-spoke alloys)
  - `ImportedModels/RealTraffic/Kenya_Canter_Commercial_Truck.glb` (Cab-Over Commercial Cargo Truck w/ wind deflector, slatted grille, solid mirror brackets & Kenyan safety stripes)
- **Original Sculpted 3D Vehicles (Owned by You):**
  - `KenyaPoliceLC79MeshFactory` (Playable Patrol Land Cruiser 79)
  - `KenyaOwnedTrafficMeshFactory` (Backup sculpted traffic meshes)
- **CC0 2K PBR Textures & 3 African Highland Panoramic Skyboxes** (`Textures/CC0_PBR/` — Poly Haven CC0)
- **Real Field-Recorded Audio** (`Assets/KenyaStreetSim/Audio/` — Public Domain / CC0 / CC BY 4.0; keep `AUDIO_CREDITS.txt` included in your in-game Credits screen!)

## 2. Replace Before Paid Commercial Store Launch
The following prototype models in `ImportedModels/` are great while building/testing, and should be swapped for **Unity Asset Store commercial packs, Blender custom models, or verified CC0/CC-BY commercial assets** before publishing a paid build:
- `ImportedModels/ScaniaTouring2026/Scania_Touring_2026.glb`
- `ImportedModels/Fleet/Isuzu_FRR_Kenyan.glb`
- `ImportedModels/Fleet/Isuzu_TX_Lorry.glb`
- `ImportedModels/Fleet/Marcopolo_G7_Volvo.glb`
- `ImportedModels/Fleet/Marcopolo_G7_1200_Express.glb`
- `ImportedModels/Fleet/Yutong_ZK6888H_ETS2.glb`
- `ImportedModels/Fleet/RangeRover_Sport_2018.fbx`
- `ImportedModels/People/` (verify commercial license or swap with Mixamo/Quaternius CC0 characters)

## 3. Trademark-Safe Brands Switch (Built Right Into Unity!)
Before selling publicly, open:
**`Kenya Street Sim → Commercial Ownership & Traffic Manager`**
and turn **ON** **"Use 100% Trademark-Safe Original Brands"**, then click **"Rebuild 16-Town Kenya Map"**.
This replaces trademarked real company signs (`SAFARICOM`, `M-PESA`, `RUBIS`, `NAIVAS`, `KCB`, `EQUITY`, `CARREFOUR`) with original Kenyan commercial-safe names (`SAFARITEL`, `M-PAY`, `RUBIX ENERGY`, `NAVA MART`, `K-BANK`, `HERITAGE BANK`, `METRO EXPRESS`).

## 4. How to Add Unlimited Commercial Real 3D Traffic Cars
1. Download/buy a commercial Unity Asset Store car/truck pack or export your own Blender `.glb`/`.fbx` vehicles.
2. Drop them into **`Assets/KenyaStreetSim/ImportedModels/RealTraffic/`**.
3. Open **Commercial Ownership & Traffic Manager** → click **Rebuild 16-Town Kenya Map**.
4. Click **Build Standalone Windows .exe** to export your ready-to-sell PC build in `Builds/KenyaStreetSim/KenyaStreetSim.exe`!
