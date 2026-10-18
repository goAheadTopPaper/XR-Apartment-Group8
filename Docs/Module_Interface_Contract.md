# Module Interface Contract — 厨房评审核心流程

**状态**：Frozen v0.5（2026-10-18）；公共接口基线，变更须先修订本文并审查
**适用范围**：P0 B1=Proud+F2、B2=Flush-oriented+F2 的运行时评审旅程及 Core、Feedback、UI、Conflict Detection、Storage 的交接  
**需求依据**：Lean PRD v0.3 Final §§8–12、FR-01/02/03/05/06/07/09/10/11/12、SAFE-01。本文约定模块交接，不代替 PRD 验收。现有模型与 PRD 的差异单独记录，由模型模块修复。

## 1. 使用顺序与共同规则

1. **第一关仅交付契约**：流程负责人已审查本文、拟修改文件与分工；各工作包负责人已核对下述 C# 公共签名、状态与失败规则。本文现作为跨分支接口基线；共享接口变更先修本文并审查，再改代码。
2. **第二关才实施 Core Skeleton**：B1/B2 运行时切换、完整状态流转、真实 XR 交互的八步证据、任务进度、暂停/跳过/重试/退出及模块协调。先以 Mock Feedback/Storage 和临时 Debug UI 贯通；正式模块可依本文并行开发。Mock 与临时 UI 不构成 P0 验收。
3. 各包在独立功能分支验证并提交 PR。优先交付独立脚本和 Prefab；共享 Scene/核心 Prefab 只由指定场景集成人接入。必要的最小场景改动须逐项列清单，在独立功能分支提交，不直接改团队集成分支。每合入一包即进行联合检查。
4. 最后执行完整 Unity 构建和 Quest 3 + Quest Link 试跑。EVAL-01 静态前测本轮暂缓，保持未完成，不能宣布完整 P0 验收通过。

本文定义**交接接口**，不是要求每个交接处都创建一个 C# `interface`。正式与 Mock 均需替换的场景、反馈和存储适配使用窄接口；固定数据和纯流程函数使用明确类型。跨模块类型、顺序约束和错误规则同属接口。

## 2. 模块与文件归属

| 模块/所有者 | 拥有的实现和输出 | 输入 | 不拥有的决定 |
| --- | --- | --- | --- |
| Core（流程工作包） | `SessionPlan`、唯一 `JourneySnapshot`、命令处理、任务追踪、B1/B2 切换协调、效果/回执配对 | UI 命令、场景证据、反馈结果、场景/存储回执、冲突/安全通知 | 正式表单和校验、磁盘格式、几何检测、正式 UI、F2 模型 |
| Unity 场景适配（Core 编写，场景集成人接线） | 加载/重置实际方案，把既有 XRI 门体、抓取和区域行为转换成 `TaskEvidence` | Core 效果、注册表、Prefab、登记参数、语义角色 | 自行推进会话或重写底层交互 |
| Feedback（独立工作包） | 正式反馈 UI、字段级校验、反馈草稿和结果 | Core 维护的 `FeedbackRequest`/DTO、快照、用户输入 | 公共 DTO 定义、方案顺序、任务状态、保存成功判定 |
| UI 与 Orientation（独立工作包） | 引导、正式流程 UI、任务提示、拒绝提示、Yellow/Amber 和 Red 视觉 | 快照、命令结果、冲突/安全通知 | 直接改状态或记录、做几何检测 |
| Conflict Detection（独立工作包） | 基于登记参数的客观几何判断和 `ConflictNotice`；独立安全来源发 `SafetyNotice` | 当前模型/对象状态、参数和版本 | 主观评价、反馈与会话推进 |
| Storage（独立工作包） | 匿名本地写入、替换、读回与正式读取/恢复，返回 `SaveResult` | Core 维护 schema 的独立 `SessionRecord` 副本 | 公共 DTO 定义、选择下个方案、把写入失败当完成 |
| 场景集成人（已有人选） | 审查并合入唯一共享 Scene/核心 Prefab，联调各包 | 各包脚本、Prefab、接线清单 | 重定义模块规则 |
| 模型模块 | 修正 F2 形态、参数和版本证据 | PRD Final §6.2、Assumption Register | 按旧代理形态定义 Core 任务 |

程序集依赖维持 `Data ← Simulation ← Runtime` 的单向调用关系；`Simulation` 不引用 `UnityEngine`。共享纯类型放在 Data/Simulation，Runtime 各包使用独立目录和文件，避免并行修改同一实现。Core 是唯一会话状态所有者；其他模块只能提交输入或执行效果。`Assets/Editor/KitchenBenchmarkMenu.cs` 仅作制作/调试入口，正式构建通过运行时场景适配切换方案。

公共接口及跨模块 DTO 由 Core 工作包统一维护在 `Simulation/JourneyContracts.cs`（引用现有 Data 类型、无 UnityEngine）。Core 也维护 `Data/SessionOutcome.cs` 及现有 `Data/SessionRecord.cs` 的公共字段；Feedback/Storage 可提出字段需求，但不在各自分支直接重定义共享 schema。Feedback、UI、Conflict、Storage 负责人审查签名，在各自 Runtime 子目录实现。若以后拆 Runtime 程序集，依赖只可从实现指向 Simulation/Data，不能让 Core 引用某个具体 Runtime 模块。

## 3. 会话计划与身份

`SessionPlan` 在开始会话时冻结，包含：

| 字段 | 规则 |
| --- | --- |
| `sessionId` | 匿名、不含姓名或账号；同一会话所有事件、反馈与保存结果使用同一个 ID。为未来外部静态前测保留传入 ID 的能力；本轮未传入时由本地生成。 |
| `modelVersion` | 取本次使用的模型与参数版本；保存后不得用另一版本改写旧记录。 |
| `benchmarkIds` | 按实际可用模型和本次评审目的明确给出有序、唯一 ID；首轮集成使用 B1/B2。B3 是否加入由后续会话计划决定，不因注册表中存在就自动进入。 |
| `taskConfig` | 每个 Challenge **总共 3–5 件不同物品**，包含至少一件原有物品和一件新购物品；不是“先取出 3–5 件，再额外加入新购食品”。Take out 只统计原有物品，Put back 统计全部任务物品。B1/B2 复用同一组 ID、数量、角色与等价目标区；只变几何。 |

