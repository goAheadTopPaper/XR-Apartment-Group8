# Review V2 · T2 Final Handoff

## A. 交付状态与证据

T2 的 UI-01 Setup、UI-02 Onboarding、UI-03 Task Book、UI-04 Help、UI-05 Global Menu 和 UI-08 Notifications 均已通过独立验收。正式 Host 联调、T3 Marker／Review 联调、Windows64 与 Quest 3／Quest Link 端到端验收均为 **Pending**。本次只核对资产和交付文档。当前分支 `feature/ycy`、HEAD `7175372f470fe3592304bac4dcefbc9363413bba`、Unity `2022.3.62f3`。

**证据分层**

| 类型 | 实际记录 |
|---|---|
| 自动化 | 最近可用的 P2-7D Final Unity MCP EditMode 作业 `e038a3a3fe9c407885ce673a6db569d5`，**122 Passed / 0 Failed / 0 Skipped**。测试源和分项数量见 B。结果来自该轮 MCP 输出，未找到本次可引用的独立 T2 XML/Markdown 测试报告。本次未重跑。 |
| 桌面 Mock | P1-H Setup 初始化与交互；P2-4D Continue／Skip、分页和 Replay；P2-3 Task Book 状态；P2-5D Help 三轮导航；P2-6D 命令、互斥及 Reset 无回执；P2-7D Final 暂停快照、unscaled Toast、会话清理、长标题。 |
| 静态／资源 | 本次 Unity MCP 读取六个 Prefab，根均启用，均为 0 Missing Scripts、0 已序列化失效对象引用。场景引用尚未在正式场景接线；Prefab 中预留的空外部引用须由 T1 绑定。 |
| 正式 Host | Pending。P2-3 曾有独立临时场景 Host 路径证据，不能覆盖 M2 正式场景 A/B 联合验收。 |
| Quest 3 | Pending。桌面 EventSystem Submit 与截图有独立证据。 |

