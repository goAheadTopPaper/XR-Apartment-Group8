# T1 → T2 / T3 接口清单与产品逻辑接入手册

2026-11-17 占位UI完整流程增量：已增加体验快捷入口、可试填的Mark表单和Review列表/详情外壳；T3引用可留空并明确显示未接入。试填不创建Marker，不代替T3仓储、上下文或存储。组件字段、操作方式及已验证范围见 [占位UI使用与T3接线](ReviewV2_Placeholder_UI_Usage.md)。公共ReviewContracts签名不变；正式T3接口以实际交付后适配为准。

2026-11-17 临时入口增量：现有 `ReviewIntegrationPanel` 已在Experience提供A/B切换及独立确认/取消，复用本手册的ChangeScheme契约和真实端口；Busy锁定、匹配回执、同步完成及失败重试已独立验证。此入口供Play联调，正式T2菜单和T3持久化仍待交付；替换时关闭旧面板对象并停用其组件，避免保留输入和按钮回调入口。当前未接存储时仍明确显示数据未保存。

更新：2026-11-17（Asia/Shanghai）。核对基线：`feature/zh`，`ed7d549`；本手册描述当前源码，尚不代表 T2/T3 联合冻结或端到端验收。此次仅补充文档，不修改接口、场景或业务实现。

## 1. 适用范围与阅读路径

唯一产品场景：`Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`。Unity 2022.3.62f3，StandaloneWindows64，正式设备路径为 Quest Link。

| 使用者 | 先看 | 需要交付 |
|---|---|---|
| T2：正式 UI、引导、菜单、确认 | 第 2–5 节、第 9 节 T2 流程 | 独立 UI Prefab、组件、Host 引用及输入接管说明 |
| T3：Marker、上下文、存储、历史 | 第 2、4、6–9 节 | Marker/Review Prefab、保存端口、存储 Schema 与真实读回证据 |
| T1 / T4：接线和验证 | 第 3、8、10–11 节 | 唯一主场景接线、联合验证记录 |

当前真实资源：

| SchemeId | ModelVersion | 完整世界 Prefab |
|---|---|---|
| `SCHEME_A` | `integrated-kitchen-m2-v5` | `Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab` |
| `SCHEME_B` | `external-fridge-m2-v8` | `Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_B.prefab` |

默认从 A 开始；可在 Awake 前配置仅 B 起始。`COOKING_BASELINE` 和 `MOCK_A/B` 用于历史回归或替身，不能当作当前生产方案。

依据与优先级：可编译签名查 [ReviewContracts.cs](../Assets/_Project/Scripts/Data/ReviewV2/ReviewContracts.cs)，实际行为查 [SessionController.cs](../Assets/_Project/Scripts/Simulation/ReviewV2/SessionController.cs) 和 [ReviewSessionHost.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/ReviewSessionHost.cs)。本手册汇总现状；冲突时核对源码并同步文档，不据此自行改公共接口。场景所有权查 [Scene Ownership](ReviewV2_Scene_Ownership.md)，接入状态查 [M2 Integration Manifest](M2/Integration_Manifest.md)。旧 [M0 集成说明](ReviewV2_M0_Integration.md) 的 Baseline/NotSupported 等历史段落不覆盖当前 M2。

## 2. 接入架构与责任

产品流程为 `Setup → Onboarding → Experience ↔ Review → Finished`；`SafeExit` 到 `Exited`。暂停是叠加状态，不是新增 Phase。零 Goal、零 Marker 都允许 Review 和结束。

唯一 `ReviewSessionHost` 持有 Core；T2/T3 绑定该 Host，读取快照、订阅事件、发送命令。不要另建生产 SessionController，也不要直接写 Goal 或调用世界加载来推进产品流程。

| 能力 | 入口 / Owner | 消费方式 |
|---|---|---|
| 当前会话、目标、可用性 | `ISessionReadModel` / T1 | T2/T3 读 `Snapshot`，订阅 `Changed` |
| 产品流程操作 | `IReviewSessionCommands` / T1 | T2 经 `Execute` 发命令；T3 可请求 Review，入口与 T2 协调 |
| 异步操作结果 | `ReviewSessionHost.OperationCompleted` / T1 | T2 显示成功、失败与重试；T3 关联保存请求 |
| 实际交互事实 | `IInteractionEventSource` / T1 | T3 订阅 `Observed`，构建上下文 |
| 当前空间身份 | `ReviewSessionHost.Resolve` / T1 | T3 获取当前 Transform，立即复制位置数据 |
| Marker 创建、编辑、回顾、即时保存 | T3 正式组件 | 目前没有 T1 的 CreateMarker/UpdateMarker/GetMarkers 接口 |
| 会话最终保存、切换前保存 | `ISessionSavePort` / T3 实现 | T1 Core 调用 T3 端口，T3 完成真实写盘和读回 |
| 正式标记入口 / 反馈面板显示 | T2 ↔ T3 | 两方需确认 UI 调用约定，不能假定 T1 已提供 |

命名空间：契约为 `XRApartment.Data.ReviewV2`；Host 为 `XRApartment.Runtime.ReviewV2`；纯逻辑/Mock 为 `XRApartment.Simulation.ReviewV2`。独立 asmdef 按实际使用引用 `XRApartment.Data`、`XRApartment.Runtime`；使用 Mock 再引用 `XRApartment.Simulation`。现有 Runtime 程序集已引用前两层。

## 3. 初始化与生命周期

