# UI 阶段 0：开发基线

核对日期：2026-10-18。范围：`feature/yzk` 上的 P0 B1/B2 Kitchen Design Review UI。阶段 0 只记录现状，不改变流程或共享场景。

## Git 与项目

- 仓库根目录：`repo/`；分支：`feature/yzk`。
- HEAD：`1841bd6 feat(review): 落地核心评审旅程与场景联调 [FR-03 FR-11 FR-12]`。
- HEAD 与 `origin/feature/zh` 相同；核对前工作区干净。现在仅新增本阶段的这份记录，尚未提交。`test` 是暂定集成分支，尚未合入。
- Unity 版本由 `ProjectSettings/ProjectVersion.txt` 锁定为 `2022.3.62f3`；唯一启用的构建场景为 `Assets/_Project/Scenes/SCN_Kitchen_Review.unity`。
- Unity 编辑器已打开本项目。编辑器日志未发现 `error CS`、编译失败或 `Journey startup` 错误。用户已确认阶段 0 的 Play 实测通过；具体按钮和八步结果未逐项记录。

## 五个关键文件的接入关系

| 文件 | 阶段 0 结论 |
| --- | --- |
| `Runtime/Debug/JourneyDebugUI.cs` | 临时 `IJourneyView` 实现。`Render()` 接收 Snapshot、错误码和消息；`OnGUI()` 按状态显示操作入口。Setup/Orientation/Explore/Challenge/Feedback/Comparison 的按钮和文案可作功能参考，不作为正式 XR 界面。 |
| `Runtime/Core/JourneyRuntimeHost.cs` | `viewAdapter` 按 `IJourneyView` 调用 `Render()`；UI 命令走 `Submit(JourneyCommand)`，反馈走 `SubmitFeedback(FeedbackDraft)`。Host 创建 Core、转发场景证据和通知，并处理场景/存储回调。 |
| `Simulation/JourneyContracts.cs` | `JourneySnapshot` 是只读呈现来源；`JourneyCommandType` 定义操作；`IJourneyView.Render(snapshot, errorCode, message)` 是正式 UI 的接口。 |
| `Runtime/Core/TaskTargetGuide.cs` | 已按 Snapshot 的当前步骤显示目标箭头和区域提示；正式 UI 可复用，不需重建对象追踪。 |
| `Runtime/KitchenMenuController.cs` | 旧菜单的 `TogglePause()` 直接改 `Time.timeScale`，`ResetToStart()` 直接移动 XR Rig；正式菜单必须改走 Host 命令与状态。 |

`PREF_Core_PlayValidation.prefab` 已给 Host 关联场景适配器、Mock Feedback、Mock Storage 和临时 View；`conflictSource`、`safetySource` 仍为空。正式接线由共享场景集成人协调。

## Play 验证记录

用户已于 2026-10-18 反馈“Play 实测已经通过”。阶段 0 验证视为通过；未收到逐项操作记录，因此不把完整八步、Quest Link 或正式 UI 验收纳入该结论。

## 后续 UI 文件清单（建议，实施时按模块契约核对）

- 新增 `Assets/_Project/Scripts/Runtime/UI/JourneyUIView.cs` 及 `.meta`：实现 `IJourneyView`，只根据 Snapshot 与错误呈现状态。
- 新增 `Assets/_Project/Scripts/Runtime/UI/JourneyUIActions.cs` 及 `.meta`：将 XR 按钮、菜单和输入映射到 Host 的 `Submit()` / `SubmitFeedback()`，不直接推进 Core。
- 新增 `Assets/_Project/Prefabs/UI/PREF_UI_Journey.prefab` 及 `.meta`：独立世界空间 UI，包含 Setup/Orientation、Explore、Challenge、Comparison 与通用状态/错误区域；Feedback 表单组件由 E1 提供。
- 修改 `Assets/_Project/Prefabs/Core/PREF_Core_PlayValidation.prefab`：集成时将 `viewAdapter` 指向正式 UI，临时 OnGUI 留作调试或移除；由场景集成人统一接线。
- 修改 `Assets/_Project/Prefabs/PREF_UI_KitchenMenu.prefab` 与 `Assets/_Project/Scripts/Runtime/KitchenMenuController.cs`：将暂停、恢复、重置、回中心和退出入口与 Host 状态对齐；此处涉及共享菜单，实施前与集成人协调。
- 按实际 UI 行为新增 PlayMode 验证，覆盖可用命令、错误、暂停/恢复与输入保留；不复制 Core 的状态机测试。

若需改 `JourneyContracts.cs` 或 `JourneyRuntimeHost.cs` 的共享接口，先更新 `Docs/Module_Interface_Contract.md` 并与 Core 负责人对齐。