本机原始截图目录为 `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs`。下列文件已确认存在，随团队交接转交所需图片时保留文件名。自动化详细输出见 P2-7D Final 会话中的上述作业结果。
- Setup · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P1-H-Ready.png`
- Onboarding · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-4D-4-Comfort-and-Safety.png`
- Task Book · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-3-Mock.png`
- Help · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-5D-3-Explore-and-Feedback.png`
- Global Menu · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-6D-Reset-Unconfirmed.png`
- Notifications · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-7D-Final-Long-Title.png`
- Reset／Safe Exit 与长标题并存 · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-7D-Final-Reset.png`、`C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-7D-Final-Safe-Exit.png`
- Task Book 临时 Host 对照 · `C:/Users/Doris/Documents/Codex/2026-11-16/unity-plugin-unity-openai-curated-remote-8/outputs/P2-3-Host.png`

## B. Asset & Prefab Manifest

| 模块 | Prefab 与 GUID | Runtime | EditMode 测试 | 独立场景 |
|---|---|---|---|---|
| Setup | [PREF_UI_ReviewV2_T2.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_T2.prefab) · `d9539c40fc379844185d297f2bb23d0e` | [ReviewSetupView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewSetupView.cs) | [ReviewSetupViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewSetupViewTests.cs) · 15 | [SCN_T2_Setup_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_Setup_Test.unity) |
| Onboarding | [PREF_UI_ReviewV2_Onboarding.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Onboarding.prefab) · `524cc6a2743fd9e4a89fca70109e151b` | [ReviewOnboardingView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewOnboardingView.cs) | [ReviewOnboardingViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewOnboardingViewTests.cs) · 19 | [SCN_T2_Onboarding_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_Onboarding_Test.unity) |
| Task Book | [PREF_UI_ReviewV2_TaskBook.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_TaskBook.prefab) · `e31961646abf9f647a909e327d06bdaf` | [ReviewTaskBookView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewTaskBookView.cs) | [ReviewTaskBookViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewTaskBookViewTests.cs) · 22 | [SCN_T2_TaskBook_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_TaskBook_Test.unity) |
| Help | [PREF_UI_ReviewV2_Help.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Help.prefab) · `7bb5e4d624ee5424895aa39754be4502` | [ReviewHelpView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewHelpView.cs) | [ReviewHelpViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewHelpViewTests.cs) · 21 | [SCN_T2_Help_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_Help_Test.unity) |
| Global Menu | [PREF_UI_ReviewV2_GlobalMenu.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_GlobalMenu.prefab) · `f343220a2ff3c7a4da28906e1e61b5b5` | [ReviewGlobalMenuView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewGlobalMenuView.cs) | [ReviewGlobalMenuViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewGlobalMenuViewTests.cs) · 27 | [SCN_T2_GlobalMenu_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_GlobalMenu_Test.unity) |
| Notifications | [PREF_UI_ReviewV2_Notifications.prefab](../Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2_Notifications.prefab) · `9d0247a2a78edc640b7e059afd7c2025` | [ReviewNotificationView.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/UI/ReviewNotificationView.cs) | [ReviewNotificationViewTests.cs](../Assets/_Project/Tests/EditMode/ReviewNotificationViewTests.cs) · 18 | [SCN_T2_Notifications_Test.unity](../Assets/_Project/Scenes/T2_Temp/SCN_T2_Notifications_Test.unity) |

完整类型名统一为 `XRApartment.Runtime.ReviewV2.UI.<表中 Runtime 文件名去掉 .cs>`，命名空间为 `XRApartment.Runtime.ReviewV2.UI`。六个 View 属于 `Assets/_Project/Scripts/Runtime/XRApartment.Runtime.asmdef`。该程序集引用 Data、Simulation、UnityEngine.UI、TextMeshPro、XR Interaction Toolkit、XR CoreUtils、Input System 和 HDRP Runtime。EditMode 使用 `Assets/_Project/Tests/EditMode/XRApartment.Tests.EditMode.asmdef`，引用本项目三层程序集、Unity 测试运行器、NUnit、TMP、XRI 与 HDRP。接入保留伴随 .meta，避免 GUID 变更。

| View | Inspector 与默认状态 | API、生活周期和独立验收 |
|---|---|---|
| **Setup** | 当前 Prefab 内有 Adapter 与 Controller，`setup` 注册 SetupPanel；Prefab 的 SetupPanel 初始开。站／坐按钮和选择对象、安全 Toggle、Begin、Safe Exit、Close、姿态与反馈 TMP 已在内部接线；场景提供 Host／共享 Adapter。 | `BindSession`、`OpenSetup`。启用连接快照并读首屏，禁用解订阅。Mock 实测首次启动不需重绑、姿态选项与安全确认、CompleteSetup、关闭／恢复。真实校准待设备。 |
| **Onboarding** | 面板默认关，自带 `onboarding` Controller 注册；外接 Adapter。保留四组 `pages` 的 root、Previous／Next／Replay／Skip／Continue、反馈，以及 Teleport 预留按钮和提示。 | `BindSession`、`Replay`、`Skip`、`Continue`。从快照进入 Onboarding 开启；禁用解订阅，重新启用读当前阶段。独立 Mock 的 Continue／Skip 两路径、分页、Replay 已验。真实练习落点、输入和移动门控待 T1，Experience 重看需后续展示入口。 |
| **Task Book** | `TaskBookPanel` 默认关；外接 Adapter 和 Experience Controller，内部五张 card 保留 id、标题、状态及样式引用。 | `BindSession`、`CanOpen`、`TryOpenFromMenu`、`OpenFromMenu`、`Close`、`RefreshFromSnapshot`。根保持启用，隐藏面板仍渲染 Changed；禁用退订。Mock 多状态、互斥、重新读取已验证；临时 Host 与 M2 真实目标是不同验收。 |
| **Help** | `HelpPanel` 默认关；外接 Adapter、Experience Controller 和菜单的 `menuHelpButton`，内接 panel、Back、四页和分页按钮。 | `BindSession`、`TryOpen`、`OpenFromMenu`、`Close`、`IsOpen`。启用订阅并读 Snapshot，禁用退订；监听阶段失效并检查静默解绑。打开回首页，返回恢复 Help 焦点。Mock 暂停查阅和三轮导航已验。 |
| **Global Menu** | 菜单与确认层默认关；外接 Adapter、Experience Controller、Task Book View、Task Book／Help Panel、可选 `menuOpener` 焦点目标。保留 `panel`、`confirmationPanel`、`menuBody`、任务与系统按钮、Scheme／Session／Pause／反馈与确认文案引用。 | `BindSession`、`TryOpen`、`OpenFromInput`、`Close`、`IsOpen`。启用连接 Changed 和完成事件，禁用清理整个菜单组。Mock 互斥、取消、暂停、Review、Safe Exit 已验；回执顺序由可控替身补验，真实 Host 待验。 |
| **Notifications** | Toast、Paused、Important Message 及 Action Area 默认关；外接 Adapter、实际 Global Menu 的 `confirmationPanel`。内部绑定区域、TMP、Dismiss、Optional Action。独立 Overlay，不加入互斥 Controller。 | API 和生命周期见 C。Mock 的 IsPaused、静默解绑及终态清理、Preview 布局与焦点已验，正式业务事件待接入。 |

## C. Host Integration Guide

### 唯一场景与初始化

1. T1 在唯一产品场景 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity` 建议挂载常驻 `UI / T2` 根，六个 World Space Canvas 作为实例子节点。根、唯一 Host、Rig 和 T3 仓储位于会话场景，独立于会被替换的方案世界。实例 Event Camera 指同一 XR Main Camera。复用一个 EventSystem + XR UI 输入模块和一个 XR Interaction Manager，保留各 Canvas 的 TrackedDeviceGraphicRaycaster。
2. 按 T1 手册，Scheme Port Awake -200 绑定初始世界并设置 Scheme／Model；Host Awake -100 创建唯一 Core 并读取预配保存端口。消费者在此之后订阅，再主动读 Snapshot。为六个 View 接同一个有效真实 Adapter，设置 `host`／关闭 `useMockMode`，避免 Setup 内置 Adapter 成为第二个活动来源。Setup、Onboarding 可保留各自面板 Controller；菜单、Task Book、Help 共用一个 Controller，IDs 为 `global_menu`、`task_book`、`help`。Help View 自行注册 `menuHelpButton`，无需重复添加监听。
3. Adapter 的 Host 路径转发 `Changed` 和 `OperationCompleted`，有效绑定后可发布当前快照。各 View 启用仍需主动读 Snapshot，禁用成对退订，并在重新启用时用当前状态恢复。保留 View 根启用，以支持面板隐藏期间接收状态。用 Phase 驱动首屏和阶段切换，勿建第二 Core。菜单、Help、Overlay 对静默解绑有刷新检查；正式重绑应由常驻场景统一执行和验收。
4. 正式项目不携带 `SCN_T2_*_Test`、Mock Provider、Preview Launcher、驱动用入口或静态确认监听。由 T1 迁移 M0 `ReviewIntegrationPanel` 的 F8、左手 secondaryButton 和 menuButton 唯一输入所有权到正式入口，避免只隐藏旧画面却保留监听。旧台面 Reset 入口与新菜单确认需归到同一所有者。通过 `OpenFromInput()` 开 Experience 菜单，确认层优先接收输入；移动门控须覆盖整个菜单组（含两个子页/确认层）、Pause、Busy 和终态。T1 保留其他阶段的系统入口，T4 验证真机绑定、关闭后的焦点和移动恢复。

