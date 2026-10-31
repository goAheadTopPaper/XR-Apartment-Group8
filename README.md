# XR Apartment — Kitchen Design Review

## 1. Introduction

### Group 8

- ZHANG Heng — 26106827g
- YANG Zhankun — 26124585g
- YUE Chuyu — 26108683g
- XU Jingxiang — 26047724g
- MIN Zuohong — 26050954g
- HAN Zichen — 26043276g

This is a kitchen design review prototype for COMP5424 Scenario C. Participants complete standardized everyday tasks in the same kitchen and experience and compare two predefined designs:

- **B1**: Proud + F2
- **B2**: Flush-oriented + F2

The current runtime review journey is configured for B1 and B2 only. This is not a general-purpose apartment walkthrough, a free-form interior design tool, a compliance certification tool, or a cloud service. All local geometry measurements are prototype assumptions, not client facts or regulatory conclusions.

The product scope and acceptance criteria are defined in the [Lean PRD v0.3 Final](Docs/COMP5424_Scenario_C_Lean_PRD_CN_Final.docx). Team interfaces and implementation constraints are documented in the [Module Interface Contract](Docs/Module_Interface_Contract.md).

> **New to the project? Start with the [Kitchen Design Review User Manual](Docs/User_Manual_CN.md)** for startup, posture calibration, controller practice, B1/B2 tasks, feedback, pause and resume, and safe exit.

## 2. Quick Start

### Requirements

| Item | Configuration |
| --- | --- |
| Unity | **2022.3.62f3**; do not upgrade the engine |
| Rendering | Built-in Render Pipeline, Linear Color Space |
| Input | New Input System only (`activeInputHandler: 1`) |
| XR | OpenXR + XR Interaction Toolkit 3.5.0 |
| Target experience | Windows PC build, used with **Quest 3 + Quest Link** |
| Main scene | `Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity` (city panorama and cooking) |
| Review scene | `Assets/_Project/Scenes/SCN_Kitchen_Review.unity` |
| Data | Review journey: local CSV on the host PC; no account, server, or network service required |

### Clone and Open

```bash
git lfs install
git clone https://github.com/goAheadTopPaper/XR-Apartment-Group8.git
cd XR-Apartment-Group8
git lfs pull
```

Open the repository root in Unity Hub and select **2022.3.62f3**. After UPM finishes its first package resolution, select **XR Apartment → Open Main Kitchen**. The city-panorama cooking kitchen is the first enabled scene in `ProjectSettings/EditorBuildSettings.asset`, so a Player build starts there. **XR Apartment → Cooking → Use City Kitchen as Main Scene** also sets the local Editor's Play start scene.

The formal review journey remains available through **XR Apartment → Open Kitchen Review Scene**, which clears the Editor Play override so the opened review scene can be tested. It remains the second build scene. The cooking entry point does not collect the formal review feedback or CSV records.

For headset-free testing in the Editor, use the included XR Interaction Simulator and follow its in-Play prompts (`Y`). The simulator is useful for checking interaction logic, but it does not replace spatial, comfort, or device acceptance testing with Quest Link.

The project simulator routes default FPS keyboard walking through the rig's character collision, so solid cabinets and walls block movement. Independent controller/head pose editing remains available for tracking experiments. See [Keyboard locomotion](Docs/KeyboardLocomotion.md).

Open **Movement** with the left controller's **Y / Menu**, or **F8** on the keyboard. Choose **Teleport** (the first-run default) or **Continuous**; the choice is saved locally. Continuous uses the left stick to walk; both modes retain right-stick snap turning and teleport. The settings panel stops locomotion while open. See [Movement controls and limits](Docs/LocomotionModes.md).

For headset Play testing, run the Editor on Windows, connect Quest Link (or Windows Virtual Desktop for development), and turn off **Use XR Interaction Simulator in scenes** in **Project Settings → XR Plug-in Management → XR Interaction Toolkit**. Virtual Desktop on Mac streams the desktop and does not supply this PCVR connection. Keep the Windows64 build target unchanged.

## 3. Current Implementation

### Review Journey

