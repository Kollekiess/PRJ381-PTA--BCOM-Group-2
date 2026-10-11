# PRJ_VR - Cardboard VR Experience

An interactive Mobile VR project developed in **Unity 6** featuring gaze-based interaction, reticle feedback, touch/click triggers, and first-person navigation.

---

## 🚀 Features

- **Cardboard Gaze Controller (`CardboardGazeController.cs`)**:
  - Gaze raycasting for both 3D colliders and Graphic Raycaster UI elements (Buttons, Menus).
  - Configurable dwell timer with radial fill reticle (`gazeImage.fillAmount`).
  - Screen tap and mouse click input handling via the new Unity Input System.
  - Custom event dispatching (`OnGazeTrigger`) for non-UI 3D objects.
- **First Person Navigation**: Movement and camera controls tailored for VR testing.
- **Flocking / Boid Simulation**: Autonomous flocking behaviors integrated into the environment.

---

## 🛠 Prerequisites & Environment

- **Unity Version**: `Unity 6 (6000.3.9f1)` or compatible
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input System**: Unity New Input System package (`com.unity.inputsystem`)
- **Version Control**: Git with [Git LFS](https://git-lfs.github.com/) installed

---

## 📦 Setup & Installation

1. **Clone the Repository** (ensure Git LFS is installed first):
   ```bash
   git lfs install
   git clone https://github.com/<your-username>/PRJ_VR.git
   ```

2. **Open in Unity Hub**:
   - Open Unity Hub.
   - Click **Add** -> **Add project from disk**.
   - Select the `PRJ_VR` root directory.
   - Open using Unity **6000.3.9f1**.

3. **Open the Scene**:
   - Navigate to `Assets/Scenes/`.
   - Open the primary scene to explore and run the project in Play mode.

---

## 🎮 Controls & Interactions

| Action | Description |
|---|---|
| **Gaze Reticle** | Point the central reticle at buttons or interactable objects. |
| **Dwell Trigger** | Hold gaze for the configured hold duration (default: 2.0s) to trigger. |
| **Touch / Click** | Tap the screen or left-click with the mouse while gazing to click instantly. |

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