所有跨模块 ID 均为非空稳定字符串，不含姓名或账号；时间明确为 UTC 或会话内单调秒数。只读快照及保存输入须复制集合，不能向 UI 暴露可变 `SessionRecord`。创建计划时验证每个 ID 能由 `BenchmarkRegistry.Find()` 找到、允许 Challenge，且 Prefab、语义目标及任务配置可用。失败返回 `PlanInvalid` 和缺失项，停留 Setup。现有 `BenchmarkRegistry.OrderFor()` 固定三项，不作为 P0 顺序来源。`BENCH_B1/B2/B3.asset` 的 `controlledQuestion` 当前为空；正式比较前由负责人填写 PRD Final §6.3 的实际受控问题，不从模型外观推断结论。

| 共享类型 | 必须包含 | 生产者 → 消费者 |
| --- | --- | --- |
| `JourneySnapshot` | `sessionId`, 状态、`SessionOutcome`、`SaveStatus`、展示中与挑战中的方案 ID、顺序、逐方案结果、当前步骤、任务物品进度、反馈摘要和冲突摘要 | Core → UI/Feedback/测试；返回防御性副本，UI 不持有 Core 内部集合 |
| `TaskEvidence` | `sessionId`, `benchmarkId`, `runId`, 步骤、`ReviewRole`, 对象/物品/区域稳定 ID、会话内时间、实际测量、已释放抓取标志 | 场景适配 → Core；`runId` 仅区分当前 Challenge 与旧实例/重试；同一步骤/物品只计一次。Open/Close 对照 `TaskConfig.AccessTargets` 验证全部指定门体/抽屉 |
| `FeedbackRequest` | session/benchmark ID、该方案 Completed/Skipped | Core → Feedback；草稿由 Feedback UI 自行保留 |
| `FeedbackDraft` | session/benchmark ID、Decision/Category/Importance、可选 Short explanation/额外字段或跳过原因（见 §6 用户确认变更） | 正式表单或 Debug UI → Feedback 校验；不得直接变更 Core 状态 |
| `FeedbackValidationResult` | session/benchmark ID、`isValid`、字段级错误、校验后的 `BenchmarkFeedback` 或跳过原因 | Feedback → Core；正式校验由 Feedback 所有 |
| `FinalComparison` | `preferredBenchmarkId`, `tradeOffSummary` | UI/Feedback → Core；Core 复核资格并推导 `SessionOutcome` |
| `SessionRecord`（复用） | session/model ID、实际方案顺序、逐方案结果/反馈、帮助/重置/舒适事件、冲突关联、最终比较和 outcome | Core 交给 Storage 的独立副本；Storage 不持有 Core 内部对象，正式序列化字段由 Feedback/Storage 共同核对 |

正式与 Mock 的**替换点**如下。§3.1 给出最小 C# 签名：场景和存储异步完成时调用一次回调；Runtime Host 将结果交给 Core。一个会话同一时刻最多有一个待完成的加载/重置/保存请求，不需要通用事件总线。

| 接入点 | 输入 → 输出 | 正式实现与测试替身 |
| --- | --- | --- |
| `IReviewScenePort.Execute` | `JourneyAction`（Load/Reset/Return/Replay/Recenter）+ `Action<SceneActionResult>` 回调；场景证据由 `EvidenceObserved` 发出 | Runtime 场景适配 / Mock Scene；Core 不知道 GameObject |
| `IFeedbackPort.Validate` | `FeedbackRequest` + `FeedbackDraft` → `FeedbackValidationResult`（含字段错误） | 正式 Feedback 校验 / Mock Feedback；正式或 Debug UI 均通过它再提交 Core |
| `ISessionStore.Save` | `requestId` + `SessionRecord` 独立副本 + `Action<SaveResult>` 回调 | 本地存储 / Mock Storage；`TryRead(sessionId)` 返回记录或明确错误 |
| `IJourneyView.Render` | `JourneySnapshot` + `JourneyErrorCode` + 可读消息 → 仅呈现，不返回状态变更 | 正式 UI / 临时 Debug UI；用户操作另走 `IJourneyCore.Submit` |
| Conflict/Safety notice 入口 | `ConflictNotice`/`SafetyNotice` → Core 关联记录并广播最新快照/notice | 正式检测器或测试事件源；notice 不直接调用 Feedback/Storage |

Runtime Host 连接这些接口，只在一处把场景/存储回调包装为 `SceneCompleted`/`SaveCompleted` 命令。回调可同步触发，但 Host 必须排队到当前 `Submit` 返回后再交 Core，避免重入。回调必须在 Unity 主线程交付；正式适配器若在后台完成 I/O，先切回主线程。Core 只接受当前待处理 `requestId`；退出或重试后的旧回调丢弃。各包不直接调用彼此的具体实现，也不复制流程规则。

### 3.1 最小公共 C# 签名

先在 `Data/SessionOutcome.cs` 定义持久化结果，以免 Data 反向引用 Simulation：

```csharp
namespace XRApartment.Data
{
    public enum SessionOutcome { None, Completed, Incomplete, Aborted }
}
```

下列声明是拟放入 `Simulation/JourneyContracts.cs` 的**可编译接口形状**，引用该枚举及仓库现有的 `SessionState`、`ChallengeStep`、`ReviewRole`、`TaskOutcome`、`BenchmarkFeedback`、`SessionRecord` 和反馈枚举。DTO 以自动属性展示跨模块字段；Core/适配器在入口验证并复制集合和 `SessionRecord`，不把可变实例直接交给 UI。一个命令最多返回一个待执行动作，避免额外的通用效果队列。