### T1 分支手册与当前源码对照

来源为用户提供的 `ReviewV2_T1_Interface_Reference(1).md`，标注 `feature/zh @ ed7d549`、2026-11-17。已直接阅读附件；该参考文件未在当前仓库找到，本地 Git 也无法解析该提交，因此以下 M2 信息是**手册所述**，待 T1 在实际集成代码核对。

- 核心共享形状相符：`ISessionReadModel.Snapshot/Changed`、`IReviewSessionCommands.Execute(CommandType, string targetSchemeId = null)`、六个 Phase、快照身份及 Goals、`CommandResult.Accepted/Reason/RequestId`、Host `OperationCompleted` 和 `ISessionSavePort.Save(string, SessionSnapshot, Action<string,bool,string>)`。回执有 RequestId、Command、Succeeded、Message；会话身份需由请求上下文持有。T1 的 `Observed`、`Resolve` 是 T3 事实和当前空间身份来源。双方均无正式 T1 Marker CRUD 或 T2/T3 面板调用约定。
- **A/B 差异**：手册的 M2 Host 配置真实 `SCHEME_A`（`integrated-kitchen-m2-v5`）和 `SCHEME_B`（`external-fridge-m2-v8`），默认 A，可预配置 B。当前 `feature/ycy` Host 以 `new SessionController(this)` 启动 `COOKING_BASELINE`，未配置 Scheme 端口。T2 Adapter 当前 `Execute(CommandType)` 也无法转发目标 ID。需 T1/T2 协议确定已有 Host 的目标命令入口和能力表达，当前菜单保持禁用。
- **保存差异**：手册说明 M2 可在 Awake 注入端口，Finish 和切换前均调用保存，当前 T1 主场景仍空。T2 可见 M0 Host 无保存端口接线，Core 的可选 Save 仅用于 Finish 逻辑；Mock 不做真实保存且现有 Mock Adapter 不转发完成回执。
- **目标差异**：手册 M2 中 G01/G02 可用；本分支真实 GoalTracker 仅 G02 可用，Host 在成菜时完成 G02。Mock 五项均可用以模拟状态，不代表真实检测覆盖。T1/T2 接入后需验证 G01 和 G02 的真实信号、标题和状态。

