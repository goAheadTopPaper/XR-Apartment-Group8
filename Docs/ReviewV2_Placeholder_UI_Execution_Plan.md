# Review V2 最简完整占位 UI 执行计划

日期：2026-11-17（Asia/Shanghai）。状态：**P1–P5 Verified — Independent / Ready for T3 Integration；P6 待 T3 交付**。

初版按“先写执行计划”仅产出文档；负责人随后明确“执行”，已按该范围完成 P1–P5。以下验收条件保留，完成证据在 M2 进度与接入清单中记录，使用方式见 [占位UI使用与T3接线](ReviewV2_Placeholder_UI_Usage.md)。

## 1. 目标与当前依据

T2 正式 UI 暂未交付，T3 已完成但成果尚未交到本工作区。先扩展现有占位 UI，让负责人能操作完整页面流程；T3 到位后，通过适配接入真实标记、回顾和存储。

核对基线：`feature/zh`，HEAD `ed7d549`，包含现有未提交修改。唯一产品场景为 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`。

| 当前事实 | 实施影响 |
|---|---|
| `ReviewIntegrationPanel` 已有设置、引导、暂停、A/B 切换确认、重置确认、回顾/返回和结束入口 | 在原有 UI 上增量扩展，保留现有功能、对象引用与 Prefab GUID |
| 唯一 `ReviewSessionHost` 已提供 Snapshot、Changed、Execute、OperationCompleted、Observed 和 Resolve | 会话阶段、方案、目标及异步操作结果继续由 T1 提供 |
| T1 没有 CreateMarker/GetMarkers 等正式标记接口；当前工作区未发现 V2 T3 正式交付组件 | 不臆造 T3 方法或落盘格式；Marker 数据与存储继续等待交付 |
| `sessionSavePort` 在 Host Awake 时注入；当前可为空 | 先保持真实的未接入状态；接入 T3 时必须在 Awake 前完成引用配置 |
| 用户已明确点击问题解决 | 不重新修改现有输入配置；验证新增界面的实际点击是否正常 |

本轮假设：首版用于 Editor 键鼠/模拟器联调；中文为主，保留关键英文标签；沿用 Unity 2022.3.62f3、现有 uGUI/TMP、Rig 和渲染配置。Quest Link 的文字输入体验随正式交付另行验证。

## 2. 最简页面及交互

会话主流程保持 `Setup → Onboarding → Experience ↔ Review → Finished`，中止仍到 Exited。Mark 编辑、菜单和确认窗口属于界面状态，不新增 Core Phase。

| 页面/窗口 | 操作与内容 | T3 未接入时 |
|---|---|---|
| 初始设置 | 移动方式、站姿/坐姿校准、重新居中、确认设置 | 使用现有真实功能 |
| 操作引导 | 简短说明探索、抓取、Mark、Review 的用途；开始体验 | 明确标注标记/存储未接入，不把一页说明当完整教学验收 |
| 体验快捷入口 | 提供 Mark、Review、菜单三个直接入口；显示当前方案 | Mark 可打开占位编辑页，Review 可打开占位看板；菜单沿用 F8/左手 Y |
| Mark 编辑 | 当前方案提示、位置区域、备注输入、提交、取消/返回 | 显示“标记服务未接入”；位置捕捉和提交禁用；备注可试填，标注“界面草稿，未提交、未保存” |
| Review 看板 | 当前方案/全部方案筛选区域、标记列表区域、详情区域、继续体验、结束体验 | 显示“T3 未接入，暂不能读取标记”；筛选/记录详情操作禁用，继续和结束仍走真实 Core |
| 暂停/设置 | 恢复、移动方式、校准、中止 | 沿用现有暂停状态与移动门控 |
| A/B 切换、重置确认 | 明确进度重置与反馈归属，确认/取消、等待、失败及重试 | 沿用真实 T1 切换/重置；不新增反馈持久化承诺 |
| 结束/中止 | 显示实际会话与保存结果；保存失败时可重试 | 无存储端口时显示“体验已结束，数据未保存”；SafeExit 不显示保存成功或应用已关闭 |

体验快捷入口固定在可发现的位置，沿用现有射线点击方式；首版不新增手柄快捷键，避免与已有动作冲突。Mark 不要求先进入 Review。

Review 的“未接入”“已接入且无标记”“读取中”“有标记”“读取失败”必须可区分。当前只进入未接入状态；其余状态先做好显示结构，后续由真实 T3 结果驱动。生产界面不放演示 Mark，不把空占位列表显示成真实的零条反馈。

## 3. 页面和数据边界

1. **T1 保持会话唯一所有者。** 页面由最新 Snapshot 渲染；Review/Continue/Finish、Pause/Resume、ChangeScheme、ResetWorld 等仍调用 Execute。Accepted 只代表接受请求，异步成功按匹配 requestId 的 OperationCompleted 判断，并兼容同步完成。
2. **UI 只保存短期表单草稿。** T3 未接入时，不生成正式 Marker ID、世界标记、事件仓储或磁盘文件。试填内容不混入正式反馈。取消清理草稿；非空草稿关闭前提示丢弃，允许返回继续填写。
3. **T3 相关 Inspector 引用允许为空。** 显式显示不可用状态，禁用提交/读取动作；空引用不得抛异常。`sessionSavePort` 保持为空，不挂空实现或返回成功的替身。
4. **只预留占位 UI 内部的适配位置。** 收敛标记能力可用性、打开/提交/取消、列表/详情和状态更新这些需求；不提前冻结公共方法名、Marker DTO、Schema 或上下文窗口。收到 T3 后按实际组件做最小适配。
5. **UI、Host 和未来 T3 组件归会话场景。** 不挂进 A/B 切换时销毁的方案世界。UI 不长期持有可销毁对象的 Transform 作为历史位置。
6. **避免堆大控制器。** 保留统一菜单输入和会话命令入口；Mark 表单、Review 列表/详情分别由小型视图组件负责。沿用现有 Runtime 程序集，不为占位 UI 建立通用服务框架或第二套 SessionController。

## 4. 实施顺序和每步交付

| 步骤 | 具体工作 | 完成证据 |
|---|---|---|
| P1：保护基线 | 记录工作树、当前场景/未保存状态、现有输入与 Prefab 引用；保留用户已有修改 | 基线记录；修改范围清单 |
| P2：统一界面结构 | 原位扩展既有 Prefab，整理设置、引导、菜单；增加体验快捷入口、Mark 表单和 Review 看板容器 | 可编译；页面截图；既有引用保持且无重复 EventSystem/菜单监听 |
| P3：完成无 T3 的页面流程 | 接真实 T1 状态和命令；完成 Mark 界面草稿、取消/丢弃确认、未接入提示；Review 分离未接入与空数据状态 | 不配置 T3 也能进入/退出所有占位页面；提交保持禁用且不会假保存 |
| P4：处理生命周期与边界 | 处理 Busy、暂停、结束、中止、禁用/重启用、Visit 变化及确认窗口互斥 | 不重复订阅；不重复发命令；不把旧草稿带入新方案 |
| P5：验证与交接 | 执行有意义的定向测试和实际输入操作，检查布局与字体，更新进度和接入说明 | 测试报告、页面截图、实际点击证据及 T3 待接清单 |
| P6：T3 交付后联调 | 检查真实交付接口，接标记/列表/保存，完成真实闭环 | 真实标记、历史归属和写盘读回证据；依赖 T3 到位，单独记录验收状态 |

P1–P5 可先独立实施，P6 等交付后再开展。T3 缺失不阻止完成 UI 外壳，也不以完成 UI 外壳宣称真实反馈闭环通过。

## 5. 预计修改范围

| 路径 | 预计变化 |
|---|---|
| `Assets/_Project/Scripts/Runtime/ReviewV2/ReviewIntegrationPanel.cs` | 接入页面显示与快捷入口，保留 Host 命令和统一菜单控制 |
| `Assets/_Project/Scripts/Runtime/ReviewV2/` 下 UI 专属小组件 | Mark 表单、Review 看板及临时适配边界；命名在实施时按职责确定 |
| `Assets/_Project/Prefabs/UI/PREF_UI_ReviewV2Integration.prefab` | 增量增加页面与控件，保留原对象身份和接线 |
| `Assets/_Project/Editor/ReviewV2Authoring.cs` | 扩展可重复执行的原位作者入口，避免重复生成控件/丢失引用 |
| `Assets/_Project/Tests/PlayMode/ReviewV2PanelPlayTests.cs` 及必要的 UI 测试 | 页面权限、空接入、草稿生命周期、真实输入与既有功能回归 |
| `Docs/M2/` 进度与接入文档、T1 接口手册 | 记录真实完成范围、如何操作、T3 交付后如何接入 |

优先通过现有 Prefab 传播改动；仅当实际需要新增场景引用时增量修改唯一 V2 主场景。保持 A/B 方案资源、旧 Cooking、Samples、Packages、公共 ReviewContracts、输入绑定及默认 Build Settings 的范围不变。若接入必须改变公共契约或扩展正式 T3 业务，先说明具体差异再审批。提交/推送不在本计划内。

## 6. 本阶段验收清单（P1–P5）

| 编号 | 验收要求 | 验证方式 |
|---|---|---|
| U01 | Setup/Onboarding/Experience/Review/终态页面与 Host 一致；暂停作为覆盖状态 | 定向 PlayMode 测试；禁用后重启用读取最新快照 |
| U02 | T3 引用全部留空也能正常 Play；Mark 可打开、取消，提交禁用；不显示已保存 | 空接入运行检查、控制台错误检查 |
| U03 | Review 显示未接入，而非误显示零条真实反馈；零 Goal/Marker 可继续和结束 | 页面状态与 Core 命令测试 |
| U04 | 非空草稿关闭有丢弃确认；取消不产生 Marker；Visit 变化使旧草稿失效并明确提示 | 草稿取消、关闭、A/B 切换和重置的定向测试 |
| U05 | Busy/暂停/终态禁止新增 Mark；确认窗口互斥；切换失败不更新为目标方案 | 状态权限、失败/重试、同步与延迟回执测试 |
| U06 | 原有 A→B→A、重置、暂停、校准、Review 往返和结束行为保持 | 运行与改动相关的现有定向回归；不以旧批次结果代替本轮验证 |
| U07 | 新按钮实际可悬停、按下并释放；备注框可输入；菜单快捷键正常 | 复用已解决的输入路径，实际键鼠/模拟手柄操作并记录结果；仅调用 onClick.Invoke 不足以证明此项 |
| U08 | 文字清晰，列表/详情/长备注可阅读，按钮无重叠和视野外裁切 | 至少核对常用 16:9、较窄 Game 视图及俯视打开菜单；列表采用滚动容器，检查正常和长文本截图 |
| U09 | 关闭/禁用 UI 后解除自身订阅、输入监听和临时渲染处理 | 生命周期测试及重复开关检查，保留既有 FXAA 恢复行为 |

本轮不重做用户已解决的点击问题。U07 若发现新增控件的回归，修复新增 UI 接线；不直接升级为全局输入改造。测试用替身只能验证状态和失败边界，不放进生产场景或冒充 T3 保存成功。

## 7. 收到 T3 后的接入清单（P6）

先取得并核对：交付分支/目录、组件和 Prefab 路径/GUID、Host 引用、标记创建/编辑/取消和列表/详情入口、保存状态与事件、Schema/坐标/时间/上下文窗口规则，以及真实保存失败/读回说明。

随后执行：

1. 将组件放在会话场景；在 Host Awake 前接好真实 `ISessionSavePort`，按实际接口接 UI 适配处。
2. 按真实能力启用位置捕捉、提交和列表，不默认交付包含所有预想能力；缺项明确显示并记录。
3. 创建 Mark 时使用捕捉当时的 Session/Scheme/Visit/Model；未知锚点不猜坐标，历史位置不使用新方案同名对象重解释。
4. UI 根据真实标记仓储与逐条保存状态显示结果，不能用 Host.SaveStatus 代替每个 Mark 的保存状态。
5. 实跑“在 A 中标记 → 立即保存并读回 → Review 查看 → 继续体验 → 切 B → 查看 A 历史 → 在 B 标记 → 结束保存并读回”。补测空记录、保存失败重试、Reset 后历史保留和未完成目标时结束。

只有该批次真实执行通过后，才报告 T3 联调闭环。Windows Player 和 Quest Link 分别记录验证范围。

## 8. 风险和退出条件

- **T3 接口与预留不一致：** 预留只在占位 UI 内部，收到交付后做适配；不先要求 T3 实现一套未经确认的接口。
- **草稿串方案：** 草稿带打开时的会话上下文；提交前复核，外部切换/Reset 后失效。T3 缺失期间始终标为未提交，不落盘。
- **扩成正式 UI 产品：** 首版只满足页面可操作、信息清晰和接线需要；不做视觉精修、语音输入、虚拟键盘、历史空间重投影或正式新手教程。
- **把未接入误当成功：** 缺失组件显式不可用，保存状态来自真实结果；不会通过空回调完成存储验收。

P1–P5 的完成条件是：以上 U01–U09 有相应证据，负责人可在没有 T3 的情况下操作完整占位页面流程，且明确知道哪些能力仍未接入。本计划已获批准并执行，以下为验收对应关系；P6不据此提前验收。

| 验收项 | 本轮证据 |
|---|---|
| U01 / U02 / U03 | `ButtonsDriveRealPhasesPauseConfirmationAndUnsavedFinish`、`UnconnectedFeedbackPagesSupportDraftDiscardAndZeroMarkerFinish`；实际输入记录与Mark/Review截图 |
| U04 | `DraftIsInvalidatedByPauseResetAndSchemeChangesAndDoesNotSurviveDisable`、草稿保留/丢弃测试及实际输入操作 |
| U05 | Pending保存夹具中Mark不可开；Setup/Review/终态开Mark被拒绝；方案失败/回执/重试测试 |
| U06 | 本轮最终四组24/24通过，包含既有Integration/Scheme/InitialScheme；未将此前批次合并计数 |
| U07 | `Logs/ReviewV2/PlaceholderUI/actual-input.jsonl`：T事件经模拟器驱动UI，原生备注输入；F8事件弹出丢弃确认并可返回菜单 |
| U08 | `FeedbackPagesAndShortcutsFitNormalAndNarrowViews`、长文本滚动/筛选文字溢出断言；16:9与4:3实际渲染截图 |
| U09 | 禁用/重新启用、草稿清理、FXAA恢复的定向测试；全部UI关闭或控制器禁用后恢复FXAA，快捷栏仍显示时保持文字可读 |

参考：[T1 接口与接入手册](ReviewV2_T1_Interface_Reference.md)、[开发工作流](ReviewV2_Development_Workflow.md)、[M2 进度](M2/T1_Development_Progress.md)、[M2 接入清单](M2/Integration_Manifest.md)。