```csharp
using System;
using System.Collections.Generic;
using XRApartment.Data;

namespace XRApartment.Simulation
{
    public enum SaveStatus { None, Pending, Verified, Failed }
    public enum JourneyActionType { LoadBenchmark, ResetTask, ReturnItem, ReplayBenchmark, Recenter, SaveRecord, RestoreCurrentStep }
    public enum JourneyCommandType
    {
        CompleteSetup, AcknowledgeOrientation, ShowBenchmark, StartChallenge,
        SubmitTaskEvidence, ConfirmReorganisation, SkipChallenge, SubmitFeedback,
        ConfirmComparison, Pause, Resume, ResetCurrentTask, ReturnItemToTray, ReplayCurrentBenchmark,
        Recenter, Retry, RequestExit, SceneCompleted, SaveCompleted, RestoreCurrentStep,
        ConflictObserved, SafetyObserved
    }
    public enum JourneyErrorCode
    {
        None, InvalidPlan, WrongSession, InvalidState, WrongBenchmark,
        InvalidEvidence, InvalidFeedback, OperationFailed, StaleResult
    }

    public sealed class TaskItemSpec
    {
        public string ItemId { get; set; }
        public bool IsExistingItem { get; set; }
        public string SourceZoneId { get; set; }
        public string TemporaryZoneId { get; set; }
        public string ReturnZoneId { get; set; }
    }
    public sealed class TaskConfig
    {
        public IReadOnlyList<TaskItemSpec> Items { get; set; }
        public IReadOnlyList<TaskAccessSpec> AccessTargets { get; set; }
        public string ApproachZoneId { get; set; }
        public string LeaveZoneId { get; set; }
        public float OpenProgressThreshold { get; set; }
        public float ClosedProgressThreshold { get; set; }
    }
    public sealed class TaskAccessSpec
    {
        public string ObjectId { get; set; }
        public ReviewRole Role { get; set; }
    }
    public sealed class SessionPlan
    {
        public string SessionId { get; set; }
        public string ModelVersion { get; set; }
        public IReadOnlyList<string> BenchmarkIds { get; set; }
        public TaskConfig Task { get; set; }
    }
    public sealed class TaskEvidence
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public long RunId { get; set; }
        public ChallengeStep Step { get; set; }
        public ReviewRole Role { get; set; }
        public string ObjectId { get; set; }
        public string ItemId { get; set; }
        public string ZoneId { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public float Measurement { get; set; }
        public bool Released { get; set; }
    }
    public sealed class FeedbackRequest
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public TaskOutcome TaskOutcome { get; set; }
        public string OutcomeReason { get; set; }
    }
    public sealed class FeedbackDraft
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public CombinationDecision Decision { get; set; }
        public FeedbackCategory Category { get; set; }
        public Importance Importance { get; set; }
        public string ShortExplanation { get; set; }
        public string SkipReason { get; set; }
        public ExperienceTag Experience { get; set; }
        public EverydayImpact EverydayImpact { get; set; }
        public DiscoveryMode DiscoveryMode { get; set; }
    }
    public sealed class FeedbackValidationResult
    {
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public bool IsValid { get; set; }
        public IReadOnlyDictionary<string, string> FieldErrors { get; set; }
        public BenchmarkFeedback Feedback { get; set; }
    }
    public sealed class FinalComparison
    {
        public string PreferredBenchmarkId { get; set; }
        public string TradeOffSummary { get; set; }
    }
    public sealed class ConflictNotice
    {
        public string IssueId { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ModelVersion { get; set; }
        public string ObjectId { get; set; }
        public ReviewRole Role { get; set; }
        public string AssumptionKey { get; set; }
        public float Measurement { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public string Message { get; set; }
        public bool FeedbackRecorded { get; set; }
    }
    public sealed class SafetyNotice
    {
        public string SafetyEventId { get; set; }
        public string SessionId { get; set; }
        public string RiskType { get; set; }
        public bool IsActive { get; set; }
        public float OccurredAtSessionSeconds { get; set; }
        public string Message { get; set; }
        public string SuggestedAction { get; set; }
    }
    public sealed class SceneActionResult
    {
        public long RequestId { get; set; }
        public bool Succeeded { get; set; }
        public string ActualBenchmarkId { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class SaveResult
    {
        public long RequestId { get; set; }
        public bool Succeeded { get; set; }
        public bool ReadBackVerified { get; set; }
        public string Location { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class ReadResult
    {
        public bool Succeeded { get; set; }
        public SessionRecord Record { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
    }
    public sealed class JourneyAction
    {
        public long RequestId { get; set; }
        public long RunId { get; set; }
        public JourneyActionType Type { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ItemId { get; set; }
        public ChallengeStep? Step { get; set; } // RestoreCurrentStep only
        public SessionRecord Record { get; set; }
    }
    public sealed class JourneySnapshot
    {
        public string SessionId { get; set; }
        public SessionState State { get; set; }
        public SessionOutcome Outcome { get; set; }
        public SaveStatus Save { get; set; }
        public string DisplayedBenchmarkId { get; set; }
        public string ActiveBenchmarkId { get; set; }
        public long ActiveRunId { get; set; }
        public ChallengeStep? CurrentStep { get; set; }
        public int CompletedItemCount { get; set; }
        public int TotalItemCount { get; set; }
        public bool IsBusy { get; set; }
        public IReadOnlyList<string> BenchmarkOrder { get; set; }
        public IReadOnlyDictionary<string, TaskOutcome> BenchmarkOutcomes { get; set; }
        public IReadOnlyList<BenchmarkFeedback> Feedback { get; set; }
        public IReadOnlyList<ConflictNotice> Conflicts { get; set; }
        public SafetyNotice CurrentSafety { get; set; }
    }
    public sealed class JourneyCommand
    {
        public JourneyCommandType Type { get; set; }
        public string SessionId { get; set; }
        public string BenchmarkId { get; set; }
        public string ItemId { get; set; }
        public string Reason { get; set; }
        public bool PrerequisitesMet { get; set; }
        public TaskEvidence Evidence { get; set; }
        public FeedbackValidationResult Feedback { get; set; }
        public FinalComparison Comparison { get; set; }
        public SceneActionResult SceneResult { get; set; }
        public SaveResult SaveResult { get; set; }
        public ConflictNotice Conflict { get; set; }
        public SafetyNotice Safety { get; set; }
    }
    public sealed class JourneyResult
    {
        public bool Accepted { get; set; }
        public JourneyErrorCode ErrorCode { get; set; }
        public string Message { get; set; }
        public JourneySnapshot Snapshot { get; set; }
        public JourneyAction Action { get; set; }
    }

    public interface IJourneyCore
    {
        JourneySnapshot Snapshot { get; }
        JourneyResult Submit(JourneyCommand command);
    }
    public interface IReviewScenePort
    {
        event Action<TaskEvidence> EvidenceObserved;
        void Execute(JourneyAction action, Action<SceneActionResult> completed);
    }
    public interface IFeedbackPort
    {
        FeedbackValidationResult Validate(FeedbackRequest request, FeedbackDraft draft);
    }
    public interface ISessionStore
    {
        void Save(long requestId, SessionRecord record, Action<SaveResult> completed);
        void TryRead(string sessionId, Action<ReadResult> completed);
    }
    public interface IJourneyView
    {
        void Render(JourneySnapshot snapshot, JourneyErrorCode errorCode, string message);
    }
    public interface IConflictSource
    {
        event Action<ConflictNotice> ConflictObserved;
    }
    public interface ISafetySource
    {
        event Action<SafetyNotice> SafetyObserved;
    }
}
```

