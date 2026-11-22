# Review V2 M0 集成基线

**当前M2增量（2026-11-14）：** 唯一V2主场景已接完整A/B世界端口，运行时初始A；原M1世界原位保留停用，并另存M1Regression场景作历史测试。Host新增可选schemePort/initialSchemeId，ReviewSchemePort在-200绑定当前世界，Host在-100创建Core，UI/门控随后初始化。正式T2/T3仍按Docs/M2/Integration_Manifest.md接入；下文M0 NotSupported/Baseline/旧烘焙说明仅适用于其历史基线，不能覆盖当前M2实际状态。最新测试范围与失败见Docs/M2/T1_Development_Progress.md。

本实现根据已确认的 M0 任务和实施计划隔离接入 Cooking。它是可执行接口，不宣称两份 v0.1 Draft 已获得团队冻结批准。旧 Phase 2 契约仍只适用于旧 Review。

当前集成基线为 `origin/test@e319aaf`（HDRP 14.0.12），开发安排对齐 `XR_Group8_Five_Task_Briefs_and_Unified_Narrative_v0.4.docx`。M0 接口与后续分工见 `ReviewV2_Contract_v0.4_M0.md`；原 Built-in 基线的验证仅作历史证据。

Phase 3 正式产品集成与验收统一使用 `SCN_Kitchen_ReviewV2`。Cooking 用于功能回归，旧 Review 用于 Phase 2 历史参考。场景、Prefab、烘焙数据的依赖和 T1–T5 文件所有权见 `ReviewV2_Scene_Ownership.md`。

## 打开与体验

Unity **2022.3.62f3**，StandaloneWindows64。菜单 `XR Apartment / Review V2 / Open Phase 3 Review` 打开 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`，并仅设置当前编辑器会话的 Play 场景覆盖。默认构建入口仍为 Cooking，V2 没有替换 Build Settings。

Play 后使用 XR 射线操作临时面板，或用 F8 / 左手 Y / Menu 打开面板。依次 Confirm setup、Start experience 后即可探索，任意时刻 Review feedback，再 Continue exploring 或 Finish。零目标、零反馈均允许结束。Recenter 调用当前 XR 子系统，设备不支持时明确返回失败；它不是强制传送，也不替代头显系统的地面/房间校准。

M0 的 Finish 表示体验结束，**不表示数据已保存**。面板显示“存储未接入，数据未保存”。Safe exit 可从任意非终态立即结束体验，不强制退出编辑器。Setup 阻止物理交互；Onboarding 可练习。Pause 冻结模拟时间并取消物体抓取、阻止新的物理交互；Resume 恢复原时间倍率。UI 射线保持可用。Standing view / Seated view 复用新版站坐姿校准，不更换 Visit，也不重置世界。Review 本身不冻结或重置 Cooking，进行中的加热等仍继续。

台面 Reset 与菜单 Reset world 都要求确认，并复用原整屋重置。V2 中 RECIPE 按钮隐藏并解除绑定，避免切换菜谱触发无提示重置；原 Cooking 场景不变。Layout Lab 被停用，旧 Start Design Review 不可达。

## 所有权与接口

| 层 | 文件/组件 | 职责 |
|---|---|---|
| Data/ReviewV2 | ReviewContracts | 不可变快照、命令/结果、交互事件、World/Scheme/Save 窄接口 |
| Simulation/ReviewV2 | SessionController、GoalTracker | 纯 C# 会话与独立目标；不引用 UnityEngine |
| Simulation/ReviewV2 | MockReviewProvider | 无场景依赖的替身，同一接口，所有数据标记 IsMock |
| Runtime/ReviewV2 | ReviewSessionHost | 唯一 Session Controller 实例，真实 Cooking/重置/XR 适配 |
| Runtime/ReviewV2 | ReviewSceneIdentity、CookingGrabBridge | 固定标识解析，真实 XRI 首次抓取/最终释放 |
| Runtime/ReviewV2 | ReviewWorldInteractionGate | 按会话快照过滤场景物体交互；不拦截 UI 射线 |
| Runtime/ReviewV2 | ReviewIntegrationPanel | 可替换的 M0 操作面板，唯一菜单输入所有者 |
| Runtime/ReviewV2 | ReviewConsumerExample | 可编译 T2/T3 消费者，可选真实 Host 或独立 Mock |
| Editor | ReviewV2Authoring | 从 Cooking 复制和配置隔离场景、场景检查、显式 Windows 构建 |

快照提供 SessionId、Phase、SchemeId、SchemeVisitId、ModelVersion、SchemeHistory、Goals、CanReview、IsPaused、IsBusy、IsMock、SaveStatus。GoalSnapshot 包含 Id、State、Available、EvidenceId、可空 UpdatedAtMilliseconds。快照与集合均防御性复制，旧快照不会随新状态变化。

G01–G05 为稳定 ID，无 ActiveGoal。M1 V2 原型在完整配置 ReviewGroceryActivity 时开放 G01，其他旧/缺少配置的环境仍只开放 G02；G03–G05 Available=false。G02 InProgress 来自真实番茄 CutCount 首次增加或新出现的 CrackEgg 足量入碗标志；SliceTomato 仅作整颗完成切分后的后备证据。开冰箱、取材、拿工具、开炉及普通抓取不推断开始。G02 Completed 仍来自真实 TomatoEgg Ready 边沿。可用性、同对象购物/料理策略及回归状态见 `M1/T1_Development_Progress.md`。

### T2：快照、订阅和命令

以下代码可放入消费者组件中；OnEnable/OnDisable 成对管理订阅。完整可编译范例见 `ReviewConsumerExample.cs`。

```csharp
using XRApartment.Data.ReviewV2;
using XRApartment.Runtime.ReviewV2;

