# WP D 阶段一：World Space UI 接线

正式 UI Prefab 已接入 `SCN_Kitchen_Review` 的场景实例。Core 接口及状态机未修改，`JourneyDebugUI` 组件保留但在该场景中禁用。所有改动位于 `feature/yzk`，尚未提交。

## 资源

- `Assets/_Project/Scripts/Runtime/UI/JourneyWorldSpaceView.cs`：实现 `IJourneyView.Render()`；显示状态、方案、八步进度和错误。按钮只向 `JourneyRuntimeHost.Submit()` 发送 `CompleteSetup`。
- `Assets/_Project/Prefabs/UI/PREF_UI_JourneyStageOne.prefab`：约 1.25 m 宽的 World Space Canvas，包含 `TrackedDeviceGraphicRaycaster`、两项 Setup 确认和按钮。编辑器首次导入脚本时生成；可用 `XR Apartment → UI → Rebuild Stage One UI Prefab` 重建。
- Setup 的两项勾选仅为阶段一的**人工完成确认**，不执行自动校准或练习。只有两项均选中时才发送 `PrerequisitesMet=true`；未选中时由 Core 拒绝并显示错误。没有开发用自动绕过。

## 已完成的场景接线（可在 Inspector 逐项核对）

1. `PREF_UI_JourneyStageOne` 已置于 XR 相机初始位置前方 1.4 m，世界坐标约 `(-2.85, 1.36, 2.48)`；Canvas 为 World Space，`worldCamera` 引用 XR 相机。
2. UI 实例的 `JourneyWorldSpaceView.host` 已指向 `PREF_Core_PlayValidation` 的 `JourneyRuntimeHost`。
3. Host 实例的 `viewAdapter` 已指回 UI 实例的 `JourneyWorldSpaceView`。这些跨 Prefab 引用保存在场景实例覆盖中。
4. Host 实例上的 `JourneyDebugUI` 已禁用，脚本和组件仍在；其他 Core 接线未动。
5. 场景已有 `XRUIInputModule`；Quest 3 控制器射线命中和佩戴舒适度仍需设备验收。

## 独立验收步骤

1. **资源**：在 Project 中选中 `PREF_UI_JourneyStageOne`，核对 `Canvas.renderMode=WorldSpace`、`TrackedDeviceGraphicRaycaster`、两个 Toggle、`Complete Setup` 按钮。这一步不依赖场景。
2. **场景接线**：打开 `SCN_Kitchen_Review`，按上节 1–4 项在 Inspector 核对。这一步不需要 Play。
3. **拒绝路径**：Play 后应显示 `State: Setup`。不勾选两项直接点击 Complete Setup，状态保持 Setup，错误区显示 `Required setup or orientation is incomplete.`。
4. **成功路径**：勾选校准和控制器练习两项，再点击 Complete Setup；状态变为 Orientation，Setup 控件隐藏。这两项是人工完成确认，不能代替真实校准和练习。
5. **回归**：Unity Test Runner 运行 `JourneyWorldSpaceViewTests` 和 `CorePlayWiringSmokeTests.WorldSpaceSetupButtonAndTaskCardTrackTheJourney`；后一项也检查 B1、第一步进度及暂停/恢复。最后检查 Console 编译错误数为 0。
6. **设备**：在 Quest 3 上确认 UI 位于舒适视距、文字可读、控制器 UI Ray 能操作两个 Toggle 和按钮。此项需要实机，编辑器测试无法替代。

阶段一暂不提供 Orientation、Explore、Challenge、Feedback 的正式按钮；这些状态只显示快照。完整旅程由既有 Core 和其他测试继续覆盖。

## 本地验证记录（2026-10-18）

- Unity 2022.3.62f3、`feature/yzk`、基线 `1841bd6`。
- 新 UI 的 Setup 拒绝、Setup→Orientation、B1 第一任务进度与暂停/恢复：PlayMode 测试通过。
- 全部 PlayMode：12/12 通过。全部 EditMode：86/86 通过。测试后 Unity Console 错误数为 0。
- 场景双向引用和旧 Debug UI 禁用状态已在编辑器核对，`git diff --check` 通过。
- Quest 3 实机的文字可读性、舒适视距和控制器 UI Ray 尚未测试，需按第 6 步验收。