`null` 仅表示某命令不使用该载荷或当前没有待执行动作；必填字符串不接受空白。`RequestId` 由 Core 单调生成，仅配对当前异步场景/存储请求；`RunId` 由 Core 在开始/重试 Challenge 时生成。Core 不需要通用 `commandId`/`eventId`/`effectId`、双 revision 或事件日志：重复点击由状态和唯一待处理操作拒绝，重复物品证据由步骤与 item ID 集合去重。正式存储及场景适配须恰好回调一次；重复或迟到回调按 `RequestId` 忽略。

`TaskConfig.Items` 的 `Count` 必须为 3–5，`ItemId` 各异，至少各有一件 `IsExistingItem=true/false`。新购物品的 `SourceZoneId` 可为 `null`；原有物品必须有来源区。所有物品均须有放回目标区，原有物品须有临时放置区。`AccessTargets` 至少包含一个稳定对象 ID，角色只能为 `FridgeDoor`/`FridgeDrawer`；Open/Close 必须分别收齐全部目标。区域、阈值、访问目标和物品在创建计划时校验，不能在 B1/B2 中途变更。

## 4. 唯一流程状态与推进接口

```
Setup → Orientation → Explore
                  → Challenge.Active(当前方案) → Feedback.Pending(当前方案)
                  → Explore / 下一个方案
                  → Comparison.Summary → 保存验证 → Finished
```

`Paused` 记住进入前状态和任务游标，恢复后检查场景仍为当前方案。`SessionState` 在现有八态上只增加终态 `Exited`，表示用户安全离开，**不表示已保存**；无需为每个异步操作增加公开状态。`JourneySnapshot.Save` 独立显示 `None/Pending/Verified/Failed`。一个会话同一时刻只允许一个待完成场景或存储动作，`RequestId` 用于忽略旧回调。

核心流程对外只暴露一条命令入口和一份只读状态快照：

| 接口 | 必须包含 | 返回/行为 |
| --- | --- | --- |
| `IJourneyCore.Submit` | `JourneyCommand`（见 §3.1）；按 `Type` 使用对应载荷 | `JourneyResult` 含 accepted、`JourneyErrorCode`、可读详情、最新快照及至多一个 `JourneyAction`；状态不允许时拒绝，不产生动作 |
| `JourneySnapshot` | 见 §3 | UI 唯一流程显示来源；按钮禁用只是提示，Core 仍验证命令 |
| `JourneyAction` | `RequestId`、类型、可选 benchmark/item ID；保存时附独立 `SessionRecord` 副本 | Runtime Host 分派给 Scene 或 Storage；发出不等于成功 |
| 异步完成 | Scene 回 `SceneActionResult`、Storage 回 `SaveResult` | Host 分别提交 `SceneCompleted`/`SaveCompleted`；仅当前 `RequestId` 可改变状态 |

Core 在 Unity 主线程按序处理命令。可预期拒绝不抛异常、不改记录或任务进度；适配器异常转成失败结果。只允许计划内未解决方案按计划顺序进入 Challenge。反馈须与当前 session/方案匹配且经 Feedback 校验。`SessionOutcome` 表示**评审结果**，`SaveStatus` 表示**本地记录是否验证**，两者不能互相推断。

| 命令（提交者） | 载荷和成功条件 |
| --- | --- |
| `CompleteSetup`, `AcknowledgeOrientation`（UI） | 校准/练习完成后依次进入 Orientation、Explore；正式引导内容由 UI 模块负责。 |
| `ShowBenchmark`（UI） | Explore 中请求计划内方案；发 `LoadBenchmark`，收到成功回执才更新展示方案，仍在 Explore。Explore 交互不计入标准 Challenge 证据。 |
| `StartChallenge`（UI） | 仅未解决方案且符合计划顺序；发加载/重置效果，收到实际方案成功回执后才进入 Challenge.Active，失败留 Explore 并可重试。 |
| `SubmitTaskEvidence`（场景） | 含完整 `TaskEvidence`；核对 session、方案、任务代次、当前步骤、对象/区域及阈值后推进。 |
| `ConfirmReorganisation`（UI/临时 Debug UI） | 仅在真实物品已发生登记储物区移动后接受参与者确认；不声称系统判断主观整理质量。 |
| `SkipChallenge`（UI） | 当前方案且原因非空；保留证据，转 Feedback.Pending，允许四项评价留空，outcome 为 Incomplete。 |
| `SubmitFeedback`（Feedback） | 携 `FeedbackValidationResult`；无效留 Feedback.Pending 并返回字段错误。有效时记录一次、触发对应冲突 Amber、请求逐方案保存；收到已验证保存回调后才进入 Explore/Comparison。保存失败留 Feedback.Pending 并可重试，安全退出始终可用。 |
| `ConfirmComparison`（UI/Feedback） | 所有计划内方案已解决；至少一项 Completed 时偏好指向已完成方案且 trade-off 非空；全 Skipped 时偏好可空。设置 `SessionOutcome.Completed/Incomplete`，发最终保存；验证后才进入 Finished。 |
| `Pause`, `Resume`, `RequestExit`（UI/安全入口） | 暂停/恢复不丢任务或草稿；`RequestExit` 立即设 `Exited + Aborted` 并尽力保存，允许用户离开，不等待存储回调，也不变为 Finished。 |
| `ResetCurrentTask`, `ReturnItemToTray`, `ReplayCurrentBenchmark`, `Recenter`（UI） | 发场景效果；成功后更新进度/帮助事件，失败保留数据并可重试；Recenter 仅改定位。 |
| `RestoreCurrentStep`（UI） | 仅活动 Challenge 且非忙碌/暂停时可用；发同名场景效果并携当前 `ChallengeStep`，只恢复本步骤起点、清空本步骤部分证据，保留已完成步骤、方案和 `RunId`。失败不改变 Core 进度，可重试或安全退出。 |
| `Retry`（UI） | 对加载/重置/保存失败发新 `RequestId`；旧回调失效。 |
| `SceneCompleted`/`SaveCompleted`（Runtime Host） | 分别带 `SceneActionResult`/`SaveResult`；仅当前请求可被接受；保存必须 `Succeeded && ReadBackVerified` 才是 Verified。 |