`JourneyRuntimeHost` runs a single session using the plan configured in the Prefab. B1 and B2 use the same task items, semantic regions, and action sequence; only the design instances in the benchmark slots change. Scene adapters collect task evidence, while the pure C# `JourneyCoordinator` exclusively owns workflow state.

```text
Setup → Orientation → Explore
      → ChallengeActive → FeedbackPending (for each design)
      → ComparisonSummary → Save and verify → Finished
```

Each challenge has eight fixed steps:

1. Approach the refrigerator.
2. Open the specified refrigerator door.
3. Take out the existing item.
4. Place it temporarily.
5. Reorganize the items.
6. Put the task item back.
7. Close the specified refrigerator door.
8. Leave the interaction area.

The current session plan uses three items: a tomato, an onion, and a cereal box. Sensor and interaction evidence is validated against stable object IDs, semantic roles, design, and task generation. The state machine rejects evidence that does not match the current step or target. Explore previews do not count as formal challenge evidence.

After completing or skipping a challenge, participants can submit feedback for that design using a world-space form. For a completed task, **Decision, Main category, and Importance** are required; experience tags and Everyday impact are optional. Skips use the recorded skip reason. The form does not collect free-text explanations. Once both designs are resolved, if at least one was completed, the final comparison requires participants to choose a preferred completed design and explain the trade-off. If one design was skipped, the interface offers a shortcut to populate the skip context. If both were skipped, the session is marked Incomplete and the preference can be left blank.

The workflow also supports pause and resume, resuming from the current step, resetting the current task, replaying the current design, returning an item, repositioning, skipping a challenge, retrying a failed save, and safe exit. Safe exit is distinct from review completion and does not imply that saving succeeded.

### Scene Interaction and Demonstration Features

- XRI controllers drive movement, teleportation, grabbing, and door/drawer interactions. Doors and drawers use kinematic logic rather than physics hinges.
- `PREF_XR_KitchenRig` provides controller-driven hand models and grip animations. Spherical palm proxies can dock to static surfaces. There is **no** camera-based bare-hand tracking, per-finger collision, or adaptive grasping.
- Held props use whole-object collision sweeps: obstacles stop the object, a translucent hand shows the tracked position, and blocked release suppresses artificial throwing velocity. The same configuration is used by both runtime designs and kitchen authoring tools.
- The kitchen water station supports filling and pouring, with finite container volume, gravity-driven streams, constrained sloshing, and splashes. The implementation uses bounded water-flow segments; it is not SPH/FLIP fluid simulation and does not include buoyancy, weight feedback, or floor puddles.
- The kitchen environment, props, daylight, sky, and baked lighting are stored in Prefabs and assets. Rebake lighting after changing static environment geometry, the sun, or emissive materials.
- The scene menu includes demonstration controls for returning to the starting point, pausing, and changing locomotion. The water system can be reset from an Editor menu. These features do not change ownership of formal review workflow state.

An additional cooking sandbox is available through **XR Apartment → Cooking → Open Cooking Kitchen**. Follow the prompts to open the fridge, retrieve tomato and eggs, slice then dice the tomato, crack and whisk an egg in a bowl, switch on the stove, place the pan, pour in the egg, let it set, scramble with the spatula, add the diced tomato and stir-fry the finished dish. Food can burn; RESET restores the ingredients, utensils and fridge. The sandbox also retains plate breaking, other prepared food cuts and the water station. [Recipe instructions, models and validation](Docs/TomatoEggRecipe.md) describe the interaction and its limits. The furniture batch passed EditMode 145/145 and PlayMode 50/50 on macOS; [subsequent cleanup checks and observed test instability](Docs/ProjectCleanup.md) record the latest reruns. Windows Player / Quest Link remain untested. This sandbox is separate from the formal review journey. [Pilot assets and measured results](ArtSource/KitchenAssets/Validation/PilotResults.md) include the character/body-rig and cabinet samples, their remaining art work, and the rejected multiview contrast.

The tomato cut surfaces and cooked egg now use two additional actual Comfy Cloud Pixal3D outputs, with visible seeds/flesh, folded egg geometry, PBR textures and egg LODs. [Current food models and before/after validation](Docs/FoodVisualUpgrade.md) document their provenance and integration; the original collision and recipe interactions are retained.

