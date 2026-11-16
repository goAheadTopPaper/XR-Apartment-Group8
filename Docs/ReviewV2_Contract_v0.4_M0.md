# Review V2：v0.4 叙事与 M0 开发边界

**Phase 3 M0 State Contract 的唯一接口依据。** 对 `SCN_Kitchen_ReviewV2`，本文件定义会话/目标状态和跨角色接入边界；具体可编译签名以 `Assets/_Project/Scripts/Data/ReviewV2/ReviewContracts.cs` 为准，消费示例与事实 payload 见 `ReviewV2_M0_Integration.md`。`Module_Interface_Contract.md` 是 Phase 2 Legacy，不参与 V2 接口判定。修改公共状态或签名前须同步更新本文件、实现和相关测试，并由 T1/T2/T3 审核。本文不把尚未实现的 Marker Schema 或整厨 A/B 适配宣称为已冻结。

依据用户提供的 `XR_Group8_Five_Task_Briefs_and_Unified_Narrative_v0.4.docx`，烹饪用于理解厨房空间；不要求完成菜谱才能回顾，也不要求选出 A/B 胜者。本文件描述当前实现与接入安排，不代表团队已完成接口冻结或后续里程碑验收。

Phase 3 唯一产品集成与验收主场景已确定为 `SCN_Kitchen_ReviewV2`。T1–T5 的文件所有权、Prefab 接入与场景更新流程见 `ReviewV2_Scene_Ownership.md`。

## 当前可接入契约

- 会话由 `SessionController` 独占：Setup → Onboarding → Experience ↔ Review → Finished；SafeExit 到 Exited，Pause 为叠加状态。零目标可回顾和结束。
- `ReviewSessionHost` 提供 `ISessionReadModel`、`IReviewSessionCommands`、`IInteractionEventSource` 和 `Resolve(id)`。消费者只订阅、发命令，不写 Controller 内部状态。
- 快照包含 SessionId、SchemeId、SchemeVisitId、ModelVersion、SchemeHistory、Goals、暂停/忙碌/保存状态及 IsMock。方案历史只在成功切换后追加；回顾、暂停、校准不创建 Visit。
- 五个目标独立维护三态，可同时 InProgress。Available 独立表达功能可用性；EvidenceId 和 UpdatedAtMilliseconds 记录实际证据及会话单调时间。无证据不推断进度。
- v0.5 叙事中的 `Unavailable` 在当前 M0 接口表示为 `Available=false`，不是 `GoalState` 第四个枚举值；T2 应显示“未接入”，T3 不把它当作失败或未完成证据。
- 当前唯一V2主场景初始为真实SCHEME_A，可配置仅B起始；COOKING_BASELINE保留M0/M1历史回归。M1-01的首刀/足量蛋液G02证据和同对象释放后稳定G01检测已接入两案，开门/取材/拿工具/开炉仍不算G02开始；G03–G05仍未接入。M1与M2实际状态和失败分别见对应Development Progress。
- `CommandResult.Accepted` 仅表示接受命令；异步操作通过 OperationCompleted 报告成功/失败。旧 Visit 证据和不匹配的保存回执拒绝。
- M0 按用户确认允许未保存结束，始终显示“存储未接入，数据未保存”。T3 正式接入 ISessionSavePort 后必须真实写盘并读回验证；失败留在 Review。Mock 不产生真实保存成功。

可编译消费者在 `Assets/_Project/Scripts/Runtime/ReviewV2/ReviewConsumerExample.cs`；订阅、退订和事件 payload 见 `ReviewV2_M0_Integration.md`。

## 方案身份

2026-11-14 M2兼容增量：新增可选 `ISchemeModelCatalog.ModelVersionFor(schemeId)`，原 `ISchemePort` 签名不变。Core在请求前捕捉目标版本，空版本在端口执行前拒绝；仅匹配的成功回执更新Scheme/Visit/Model并清Goal，失败保留原快照。旧端口无目录时保留原版本，不能据此宣称生产A/B版本正确。新增显式初始Scheme构造重载，旧调用仍使用Baseline/Mock默认值。当前Core/资产定向26/26与最终场景专项6/6分批通过；完整历史证据及联合验收边界见Docs/M2。

M2运行时新增 `ReviewSchemeWorld` 与 `ReviewSchemePort`：V2专属完整世界Prefab以独立运行时Scene隔离CookingRoomReset的扫描范围，已接唯一主场景。保存等待与目的地预检失败保留原世界及已有抓取；预检在无帧/物理推进的同步调用内完成，成功后才停用旧世界并绑定新访问。真实双向切换、失败/重试和世界恢复已独立验证，正式T2/T3联合及设备尚未验收。T2在执行ChangeScheme前负责确认说明；T3始终保存捕捉时身份。