`JourneyAction` 仅表示外部工作：`LoadBenchmark`, `ResetTask`, `ReturnItem`, `ReplayBenchmark`, `Recenter`, `SaveRecord`, `RestoreCurrentStep`。`LoadBenchmark` 是原子操作：加载指定方案并恢复该方案的任务基线；用于 Challenge 时携带新的 `RunId`，用于 Explore 展示时 `RunId=0`。UI 根据快照渲染，不发送 `ShowUI` 动作；拒绝原因由 `JourneyResult` 呈现。场景部分加载失败时，适配器恢复到上一个确认方案；不能恢复则回 `OperationFailed` 并禁止进入 Challenge。

暂停时若已有场景请求，Core 先记录 Paused 并等待该请求结束或超时，再按顺序尝试保存；期间任务证据不推进。安全退出优先级更高：立即使旧 `RequestId` 失效、转 Exited、以当前内存记录发一次尽力保存并允许离开。Runtime Host 对无回调的请求设定测试可控的超时，转 `OperationFailed` 供重试；不让用户无限等待。

| 情况 | 终点 `SessionState` | `SessionOutcome` | `SaveStatus` 与用户可见结果 |
| --- | --- | --- | --- |
| 正常完成、两方案均完成 | `Finished`，仅在最终读回验证后 | `Completed` | `Verified`；显示保存摘要 |
| 正常结束但有方案跳过 | `Finished`，仅在最终读回验证后 | `Incomplete` | `Verified`；显示跳过原因与不完整结果 |
| 安全退出/中止（任何活动状态） | `Exited`，立即允许离开 | `Aborted` | 可为 `Pending/Verified/Failed`；未验证时明确显示“记录未验证”，绝不显示 Finished |
| 正常流程的最终保存失败 | 保持 `ComparisonSummary`，可重试或安全退出 | 已确定的 `Completed/Incomplete` | `Failed`；保留内存内容，不显示完成或已保存 |

## 5. 场景和任务证据接口

| 方向 | 消息 | 规则 |
| --- | --- | --- |
| Core → 场景 | `IReviewScenePort.Execute(action, completed)`，`action.Type=LoadBenchmark` | 以 `SceneActionResult.RequestId` 返回**实际**加载的方案 ID、成功/失败。切换时解除旧实例订阅、清理任务物/区域状态，只替换 benchmark 槽位，保留共同厨房/XR Rig/UI。失败恢复前一确认方案；不能恢复则报告失败并阻止 Challenge。 |
| Core → 场景 | 同一 `Execute`，动作类型为 ResetTask/ReturnItem/ReplayBenchmark/Recenter | 返回同 `RequestId` 的结果；恢复物品、门/抽屉和任务状态；Recenter 只处理定位。重置或重新开始 Challenge 产生新的 `RunId`。 |
| Core → 场景 | 同一 `Execute`，动作为 `RestoreCurrentStep`，`Step` 等于当前步骤 | 场景适配恢复该步骤入口的门和共享物品状态、重置当前步骤的证据采集；不移动 XR Rig。Core 仅在成功回执后清空当前步骤部分进度，保留此前步骤与同一 `RunId`。不得把恢复当成真实任务证据。 |
| 场景 → Core | `EvidenceObserved(TaskEvidence)` | 当前 `RunId`、方案、步骤和物品/区域必须匹配；Core 按步骤及稳定 item ID 去重，无需通用 event ID。 |

每个活动 Challenge 步骤都有可恢复的场景入口检查点。“恢复本步骤”只清零该步骤的部分计数，保留已完成步骤，不移动 XR Rig；恢复失败不改变 Core 进度，且要求用户先释放仍被抓取的任务物品。Approach/Open 阶段拒绝任务物品抓取并显示原因，Explore 自由抓取不受限；Take out 开始前来源物品不得因提前抓取丢失来源区边沿。若物理异常使其离开来源区，用户可恢复本步骤，不得伪造取出证据。

| 八步任务 | 推进所需证据 |
| --- | --- |
| Approach | XR Rig 到达当前方案登记的操作区，记录区域 ID；按钮不能直接完成。 |
| Open | 指定 `FridgeDoor`/`FridgeDrawer` 的**实际运动进度**达到 TaskConfig 开启阈值；不能以 `IsOpen` 目标值或 select 点击代替。 |
| Take out | `TaskConfig.Items` 中标为原有的物品逐件离开来源 `StorageZone`；总任务物品数为 3–5，原有物品不必单独达到 3 件。 |
| Temporary placement | 上述物品逐件进入登记的 `CounterSurface` 临时区且已释放抓取；瞬间经过碰撞体不计。 |
| Reorganise | 登记储物区至少发生一次指定真实物品重定位，再由参与者确认；只记录动作及确认，不判断整理质量。 |
| Put back | 原有物品与标为新购的物品（两类合计 3–5 件）逐件进入各自有效目标 `StorageZone` 且已释放；按 item ID 和目标区核对。 |
| Close | 本次涉及的门/抽屉实际回到关闭阈值；不以点击代替。 |
| Leave | XR Rig 离开操作区并达到登记离开区/边界，记录空间事件；按钮不能直接完成。 |