Cooking now takes place inside the small kitchen, with washing by the window, preparation on the back counter and a gas hob on the right. Select its black front knob to light/extinguish the blue gas flames. Eight further Comfy Cloud jobs supplied the fridge door and main cookware; a locally refined fridge casing and vendor ceramic meshes complete the set. [Kitchen layout, cookware sources and current verification](Docs/KitchenRoomAndCookware.md) document the changes.

The cooking sandbox also includes a matching oak dining table, four chairs, an ivory pendant and a refined single-door fridge. Four actual Pixal3D furniture jobs were reviewed; the generated wood furniture is retained, while the fridge's manufactured panels and the lamp's hollow shade were rebuilt locally for clean geometry. [Furniture sources, saved Prefabs and scene validation](Docs/KitchenFurniture.md) describe this batch and its limits.

The counter's **RESET ROOM** button restores the cooking sandbox, including loose props, food, water and fire, while keeping the player in place. A CC0 urban rooftop panorama provides a city view through the windows. [Reset scope, city asset and verification](Docs/RoomResetAndCity.md) describe this extension.

Related asset and maintenance notes:

- [Code structure and maintenance entry points](Docs/CodeStructure.md)
- [Unused imported assets and authoring-code cleanup](Docs/ProjectCleanup.md)
- [Comfy Cloud kitchen asset pipeline and current acceptance status](Docs/KitchenAssetPipeline.md)
- [Held-object collision constraints](Docs/HeldPropCollision.md)
- [Kitchen migration and scene provenance](Docs/MzhKitchenMigration.md)
- [Kitchen prop dressing](Docs/KitchenDressing.md)
- [Sky and pouring interaction](Docs/KitchenWaterAndSky.md)

## 4. Data Storage

At runtime, `LocalCsvStorageAdapter` saves anonymous sessions to:

```text
Application.persistentDataPath/ReviewRecords/sessions.csv
```

On Windows, this is normally under the current user's `AppData/LocalLow` directory. The full path is available from the storage component's **Log CSV Storage Path** menu item in Unity.

Each CSV row represents one session. Checkpoints such as per-design feedback update that session's row, and the final comparison is written to the same row. Writes are verified by reading the data back. On failure, the workflow remains at its current stage and allows a retry rather than reporting a false success. The storage format contains a readable summary and the complete `SessionRecord` JSON, with support for Chinese text and CSV escaping.

See the [Local CSV Storage guide](Docs/LocalCsvStorage.md) for the data structure, write guarantees, and failure handling.

## 5. Code Structure

```text
Assets/_Project/
  Art/                 Kitchen models, materials, and textures
  Editor/              Shared kitchen authoring, journey UI builders, and XR setup
  Prefabs/             Environment, benchmarks, interactables, and UI Prefabs
  Scenes/              SCN_Kitchen_Cooking (build entry), SCN_Kitchen_Review
  Scripts/
    Data/               Benchmarks, parameters, feedback categories, and session records
    Simulation/         Workflow state machine, task tracking, contracts, and kinematics
    Runtime/            Unity scene adapters, XR interactions, UI, water, and CSV storage
  Settings/             Benchmark registry, designs, model assumptions, and interaction parameters
  Tests/
    EditMode/            Pure logic, data, and Prefab/scene contract tests
    PlayMode/            Scene wiring and runtime journey tests
Packages/                UPM manifest and lock file
ProjectSettings/         Unity project and build scene settings
Docs/                    Interface, acceptance, and feature maintenance documentation
```

Assembly dependencies are `XRApartment.Data ← XRApartment.Simulation ← XRApartment.Runtime`. `Simulation` does not depend on Unity Engine. Core workflow state can only be advanced through the `JourneyRuntimeHost` command entry point.

Key entry points:

- Workflow and adapters: [JourneyRuntimeHost.cs](Assets/_Project/Scripts/Runtime/Core/JourneyRuntimeHost.cs), [JourneyCoordinator.cs](Assets/_Project/Scripts/Simulation/JourneyCoordinator.cs)
- Evidence collection: [RuntimeBenchmarkAdapter.cs](Assets/_Project/Scripts/Runtime/Core/RuntimeBenchmarkAdapter.cs), [TaskEvidenceBridge.cs](Assets/_Project/Scripts/Runtime/Core/TaskEvidenceBridge.cs)
- Feedback and comparison: [FeedbackFormView.cs](Assets/_Project/Scripts/Runtime/Feedback/FeedbackFormView.cs), [JourneyPhaseFiveView.cs](Assets/_Project/Scripts/Runtime/UI/JourneyPhaseFiveView.cs)
- Local storage: [CsvSessionStore.cs](Assets/_Project/Scripts/Runtime/Storage/CsvSessionStore.cs)

## 6. Tests and Acceptance

Run EditMode and PlayMode tests separately in the Unity Test Runner. Integration test results (2026-10-27, `feature/hzc` + `main@3a5c009`):

| Test mode | Passed | Failed |
| --- | ---: | ---: |
| EditMode | 133 | 0 |
| PlayMode | 28 | 0 |

PlayMode tests cover scene startup, the B1/B2 task journey, comparison input, XR ray and Editor mouse-ray targets, task-area sensors, and the recovery menu. EditMode tests cover workflow state, feedback validation, CSV, kinematics, water volume, and scene/Prefab integrity.

PlayMode also checks two constrained held props, actual XRI task-item extraction and reset in both designs, and the editor menu respecting workflow state. The current Mac installation has no Windows Build Support, so this integration has not been built as a Windows Player here.

The upstream `main` README reports an earlier Quest 3 + Quest Link acceptance pass. The combined `feature/hzc` revision, including the held-object collision integration, still requires its own Windows and Quest Link check; Editor automation does not establish hardware comfort or performance.

See the [Core Review Playbook](Docs/Core_Review_Playbook.md) and [Scene Wiring Guide](Docs/Core_Scene_Wiring.md) for workflow and joint acceptance steps. The detailed Phase 5 checklist is in [Phase 5 Manual Acceptance](Docs/UI/Phase5_Manual_Acceptance.md).

## 7. Limitations and Items to Verify

- The build and experience target is **StandaloneWindows64 + Quest Link**. Running as an Android APK or standalone Quest app is not a current delivery target.
- The runtime is configured for B1 and B2 only. Other designs in the registry or assets are not part of the current formal review journey. B3/F4, F3 Explore-only, and position markers are future scope under the PRD.
- The current F2 proxy has two vertically stacked doors, while PRD Final §6.2 requires one large door. The F3/F4 proxies also do not fully match the PRD definitions and are future scope. Any correction and acceptance must verify door count, hinges, opening direction, collisions, and registered parameters individually. A Prefab's presence or passing automation tests does not constitute design acceptance.
- The workflow contract includes conflict and safety notification interfaces, but the current core Prefab does not bind `conflictSource` / `safetySource`, and the repository has no corresponding runtime detection implementation. Therefore, **objective geometric conflict prompts, Yellow-to-Amber feedback linkage, and independent Red safety prompts are not yet implemented**.
- CSV read-back verification and automated tests do not provide a UI for restoring sessions across application launches.

All conclusions about space, dimensions, clearance, and reach must be identified as prototype assumptions supported by the register, not client facts. Requirement status and the acceptance baseline are defined in PRD Final and the [Module Interface Contract](Docs/Module_Interface_Contract.md).

## 8. Collaboration and Version Control

- Develop features on `feature/<name>` branches and merge them through pull requests. Confirm with the team before committing to or pushing shared branches, changing the build platform, or rewriting shared history.
- Follow [AGENTS.md](AGENTS.md) and the [Module Interface Contract](Docs/Module_Interface_Contract.md) for Unity version, design switching, and module ownership boundaries.
- **Do not modify `Assets/Samples/**`**. Copy assets into `Assets/_Project/` before customizing them.
- Commit each `Assets/**/*.meta` file together with its corresponding asset. `Assets/XR/`, `Assets/XRI/`, `ProjectSettings/`, `Packages/manifest.json`, and `Packages/packages-lock.json` are part of the project configuration.
- Binary art assets use Git LFS as configured in `.gitattributes`. Run `git lfs pull` after cloning.
- Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, build artifacts, signing keys, passwords, logs, or local `.qoder/settings.local.json` files.