1. T1 在会话场景接线：唯一 Host、Rig、UI 根、方案端口和可选保存端口。T2 UI、T3 Marker/存储组件留在会话场景，不能挂在切换时会销毁的方案世界中。
2. `ReviewSchemePort.Awake`（执行顺序 -200）实例化初始方案到独立运行时 Scene，绑定 station、recipe、roomReset、groceries、identities，并把初始 Scheme/Model 写给 Host。
3. `ReviewSessionHost.Awake`（-100）创建 Core，读取并固定保存端口引用。`sessionSavePort` 必须提前指向实现 `ISessionSavePort` 的 MonoBehaviour；运行后只改 Inspector 字段不会重注入 Core。
4. 消费者在 Host 初始化后订阅 `Changed`、`Observed`、`OperationCompleted`，并主动读取一次 `Snapshot` 渲染首屏。订阅本身不会重放当前快照或过去事件。
5. 禁用/销毁时成对退订。重新启用时重新读取快照，避免漏掉禁用期间的状态变化。

不要在比 Host 更早的 Awake/OnEnable 读取 `Snapshot`。空间解析也应在初始世界绑定后进行。运行中切换世界会使旧 Transform 失效，历史记录只保留复制的数据。

当前主场景 `sessionSavePort` 为空，尚未接正式存储。场景序列化的 Host `modelVersion` 仍可见旧 v2，但 -200 的方案端口会以真实 Prefab 版本覆盖它；业务始终读运行时 `host.Snapshot.ModelVersion`，不要抄 Inspector 旧值。

## 4. 对外函数、属性与事件清单

以下签名中的 DTO 均见第 6 节；`Action<string, bool, string>` 的参数依次为原请求 ID、是否成功、结果说明。

### 4.1 T2/T3 直接消费

| 所属 | 精确签名 | 输入 | 返回 / 输出 | 约束与用途 |
|---|---|---|---|---|
| `ISessionReadModel` | `SessionSnapshot Snapshot { get; }` | 无 | 当前不可变快照 | 每次读取创建快照；不向其回写状态 |
| `ISessionReadModel` | `event Action<SessionSnapshot> Changed` | 订阅回调 | 最新快照 | 状态/Goal/Busy 等变更触发；无初始重放 |
| `IReviewSessionCommands` | `CommandResult Execute(CommandType command, string targetSchemeId = null)` | 命令；切换时为目标方案 ID | `CommandResult` | 接受不代表异步成功；详见第 5 节 |
| `IInteractionEventSource` | `event Action<InteractionEvent> Observed` | 订阅回调 | 一条不可变事实 | 没有历史查询或自动事件缓存 |
| `ReviewSessionHost` | `event Action<OperationResult> OperationCompleted` | 订阅回调 | 请求 ID、命令、结果、说明 | 此事件未包含在上述三个接口中，正式消费者须保留 Host 引用 |
| `ReviewSessionHost` | `Transform Resolve(string stableId)` | 当前方案登记的稳定 ID | 当前 Transform；未知/缺失返回 `null` | 仅解析当前世界，不跨方案/Visit 查历史；不返回 DTO 或位置快照 |
| `ReviewSessionHost` | `bool CanObserve { get; }` | 无 | 当前是否允许发布事实 | Experience/Review、未暂停、未忙碌且世界未重置；不是 Marker 创建权限的完整规则 |

`Resolve` 的调用前提是 Host 已正确绑定 identities；调用者应传入有效登记 ID并处理空返回，不假定未配置 Host 也能安全工作。

### 4.2 T3 实现、由 Core 调用的端口

| 所属 | 精确签名 | 输入 | 输出 / 回调 | 责任 |
|---|---|---|---|---|
| `ISessionSavePort` | `void Save(string requestId, SessionSnapshot snapshot, Action<string, bool, string> completed)` | Core 原请求 ID；请求时快照；完成委托 | 无直接返回；`completed(requestId, verified, message)` | T3 实际写盘并读回核对后才能回 true；失败 false + 可显示原因 |

`Save` 不接收 Marker 列表、路径、JSON 或取消令牌，也不返回文件路径。T3 必须用自身持有的 Marker/事件仓储完成保存；不要认为传入 SessionSnapshot 已包含全部反馈。端口会在 **Finish** 和 **ChangeScheme 保存前置** 两种场景被调用，当前签名不含操作类型，不能把每次 Save 都解释为结束会话。

传入快照是保存请求进行中的上下文：Finish 时通常为 Phase=Review、IsBusy=true、SaveStatus=Pending；切换前为旧方案的 Experience/旧 Visit。它不是最终 Finished/新方案快照。T3 的磁盘记录应区分快照原值与自身持久化验证结果，不能仅凭输入状态判定保存失败或会话已结束。

### 4.3 T1 基础设施端口（供理解与替身接入）

| 所属 | 精确签名 | 输入 → 输出 | 行为 |
|---|---|---|---|
| `ISchemePort` | `bool Supports(string schemeId)` | 方案 ID → bool | 当前生产实现要求唯一、已配置的布局 |
| `ISchemePort` | `void Change(string requestId, string schemeId, Action<string, bool, string> completed)` | 原请求 ID、目标方案、回调 → 无直接返回 | 完整世界就绪才成功；失败恢复旧世界 |
| `ISchemeModelCatalog` | `string ModelVersionFor(string schemeId)` | 方案 ID → 版本或 null | 可选兼容扩展；当前生产端口已实现；Core 在请求前固定目标版本 |
| `IWorldPort` | `void Reset(string requestId, Action<string, bool, string> completed)` | 请求 ID、回调 → 无直接返回 | 当前 Host 等待 station/roomReset 恢复完成；超时会失败 |
| `IWorldPort` | `CommandResult Recenter()` | 无 → 命令结果 | 调用运行中的 XR 子系统；不支持时失败 |
| `ICalibrationPort` | `CommandResult Calibrate(bool seated)` | true 坐姿 / false 站姿 → 命令结果 | 复用 locomotion 的眼高校准；原型值 1.2m / 1.65m |