每步由 Core 核对当前方案、步骤及证据，切换/重置后用 `RunId` 排除迟到事件。Take out、Temporary placement、Put back 以各步骤的 item ID 集合计数，重复进入区域不能重复推进。现有 `KinematicSwingDoor`/`KinematicLinearDrawer`、XRI 抓取和移动继续使用；场景适配监听运动完成和区域变化，不重写底层交互。`ReviewElement` 的语义角色用于跨方案解析目标，不按 B1/B2 硬编码门数。任务可跳过：原因必填、保留证据，进入该方案 Feedback.Pending；四项评价可空，最终 outcome 为 Incomplete。两方案均跳过时不强迫偏好。现有 F2 两扇门与 PRD Final 单门要求的差异由模型模块修复，不以本轮旧模型完成 P0 验收。

**当前 Core Play 验证场景的任务布局（不改变公共 DTO）**：两件原有物品从上方 Freezer 的 `ZONE_FreezerSource` 取出，临放台面后与一件新购物品一起放入下方 Fresh。`ZONE_FreshDestination` 是一个稳定的逻辑 `ReturnZoneId`，可由下方三格各自的触发体共同实现；任一格有效，但必须已释放并稳定停留，路过不计。Open/Close 对上、下两扇实际访问门逐一取证。此布局是现有 F2 双门代理的 Play 验证接线，不改变前述模型与 PRD Final §6.2 偏差的状态，也不要求其他模块按 Prefab 名称写死流程。

## 6. UI、反馈与冲突接口

2026-10-18 用户明确确认：当前 Feedback **只保存选择项，取消文字说明必填要求**。完成方案必填 Decision、Category、Importance；Experience、Everyday impact 等仍为可选。`ShortExplanation` / `shortExplanation` 字段为兼容既有记录而保留，但可空，不自动生成文字、不把口述伪装成文字或录音。本次按用户确认覆盖原 Short explanation 必填规则；这属于相对 PRD Final FR-07 四项必填的明确偏差，不代表原 FR-07 已完整验收。最终比较的 trade-off 与跳过原因规则保持原契约。

UI 使用 `JourneySnapshot` 呈现 Setup、Orientation、Explore、任务、反馈、比较、暂停及错误；只通过命令提交意图。按钮是否可用由快照决定，但 Core 仍验证每条命令。UI 不直接设置 `SessionState`、方案 ID 或保存成功标志。临时 Debug UI 只用于 Play 测试，采用同一命令和反馈接口，正式 UI 可替换它。

| 输入/输出 | 合同 |
| --- | --- |
| `FeedbackRequest` → `FeedbackValidationResult`（Feedback 工作包） | 完成方案必须有 Combination ID、Decision、Category、Importance；Short explanation、Experience、Everyday impact、Discovery mode 不阻塞。Task outcome 仍由 Core 的 Completed/Skipped 决定。跳过要求原因，评价可空。失败返回 `fieldId → errorCode/message`，取消/无效不改 Core 状态。Core Mock 使用同一输入输出，不绕过 session/方案匹配。 |
| `FinalComparison`（UI/Feedback → Core） | 至少一个 Completed 时偏好必须指向已完成方案，主要 trade-off 非空；存在跳过仍为 Incomplete。全跳过时偏好可空，不写成完成比较。 |
| `ConflictNotice`（Conflict Detection → Core/UI） | `issueId`, benchmark/model ID、对象 ID、登记参数 key、客观测量及说明。Core 按 issue+方案去重并关联反馈；UI 在客观触发时立即显示 Yellow＋文字/图标，不阻塞任务。参与者**提交该方案的有效反馈后**转 Amber，仅表示反馈已记录在本次会话内；不表示已保存、已解决或模型已修正。 |
| `SafetyNotice`（安全来源 → Core/UI） | `safetyEventId`, session ID、风险类型、时间及建议动作；真实世界边界/play-area 风险独立以 Red＋文字/图标显示，可触发 Pause/RequestExit，不进入设计冲突状态。 |

这与 PRD Final §10.2 的“反馈后 Amber”一致。`SaveStatus` 单独呈现持久化结果：反馈有效但保存失败时，Amber 保持，同时显示明确的“本地保存失败/可重试”；不得用颜色暗示记录已安全落盘。Conflict Detection 对门扇/抽屉与登记阻挡区等可复现几何事实作判断，读取 `ASM_KitchenParameters` 的登记值及版本，不自动判定“舒适”“方便”或“空间足够”。UI 负责视觉，检测模块负责测量，Core 负责 issue 与反馈的关联。Red 不复用 Yellow/Amber 等级。

`ConflictNotice.FeedbackRecorded` 由 Core 在发给 UI 的快照副本中设置：Conflict Detection 产生的原始 notice 始终不决定颜色。Core 将最小冲突关联复制为 `SessionRecord.conflicts`，包含 issue、方案、模型、对象、登记参数、测量及反馈已记录状态。`JourneySnapshot.CurrentSafety` 保留当前 Red 安全通知，安全来源以同一 `SafetyEventId` 的 `IsActive=false` 通知解除；它不改变设计冲突状态。

## 7. 本地保存接口与终态