// host 由 Inspector 绑定本场景唯一 ReviewSessionHost，不使用跨场景全局单例。
ISessionReadModel model = host;
IReviewSessionCommands commands = host;
SessionSnapshot initial = model.Snapshot;
model.Changed += Render;
CommandResult result = commands.Execute(CommandType.Review);
// 禁用/销毁消费者时：model.Changed -= Render;
```

支持 CompleteSetup、CompleteOnboarding、Review、Continue、Finish、Pause、Resume、Recenter、CalibrateStanding、CalibrateSeated、ChangeScheme、ResetWorld、SafeExit。UI 可以按快照禁用按钮，但命令仍再次校验。接受异步请求只表示开始，最终结果由 Host.OperationCompleted 和 Changed 提供；Save/Reset/Scheme 操作在忙碌时拒绝重复请求，SafeExit 始终可用。

T2 独立联调时使用 `new MockReviewProvider()` 替代 Host：执行 Setup/Onboarding 后 `SetGoal("G01", GoalState.InProgress)`、`SetGoal("G02", GoalState.InProgress)`，可用 `FailNextOperation` 测试失败、`EmitGrab()` 测试事实消费者。Mock 不创建物理世界、不保存反馈。

### T3：标识与事实上下文

```csharp
IInteractionEventSource source = host;
source.Observed += OnFact;
var context = host.Snapshot;
var anchorTransform = host.Resolve("COOKING_STATION");
// OnFact 接收不可变 InteractionEvent。禁用/销毁时 source.Observed -= OnFact。
```

真实方案是 COOKING_BASELINE，不是最终 A/B。M0 模型版本为 cooking-m0-hdrp-e319aaf；启用同对象购物原型的 V2 为 cooking-m1-groceries-v1。SessionId 启动时生成，成功显式重置更新 Visit，普通回顾/暂停/返回不改变。T3 捕获时保存完整身份，不能用后来的当前快照覆盖历史。

对象目录见 `ReviewV2_M0_Identities.tsv`。OBJECT_nnn 是新场景中序列化的固定 ID；运行时不从名称、路径或 GetInstanceID 派生。首次作者生成时分配并保存；显式 RebaseOnCooking 作者入口读取旧场景的序列化对象与 Prefab 实例标识来保留 ID，新对象在旧最大编号后追加；同名对象不靠层级路径区分。若重建源对象改变了序列化身份，须人工核对映射，不将其自动视为同一对象。运行时不执行此编辑器匹配。目录中的层级路径仅用于作者定位。

M0 明确登记 COOKING_STATION、FRIDGE 和已接抓取对象。M1 增量登记 GROCERY_BAG、ZONE_FRIDGE_STORAGE；IngredientStored 带具体食材 ObjectId 和储物 ZoneId/AnchorId，其他原事件仍可为空。其他语义锚点（DINING_TABLE、SINK、MICROWAVE）尚未登记，Resolve 返回 null 不等于物理模型不存在。T3 不应猜测坐标，位置转换使用 Runtime 返回的 Transform。最新目录见 ReviewV2_M1_Identities.tsv，交接见 M1/T1_T3_Context_Handoff.md。

事实事件带 EventId、单调 ElapsedMilliseconds、SessionId、SchemeId、SchemeVisitId、ModelVersion、Kind、ObjectId、可空 ZoneId/AnchorId、IsMock。事件目前在 Experience 和 Review 期间产生，暂停/重置/终态隔离；程序取消抓取不报告用户释放。例子（示意 JSON，不代表写盘产物）：

```json
{
  "schemaVersion": "review-v2-m0-1",
  "eventId": "example-event-only",
  "sessionId": "example-session-only",
  "schemeId": "COOKING_BASELINE",
  "schemeVisitId": "example-visit-only",
  "modelVersion": "cooking-m0-hdrp-e319aaf",
  "elapsedMilliseconds": 18000,
  "kind": "ObjectGrabbed",
  "objectId": "OBJECT_001",
  "zoneId": null,
  "anchorId": null,
  "isMock": false
}
```

同一抓取从第一只手选中到最后一只手释放只产生一对事件。Bridge 在禁用时只移除自己的监听。DishCooked 是结果事实，不包含舒适性、空间质量等主观判断。

M1-01 追加 `InteractionKind.TomatoCut` 与 `InteractionKind.EggReceivedInBowl`，分别表示上述两种已验证的配方加工边沿。`ObjectId=COOKING_STATION` 是配方事实来源，不代表具体食材的稳定 ID；ZoneId/AnchorId 仍为空。事件沿用 EventId、SessionId、SchemeId、SchemeVisitId、ModelVersion、ElapsedMilliseconds 和 IsMock 字段。G02 刚进入 InProgress 时，`GoalSnapshot.EvidenceId` 等于该事件的 EventId。首次开始后其他加工动作不会重复发布开始事件；Review 往返保留证据，显式 WorldReset 成功后新 Visit 可重新产生开始事实。既有 `DishCooked` 事件与消费者订阅方式不变。T3 应按事件当时的 Visit 归属保存，不以当前快照重写历史。

## 方案、重置与保存边界

共享基础设施：XR Rig、输入、EventSystem、V2 Host/面板、会话身份与未来反馈存储。未来可替换方案资产：冰箱、厨房柜体/布局、方案内道具与区域/锚点映射。M0 未移动或重组原美术/烘焙层级；保留引用优先，最终方案适配由后续里程碑完成。

`ISchemePort.Supports/Change` 为整厨替换边界，生产 Host 暂不配置，ChangeScheme 返回 NotSupported 且身份不变。计划中 `SchemeIds.IntegratedKitchen = SCHEME_A`、`SchemeIds.ExternalRefrigerator = SCHEME_B` 只登记身份，不代表模型已实现。SchemeHistory 记录初始方案与成功切换，失败和世界重置不追加。Mock 支持 MOCK_A/B，成功时生成新 Visit、初始化目标，失败保持旧状态。未来适配必须保证失败保留旧世界，成功回执前完成世界加载，并与 T3 协调未保存标记；不能直接把旧 Layout Lab 或 F2 benchmark 接成此接口。真实 A→B→A 和历史反馈保存尚未验证。

World reset 在确认后调用 KitchenRoomReset，等待 station 和 roomReset 两者 IsResetting=false 才发布成功、新 Visit 与清空后的目标。期间的程序释放被隔离。T3 应按 IsBusy 关闭旧 Visit 的上下文窗口，新快照后建立新窗口；本模块没有权限删除历史反馈。

ISessionSavePort.Save 为 T3 存储适配点。Host 新增可选的 sessionSavePort MonoBehaviour 引用，由 T1 接线；组件必须实现该接口，否则 Finish 得到失败。回调携带原 RequestId，true 仅允许真实写盘并读回验证。正式端口接入后，失败留在 Review 可重试，成功才 Finished/Verified，旧/重复/退出后回执无效。当前场景仍未连接正式存储，保持 NotConfigured；测试端口仅证明接线，不证明写盘。Mock 不生成 Verified。

与 v0.1 草案的明确差异：按用户确认，M0 未配置存储仍可 Finished；可用性与三态 GoalState 分开；scheme_epoch 用唯一 SchemeVisitId 表达；初始 Cooking 基线不命名 SCHEME_A。暂不提供最终 Marker/仓库 schema，不把演示事实缓存称为反馈存储。

## 迁移分类

| 分类 | 旧组件 | M0 处理 |
|---|---|---|
| Reused | XR Rig、EventSystem、KitchenLocomotionSettings、Teleport、抓取、门/抽屉、水系统、KitchenRoomReset | 保留原实现与共享资产 |
| Adapted | KitchenCookingStation、KitchenRecipeController 的场景接线 | V2 实例绑定：重置转 V2 确认、菜谱切换停用；内部机制不改。RecipeController 仅修改一处提示文本：无 recipeButton 时不再提示 RECIPE to switch；旧场景有绑定，文本保持原样 |
| Reference only | LocomotionSettingsView、RecoveryMenuView、JourneyWorldSpaceView | 参考输入/移动/恢复职责；V2 不启动旧菜单或旧旅程 |
| Reference only | KitchenLayoutLab | 新场景停用控制；几何保留，不作为整厨 A/B 架构 |
| Retained for historical compatibility | JourneyContracts、JourneyCoordinator、JourneyRuntimeHost、旧 Feedback/Storage 与原 Cooking/Review 场景 | 全部保留，不改旧阶段、GUID 或接口 |
| Reference only | KitchenActivityGuide、KitchenPauseMenu、KitchenFeedbackNotes | 新 test 已存在，V2 通过场景实例关闭其旧菜单父对象；原 Cooking 保留。旧基线 bac8771 未找到 Guide/Pause 的记录仅为历史差异。未创建伪兼容组件 |

## 验证与后续

新增 EditMode 测试覆盖零目标结束、状态与目标隔离、并行目标、非法命令、只读快照、Mock 标记、切换失败、重置与旧访问证据、保存失败/重试/迟到回执。PlayMode 测试实际加载新场景并经 XRInteractionManager 驱动抓取和释放，检查去重、启停订阅、程序重置隔离及回顾保持世界。

实际执行结果见 `ReviewV2_M0_Validation.md`。日志、XML、构建输出放在忽略的 Logs/ReviewV2 与 Builds/ReviewV2，不提交。

M1-01 更新（2026-11-14）：G02 进行中检测已通过本次 Unity 编译、V2 EditMode 16/16 与 PlayMode 7/7（含真实料理纵切），过程失败和复测见 `M1/T1_Development_Progress.md`。这是 COOKING_BASELINE 的编辑器技术验证，不是完整 M1 或设备验收。

未完成：T2 正式 UI/教学、T3 Marker 和真实持久化、G03/G04/G05 检测、完整语义锚点、最终 A/B 与设备验收。G01 已有原型实现，但完整叙事和最新真实测试状态须查 M1/T1_Development_Progress.md，不能沿用上面的历史 G02 结果。后续依赖计划见 ReviewV2_M1_Narrative_Plan.md。