T2 发产品命令时使用 `Execute`；直接调用 Change/Reset 会绕过 Phase、Busy、保存前置和 Visit 更新。`ReviewSchemeWorld.Bind`、`ReviewSessionHost.WorldRebound`、`ReviewSceneIdentity.Configure` 是 T1 制作/绑定入口，T2/T3 不应用它们改运行时身份。Host 的 `Observe` / `ObserveGroceryStored` 为 internal，事实由真实桥接发布。

## 5. 命令、状态转换与失败处理

所有命令先检查终态：Finished/Exited 拒绝并返回 `SessionEnded`。非终态中 SafeExit 优先；其余命令在 Busy 时返回 `OperationPending`。下表省略了这些公共前置规则。

| CommandType | 允许条件 | targetSchemeId | 成功结果 | 完成方式 |
|---|---|---|---|---|
| `CompleteSetup` | Setup，未暂停 | 不使用 | → Onboarding | 同步结果 + Changed |
| `CompleteOnboarding` | Onboarding，未暂停 | 不使用 | → Experience；跳过教学也通过此命令 | 同步 |
| `Review` | Experience，未暂停 | 不使用 | → Review；世界/Visit/Goal 保留 | 同步 |
| `Continue` | Review，未暂停 | 不使用 | → Experience；世界保留 | 同步 |
| `Finish` | Review，未暂停 | 不使用 | 有端口：验证保存后 Finished；无端口：直接 Finished、仍 NotConfigured | 有端口产生 OperationCompleted；无端口同步且无该完成事件 |
| `Pause` | 非终态、非 Busy、当前未暂停 | 不使用 | IsPaused=true；Phase 不变 | 同步 |
| `Resume` | 非终态、非 Busy、当前已暂停 | 不使用 | IsPaused=false；Phase 不变 | 同步 |
| `Recenter` | 非终态、非 Busy；暂停中也可调用 | 不使用 | 取决于当前 XR runtime | 同步；不改变 Visit |
| `CalibrateStanding` / `CalibrateSeated` | 非终态、非 Busy；暂停中也可调用 | 不使用 | 视高校准；不改变 Visit | 同步 |
| `ChangeScheme` | Experience、未暂停；目标有效且受支持、版本非空 | 必填 `SCHEME_A` 或 `SCHEME_B`，且不同于当前 | 成功后新 Scheme/Visit/Model、Goal 清零、历史追加；目标世界从初始状态加载 | 请求结果 + OperationCompleted |
| `ResetWorld` | Experience、未暂停；世界端口存在 | 不使用 | 同 Scheme/Model，新 Visit，Goal 清零；反馈历史由 T3 保留 | 请求结果 + OperationCompleted |
| `SafeExit` | 任意非终态，即使 Busy/暂停 | 不使用 | 立即 Exited，清 pending；Pending 保存状态改 Failed | 同步；不是可靠保存承诺，也不退出应用进程 |

Pause/Resume 重复调用没有独立幂等成功语义，可能返回 Paused/InvalidPhase；UI 按当前 IsPaused 提供正确按钮。Review 不冻结模拟，烹饪加热仍可继续；Pause 才冻结 `Time.timeScale`，并取消已有抓取。Busy 单独门控时阻止新抓取，保留既有抓取直到成功切换或重置处理。

### 5.1 CommandResult 与 OperationCompleted 的使用

1. **调用前先订阅** OperationCompleted 和 Changed。端口可以同步调用完成委托，因此 OperationCompleted 可能在 Execute 返回前触发；不要假定下一帧才返回结果。
2. `Accepted=false`：读取 Reason，保持当前产品流程，显示可理解的拒绝原因。不等待完成事件。
3. `Accepted=true` 且 `RequestId=null`：同步操作，用当前快照确认最终状态；Recenter/Calibrate 可能没有 Changed。
4. `Accepted=true` 且 RequestId 非空：已发起异步协议操作，以同 ID 的 OperationResult 判断最终结果。UI 可缓存提前到达的回执，再与返回 ID 匹配；不要在返回后才注册一次性监听。重复点击依据 IsBusy 禁用，并仍接受 Core 校验。
5. 完成时 Core **先 Changed，再 OperationCompleted**。只看到 Busy=false 不足以显示“切换成功”。以匹配回执的 Succeeded 和最新快照共同更新 UI。

同步端口甚至可在 Execute 返回前失败，所以 Accepted 仍可能为 true。OperationCompleted 仅用于 Begin 路径（带端口的 Finish、ResetWorld、ChangeScheme），不是每条命令都有。

### 5.2 拒绝与操作失败