### T1 Host 命令对照

下表为手册条件。终态全部拒绝；Busy 阻断除 SafeExit 外的命令。所有同步拒绝均显示真实 `Reason`，异步失败显示匹配回执的 `Message`。现状栏描述独立开发范围，所有正式产品接线均待 T1 验证。

| 命令 | 发起 UI／允许 Phase | 暂停中 | 完成与刷新 | 本分支独立状态 |
|---|---|---|---|---|
| CompleteSetup | Setup Begin／Setup | 否 | 同步无请求 ID，以 Phase 开 Onboarding | Mock 已验 |
| CompleteOnboarding | Continue 或 Skip／Onboarding | 否 | 同步无 ID，以 Phase 开 Experience | 两条 Mock 路径已验 |
| Review | 菜单 Review／Experience | 否 | 同步无 ID，Phase→Review，世界、Goals、Visit 保留 | Mock 已验，T3 页面待接 |
| Continue | T3 Review 返回／Review | 否 | 同步无 ID，Phase→Experience，世界保留 | 入口待 T3 |
| Finish | T3 Review 结束／Review | 否 | 有端口时请求 ID＋回执，以 Phase／SaveStatus 刷新；无端口同步为 Finished/NotConfigured | 正式端口与页面待接 |
| Pause | 菜单／非终态且未暂停 | 否 | 同步无 ID，IsPaused=true | Mock 已验，T2 菜单限 Experience |
| Resume | 菜单／非终态且已暂停 | 是 | 同步无 ID，IsPaused=false，当前 T2 关闭菜单 | Mock 已验 |
| Recenter | 菜单／非终态 | 是 | 同步无 ID，检查 Accepted/Reason，可能没有 Changed | Mock 仅 Unsupported，真机待验 |
| CalibrateStanding | Setup Standing／核心任意非终态 | 是 | 同步无 ID，按 Accepted/Reason 刷新姿态反馈，Visit 保留 | Mock 姿态选择已验，非硬件校准 |
| CalibrateSeated | Setup Seated／核心任意非终态 | 是 | 同步无 ID，按 Accepted/Reason 刷新姿态反馈，Visit 保留 | Mock 姿态选择已验，非硬件校准 |
| ChangeScheme | 未来方案确认／Experience，有效目标和端口 | 否 | 有 ID＋匹配回执，成功依据 SchemeId、Visit、Model、Goals、History | 禁用，待 M2 接线 |
| ResetWorld | 菜单 Reset 确认／Experience，世界端口存在 | 否 | 有 ID＋匹配回执，以新 Visit、Goals、IsBusy 刷新 | Mock Pending/Unconfirmed，替身回执已验 |
| SafeExit | Setup／菜单确认，核心任意非终态 | 是，Busy 也可 | 同步无 ID，Phase→Exited，清 pending；原 Pending 保存可转 Failed | Mock 已验，正式确认策略待 T1 |