本地 CSV 实现约定（2026-10-18，FR-10，不改变公共签名）：`Runtime/Storage/CsvSessionStore` 实现现有 `ISessionStore`，`LocalCsvStorageAdapter` 提供 Unity 组件接入。默认文件为 `Application.persistentDataPath/ReviewRecords/sessions.csv`。每位评审者每次新会话生成新的匿名 `sessionId`，增加一行；同一会话的逐方案反馈、暂停、最终确认及重试更新同一行，避免把检查点重复计为用户提交。CSV 包含可读摘要和完整 `SessionRecord` 的 JSON 单元格，后者用于无损恢复；没有独立 JSON 文件。UTF-8 BOM、标准 CSV 引号转义保留中文和多行文字。实现同步串行保存，在 Unity 主线程调用/回调；同目录临时文件落盘验证后原子替换，最终文件再次读回匹配才返回成功。文件锁定、格式损坏、写入或验证失败返回 `OperationFailed`，不覆盖坏文件、不自动另存绕过错误。按本次用户接线授权，`PREF_Core_PlayValidation` 的 Storage Adapter 改为 Local CSV，场景实例继承该配置；保留 Mock 脚本供测试使用。Feedback 表单使用同一 Host 保存流程；保存失败展示错误及 Retry save，已提交的选择保留，重试成功前不推进方案。

`ISessionStore.Save(requestId, record, completed)` 接收 Core 制作的独立 `SessionRecord` 副本，回调 `SaveResult` 包含相同 `RequestId`、写入是否成功、读回是否验证、存储位置和错误码。`TryRead(sessionId, completed)` 由 Storage 工作包正式实现，返回 `ReadResult`。格式为内部选择；不得要求账号或云端，不保存音视频或身份。Core 维护现有 `SessionRecord` 的公共字段：新增 `BenchmarkFeedback.shortExplanation`、`SessionRecord.sessionOutcome` 与 `SessionRecord.conflicts`，逐方案结果/原因复用现有 `BenchmarkFeedback.taskOutcome`/`outcomeReason`。Feedback/Storage 审查字段含义，在独立文件实现正式校验与序列化。现有 `HasAllRequiredFields()` 不能替代 Feedback 模块的正式字段校验。

- 每次逐方案反馈、暂停/安全退出及最终确认后请求保存。对同一 session ID 临时写入、完成替换并读回验证，避免半写入冒充成功。同一时刻只有一个当前有效保存请求，旧 `RequestId` 回调无效。安全退出可使尚未返回的旧请求失效；Storage 必须按 `RequestId` 串行提交同一 session 的替换，保证较新的退出快照最终生效，不建立通用多版本框架。
- 逐方案保存失败时保留内存反馈、保持 Feedback.Pending，显示失败和重试入口；安全退出始终可用。Amber 仍表示该反馈已提交，与保存状态分开。
- 正常比较完成后，保存失败保持 Comparison.Summary + `SaveStatus.Failed`，不显示 Finished；验证成功才进入 Finished。成功保存的跳过会话 outcome 为 `Incomplete`。安全退出立即为 Exited + `Aborted`；尽力保存，失败或来不及回调时记录未验证，仍允许离开。
- Mock Storage 可模拟成功、失败、迟到回执及重试，只用于 Core 骨架验收。外部静态前测目前没有实现；`sessionId` 保留未来关联入口。EVAL-01 在补齐前始终为未完成验收项。

## 8. 每包交付与联合测试

| 工作包 | 独立完成判据 | 接入后联合检查 |
| --- | --- | --- |
| Core Skeleton | EditMode 覆盖 B1/B2、八步证据、重复物品/旧 `RunId`/乱序事件、方案隔离、暂停/跳过/重试/安全退出及 Mock 保存失败 | Play 以真实 XR 动作推进任务，Mock Feedback/Storage 和临时 UI 经正式接口使用；不把 Mock 当最终验收 |
| Unity 场景适配 | 加载/重置并产生带稳定 ID 的真实任务事件；对象语义和阈值可追溯 | 运行时 B1/B2 切换、无旧实例/事件残留，失败回到确认状态 |
| Feedback | 完成/跳过字段级校验、草稿/取消及正式反馈界面 | 无效数据不推进；当前方案的有效结果触发 Core 保存请求 |
| UI 与 Orientation | 各状态入口、说明、错误与安全退出；Yellow/Amber/Red 语义 | 快照与按钮同步；暂停恢复不丢输入，不直接写 Core 状态 |
| Conflict Detection | 登记参数和触发证据可追溯，Design 与 Safety 分离 | Yellow 立即出现，**对应有效反馈提交后**变 Amber；保存失败另行提示 |
| Storage | 写入、替换、读回、失败重试及迟到回执测试 | 两方案可回读；未验证最终保存不显示 Finished |
| 场景集成/模型 | 最小共享资产修改清单；F2 模型修正由模型包独立验证 | Windows build、Quest Link 试跑；模型偏差和 EVAL-01 未完成项明确记录 |

当前仓库已有 `BenchmarkDefinition`、`BenchmarkRegistry`、`ModelAssumptions`、`SessionStateMachine`、`ChallengeTaskTracker`、`SessionRecord` 与 `ReviewElement`，应优先复用；它们**尚未满足本契约**。特别是固定三方案排序、仅计数的完成判定、缺少 Short explanation 的旧反馈模型、仅在编辑器的切换、缺少任务区域与真实写盘，须按归属修正，不能把现有类名当成验收证明。

## 9. 错误处理和幂等规则

跨模块统一使用 §3.1 的 `JourneyErrorCode` 与可读详情；Feedback 另返回字段级错误。`InvalidPlan` 包含缺失的 benchmark/Prefab/任务区域说明；`WrongBenchmark` 覆盖非计划、已解决或与当前方案不符；`InvalidEvidence` 覆盖旧 `RunId`、乱序、重复物品和测量不足；`OperationFailed` 覆盖场景加载/恢复或保存失败，`StaleResult` 覆盖迟到回调。可预期拒绝不抛异常；适配器异常转失败结果并保留可恢复快照。日志只携匿名 session、benchmark、`RunId`、`RequestId`，不写个人信息。

同一 UI 命令在已有待处理操作时由状态守卫拒绝；同一步骤的同一物品只计一次。命令拒绝与动作失败分开呈现。加载/重置失败时场景适配报告实际状态，必要时阻止挑战并提示人工恢复。暂停或安全退出期间迟到的任务证据不推进流程。场景/存储回调只匹配当前 `RequestId`；Core 在前一个请求结束或失败前不发下一个异步请求。这里不建立持久命令日志、通用事件总线或多版本并发保存框架。

