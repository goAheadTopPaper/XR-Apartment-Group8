# WP D UI 阶段五人工验收清单

状态：**全部未测试**。本轮仅完成代码、Prefab 与场景接线；不得把编辑器导入或静态接线检查记为验收通过。批准设备为 Quest 3 经 Quest Link 连接 Windows PC。每项执行后由人工记录日期、设备、结果和缺陷链接；仅通过时勾选。

## Unity Editor（静态接线前提）

检查场景中 `PREF_UI_JourneyPhaseFive`、Host `viewAdapter`、`JourneyWorldSpaceView.phaseFiveView`、唯一 EventSystem、正式模块槽位和 Inspector 引用。此步骤只记录接线，不勾选下面的行为验收。

## PlayMode 行为验收（第 15 项另须 Quest 3）

- [ ] **01 Challenge → FeedbackPending**｜操作：在 PlayMode 完成 Challenge 的 Leave 步骤。预期：Core 进入 FeedbackPending，新容器显示当前方案及任务结果，旧 PendingPanel 隐藏。通过标准：无第二套正式反馈面板同时启用，当前方案 ID 正确。Requirement ID：FR-07、UX-02。状态：待集成验证（E1）。
- [ ] **02 B1/B2 草稿隔离**｜操作：接入 E1 表单 Prefab 后，分别查看其 Inspector 是否实现 `IFeedbackInputView`，并在下方 PlayMode 按 B1、B2 顺序输入。预期：外层按 session/benchmark 键保存草稿。通过标准：返回 B1 时不会出现 B2 内容。Requirement ID：FR-07、FR-12。状态：待集成验证（E1）。
- [ ] **03 字段级错误**｜操作：核对 E1 表单进入 `E1_FeedbackInputSlot`，E1 校验器接到 Host `feedbackAdapter`，然后在 PlayMode 留空必填项提交。预期：`LastFeedbackValidation.FieldErrors` 传给表单。通过标准：字段旁显示具体错误且 Core 留在 FeedbackPending。Requirement ID：FR-07。状态：待集成验证（E1）。
- [ ] **04 Completed/Skipped 规则**｜操作：核对 E1 使用 `CurrentFeedbackRequest.TaskOutcome/OutcomeReason`；PlayMode 对 Completed 打开 E1 表单并提交，对 Skipped 使用独立的“Save skip and continue”入口。预期：Completed 校验四项，Skipped 由 Host 从 Core 保存的 OutcomeReason 生成反馈并交给配置的 `IFeedbackPort` 校验、提交。通过标准：Skipped 不要求 Completed 的四项字段，且无有效跳过原因时不得推进。Requirement ID：FR-07、FR-12。状态：待集成验证（E1）。
- [ ] **05 暂停/取消/恢复草稿**｜操作：在 PlayMode 填半份反馈，关表单、重开、暂停、恢复、再重开。预期：草稿仍归属同一方案。通过标准：已输入字段逐项不丢失。Requirement ID：FR-12。状态：待集成验证（E1）。
- [ ] **06 有效反馈触发保存**｜操作：PlayMode 完成 B1 任务并用 E1 正式表单有效提交。预期：调用 `JourneyRuntimeHost.SubmitFeedback()`，Core 发保存动作。通过标准：进入 Save Pending，保存回执处理后才推进。Requirement ID：FR-07、FR-10。状态：待集成验证（E1/E2）。
- [ ] **07 保存四态**｜操作：Inspector 核对 Host `storageAdapter`；PlayMode 分别观察 None、Pending、Verified、Failed。预期：None 时隐藏提示；其余状态在界面边缘以无底色的小字显示，Failed 提供 Retry。通过标准：Verified 仅对应本地读回验证；Failed 显示失败原因。Requirement ID：FR-10。状态：待集成验证（E2）。
- [ ] **08 保存失败 Retry 幂等**｜操作：PlayMode 用正式 E2 故障注入使逐方案保存失败，按 Retry 一次。预期：输入保留，Retry 仅发 Core `Retry`。通过标准：SessionRecord 同方案反馈无重复记录，成功后前进一次。Requirement ID：FR-10、FR-12。状态：待集成验证（E1/E2）。
- [ ] **09 Summary 两组结果**｜操作：PlayMode 完成或跳过两组并进入 ComparisonSummary。预期：B1/B2 各显示任务结果与对应反馈摘要。通过标准：ID、结果、判断/原因不串组。Requirement ID：FR-09。状态：待集成验证（E1/E2）。
- [ ] **10 偏好与 trade-off**｜操作：接入 E1 最终输入组件；PlayMode 尝试选已跳过方案、空 trade-off，以及双跳过无偏好。预期：非法组合拒绝，双跳过可留空偏好。通过标准：仅符合 Core 规则的输入可经 ConfirmComparison。Requirement ID：FR-09。状态：待集成验证（E1）。
- [ ] **11 最终读回前禁止 Finished**｜操作：PlayMode 让最终保存保持 Pending 或返回 Failed。预期：保持 ComparisonSummary。通过标准：只有 Verified 后出现 Finished，Failed 时可 Retry。Requirement ID：FR-09、FR-10。状态：待集成验证（E2）。
- [ ] **12 Yellow → Amber**｜操作：接入 CF 正式来源；PlayMode 触发客观设计冲突并提交该方案有效反馈。预期：反馈前 Yellow，反馈记录后 Amber，任务不被挡住。通过标准：仅 `ConflictNotice.FeedbackRecorded` 改变文案/颜色。Requirement ID：FR-06。状态：待集成验证（CF/E1）。
- [ ] **13 Amber 与保存分离**｜操作：PlayMode 使冲突转 Amber，同时让保存失败。预期：Amber 仍写“本次会话已记录、问题未解决”，保存另显示 Failed。通过标准：UI 不把 Amber 解释成已落盘或设计已修复。Requirement ID：FR-06、FR-10。状态：待集成验证（CF/E1/E2）。
- [ ] **14 Red 安全与 Safe Exit**｜操作：接入独立 Safety 来源；PlayMode 触发和解除 SafetyNotice，再按 Pause 和 Safe Exit。预期：Red 独立于设计冲突，操作走 Host/Recovery Menu。通过标准：可暂停并安全退出；解除通知后 Red 消失且 Yellow/Amber 不变。Requirement ID：SAFE-01、FR-12。状态：待集成验证（Safety）。
- [ ] **15 Quest 3 控制器正式操作**｜操作：Quest 3 + Quest Link 启动 Windows build，以射线操作反馈、比较、Retry、Pause 和 Safe Exit。预期：单一 EventSystem/TrackedDeviceGraphicRaycaster 正常响应，通知不遮挡核心任务。通过标准：无需鼠标或持续 facilitator 代操作即可完成可用流程。Requirement ID：TECH-02、UX-02、SAFE-01。状态：待集成验证（E1/E2/CF/Safety）。