调用前订阅；因为完成事件可能在 Execute 返回前发生，缓存早到回执，再关联当前 Session、Command 和 RequestId。Core 顺序可为 Changed 后 OperationCompleted。Accepted 有 ID 仅表示受理，IsBusy=false 或 Visit 变化不足以报告成功。丢弃终态、错 ID、重复和旧 Session 结果。

### Global Menu 重点

- **A/B**：正式接线应调用 `Execute(CommandType.ChangeScheme, targetSchemeId)`，目标为另一个受支持的 A/B ID。确认文案说明世界从初始状态开始、本次目标进度重置，历史 Marker 保留原 Scheme/Visit。Cancel 不发送。根据手册，如配置保存端口，先保存旧 Scheme/Visit，读回成功后才加载；Busy 提示为保存／切换中。成功回执到达后读新标签、Model、Goal、新 Visit。失败时保留旧标签与身份；世界回滚由 T1 端口验收。保存 Verified 且加载失败时，切换结果仍失败。本分支不具备正式切换入口。
- **Reset**：显示确认层，再发 `ResetWorld`。区分 Pending、Succeeded、Failed 与 Unconfirmed；无回执时 UI 超时提示先检查世界。真实成功应同 Scheme/Model、新 Visit、Goal 重置。失败可能留下部分世界变化，不能承诺恢复；Reset 不自动保存，T3 历史不得被清理。
- **Safe Exit**：当前 T2 有暂定确认层。确认进入 Exited，结束业务会话；不自动关闭 Unity/Player，也不会验证保存。可能存在未保存的数据，已发出的磁盘 IO 也可能继续。普通结束走 Review → Finish。正式确认策略和中止后的数据提示由 T1/T3 确认。

### Notifications 重点

`BindSession` / `RefreshFromSession` 从当前有效 Experience 的 `Snapshot.IsPaused` 驱动横幅。 `ShowToast(MessageIdentity, string, Outcome, float)` 默认 3 秒，限制 1–8 秒，使用 unscaled time；显示一条，等待最多四条，操作状态优先、同级 FIFO，满时保留先来的同级，高优先级可替换最低级的最新项。 `QueuedToastCount` 为等待数。按 Session、来源、ID、结果类别去重，滚动保留 64 项；无 ID 不按正文合并。

`ShowImportantMessage(MessageIdentity, title, message, Severity, canDismiss)` 持续展示，优先级 Safety Preview > Critical > Important，期间暂停下方 Toast；`DismissImportantMessage` 仅可关闭允许且没有确认层阻挡的消息。显式选中 Dismiss 可 Submit，Optional Action 默认隐藏。 `ClearNotifications` 清消息与队列；会话更换、失效、离开 Experience 或禁用时清理，重启只读当前快照。Safety 仅 PREVIEW；Overlay 不发命令、不订阅全量完成事件，也不接管 Reset/Safe Exit 确认。

建议 Global Menu 继续把 Reset、Recenter 结果作为本地反馈。跨面板业务消息由明确的发起 Owner 转换并提供身份；T1 确认物品恢复、安全与整会话保存来源，T3 确认 Marker 即时保存来源。Review 阶段的保存失败不在现有 Experience Overlay 门控内，须先确定 Review 页面 Owner 和适用范围。

`NotConfigured` 是没有保存端口；`Pending` 是等待；`Failed` 是权威失败；`Verified` 是已写盘并读回。 `Accepted` 为命令受理，`Succeeded` 为对应操作成功，`Unconfirmed` 为 UI 缺失可信回执，不能视为执行失败或成功。Marker 本地保存与 Session 保存分开。

### Task Book 目标映射

按 `GoalSnapshot.Id` 查 G01–G05，支持并行更新。Available=false 或缺失时显示 Unavailable，优先于三种 State；可用时映射 NotStarted、InProgress、Completed。无手动完成或 ActiveGoal 门控；零完成可以 Review，Reset／切换成功后读取新快照。

