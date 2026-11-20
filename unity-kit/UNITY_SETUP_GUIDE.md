<!-- markdownlint-disable MD013 -->
# Kenya Bus & Truck Simulator — Unity 6 Starter Kit 🇰🇪

This folder contains a complete, Unity 6 (`6000.x` URP) ready C# Starter Kit with **real Unity `WheelCollider` physics**, **Kenyan A104 Highway**, **ENA Coach KDK 057M (8-Wheel Twin-Steer)**, **EPH Isuzu FRR 90 (6-Wheel Dual Rear)**, **RHD Cockpit**, **Audio**, and a **1-Click Scene Builder**.

---

## 🚀 Quick Setup (3 Steps Once Unity 6 Finishes Installing)

1. **Create Your Project in Unity Hub:**
   - Open **Unity Hub ➔ Projects ➔ New project**.
   - Select **Universal 3D (URP)** and name it **`KenyaBusSim`**.
   - Click **Create project** and wait for the Unity Editor window to open.

2. **Drop the Kit into `Assets/`:**
   - Copy the folder `unity-kit\Assets\KenyaBusSim` directly into your new Unity project's `Assets\` folder (or tell me your Unity project path and I will copy it automatically!).

3. **Generate & Drive in 1 Click:**
   - Inside the Unity Editor top menu bar, click:
     **`Kenya Bus Sim ➔ 1. Build A104 Kenyan Highway Scene`**
   - Press **Play (▶)** (`Ctrl + P`) and drive!

---

## 🕹️ Controls Inside Unity

| Key | Action |
| :--- | :--- |
| **W / ▲** | Accelerate / Drive Forward |
| **S / ▼** | Air Brake / Reverse |
| **A / D** | Steer (Twin-Steer 8-Wheel Bus / Front Steer Isuzu) |
| **Space** | Handbrake |
| **H** | Horn (ENA Coach Melodic Chime / Isuzu Dual-Tone Air Horn) |
| **C** | Cycle Camera (ETS2 Chase Orbit ⇄ RHD Cockpit ⇄ Wheel/Axle Cam) |
| **Right-Click + Drag** | Orbit around vehicle (or look left/right inside Cockpit) |
| **V** | Switch Vehicle (ENA Coach 8-Wheeler ⇄ EPH Isuzu FRR 6-Wheeler) |
| **G** | Toggle NTSA 80 km/h Speed Governor |

---

## 🎨 Swapping in Real High-Poly 3D Bus / Truck Models (`.FBX` / `.GLB` / `.OBJ`)

Whenever you download or export a realistic high-poly 3D model from **Blender**, **Sketchfab**, or the **Unity Asset Store**:

1. Drag the `.fbx` / `.glb` / `.obj` file into `Assets/KenyaBusSim/Models/`.
2. In the Unity menu bar, click **`Kenya Bus Sim ➔ 2. Select ENA Coach VisualBodyRoot`** (or `3. Select Isuzu FRR VisualBodyRoot`).
3. Hide/disable the placeholder child boxes inside `VisualBodyRoot` and drag your high-poly 3D bus/truck prefab inside `VisualBodyRoot` — all 8/6 `WheelCollider`s, suspension, steering, camera, and audio will keep working immediately!