| Reason / 情况 | 意义 | 产品处理 |
|---|---|---|
| `SessionEnded` | 已结束 | 展示终态，不继续发操作 |
| `OperationPending` | 另一请求未完成 | 显示处理中，阻止重复操作 |
| `Paused` | 暂停阻止当前命令 | 先恢复，再操作 |
| `InvalidPhase` | 当前阶段不允许；也包括某些重复 Pause/Resume | 重新按快照渲染 |
| `InvalidScheme` | 空白目标或等于当前方案 | 修正目标，不重置世界 |
| `NotSupported` | 无方案端口或目标不受支持 | 禁用对应入口并说明 |
| `ModelVersionUnavailable` | 目标模型版本为空 | 报接线问题，不伪造版本 |
| `WorldUnavailable` / `XRUnavailable` / `CalibrationUnavailable` | 世界或设备适配不可用 | 明确说明能力不可用 |
| OperationResult.Succeeded=false | 加载、保存、重置等执行失败 | 展示 Message 并允许合适的重试；消息可能为动态文本/异常信息，不按固定错误码穷举 |

方案失败保持原 Scheme/Visit/Goal 和世界；保存失败不会开始加载目标。Reset 失败不创建新 Visit，但可能已部分修改物理世界，当前超时消息要求检查场景，不能宣称通用原子回滚。XR Recenter 失败也可能返回可读文本，而非上表代码。

## 6. 数据结构与字段字典

以下公开属性均只读。快照复制 Goals 和 SchemeHistory 集合，旧快照不会被后续状态更新。C# 源码未启用 nullable 注解；下文“可空”说明实际语义，不表示签名带 `?`（`long?` 除外）。

### 6.1 SessionSnapshot

| 字段 | 类型 | 含义 / 使用约定 |
|---|---|---|
| SessionId | string | Host 创建会话时生成的 GUID N 格式；同次 Reset/切换不变 |
| Phase | SessionPhase | Setup / Onboarding / Experience / Review / Finished / Exited |
| SchemeId | string | 当前真实方案 ID |
| SchemeVisitId | string | 当前访问 GUID；成功 Reset/切换更新，Review/暂停/校准不更新 |
| ModelVersion | string | 当前方案模型版本；从运行时读取 |
| Goals | IReadOnlyList<GoalSnapshot> | G01–G05；按 Id 查找，不依赖集合索引顺序 |
| SchemeHistory | IReadOnlyList<string> | 初始方案和成功切换的方案序列；可为 A→B→A，有重复；不含 Visit、时间、Marker 或历史版本详情 |
| IsPaused | bool | 暂停叠加状态 |
| IsBusy | bool | 是否有 pending 请求；不是世界加载进度百分比 |
| IsMock | bool | 是否来自替身；需与真实用户数据区分 |
| SaveStatus | SaveStatus | NotConfigured / Pending / Failed / Verified；不是逐条 Marker 状态，也不表示所有未来修改都已保存 |
| CanReview | bool | `Phase == Experience && !IsPaused && !IsBusy`；与 Goal 数量无关 |

当前真实 A/B 中 G01/G02 可用；G03–G05 未接入。Available 独立于 GoalState：显示“未接入”使用 Available=false，不新增第四个枚举值。

### 6.2 GoalSnapshot

| 字段 | 类型 | 含义 / 可空 |
|---|---|---|
| Id | string | 稳定目标 ID：G01–G05 |
| State | GoalState | NotStarted / InProgress / Completed；同 Visit 内只向前推进 |
| Available | bool | 当前功能是否接入；false 不表示用户失败 |
| EvidenceId | string | 可空；导致当前状态的事实 EventId |
| UpdatedAtMilliseconds | long? | 可空；会话启动后的单调毫秒，不是 UTC 时间或 Unix 时间戳 |

G01 是当前 Visit 内不同食材的有效存放进度（默认 2 件达到完成）；取出料理不回退。G02 由真实加工开始到真实成菜完成推进。UI 不从开冰箱、抓工具、开火或用户点选推断 Goal 进度。

### 6.3 CommandResult / OperationResult

| 类型 | 字段 | 类型 | 含义 |
|---|---|---|---|
| CommandResult | Accepted | bool | 是否受理本次命令 |
| CommandResult | Reason | string | 默认空字符串；拒绝原因或受理说明 |
| CommandResult | RequestId | string | 默认 null；异步请求 GUID，用于匹配完成事件 |
| OperationResult | RequestId | string | 对应原请求 ID |
| OperationResult | Command | CommandType | 原命令 |
| OperationResult | Succeeded | bool | 执行是否完成成功；保存端口的 true 必须代表真实读回验证 |
| OperationResult | Message | string | 结果说明；不保证机器可解析的固定格式 |

### 6.4 InteractionEvent

| 字段 | 类型 | 含义 / 可空 |
|---|---|---|
| SchemaVersion | const string | `review-v2-m0-1`；静态常量，序列化时需显式映射；当前追加事件仍使用此值 |
| EventId | string | 每个事实新建 GUID N，可用于去重和 Goal 证据关联 |
| SessionId / SchemeId / SchemeVisitId / ModelVersion | string | **事实创建时**复制的完整上下文；不能用后来的当前快照覆盖 |
| ElapsedMilliseconds | long | Host Stopwatch 的会话单调毫秒；跨 Visit 不归零，不受 Time.timeScale 控制 |
| Kind | InteractionKind | 第 7 节列出的六种事实 |
| ObjectId | string | 事实关联对象的稳定 ID；加工事实目前为工作台 ID |
| ZoneId | string | 可空；当前 IngredientStored 为储物区 ID |
| AnchorId | string | 可空；当前 IngredientStored 与 ZoneId 相同 |
| IsMock | bool | 明确区分替身事实 |

事实不含 Transform、位置、旋转、手柄侧、用户评价、MarkerId 或墙钟时间；需要这些数据时由 T3 在捕捉时补充，并区分实测与推断。