| ID | T1 手册的 M2 信号与可用性 | 当前 UI／本分支 |
|---|---|---|
| G01 | 当前 Visit 的不同食材有效存放，默认两件完成；A/B 可用 | 默认 `Goal G01`；本分支真实未接入，Mock 可模拟 |
| G02 | 真实加工开始到西红柿炒蛋成菜；A/B 可用 | 显示 `Cook a tomato and egg stir-fry`；本分支真实只确认成菜完成事实 |
| G03 | 未接入 | `Goal G03`，真实不可用 |
| G04 | 未接入，微波炉外观无加热能力 | `Goal G04`，真实不可用 |
| G05 | 未接入 | `Goal G05`，真实不可用 |

## D. T2 ↔ T3 Interface Questions

| 待确认 | 建议 Owner 与验收 |
|---|---|
| Marker 创建／编辑入口、打开关闭方法、Cancel 语义 | T3 定义方法或事件签名、对象／位置输入、取消是否保留草稿和焦点；T2 确认入口和映射。协调 Phase 与 Busy，勿新增未经确认的 Session Command。 |
| 即时保存成功／失败、重试、T2 映射及跨面板 Toast | T3 提供 MarkerId、Session/Scheme/Visit、保存尝试 ID、可读原因和重试入口，明确每条状态与真实读回。T2/T3 确认局部或全局展示所有权，避免重复提示。 |
| Review 页面入口、Continue、Finish、空列表 | T3 负责显示页面，以 Phase 驱动，Continue 和 Finish 经 Host。空 Marker 列表仍允许回顾与结束；保存失败留在 Review，保留编辑／重试。 |
| 历史 Marker 与方案/Visit 切换 | T3 按捕捉时 Session/Scheme/Visit/Model、稳定锚点及复制位置保存历史，不用新世界 Transform 覆盖。核对 Reset、A→B→A、失败回退、跨 Visit 是否能读回。 |
| Review 阶段保存失败、最终会话保存 | T3 和 T1 明确 Review 内提示、重试以及 SavePort 注入，T2 协调跨面板展示。端口回原 ID、真实写盘且读回后才回 true；回到 Unity 主线程交付回执，联合验证超时及 Safe Exit 后迟到 IO。 |

## E. T4 Quest 3 / Quest Link Device Acceptance

以下全部 **Pending**。使用正式 Review V2 Windows64 构建，经 Quest Link，记录方案、版本、设备和证据路径。

- [ ] 六个 Canvas 的 World Space 字体、面板距离、尺寸、左右手射线与控制器 Submit。亮背景确认层对比度及 Notifications 长标题＋三行正文。
- [ ] Global Menu 快捷键不双响；Task Book／Help 返回及 Dismiss 查阅和关闭；确认层 Cancel／Confirm 焦点正确，遮挡时背景控件不可误触。
- [ ] 菜单组、确认层、Pause／Resume、Busy 和退出的移动门控，以及关闭后恢复移动与焦点。
- [ ] Setup 坐姿／站姿校准、Recenter 成功及设备不支持时的消息；Onboarding 可选 Teleport 练习在 T1 接通后复验。
- [ ] 权威设备安全提示、消隐与优先级、舒适距离和用户中止行为。Preview Safety 仅为排版证据。

## F. Known Limitations & Pending Integration

- **T1**：进入正式场景集成前，先核对 M2 提交和 A/B 请求入口、唯一 Host、正式 Reset 回执、保存端口、物品恢复／安全信号、M0 输入和移动门控。用真实 Host 跑 Setup→Experience→Review、Reset、A→B→A、失败回退和终态。
- **T2**：交付六个 Prefab 与稳定引用；集成时与 T1 核对 A/B UI 能力门控与目标命令，确认 Review 阶段通知和 T3 入口，再做最小接线验证。
- **T3**：交付 Marker／Review UI、即时保存/重试与 ISessionSavePort 实现，核对零 Marker、历史、跨 Visit 与真实写盘和读回。
- **T4**：正式场景接通后执行 E，附真实设备证据。桌面 Mock、Preview、可控替身分别作为独立验收记录。
- **已知布局范围**：Notifications 已验证两行标题与三行正文组合；亮背景下确认层对比度仍须设备验证。真实 Replay/Teleport、Review 保存失败展示、正式 A/B 与设备安全动作均待集成。
