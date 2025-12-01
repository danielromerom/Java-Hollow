# Java-Hollow Copilot Instructions

## Project Overview
Java-Hollow is a Unity VR project creating an immersive forest café experience. It features interactive elements like chair seating, coffee pouring, marshmallow roasting, and a dynamic day/night cycle with glowing objects.

## Architecture
- **Framework**: Unity with XR Interaction Toolkit for VR interactions.
- **Structure**: MonoBehaviour scripts attached to GameObjects handle logic. Key components:
  - `DayAndNight.cs`: Manages skybox blending, lighting transitions, and object glow based on sun rotation.
  - `ChairSeatController.cs`: Handles VR teleportation to chairs, disabling locomotion during seating.
  - Interactive scripts (e.g., `CupPourer.cs`, `MarshmallowRoasting.cs`) use XR grab and trigger events.
- **Data Flow**: Input from VR controllers triggers events (select, grab) that update object states, positions, and visuals.
- **Why**: Designed for cozy, interactive VR experiences; structural decisions prioritize immersion over complexity.

## Developer Workflows
- **Build**: Use Unity Editor (File > Build Settings) for platform-specific builds (e.g., Oculus Quest).
- **Debug**: Run in Unity Play mode; use Debug.Log for output. VR testing requires connected headset.
- **Custom Tools**: `ApplyTerrainSettingsAll.cs` (Tools > Apply Terrain Settings to All) optimizes terrain rendering.
- **Version Control**: Commit .meta files alongside assets; ignore Library/ and Temp/.

## Conventions and Patterns
- **Script Organization**: Place custom scripts in `Assets/Personally Imported-Developed/Scripts/`.
- **Asset Naming**: Folders by source (e.g., `Bioluminescent_Mushrooms/`, `CoffeeShopStarterPack/`).
- **Lighting**: Use gradients for color transitions (e.g., sunset tint in `DayAndNight.cs`).
- **Collections**: Use `List<Renderer>` or `List<Light>` for batch updates (e.g., glow objects).
- **Coroutines**: For timed sequences (e.g., delayed sitting in `ChairSeatController.cs`).
- **XR Integration**: Leverage `TeleportationAnchor`, `GrabInteractable` for interactions; disable locomotion during seated states.
- **Emission Materials**: Enable `_EMISSION` keyword and set `_EmissionColor` for glowing effects.

## Dependencies and Integrations
- **Unity Packages**: XR Interaction Toolkit, TextMesh Pro, Terrain tools.
- **External Assets**: Imported 3D models from asset stores (e.g., Fantasy Forest Environment).
- **Cross-Component**: Scripts communicate via Unity events and serialized references; no custom event systems.

## Key Files
- `Assets/Java-Hollow.unity`: Main scene with terrain, lighting, and interactive objects.
- `Assets/Personally Imported-Developed/Scripts/DayAndNight.cs`: Exemplifies lighting and material manipulation.
- `Assets/ApplyTerrainSettingsAll.cs`: Shows custom editor menu integration.</content>
<parameter name="filePath">/Users/mazinsaleh/GitHubProjects/Java-Hollow/.github/copilot-instructions.md