DTO 为运行时契约，尚无统一磁盘 JSON/CSV 映射。它们含 get-only 属性和只读集合，不能假定直接交给任意 Unity 序列化方式就能可靠写盘/读回；T3 需交付明确 DTO 映射、字段名、枚举表达和 schemaVersion，并验证往返结果。

### 6.5 DTO 构造签名（映射/测试用）

以下是现有构造签名，手工创建对象不会改变 Host 状态，也不会发布事实：

```csharp
CommandResult(bool accepted, string reason = "", string requestId = null)
GoalSnapshot(string id, GoalState state, bool available,
    string evidenceId = null, long? updatedAtMilliseconds = null)
SessionSnapshot(string sessionId, SessionPhase phase, string schemeId, string visitId,
    string modelVersion, IEnumerable<GoalSnapshot> goals, bool paused, bool busy,
    bool mock, SaveStatus saveStatus, IEnumerable<string> schemeHistory = null)
InteractionEvent(SessionSnapshot context, InteractionKind kind, string objectId,
    long elapsed, string zoneId = null, string anchorId = null)
OperationResult(string id, CommandType command, bool success, string message)
```

构造快照时 goals 必须非 null；schemeHistory 为 null 时默认仅当前 schemeId。InteractionEvent 构造器总会新建 EventId，因此历史读回如需保留原 ID，应使用 T3 存储 DTO，不用该构造器伪造原事件。

## 7. 事实事件与空间身份

| InteractionKind | 当前来源 / ObjectId | ZoneId / AnchorId | 产品意义与限制 |
|---|---|---|---|
| ObjectGrabbed | CookingGrabBridge，具体登记对象 | null / null | 第一只手选中；双手不重复报告；不推断使用意图 |
| ObjectReleased | CookingGrabBridge，具体登记对象 | null / null | 最后一只手正常释放；程序取消不报用户释放；不等于物体已稳定 |
| TomatoCut | Host，`COOKING_STATION` | null / null | 实际 CutCount 增加（整颗切分标志为后备）触发首次加工开始；不是每一刀的完整流水 |
| EggReceivedInBowl | Host，`COOKING_STATION` | null / null | 达到当前配方要求的蛋液入碗，作为首次加工开始；不识别具体鸡蛋 |
| DishCooked | Host，`COOKING_STATION` | null / null | 当前西红柿炒蛋配方 Ready 边沿；不表示满意、舒适或合规 |
| IngredientStored | ReviewGroceryActivity，具体食材 ObjectId | `ZONE_FRIDGE_STORAGE` / 同左 | 用户释放后通过包含/稳定条件；每对象每 Visit 去重 |

G02 开始事实被接受时，Host 先更新 Goal 并触发 Changed，再发布同 EventId 的 Observed。不要认为每次 Changed 都对应 Observed，或每次 Observed 都改变 Goal。Review 期间仍可能有事实；暂停、Busy、重置及终态隔离事实。没有事实事件合法，T3 仍应允许即时保存用户反馈。

完整身份目录（无表头 TSV，每行依次为 SchemeId、ModelVersion、StableId、作者定位名）：

- [SCHEME_A_Identities.tsv](M2/SCHEME_A_Identities.tsv)
- [SCHEME_B_Identities.tsv](M2/SCHEME_B_Identities.tsv)

关键语义：

| StableId | 当前用途 / 注意事项 |
|---|---|
| COOKING_STATION / FRIDGE / GROCERY_BAG / ZONE_FRIDGE_STORAGE | 工作台 / 冰箱 / 购物袋 / G01 储物区 |
| WORKTOP | 两案均存在，跟随砧板侧锚点；不等于整块台面的几何中心 |
| DINING_TABLE / SINK / SCENE_ANCHOR | 餐桌 / 水槽 / 场景位置后备锚点 |
| COUNTER_WORKTOP | A 专属实际台面身份，B 不保证存在 |
| MICROWAVE_VISUAL | 固定微波炉外观；不是加热设备，不开放 G04 |
| CABINET_LAYOUT / WINDOW_STORAGE / FLOATING_SHELF | 柜体及 B 专属收纳/层板；开放柜/层板不自动获得 G01 完成语义 |
| OBJECT_020/021/022 / OBJECT_030 | 当前鸡蛋 / 番茄实例；完整其他对象查 TSV，不用名称猜 ID |

同一 StableId 可能出现在 A 和 B，必须与 Session/Scheme/Visit/Model 一起使用。TSV 最后一列仅用于作者定位，不能拿物体名或 GetInstanceID 当稳定 ID。

Marker 位置建议沿用既有交接约定：解析锚点后保存锚点 ID、锚点局部位置和捕捉时世界位置快照；长度按项目米制模型，局部位置必须注明相对于哪个 Transform。旋转若需要，由 T3 schema 明确世界/局部及 quaternion 等表达。无对象命中时使用明确场景位置后备；Resolve=null 不得编造坐标。历史回顾不能只持有旧 Transform，也不能用 B 中同名锚点重解释 A 的旧 Marker。Marker DTO 与历史重投影策略仍待 T3 交付确认。

## 8. 保存协议与已知边界

**Finish：** Review 中发命令 → Busy=true、SaveStatus=Pending → `Save(id, snapshot, callback)` → 真实验证成功后 Finished/Verified；失败仍为 Review/Failed，可再次 Finish。每次重试会生成新请求 ID。

