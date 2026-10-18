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
| Scene | `Assets/_Project/Scenes/SCN_Kitchen_Review.unity` |
| Data | Local CSV on the host PC; no account, server, or network service required |

### Clone and Open

```bash
git lfs install
git clone https://github.com/goAheadTopPaper/XR-Apartment-Group8.git
cd XR-Apartment-Group8
git lfs pull
```

Open the repository root in Unity Hub and select **2022.3.62f3**. After UPM finishes its first package resolution, select **XR Apartment → Open Kitchen Review Scene** from the Unity menu, or open the scene listed above directly. This scene is enabled in `ProjectSettings/EditorBuildSettings.asset`.

For headset-free testing in the Editor, use the included XR Interaction Simulator and follow its in-Play prompts (`Y`). The simulator is useful for checking interaction logic, but it does not replace spatial, comfort, or device acceptance testing with Quest Link.

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
- The kitchen water station supports filling and pouring, with finite container volume, gravity-driven streams, constrained sloshing, and splashes. The implementation uses bounded water-flow segments; it is not SPH/FLIP fluid simulation and does not include buoyancy, weight feedback, or floor puddles.
- The kitchen environment, props, daylight, sky, and baked lighting are stored in Prefabs and assets. Rebake lighting after changing static environment geometry, the sun, or emissive materials.
- The scene menu includes demonstration controls for returning to the starting point, pausing, and changing locomotion. The water system can be reset from an Editor menu. These features do not change ownership of formal review workflow state.

Related asset and maintenance notes:

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
  Prefabs/             Environment, benchmarks, interactables, and UI Prefabs
  Scenes/              SCN_Kitchen_Review (build entry point)
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

Run EditMode and PlayMode tests separately in the Unity Test Runner. Most recent test results (2026-10-18):

| Test mode | Passed | Failed |
| --- | ---: | ---: |
| EditMode | 120 | 0 |
| PlayMode | 18 | 0 |

PlayMode tests cover scene startup, the B1/B2 task journey, comparison input, XR ray and Editor mouse-ray targets, task-area sensors, and the recovery menu. EditMode tests cover workflow state, feedback validation, CSV, kinematics, water volume, and scene/Prefab integrity.

These are Editor automation results. End-to-end acceptance has also been completed and passed with the target **Quest 3 + Quest Link** setup.

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