## 10. 拟新增和修改的文件

下表是**计划清单**，不是已实现文件；具体类名如需调整，须先修订本契约并审查。所有新增 Unity 资源随同提交 `.meta`。`Runtime` 各包使用独立子目录；暂不为每个目录新建程序集，以保留当前 Data → Simulation → Runtime 依赖方向。

| 工作包/所有者 | 拟新增文件 | 拟修改文件 |
| --- | --- | --- |
| 契约/集成负责人审查 | 无 | `Docs/Module_Interface_Contract.md`（本轮 Frozen v0.4） |
| Core / 流程负责人 | `Assets/_Project/Scripts/Data/SessionOutcome.cs`、`Simulation/JourneyContracts.cs`, `JourneyCoordinator.cs`；`Assets/_Project/Scripts/Runtime/Core/JourneyRuntimeHost.cs`, `RuntimeBenchmarkAdapter.cs`, `TaskEvidenceBridge.cs`；`Assets/_Project/Tests/EditMode/JourneyCoordinatorTests.cs`, `TaskEvidenceContractTests.cs` | `Data/SessionRecord.cs`（最小公共字段）、`Simulation/SessionStateMachine.cs`, `SessionState.cs`（加入 Exited）, `ChallengeTaskTracker.cs`；必要时 `Runtime/BenchmarkSlotMarker.cs` |
| Core Mock / 流程负责人 | `Runtime/Debug/MockFeedbackAdapter.cs`, `MockStorageAdapter.cs`, `JourneyDebugUI.cs` 或独立 Debug Prefab | 不修改正式 UI/Storage 文件 |
| Feedback / 反馈负责人 | `Runtime/Feedback/FeedbackAdapter.cs`、正式表单 Prefab/脚本、字段校验测试 | 不直接修改公共 DTO；新增字段先提给 Core 维护 |
| UI & Orientation / UI 负责人 | `Runtime/UI/JourneyPresenter.cs`、Orientation/正式流程 UI Prefab、UI 测试 | 现有 `Runtime/KitchenMenuController.cs` 如需接入 Core 命令；现有 UI Prefab 由该包维护 |
| Conflict Detection / 冲突负责人 | `Runtime/Conflict/ConflictDetector.cs`、冲突/安全通知适配与几何测试 | 登记参数/相关 Prefab 改动须与模型和场景集成人协调 |
| Storage / 存储负责人 | `Runtime/Storage/LocalSessionStore.cs`, `SessionStoreTests.cs`、独立的序列化映射 | 不直接修改公共 DTO；新增字段先提给 Core 维护 |
| 场景集成 / 指定集成人 | 按需新增任务物、操作/临时放置/储物/离开区 Prefab | 最小修改 `Scenes/SCN_Kitchen_Review.unity`, `Prefabs/PREF_Kitchen_Benchmark.prefab` 及相关任务物 Prefab，逐项列清单 |
| 模型 / 模型负责人 | F2 修正版及参数/验证证据 | `Prefabs/PREF_Fridge_F2.prefab`, `Settings/ASM_KitchenParameters.asset` 等受影响资产 |

公共 `SessionRecord.cs`、`JourneyContracts.cs` 与状态枚举只由 Core 工作包维护；Feedback 与 Storage 先确认所需字段，再分别在独立文件完成校验与持久化。这样各包无须并行编辑同一数据文件。Core 不修改正式反馈 UI、正式字段校验或本地存储实现。场景集成人已有人选；个人姓名不影响 Core 接口，只影响共享 Scene/Prefab 的修改归属和 PR 审查。

**最小公共接口代码交付清单（Core 统一维护，先于各包实现合入）**：

1. 新增 `Assets/_Project/Scripts/Data/SessionOutcome.cs`：上文四值枚举，供 `SessionRecord` 和 Core 共用。
2. 修改 `Assets/_Project/Scripts/Data/SessionRecord.cs`：保留已有字段，新增 `BenchmarkFeedback.shortExplanation`、`SessionRecord.sessionOutcome` 与 `SessionRecord.conflicts`；每方案结果和原因复用已有 `taskOutcome`/`outcomeReason`，旧校验方法不作为正式提交入口。
3. 新增 `Assets/_Project/Scripts/Simulation/JourneyContracts.cs`：§3.1 的公共 DTO 和接口，由 Core 维护；编译时不依赖 Runtime/UnityEngine。
4. 修改 `Assets/_Project/Scripts/Simulation/SessionState.cs`：仅增加 `Exited`，保留既有状态名称；终态规则见 §4。公共接口编译检查与一项跨程序集契约测试通过后，其他包即可引用。

## 11. 可独立开发的工作包和依赖

1. **Core Skeleton**：启动条件是本契约 Frozen；先交付上述公共接口代码，再复用现有状态机/追踪器实现纯流程和运行时场景适配。以 Mock 测试后用真实交互验证 B1/B2；不重写门体、抓取或移动。
2. **Feedback**：启动条件是公共接口代码可编译；实现 `IFeedbackPort`、正式表单和校验，可用构造的 `FeedbackRequest`/快照独立测试，无须等待 Core 完成。
3. **UI & Orientation**：启动条件是公共接口代码可编译；依 `IJourneyView`、快照、命令结果和 notice 开发，可用构造快照独立测试，后续替换临时 Debug UI。
4. **Conflict Detection**：启动条件是公共 `ConflictNotice`/`IConflictSource` 可编译且模型登记参数可读；独立做客观几何测试，不依赖 Feedback/Storage 实现。
5. **Storage**：启动条件是公共 `ISessionStore`、`SessionOutcome` 和 `SessionRecord` 字段可编译；用固定记录独立做写入、读回及失败/恢复测试，无须等待 Core 完成。
6. **场景集成与模型**：场景接入等 Core 与对应包的独立测试通过后由指定集成人在功能分支进行；模型模块可独立修 F2，再复核任务阈值。完整 P0 仍需正式 UI/Feedback/Conflict/Storage、模型验证、设备试跑和 EVAL-01。