**ChangeScheme：** Experience 中发命令 → 固定目标版本 → Busy=true → 如有存储，先保存**旧 Scheme/Visit**快照 → 验证成功才调用方案端口 → 新世界就绪才更新 Scheme/Visit/Model、清 Goal、追加历史 → 发布完成结果。保存期间已有抓取保持，失败不加载新世界。若保存成功但加载失败，SaveStatus 可为 Verified 而 OperationResult 为失败；不能把保存状态当成切换结果。

**ResetWorld：** 当前 Core 不自动调用 Save。T3 应即时保存 Marker，并在 Busy/Visit 变化时关闭旧上下文窗口；Reset 成功不等于历史已落盘。

端口实现规则：

1. 回调使用原 requestId；bool 仅在真实写盘并读回核对成功时为 true；Mock/仅缓存不得回“验证成功”。说明中可给出可显示的失败原因。
2. Core 忽略错 ID、旧请求、重复和终态后的回执。**忽略错 ID 不会自动失败或解除 Busy**，端口仍须对当前请求返回正确结果。
3. 端口应确保每次请求最终有一次有效完成，不能无限等待。Core 没有 Save/Change 的通用超时或取消令牌；Reset 的约 10 秒等待是其自身逻辑。
4. 当前调用链不负责线程切换，也非线程安全模型。T3 若后台做 IO，接回 Unity 主线程后再执行 completed；这是接入要求，当前 Core 不强制检查。
5. SafeExit 只终止 Core pending 状态，不取消已经启动的磁盘 IO，也不等待保存验证。T2 不能显示“退出即保存成功”；T3 的草稿恢复、迟到写入处理须联合验证。
6. Host 的 SaveStatus 不是逐 Marker 的 dirty 状态。T3 需自行管理创建、编辑、after-context 补记、重试和读回状态，并向正式 UI 提供真实信息。

当前主场景无保存端口：Finish 仍可 Finished/NotConfigured；必须明确提示“存储未接入，数据未保存”。这只是现有兼容行为，不满足正式持久化验收。

## 9. 通过接口实现产品逻辑

本节给出产品编排规则。T1 已实现的调用可直接消费；T2/T3 的正式组件与彼此调用仍按各自交付落实，不把下述流程说明当作组件已存在。

### 9.1 T2：界面随会话状态更新

单一渲染入口读取 SessionSnapshot：Phase 决定页面，IsPaused 决定暂停覆盖层，IsBusy 决定操作锁定，Goals 决定任务建议状态，SaveStatus 配合完成结果决定保存提示。页面切换通过命令和快照驱动，避免 UI 自己维护另一套会话状态机。

| 用户行为 / 页面 | 接口调用或订阅 | 判断与界面反馈 |
|---|---|---|
| 进入产品 | 订阅 Changed，读取 Snapshot | 按实际 Phase 展示；Setup 阶段不开放 Cooking |
| 确认设置 | Execute(CompleteSetup) | 成功后 Onboarding；拒绝时显示 Reason |
| 完成或跳过教学 | Execute(CompleteOnboarding) | 进入 Experience；教学练习不写正式 Goal 证据 |
| 看活动建议 | 读取 Goals 的 Id/State/Available | G01/G02 独立呈现，允许并行；未接入标注；不要求完成后才能 Review |
| 料理半途回顾 | CanReview 为 true 时 Execute(Review) | 显示 T3 的回顾内容；不调用 Reset，不清 Goal；提醒必要的持续加热状态 |
| 返回体验 | Execute(Continue) | 世界/Visit 保持；不重载方案 |
| 暂停 / 恢复 | Execute(Pause/Resume) | 按 IsPaused 渲染；暂停会释放抓取，不承诺恢复原手中选择 |
| 校准 | Execute(Recenter/CalibrateStanding/CalibrateSeated) | 同步检查 Accepted/Reason；失败不显示校准成功 |
| 结束 | Review 中 Execute(Finish) | 按第 8 节等待真实保存；零目标/零标记不得拦截 |

UI 可依据快照提前禁用无效按钮，Core 仍是最终校验者。T2 替换临时 ReviewIntegrationPanel 时须接管统一菜单输入、Reset 确认入口，避免两个菜单同时响应；不能只隐藏画面却保留旧输入监听。

### 9.2 T2：切换方案的完整产品路径

1. 在 Experience 提供另一个方案选项；Review 中先 Continue，暂停时先 Resume。当前只支持已配置 A/B，不通过按钮直接改 Host.initialSchemeId。
2. 显示确认说明：“目标方案将从初始状态开始，当前活动进度重置；既有反馈保留原方案归属。”用户取消则不发命令；Core 不替 UI 做确认。
3. 确认后调用 `Execute(CommandType.ChangeScheme, targetSchemeId)`。使用第 5.1 节的同步回调兼容方式处理返回。
4. Busy 时显示“正在保存/切换”等中性提示；当前只有 Busy/SaveStatus，没有精确加载百分比。失败时展示原因并维持旧方案标签。
5. 匹配的 OperationCompleted.Succeeded=true 后，从最新 Snapshot 更新方案名称与任务卡；T3 同时按新 Visit 建立新上下文。
6. 返回曾访问方案也从初始世界开始。T3 回顾历史仍按捕捉时方案展示，不把旧 Marker 变成当前 Visit 的新 Marker。

### 9.3 T2：重置与安全退出

Reset 显示明确确认，确认后 Execute(ResetWorld)，完成成功再显示“世界已重置”；不清理 T3 历史。Reset 失败提示检查场景及重试条件，不虚报已恢复。

