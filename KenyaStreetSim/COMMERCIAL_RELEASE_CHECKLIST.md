# Kenya Street Sim — Commercial Release Checklist (Path A: Own & Sell Your Game)

You own **100%** of your Unity 6 C# codebase, 16-town Kenyan highway network, gameplay systems, UI, and original sculpted 3D meshes. Unity Personal allows full commercial sales on **Steam, itch.io, Epic Games Store, and Google Play** up to **$200,000/year** revenue.

---

## 1. Already 100% Owned & Commercial-Safe
- **All C# Runtime & Editor Code** (`Assets/KenyaStreetSim/Scripts/` & `Editor/`)
- **16-Town Kenya Connected Highway Map, 8 Corridors, 3 Roundabouts, 4 Police Roadblocks**
- **100%-Owned Blender 5.3 Flagship Playable Fleet (Ready to Sell Commercially!):**
  - Editable Blender 5.3 Playable Studio: `BlenderStudio/Kenya_Owned_Playable_Fleet_Studio.blend` (reproducible via `tools/blender_build_playable_fleet.py`)
  - `ImportedModels/OwnedFleet/Savanna_Express_Coach_2026.glb` (13.2m Tri-Axle 6×2 Luxury Touring Coach w/ 10-row 3D walk-in cabin, seats, RHD cockpit, opening left passenger door, roof HVAC, LED route board, KDX 057M plates & 6 Alcoa wheels!)
  - `ImportedModels/OwnedFleet/Savanna_Parcel_Lorry_FRR.glb` (8.35m Heavy Commercial Parcel Lorry w/ full RHD cab interior, wind deflector, west-coast mirrors, ribbed freight box, rear swing doors w/ cam-lock rods, KDX 409G plates, 80 KPH sticker, chevrons & 4 commercial wheels!)
  - `KenyaPoliceLC79MeshFactory` (Playable Patrol Land Cruiser 79 — `KCP 380D`)
- **100%-Owned Blender 5.3 Photoreal Kenyan Passing Traffic Fleet (6 Original `.glb` Vehicles + Master `.blend`):**
  - Editable Blender 5.3 Showroom: `BlenderStudio/Kenya_Owned_Commercial_Traffic_Pack.blend` (reproducible via `tools/blender_build_kenya_vehicles.py`)
  - All 6 vehicles now feature **semi-transparent tinted glass with 3D RHD interiors (seats, dashboards & steering wheels)**, **printed Kenyan K-series license plates**, **LED Matatu route board & Highland SACCO branding**, and **80 KPH + reflective safety chevrons**!
  - `ImportedModels/RealTraffic/Kenya_Matatu_14Seater_PSV.glb` (`KDG 412B`)
  - `ImportedModels/RealTraffic/Kenya_Probox_Wagon.glb` (`KCS 704L`)
  - `ImportedModels/RealTraffic/Kenya_Prado_Highland_SUV.glb` (`KDP 900K`)
  - `ImportedModels/RealTraffic/Kenya_Hilux_DoubleCab_Pickup.glb` (`KDM 285G`)
  - `ImportedModels/RealTraffic/Kenya_Saloon_Taxi_Sedan.glb` (`KCT 518N`)
  - `ImportedModels/RealTraffic/Kenya_Canter_Commercial_Truck.glb` (`KDL 630E`)
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
