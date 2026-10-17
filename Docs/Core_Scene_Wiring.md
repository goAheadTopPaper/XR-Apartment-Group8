# Core 场景接线清单

**状态**：Core Play 验证接线已在 `feature/zh` 完成；`SCN_Kitchen_Review` 引入独立的 `PREF_Core_PlayValidation`，B1/B2 使用各自的运行时 Prefab。此接线只供 Core + Mock 联调，不等于 P0 产品、模型或 Quest Link 验收。

**Play 操作、回退/跳过/安全退出及后续模块交接**：见 [Core_Review_Playbook.md](Core_Review_Playbook.md)。

## 当前 Play 验证接线

- `PREF_Core_PlayValidation` 挂载 `JourneyRuntimeHost`、`RuntimeBenchmarkAdapter`、`TaskEvidenceBridge`、Mock Feedback/Storage 和临时 `JourneyDebugUI`。原场景里的静态 B1 benchmark 实例已由运行时空槽位取代；`Show B1/B2` 会实例化对应 Prefab，切换时保持厨房、XR Rig 和 UI 不变。
- 运行时 `SLOT_Benchmark_Runtime` 保留原静态 benchmark 的 180° Y 朝向，Fresh 门面向室内；B1/B2 同用此槽位，朝向由 PlayMode 测试覆盖。
- 两个方案都读取 `REG_Benchmarks`，分别标为 `B1=Proud+F2` 和 `B2=FlushOriented+F2`。B2 围板启用，B1 围板关闭；两者暂用现有 F2 交互代理。任务访问对象是 Freezer 与 Fresh 两扇门的运动学枢轴 `ACCESS_FridgeFreezerDoor`、`ACCESS_FridgeFreshDoor`，打开/关闭逐扇按实际 `Progress` 记录。
- 三件共享、可抓取任务物：`ITEM_Tomato`、`ITEM_Onion` 是原有物，Setup/Orientation/Explore 时摆在 `SET_Preparation` 的台面盘子上；Start B1/B2 时重置到上方 Freezer 的 `ZONE_FreezerSource`，Reset/Replay 也回到 Freezer。反馈保存成功回到 Explore 后再恢复盘子摆放。`ITEM_CerealBox` 是新购物，初始在早餐区。两件原有物从 Freezer 取出并在台面紫色 `ZONE_Temporary` 临放；第 5 步只能用其中一件原有物确认重整；第 6 步三件才都参与放回。三个分格的触发 Collider 属于同一个逻辑 `ZONE_FreshDestination`，没有指定唯一正确层板；其有效宽度和深度与可见 Fresh 层板对齐，层板侧边可放置区也有效。上方 Freezer 和下方 Fresh 两扇门均须真实开到位、关到位。F2 Fresh 门后有可见内胆、背板、层板及点光源；旧 `OPENING_FridgeDoor_Fresh/Freezer` 黑色遮挡薄片已移除。绿色 `ZONE_Approach` 和蓝色 `ZONE_Leave` 分别用于接近、离开。彩色区域采用不碰撞的半透明表面高亮，证据仍取自独立触发 Collider。临时 `TaskTargetGuide` 以绿色浮空倒三角指向当前门把手、物品或目标区，不推进状态。
- `TaskTargetGuide` 还依据 `JourneySnapshot` 控制区域 Renderer：非 Challenge 状态全部隐藏；Challenge 中仅显示当前步骤需要的区域，Reorganise 在抓住目标物后突出 Fresh 目标，PutBack 持续显示 Fresh 目标。它不禁用 Collider，不改变 Core 判定；正式 UI 可替换此视觉呈现。
- PutBack 的 Fresh 金色目标区现在持续显示到 `Items: 3/3`；松手后目标区不再消失，避免把提示隐藏误认作完成。储物证据以任务物非 Trigger 碰撞体中心为位置基准（而非可能位于模型底部的根节点），仍须松手并稳定停留。三件物品逐件计数，计满才进入 Close。
- 进入 Play 后，Game 视图左上角应出现临时 Debug UI。依次 Complete setup、Acknowledge orientation、Show B1、Start B1。八步实操：进入绿色区域 → 打开 Freezer 与 Fresh 两扇门 → 从上方 Freezer 取出番茄和洋葱 → 两件都释放在台面紫色区 → 至少一件移入下方 Fresh 任意一格并点击 Confirm physical reorganisation → 将三件物品都释放在 Fresh 任意格 → 关两扇门 → 离开绿色区并进入蓝色区。提交反馈并确认 Mock 保存后，再对 B2 重复。Reset、Skip、Pause/Resume、Safe exit、Mock 保存失败与 Retry 可用同一面板检查。
- `Assets/_Project/Tests/PlayMode/CorePlayWiringSmokeTests.cs` 是无头显启动/方案切换烟测，并通过移动真实场景对象、门体进度和区域判定验证 B1/B2 八步；门外可见性测试还会以真实图形设备分别渲染 Freezer 和 Fresh 门洞，检查内部亮度。批处理运行该视觉测试时不要带 `-nographics`，否则它会明确跳过；它不直接向 Core 注入任务证据，但也不等于 XRI 手柄抓取实测。完整动作仍须在 Play 中由人用模拟器或 Quest Link 操作。