普通结束使用 Review→Finish；SafeExit 用于立即中止，调用后展示 Exited，并准确说明尚未验证的保存状态。当前 SafeExit 不关闭 Unity/Player；应用关闭入口如需实现由正式 UI 另行设计和验收。

### 9.4 T3：点击标记到即时保存

1. T2 的“标记”入口打开 T3 捕捉组件；两方约定具体方法/事件。建议在非终态、非 Busy、非暂停时开放编辑入口，实际允许的页面由两方确认；这不是现有 T1 命令。
2. 捕捉时一次性取 Snapshot，复制 SessionId、SchemeId、SchemeVisitId、ModelVersion、IsMock；给 Marker 自身分配 ID（T3 schema），不要复用 EventId。
3. 从命中对象的登记身份或明确语义锚点调用 Resolve；立刻复制锚点局部位置与世界位置快照。编辑跨越 Visit 变化时，不得用新身份默默覆盖旧捕捉。
4. 记录用户输入的说明；Goal/事实只作上下文，不能自动生成“拥挤”“不方便”等主观评价。
5. 由 T3 的即时保存流程写盘并读回；不等待下一条 Observed，也不等 Finish 才第一次保存。失败时保留待重试内容、明确显示未保存，避免仅关闭面板造成成功错觉。
6. 异步收集同 Visit 的 after-context，之后增量保存；没有事件时保存空列表。Marker schema、读回字段与 UI 状态由 T3 提供。

### 9.5 T3：事实缓存与 Before / After 窗口

订阅 Observed 后，按完整身份缓存事实并以 EventId 去重。事件的 ElapsedMilliseconds 可用于同一会话内排序；不要把它与系统 UTC 时间直接相减。

Before/After 的窗口长度、截止条件和容量由 T3 明确交付，T1 当前未提供 GetBefore/GetAfter 查询。匹配至少检查 SessionId 和 SchemeVisitId，并核对 SchemeId/ModelVersion。Changed 进入 Busy、终态或成功更换 Visit 时，关闭旧采集窗口，防止 Reset/切换动作混入旧反馈。

Review 不更换 Visit，也不会自动阻止事实。Pause 会隔离事实但不创建新 Visit；暂停是否截断窗口属于 T3 规则。切换失败不创建新 Visit，关闭后的采集恢复策略需 T3 明确，不能等待一个永远不会发生的新 Visit。

### 9.6 T3：回顾、历史与保存结束

Review 页面读取 T3 仓储中的 Marker，按捕捉时方案/Visit/模型呈现，并允许空列表。当前 Snapshot.SchemeHistory 只提供方案序列，不能代替完整历史仓储。

同 Visit 内返回体验时沿用原 Marker；Reset/切换成功后旧反馈保持原身份。历史位置显示使用捕捉快照或经明确版本匹配的重投影，不能 Resolve 当前对象后直接替换旧位置。

T3 保存端口接到 Save 时，持久化输入快照及自身仓储的对应数据，读回核对身份、位置、备注与上下文后回原 ID,true。Finish 失败留在 Review，保留编辑和重试入口；成功后 T2 显示 Finished。切换前 Save 成功时仅完成保存职责，不提前关闭会话或清除历史。

### 9.7 一条跨模块验收示例

| 步骤 | T1 可观察结果 | T2/T3 应做什么 |
|---|---|---|
| Setup→跳过教学→A 体验 | Phase=Experience，Scheme=A，Visit=V1，Model=A-v5 | 正常显示 G01/G02；不要求先完成目标 |
| 实际切一刀番茄 | G02 InProgress，有 EvidenceId；Observed.TomatoCut | 更新任务卡；T3 缓存事实，不自动写主观评价 |
| 在工作台旁创建标记 | T1 提供 V1 快照与当前锚点 Transform | T3 保存位置/备注/上下文并实际读回 |
| Review→Continue | Scheme/Visit/世界保持 | 回顾原标记，返回继续料理 |
| 确认切换 B | 先 Save(A,V1)，成功加载后 Scheme=B、Visit=V2、Model=B-v8，Goal 清零 | T2 等匹配成功回执；T3 留存 A 标记，创建 B 的新上下文 |
| B 中零完成目标，Review→Finish | 先 Save(B,V2)，成功后 Finished/Verified | 读回 A/B 历史数据；零 Goal 不拦截结束 |

V1/V2、A-v5/B-v8 为阅读简写；真实值使用运行时 GUID 与完整 ModelVersion。上述路径必须以正式 T2/T3 组件实跑才能标记端到端通过。

## 10. 示例与独立开发支持

现有可编译消费者：[ReviewConsumerExample.cs](../Assets/_Project/Scripts/Runtime/ReviewV2/ReviewConsumerExample.cs)。展示真实 Host / Mock 选择、初始渲染及成对订阅；异步结果接入需另按第 5.1 节处理。

Mock：[MockReviewProvider.cs](../Assets/_Project/Scripts/Simulation/ReviewV2/MockReviewProvider.cs)。可 `Execute`、`SetGoal(string id, GoalState state)`（返回 bool）、`EmitGrab()`（void），以及设置 `FailNextOperation`（bool）。Mock 支持 MOCK_A/B，IsMock=true；不创建物理世界、不做真实保存、不实现校准端口，且当前未公开 OperationCompleted。需要正式回调/读回验证时使用真实 Host + 明确标注的测试端口或 T3 实现，不能声称 Mock 覆盖全部正式链路。

事实示例仅用于字段理解，**不是 T3 已冻结的落盘格式或真实用户记录**：