## 验收环境边界

- **Unity Editor**：只检查资源、Inspector、引用和组件数量；不能据此判定交互通过。
- **PlayMode**：执行 01–14 的状态、草稿、失败注入和通知流程；本轮未执行。
- **Quest 3 + Quest Link**：执行 15 及关键反馈/安全操作；本轮未执行。Virtual Desktop 调试不等于 TECH-02 验收。

## 依赖接线记录

`PREF_UI_JourneyPhaseFive` 已在 `SCN_Kitchen_Review` 中实例化，`JourneyWorldSpaceView.phaseFiveView`、Host、Recovery Menu、XR Origin 与 Canvas Camera 已接线。场景保留唯一 EventSystem，临时 `JourneyDebugUI` 在场景实例中禁用。Host 当前 `feedbackAdapter`、`storageAdapter` 仍为 Mock；`conflictSource`、`safetySource` 为空。E1 表单和最终输入组件分别待放入两个 `E1_*InputSlot` 并实现 `IFeedbackInputView`、`IComparisonInputView`；接入时替换 Host 的 Mock 端口。E2 到位时替换 Host `storageAdapter`。CF/Safety 到位时分别接入 Host `conflictSource`、`safetySource`。不得以 Mock 读回声称 FR-10 正式本地保存通过。

## 2026-10-18 自动化验证记录

- Unity 2022.3.62f3、StandaloneWindows64：EditMode **89/89 通过**；PlayMode **15/15 通过**。
- 脚本重新导入后，严格模式 Windows64 构建 **Success，0 errors、0 warnings**；输出为忽略目录 `Builds/Phase5_Test_Compiled/KitchenReview.exe`。
- 上述用例覆盖既有 Core 与场景流程；不替代本清单 15 项人工验收。E1/E2/CF/Safety 尚未接入，Quest 3 控制器和 Quest Link 未进行实机操作，因此所有 Checkbox 仍未勾选。
