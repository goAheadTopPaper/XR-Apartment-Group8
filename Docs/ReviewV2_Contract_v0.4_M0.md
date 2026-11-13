# Review V2：v0.4 叙事与 M0 开发边界

依据用户提供的 `XR_Group8_Five_Task_Briefs_and_Unified_Narrative_v0.4.docx`，烹饪用于理解厨房空间；不要求完成菜谱才能回顾，也不要求选出 A/B 胜者。本文件描述当前实现与接入安排，不代表团队已完成接口冻结或后续里程碑验收。

Phase 3 唯一产品集成与验收主场景已确定为 `SCN_Kitchen_ReviewV2`。T1–T5 的文件所有权、Prefab 接入与场景更新流程见 `ReviewV2_Scene_Ownership.md`。

## 当前可接入契约

- 会话由 `SessionController` 独占：Setup → Onboarding → Experience ↔ Review → Finished；SafeExit 到 Exited，Pause 为叠加状态。零目标可回顾和结束。
- `ReviewSessionHost` 提供 `ISessionReadModel`、`IReviewSessionCommands`、`IInteractionEventSource` 和 `Resolve(id)`。消费者只订阅、发命令，不写 Controller 内部状态。
- 快照包含 SessionId、SchemeId、SchemeVisitId、ModelVersion、SchemeHistory、Goals、暂停/忙碌/保存状态及 IsMock。方案历史只在成功切换后追加；回顾、暂停、校准不创建 Visit。
- 五个目标独立维护三态，可同时 InProgress。Available 独立表达功能可用性；EvidenceId 和 UpdatedAtMilliseconds 记录实际证据及会话单调时间。无证据不推断进度。
- 当前真实环境为 COOKING_BASELINE。只有 G02 的真实成菜边沿接入完成检测；G01、G03–G05 和 G02 进行中检测未接入。
- `CommandResult.Accepted` 仅表示接受命令；异步操作通过 OperationCompleted 报告成功/失败。旧 Visit 证据和不匹配的保存回执拒绝。
- M0 按用户确认允许未保存结束，始终显示“存储未接入，数据未保存”。T3 正式接入 ISessionSavePort 后必须真实写盘并读回验证；失败留在 Review。Mock 不产生真实保存成功。

可编译消费者在 `Assets/_Project/Scripts/Runtime/ReviewV2/ReviewConsumerExample.cs`；订阅、退订和事件 payload 见 `ReviewV2_M0_Integration.md`。

## 方案身份

| ID | v0.4 意图 | 当前状态 |
|---|---|---|
| COOKING_BASELINE | 可运行的现有 Cooking 厨房 | M0 真实场景，不宣称最终 A/B |
| SCHEME_A | Integrated Kitchen：厨房右墙双门冰箱，原冰箱区改柜体/微波炉 | 仅契约常量，模型与替换未实现 |
| SCHEME_B | External Refrigerator：餐厅窗边大型单门冰箱，释放厨房工作区 | 仅契约常量，模型与替换未实现 |

`ISchemePort` 接收整厨切换请求并返回带请求身份的完成回执；成功才切换 Visit 和初始化目标。失败保留原世界与身份。生产端当前返回 NotSupported；旧 Layout Lab 的冰箱槽位不能作为整厨实现。Mock 使用同一契约模拟成功/失败，快照与事实明确标记 IsMock。

## 分工与里程碑

| 负责人 | 接入/交付范围 | 当前依赖与下一步 |
|---|---|---|
| T1 | 会话、目标、方案、事实事件、稳定身份 | M0 Host/Mock 已提供；M1 优先真实 G01/G02 检测、区域和语义锚点；M2 整厨加载与失败恢复 |
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