```json
{
  "schemaVersion": "review-v2-m0-1",
  "eventId": "example-event",
  "sessionId": "example-session",
  "schemeId": "SCHEME_B",
  "schemeVisitId": "example-visit",
  "modelVersion": "external-fridge-m2-v8",
  "elapsedMilliseconds": 18000,
  "kind": "IngredientStored",
  "objectId": "OBJECT_020",
  "zoneId": "ZONE_FRIDGE_STORAGE",
  "anchorId": "ZONE_FRIDGE_STORAGE",
  "isMock": false
}
```

对应输入输出例：Experience 中 Execute(ChangeScheme, SCHEME_B) 可能先返回 Accepted=true、RequestId=R；最终得到 OperationResult(RequestId=R, Command=ChangeScheme, Succeeded=true, Message=...)，此时 Snapshot 才是新 B Visit。同步端口下完成事件可能早于这个返回，仍按同一 R 关联。该例中的 R 不是固定 ID。

## 11. 联调交付与检查清单

### 11.1 T2/T3 回交内容

| Owner | 交付内容 |
|---|---|
| T2 | Prefab/组件路径与 GUID、挂载位置、Host 引用、菜单输入接管、Reset/切换确认、拒绝/失败/Busy UI、T3 面板入口约定 |
| T3 | Marker/Review/存储组件路径与 GUID、SavePort 类型、序列化引用、Marker/事件磁盘 Schema、坐标/时间约定、窗口规则、即时保存/读回/重试与草稿恢复证据 |
| T1 | 唯一主场景增量接线，核对初始 A/B 版本、当前世界身份和程序集依赖；不直接代写 T2/T3 正式业务 |
| T4/T5 | 联合叙事、Windows64、Quest Link 与 Pilot 的实际证据；注明方案/版本/设备及失败复测 |

### 11.2 联合验收（待正式组件到位后执行）

- [ ] 初始 Snapshot 与 Changed 驱动同一套 UI；重启用组件无重复订阅或漏首屏。
- [ ] G01/G02 可并行；G03–G05 显示未接入；零 Goal/零 Marker 可 Review、可结束。
- [ ] 正常抓放事件去重；程序取消抓取不被记录为用户释放；切一刀和足量蛋液事件含正确身份。
- [ ] 料理半途创建 Marker 后立即写盘并读回，无后续事件也成功；空上下文合法。
- [ ] Review→Continue 保留世界/Visit/Marker；Review 仍可能继续加热，不错误冻结产品逻辑。
- [ ] A→B→A 经过确认；每次成功新 Visit/初始世界，历史 Marker 不串方案、不被重置删除。
- [ ] 同步成功/同步失败及延迟回调都可正确显示；错/旧/重复回执不会误报成功。
- [ ] 保存失败留原世界/Review，原因可见且可重试；切换保存 Pending/Failed 保持已有抓取。
- [ ] 目标家具占用导致切换失败时保留旧世界与身份；失败后的上下文采集恢复规则明确。
- [ ] Reset 新 Visit、同 Model，历史仍读得出；不把 Reset 当成自动保存。
- [ ] Finish 有真实读回才显示可靠保存；无端口明确 NotConfigured；SafeExit 不冒充保存成功。
- [ ] Marker/会话真实文件读回字段一致；Mock 数据不混入正式用户数据。
- [ ] Unity 定向回归与 Windows64 / Quest Link 分别记录，不用 Editor 或启动冒烟代替完整设备通过。

已有测试入口（本次未重跑）：[ReviewV2SessionTests.cs](../Assets/_Project/Tests/EditMode/ReviewV2SessionTests.cs)、[ReviewV2SchemeContextTests.cs](../Assets/_Project/Tests/EditMode/ReviewV2SchemeContextTests.cs)、[ReviewV2IntegrationTests.cs](../Assets/_Project/Tests/PlayMode/ReviewV2IntegrationTests.cs)、[ReviewV2SchemePlayTests.cs](../Assets/_Project/Tests/PlayMode/ReviewV2SchemePlayTests.cs)、[ReviewV2InitialSchemeTests.cs](../Assets/_Project/Tests/PlayMode/ReviewV2InitialSchemeTests.cs)。既有验证范围与历史失败见 [T1 M2 Progress](M2/T1_Development_Progress.md)。

### 11.3 待 T2/T3 确认的缺口

| 事项 | 当前事实 | 下一步 |
|---|---|---|
| T2 ↔ T3 标记/回顾调用 | T1 未定义 Marker 创建或面板显示接口 | 两方交付方法/事件名、参数、完成/取消/失败行为 |
| Marker/持久化 Schema | 当前仅有会话/事实 DTO，未冻结 Marker DTO | T3 提交版本、样例、空值/枚举/坐标/时间/历史规则 |
| 保存范围与并发编辑 | Save 无操作类型、Marker 参数、事务/取消协议 | T3 明确如何取仓储快照、处理 after 补记和编辑；遇接口不足由三方审核兼容扩展 |
| 保存卡住/立即退出恢复 | Core 无 Save 通用超时，SafeExit 不取消 IO | T3 提交有界失败/恢复设计，T2 如实展示状态，再联合验证 |
| 完整产品/设备验收 | T1 独立能力 Ready for Integration；正式 T2/T3/Quest Link 仍待验收 | 接入正式交付后执行上表，不因本手册完成而提前标 Verified — End-to-End |

后续公共签名或语义变更须同步 ReviewContracts、实现、测试和本手册，并由 T1/T2/T3 审核；本次不修改这些边界。
