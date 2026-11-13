# Review V2 M0 集成基线

本实现根据已确认的 M0 任务和实施计划隔离接入 Cooking。它是可执行接口，不宣称两份 v0.1 Draft 已获得团队冻结批准。旧 Phase 2 契约仍只适用于旧 Review。

## 打开与体验

Unity **2022.3.62f3**，StandaloneWindows64。菜单 `XR Apartment / Review V2 / Open M0 Scene` 打开 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`，并仅设置当前编辑器会话的 Play 场景覆盖。默认构建入口仍为 Cooking，V2 没有替换 Build Settings。

Play 后使用 XR 射线操作临时面板，或用 F8 / 左手 Y / Menu 打开面板。依次 Confirm setup、Start experience 后即可探索，任意时刻 Review feedback，再 Continue exploring 或 Finish。零目标、零反馈均允许结束。Recenter 调用当前 XR 子系统，设备不支持时明确返回失败；它不是强制传送，也不替代头显系统的地面/房间校准。

M0 的 Finish 表示体验结束，**不表示数据已保存**。面板显示“存储未接入，数据未保存”。Safe exit 可从任意非终态立即结束体验，不强制退出编辑器。Pause 冻结模拟时间；Resume 恢复原时间倍率。Review 本身不冻结或重置 Cooking，进行中的加热等仍继续。

台面 Reset 与菜单 Reset world 都要求确认，并复用原整屋重置。V2 中 RECIPE 按钮隐藏并解除绑定，避免切换菜谱触发无提示重置；原 Cooking 场景不变。Layout Lab 被停用，旧 Start Design Review 不可达。

## 所有权与接口

| 层 | 文件/组件 | 职责 |
|---|---|---|
| Data/ReviewV2 | ReviewContracts | 不可变快照、命令/结果、交互事件、World/Scheme/Save 窄接口 |
| Simulation/ReviewV2 | SessionController、GoalTracker | 纯 C# 会话与独立目标；不引用 UnityEngine |
| Simulation/ReviewV2 | MockReviewProvider | 无场景依赖的替身，同一接口，所有数据标记 IsMock |
| Runtime/ReviewV2 | ReviewSessionHost | 唯一 Session Controller 实例，真实 Cooking/重置/XR 适配 |
| Runtime/ReviewV2 | ReviewSceneIdentity、CookingGrabBridge | 固定标识解析，真实 XRI 首次抓取/最终释放 |
| Runtime/ReviewV2 | ReviewIntegrationPanel | 可替换的 M0 操作面板，唯一菜单输入所有者 |
| Runtime/ReviewV2 | ReviewConsumerExample | 可编译 T2/T3 消费者，可选真实 Host 或独立 Mock |
| Editor | ReviewV2Authoring | 从 Cooking 复制和配置隔离场景、场景检查、显式 Windows 构建 |

快照提供 SessionId、Phase、SchemeId、SchemeVisitId、ModelVersion、Goals、CanReview、IsPaused、IsBusy、IsMock、SaveStatus。GoalSnapshot 包含 Id、State、Available、EvidenceId。快照与集合均防御性复制，旧快照不会随新状态变化。

G01–G05 为稳定 ID，无 ActiveGoal。生产环境只有 G02 的结果检测接入；其他目标 Available=false，T2 应标为未接入而不是可完成。G02 完成来自 KitchenRecipeController 的真实 TomatoEgg Ready 状态；G02 的 InProgress 检测暂未接入。单次抓取不推断活动。Mock 可同时设置多个 InProgress，不能用来声称生产检测已完成。

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

支持 CompleteSetup、CompleteOnboarding、Review、Continue、Finish、Pause、Resume、Recenter、ChangeScheme、ResetWorld、SafeExit。UI 可以按快照禁用按钮，但命令仍再次校验。接受异步请求只表示开始，最终结果由 Host.OperationCompleted 和 Changed 提供；Save/Reset/Scheme 操作在忙碌时拒绝重复请求，SafeExit 始终可用。

T2 独立联调时使用 `new MockReviewProvider()` 替代 Host：执行 Setup/Onboarding 后 `SetGoal("G01", GoalState.InProgress)`、`SetGoal("G02", GoalState.InProgress)`，可用 `FailNextOperation` 测试失败、`EmitGrab()` 测试事实消费者。Mock 不创建物理世界、不保存反馈。

### T3：标识与事实上下文

```csharp
IInteractionEventSource source = host;
source.Observed += OnFact;
var context = host.Snapshot;
var anchorTransform = host.Resolve("COOKING_STATION");
// OnFact 接收不可变 InteractionEvent。禁用/销毁时 source.Observed -= OnFact。
```

初始真实方案是 `COOKING_BASELINE`，模型版本 `cooking-m0-1`，不是最终 A/B。SessionId 在启动时生成；SchemeVisitId 每次成功显式重置后更新，普通回顾、暂停和返回不改变。T3 捕获标记时应保存当时完整身份，不在之后读取当前 Host 身份来覆盖历史记录。

对象目录见 `ReviewV2_M0_Identities.tsv`。OBJECT_nnn 是新场景中序列化的固定 ID；运行时不从名称、路径或 GetInstanceID 派生。首次作者生成时分配并保存，后续新增对象须分配新 ID，不重编号已有对象。目录中的层级路径仅用于作者定位。

M0 明确登记 COOKING_STATION、FRIDGE 和所有已接抓取对象。Zone 事件未接入，ZoneId 为 null。其他语义锚点（包括 DINING_TABLE、SINK、MICROWAVE）尚未登记；Resolve 返回 null 不等于物理模型不存在，T3 不应猜测坐标。位置转换使用 Runtime 返回的 Transform，纯 C# 契约不含 Unity 对象。

事实事件带 EventId、单调 ElapsedMilliseconds、SessionId、SchemeId、SchemeVisitId、ModelVersion、Kind、ObjectId、可空 ZoneId/AnchorId、IsMock。事件目前在 Experience 和 Review 期间产生，暂停/重置/终态隔离；程序取消抓取不报告用户释放。例子（示意 JSON，不代表写盘产物）：

```json
{
  "schemaVersion": "review-v2-m0-1",
  "eventId": "example-event-only",
  "sessionId": "example-session-only",
  "schemeId": "COOKING_BASELINE",
  "schemeVisitId": "example-visit-only",
  "modelVersion": "cooking-m0-1",
  "elapsedMilliseconds": 18000,
  "kind": "ObjectGrabbed",
  "objectId": "OBJECT_001",
  "zoneId": null,
  "anchorId": null,
  "isMock": false
}
```

同一抓取从第一只手选中到最后一只手释放只产生一对事件。Bridge 在禁用时只移除自己的监听。DishCooked 是结果事实，不包含舒适性、空间质量等主观判断。

## 方案、重置与保存边界

共享基础设施：XR Rig、输入、EventSystem、V2 Host/面板、会话身份与未来反馈存储。未来可替换方案资产：冰箱、厨房柜体/布局、方案内道具与区域/锚点映射。M0 未移动或重组原美术/烘焙层级；保留引用优先，最终方案适配由后续里程碑完成。

`ISchemePort.Supports/Change` 为整厨替换边界，生产 Host 暂不配置，ChangeScheme 返回 NotSupported 且身份不变。Mock 支持 MOCK_A/B，成功时生成新 Visit、初始化目标，失败保持旧状态。未来适配必须保证失败保留旧世界，成功回执前完成世界加载，并与 T3 协调未保存标记；不能直接把旧 Layout Lab 或 F2 benchmark 接成此接口。真实 A→B→A 和历史反馈保存尚未验证。

World reset 在确认后调用 KitchenRoomReset，等待 station 和 roomReset 两者 IsResetting=false 才发布成功、新 Visit 与清空后的目标。期间的程序释放被隔离。T3 应按 IsBusy 关闭旧 Visit 的上下文窗口，新快照后建立新窗口；本模块没有权限删除历史反馈。

`ISessionSavePort.Save` 是后续存储适配点。回调必须包含原 RequestId，true 仅允许真实写盘并读回验证。正式端口接入后，Finish 在 Review 等待回执，失败留在 Review、允许重试，成功才 Finished/Verified。旧或重复回执无效，SafeExit 使待处理回执失效。M0 Host 未连接存储，结束保持 NotConfigured。Mock Controller 即使收到 true 也不会生成 Verified。

与 v0.1 草案的明确差异：按用户确认，M0 未配置存储仍可 Finished；可用性与三态 GoalState 分开；scheme_epoch 用唯一 SchemeVisitId 表达；初始 Cooking 基线不命名 SCHEME_A。暂不提供最终 Marker/仓库 schema，不把演示事实缓存称为反馈存储。

## 迁移分类

| 分类 | 旧组件 | M0 处理 |
|---|---|---|
| Reused | XR Rig、EventSystem、KitchenLocomotionSettings、Teleport、抓取、门/抽屉、水系统、KitchenRoomReset | 保留原实现与共享资产 |
| Adapted | KitchenCookingStation、KitchenRecipeController 的场景接线 | V2 实例绑定：重置转 V2 确认、菜谱切换停用；内部机制不改。RecipeController 仅修改一处提示文本：无 recipeButton 时不再提示 RECIPE to switch；旧场景有绑定，文本保持原样 |
| Reference only | LocomotionSettingsView、RecoveryMenuView、JourneyWorldSpaceView | 参考输入/移动/恢复职责；V2 不启动旧菜单或旧旅程 |
| Reference only | KitchenLayoutLab | 新场景停用控制；几何保留，不作为整厨 A/B 架构 |
| Retained for historical compatibility | JourneyContracts、JourneyCoordinator、JourneyRuntimeHost、旧 Feedback/Storage 与原 Cooking/Review 场景 | 全部保留，不改旧阶段、GUID 或接口 |
| 当前检出不存在 | KitchenActivityGuide、KitchenPauseMenu | 草案引用与本地 test 基线不一致；未创建假适配器或 IsFreePlay 绕行 |

## 验证与后续

新增 EditMode 测试覆盖零目标结束、状态与目标隔离、并行目标、非法命令、只读快照、Mock 标记、切换失败、重置与旧访问证据、保存失败/重试/迟到回执。PlayMode 测试实际加载新场景并经 XRInteractionManager 驱动抓取和释放，检查去重、启停订阅、程序重置隔离及回顾保持世界。

实际执行结果见 `ReviewV2_M0_Validation.md`。日志、XML、构建输出放在忽略的 Logs/ReviewV2 与 Builds/ReviewV2，不提交。

未完成：T2 正式 UI/教学、T3 Marker 和真实持久化、G01/G03/G04/G05 检测、G02 进行中检测、完整锚点/Zone 注册、整厨 A/B 模型/加载/存储协调、头显校准及 Quest Link 实测。五个建议不是五个已实现活动。
