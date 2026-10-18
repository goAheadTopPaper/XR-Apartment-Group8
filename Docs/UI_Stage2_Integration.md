# WP D UI 阶段二：Setup / Orientation 接线记录

**范围**：只实现 Setup 与 Orientation。流程状态及拒绝规则继续由 Core Snapshot / `JourneyRuntimeHost.Submit()` 决定。未修改 Core、公共契约、Explore/Challenge/Feedback 实现。

## 已实现

- `JourneyWorldSpaceView` 复用阶段一的 `IJourneyView`、状态/方案/任务/错误显示，增加三页 Setup 和五页 Orientation；返回、下一步与安全退出常驻引导页。
- Setup / Orientation 面板保留 World Space Canvas，但运行时定位到 XR 相机前方 1.7 m，并按相机水平朝向转正，避免固定在厨房世界坐标后落在启动视野之外。
- Setup 说明代表性厨房、B1 Proud / B2 Flush 的受控比较、原型模型限制和实体 play area 安全要求；第二页解释 Fixed、Reviewable、Simplified、Assumption。第三页必须明确选择站姿或坐姿，可按 5 cm 调整目标视点，调用 `XROrigin.MoveCameraToWorldLocation` 校准虚拟视点，才提交 `CompleteSetup`。
- Orientation 从实际 `TeleportationProvider.locomotionEnded`、场景练习杯的 `XRGrabInteractable.selectEntered/selectExited`、UI 控制器选择按钮及 Host `Recenter` 场景回执记录练习。重置回执成功后恢复已校准的目标视点高度。Teleport、选择、抓起并松开、重置成功及退出说明均完成后，才提交 `AcknowledgeOrientation` 进入 Explore。安全退出提交 `RequestExit`，不会作为模拟练习自动执行。
- 每次 Core `Render` 决定 Setup / Orientation 可见性和 busy 状态；错误文本显示命令拒绝或场景失败。UI 不自行更改 Core 状态。

## Prefab 与共享场景

| 对象 | 引用和改动 |
| --- | --- |
| `PREF_UI_JourneyStageOne.prefab` | 扩充既有 World Space Canvas 为 1200×800、缩放 0.0011；保留 `TrackedDeviceGraphicRaycaster`，加入 Setup / Orientation 页、操作按钮和提示文本；未加入 EventSystem。 |
| `SCN_Kitchen_Review/PREF_UI_JourneyStageOne` | `JourneyWorldSpaceView.host` 指向场景 Host；`xrOrigin` 指向 `PREF_XR_KitchenRig`；`teleportationProvider` 指向 Rig 的 Teleportation；`practiceGrabTarget` 指向场景 `PREF_Prop_Cup`；Canvas worldCamera 指向 XR Main Camera。 |
| `SCN_Kitchen_Review/PREF_Core_PlayValidation` | `JourneyRuntimeHost.viewAdapter` 指向上述视图；原 `JourneyDebugUI` 组件保持禁用、文件保留。 |
| `SCN_Kitchen_Review/PREF_UI_KitchenMenu` | 场景实例 inactive、旧 `KitchenMenuController` disabled；原 Prefab 和脚本保留。 |
| EventSystem / 输入 / Rig | 复用场景唯一 EventSystem、XR UI Input Module、Rig 上现有控制器 UI interactor；未新建实例或输入模块。 |

`Assets/Editor/JourneyUiPrefabBuilder.cs` 提供 Prefab 重建及场景接线菜单。首次导入时仅在 Prefab 缺少 Orientation 面板的情况下重建；场景接线由单独的 `Wire Setup Orientation In Review Scene` 菜单执行。本轮已执行并保存共享场景。

## 尚未验证

本轮按要求不运行 EditMode、PlayMode、Quest Link 或 Build。Quest 3 控制器射线命中、站坐校准舒适度、传送与练习杯的现场可达性、重置成功回执、错误和安全退出路径，均须在用户发出测试指令后验收。视点高度使用 1.65 m / 1.20 m 的引导默认值，可调整到 0.90–1.95 m；属于 UI 校准目标，不是客户身高事实或建筑尺寸结论。

## 后续独立验收步骤（未执行）

1. Inspector 核对上述场景引用、单一 EventSystem、World Space Canvas 和旧菜单/Debug UI 的禁用状态。
2. 在 Play/Quest Link 中分别走站姿、坐姿 Setup；检查虚拟视点与实体 play area。
3. 使用 Quest 3 控制器射线完成 Teleport、选择按钮、练习杯抓取并松开及 Reset view；确认未完成时不能进入 Explore。
4. 验证 Back、命令拒绝提示、场景失败提示及 Safe exit；确认进入 Explore 后无重复引导 UI。