**验收边界**：现有 F2 仍是两扇门代理，与 PRD Final §6.2 的大型单门不符；冰箱内部储物和金色判定区现已一致，但其几何/层高仍是 Play 验证代理，尚未经过模型与登记参数负责人确认。临时反馈、内存存储及 OnGUI 不是正式模块；Conflict Detection / Safety 来源尚未接入，因此 Yellow→Amber/Red 不能靠本场景完成端到端验收。上述限制不妨碍验证 Core 的运行时切换、状态和真实交互证据，但不得宣称完整 P0 通过。

## 最小接入

1. 在独立功能分支制作 Core 接入 Prefab，挂 `JourneyRuntimeHost`、`RuntimeBenchmarkAdapter`、`TaskEvidenceBridge`。临时 Play 验证可挂 `MockFeedbackAdapter`、`MockStorageAdapter`、`JourneyDebugUI`；正式模块随后通过同一接口替换。只由场景集成人把该 Prefab 接到 `SCN_Kitchen_Review`。
2. `JourneyRuntimeHost` 指向 `REG_Benchmarks`、场景适配、反馈、存储和视图端口；填写实际 `modelVersion`、本次 B1/B2 顺序、3–5 件任务物、全部语义目标和 0–1 进度阈值。Session ID 留空时运行时生成匿名 ID。B1/B2 必须在注册表中允许 Challenge。
3. `RuntimeBenchmarkAdapter` 的 `benchmarkSlot` 是只包含可替换方案内容的父节点；B1、B2 各绑定一个可实例化的完整方案 Prefab。每个 Prefab 的 `BenchmarkSlotMarker` ID、柜体策略与冰箱 archetype 必须和 `REG_Benchmarks` 中对应定义一致，不能把 B1 Prefab 仅改绑定标签冒充 B2。切换不得销毁共同厨房、XR Rig 或 UI。配置 `TaskEvidenceBridge`、XR Origin/Head 和重定位锚点。
4. 共享任务物放在可替换槽位之外，使用现有可抓取 Prefab 的实例。每件任务物挂 `TaskEvidenceIdentity`，填写与 `TaskConfig.Items[].ItemId` 一致的稳定 ID，勾选 `taskItem`，且同一对象上有 `XRGrabInteractable`。场景内原有物品的作者位置是 Challenge Freezer 基线，运行时在 Setup/Orientation/Explore 放到 `SET_Preparation/PREF_Prop_Plate`；新购物品初始位于放回区之外。保留盘子层级名称，否则启动接线校验会报错。
5. 每个区域提供启用的 Collider 和 `TaskEvidenceIdentity`：接近/离开区为 `CirculationZone`，临放区为 `CounterSurface`，来源/放回区为 `StorageZone`。区域可位于公共场景或各方案 Prefab，但 B1/B2 使用相同稳定 ID 和等价任务含义。区域 Collider 用于实际空间判定，不能用按钮代替。
6. 每个 `TaskConfig.AccessTargets` 对应的门/抽屉枢轴挂 `TaskEvidenceIdentity`，稳定 ID 与角色分别匹配配置中的 `ObjectId`、`FridgeDoor`/`FridgeDrawer`，并在同一对象上保留 `KinematicSwingDoor`/`KinematicLinearDrawer`。证据读取实际 `Progress`；`IsOpen` 目标值不作为完成依据。

## 正式验收前仍须确定

- 与模型/产品负责人确认正式使用的 3–5 件实物和空间位置；当前番茄、洋葱、麦片盒及冰箱中层测试区只是 Core Play 验证配置。Take out 只涉及原有物品，Put back 涉及全部物品。
- 由模型/参数负责人确认 F2 形态、两方案几何和阈值的登记依据。当前旧 F2 代理与 PRD Final §6.2 不符，不能用它宣布模型验收。
- B1/B2 的 `controlledQuestion` 当前仍为空；正式比较前由对应负责人填写，不从现有代理外观推断结论。

Host 启动时校验注册表、方案 Prefab、门体与任务物/区域 ID；缺一项会给出错误并停止旅程。接线完成后应在 Play 模式用真实 XR 动作逐步验证 B1、B2，并观察重置、暂停、跳过、保存失败、旧回调和安全退出。EditMode 通过只证明纯流程与公共接口，不代替该 Play 验证或 Quest Link 验收。

`ReturnItemToTray` 仅在 PutBack 步骤可用；场景适配把指定物品恢复到其任务初始位置并清除该物品本步骤的已发证据，使后续再次实际放回仍可被记录。`RestoreCurrentStep` 是人工触发的当前步骤检查点恢复：先检查所有任务物未被抓取，再摆放门与任务物并重新布防传感器；仅成功回执清除本步骤部分证据，不改变方案、已完成步骤、`RunId` 或 XR Rig。Approach/Open 阶段任务物的 XRI 选择过滤器拒绝提前抓取，临时视图展示原因；Explore 不锁物。整体从头重试仍用 `ResetCurrentTask`。