| ID | v0.4 意图 | 当前状态 |
|---|---|---|
| COOKING_BASELINE | 可运行的现有 Cooking 厨房 | M0/M1真实基线，保留历史回归；未仅重命名成A |
| SCHEME_A | Integrated Kitchen：厨房右墙双门冰箱、整合柜体 | M2专属世界integrated-kitchen-m2-v5；微波炉仅为固定外观；唯一主场景初始方案，联合/设备验收待完成 |
| SCHEME_B | External Refrigerator：餐厅窗边大单门冰箱，厨房增加备餐面 | M2专属世界external-fridge-m2-v8；窗边收纳、层板与固定微波炉外观已接入，联合/设备验收待完成 |

`ISchemePort` 接收整厨切换请求并返回带请求身份的完成回执；成功才切换Visit和初始化目标。失败保留原世界与身份。M2主场景已注入完整世界端口；旧未注入端口的M0/M1仍返回NotSupported。旧Layout Lab不作为整厨实现。Mock明确标记IsMock。配置正式保存端口时先保存旧快照并验证回执，失败不调用加载；无端口的独立切换仍为NotConfigured，不能声称反馈已安全保存。

M1-01 只在 `InteractionKind` 末尾追加 `TomatoCut` 与 `EggReceivedInBowl`，不改变既有枚举值、快照字段、构造签名或订阅方式。前者表示当前西红柿炒蛋配方中的番茄实际发生一刀切分，由 `CutCount` 增加检测；后者表示达到配方最低量的蛋液进入搅拌碗。`SliceTomato` 配方标志仅在番茄完成全部切分后出现，作为切分证据的后备来源。`ObjectId=COOKING_STATION` 表示发布配方事实的工作台，并不声称能区分具体食材实例。Host 先以首次事实的 EventId 更新 G02 InProgress，再向消费者发布同一个不可变事实。后续加工不重复发布“开始”；显式 WorldReset 成功后，新 Visit 可以重新开始。G02 Ready 的 `DishCooked` 和 Session Phase 规则不变。

M1 G01 增量仅追加 `IngredientStored`：ObjectId 为具体食材，ZoneId/AnchorId 为登记储物区，身份与 EventId 沿用原结构。G01 仅在完整配置时 Available=true；按 Visit 累计不同对象，取出料理不回退目标。配置参数与验证见动态记录。原有 SessionController/GoalTracker 构造签名保留，新增可用性重载；公开接口签名不变。G01 原型模型版本为 `cooking-m1-groceries-v1`，方案仍为 COOKING_BASELINE，不能视为最终 A/B。

M1模型版本由Host场景配置独立传入Core，不由Goal可用性推导；同模型缺G01配置仍保持版本，旧构造签名保留。Review往返保留Visit与版本，成功Reset仅更新Visit。此段保留M1身份修复语义；M2进一步通过上文模型目录实现实际方案版本加载。

已获负责人批准：仅 V2 配方配置排除 OpenFridge 前置，不伪造动作事实；原 Cooking 默认配方条件不变。此项不是全面重构为多路径料理判定，结构性差异和双场景测试单列于动态记录。

## 分工与里程碑

| 负责人 | 接入/交付范围 | 当前依赖与下一步 |
|---|---|---|
| T1 | 会话、目标、方案、事实事件、稳定身份 | M1 G01/G02、M2完整世界与版本/锚点已独立验证并接主场景；后续按Manifest接正式T2/T3并做联合/设备验收 |
| T2 | UI01–05/08，设置、引导、任务建议、回顾入口 | 可替换临时面板；读取快照，处理拒绝原因及异步完成；Setup 禁止烹饪、Onboarding 允许练习 |
| T3 | UI06/07、Marker、前后事实、持久化 | Marker 创建时保存完整 Visit 上下文；即时保存反馈，后续异步补 after-context，不生成主观评价；不得用当前 Visit 覆盖历史 |
| T4 | 回归、集成、设备验收 | 分清真实场景、Mock、桌面模拟和 Quest Link 证据；保存失败、旧回执、零目标结束均需覆盖 |
| T5 | 用户研究与叙事验证 | 在真实功能可用后设计任务和评估；不把缺席功能或模拟事实当用户体验数据 |

M0：接口、替身和独立可运行场景，支撑 T2/T3 并行。

M1：单方案真实 G01/G02、Marker 持久化、暂停返回与零目标结束。现有 G02 完成检测只是其中一部分。

M2：真实 A/B 模型和整厨替换、历史反馈不丢失、Quest Link 设备联调。

M3：正式用户评估。后续阶段需要团队验收，不能由 M0 的测试通过推定完成。

## 与新版 test 的兼容

从 `test@e319aaf` 的 Cooking 场景通过 Editor 作者工具生成 V2，保留 HDRP、烘焙、家具、水体、抓取和 XR 引用。Guide/Pause/FeedbackNotes 已存在于新版 test；V2 关闭旧菜单父对象，保留原 Cooking 的行为与共享 Prefab。V2 独立门控 Setup/暂停时的物体交互，UI 射线保持可用；站坐姿校准复用新版移动组件。

默认启动场景及 Build Settings 不变，显式 `Open Phase 3 Review` 只覆盖本次编辑器会话的 Play 入口。原有未提交个人修改和两份 v0.1 草案不在此改动范围。
