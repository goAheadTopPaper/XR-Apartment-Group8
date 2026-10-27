# Core Play 验收与后续开发说明

**适用版本**：`feature/zh` 当前 Core + 临时 Debug UI + Mock Feedback/Storage 接线。公共规则以 [Module_Interface_Contract.md](Module_Interface_Contract.md) 的 Frozen v0.4 为准；场景对象与接线见 [Core_Scene_Wiring.md](Core_Scene_Wiring.md)。本页帮助人在 Play 中走通流程，也说明正式模块如何替换临时件；不代表正式 P0、Quest Link 或模型验收。

## 使用者：从 Play 到完成

1. 用 Unity **2022.3.62f3** 打开项目，打开 `Assets/_Project/Scenes/SCN_Kitchen_Review.unity`，进入 **Play**。Game 视图左上角的 `CORE PLAY TEST` 面板是临时控制台；左上角 `Hide menu` 可收起总菜单，收起后右上角持续显示当前任务、步骤和动作提示，左上角 `Open menu` 可重新展开。Red 安全提示仍显示，设计提示给出数量并可展开查看详情。需要鼠标操作时把焦点放在 Game 视图。无头显可用 XR Interaction Simulator，键鼠表见 [README §7](../README.md#7-运行与调试quest-link)；正式空间/舒适度仍需 Quest 3 + Quest Link。
2. Setup 与 Explore 时，番茄和洋葱应在后台面备餐盘上，而不是冰箱中；可自由观察或抓取。点击 `Complete setup` → `Acknowledge orientation`。这两个按钮只确认测试前提，不替代真实校准。进入 `Explore` 后可用 `Show B1/B2` 预览，预览会把这两件物品恢复到盘子；点击 `Start B1` 才会把它们放进上方 Freezer 并开始 Challenge。重置/重玩任务仍从 Freezer 开始，提交反馈并成功保存回到 Explore 后它们回到盘子。`B1=Proud+F2`，`B2=Flush-oriented+F2`，二者使用同一条八步动作链。当前接线按 B1、B2 顺序完成。
3. 看面板的 `Step` 和动作提示。`Items: x/3` 是当前步骤已满足的物品数，不是总旅程进度；移动、开门、释放物品产生实际场景证据，按钮不会伪造动作。区域采用薄的半透明表面高亮：**绿色**接近、**蓝色**离开、**金色**冰箱内部、**紫色**台面临放。小绿色浮空倒三角是当前动作指引：未抓物品时指向待操作物品，抓住后转向有效放置区；Fresh 三格出现三个指引，任选一格。上方 Freezer 和下方 Fresh 是独立门，两扇都要打开和关闭。若冰箱内部仍全黑，请退出 Play 并重新打开场景，让 Unity 重新载入 F2 Prefab；不要在尚未重载的旧场景上保存覆盖。

   颜色块只在 Challenge 中按步骤显示：Approach=绿色，TakeOut=Freezer 金色，TemporaryPlacement=紫色，Reorganise=紫色（抓住目标物后切为 Fresh 三格金色），PutBack=Fresh 三格金色持续显示直到三件物品均被计数，Leave=蓝色。Open/Close 仅用浮空三角指向门把手。Setup、Orientation、Explore、Pause、Feedback 和结束状态均不显示颜色块；隐藏的是视觉提示，区域判定始终有效。**松手本身不代表成功**；以 `Items` 计数增加、箭头切向下一件物品为准。

   | 步骤 | 必须实际完成的动作 |
   | --- | --- |
   | 1 Approach | 把头显位置移入绿色区。 |
   | 2 Open | 将上方 Freezer 与下方 Fresh 两扇门都打开到位，倒三角依次指向未完成的门把手。此步抓取任务物会被拒绝并提示；两门完成后才可取物。 |
   | 3 TakeOut | 从上方 Freezer 取出番茄、洋葱两件原有物品。麦片盒此时不参与。 |
   | 4 TemporaryPlacement | 把番茄、洋葱均释放在紫色区。抓着经过不算。 |
   | 5 Reorganise | 把**番茄或洋葱**从紫色区移入下方 Fresh **任意一格**并松手，稍待位置稳定，再点 `Confirm physical reorganisation`。麦片盒是第 6 步物品，不计入第 5 步；只点按钮不算。判定区与可见三格层板对齐，侧边也应有效。 |
   | 6 PutBack | 将番茄、洋葱、麦片盒三件全部释放到下方 Fresh；三格均有效，抓着伸进去或放在门外不算。 |
   | 7 Close | 将 Fresh 和 Freezer 两扇门都关闭到位。 |
   | 8 Leave | 先离开绿色区，再把头显移入蓝色区。 |

4. 进入 `FeedbackPending` 后，为**完成的**任务选择 Decision、Category、Importance 并填写简短解释，点 `Validate and submit feedback`。等待 `Save: Verified`，状态回到 `Explore`，再点 `Start B2` 并重复八步与反馈。两方案都保存后进入 `ComparisonSummary`：填写取舍理由，点 `Prefer B1` 或 `Prefer B2`，等最终 `Save: Verified` 与 `Finished`。当前存储为内存 Mock，读回验证不等于落盘；退出 Play 后数据不保留。

## 回退、跳过、暂停和安全退出

- **当前步骤恢复**：Challenge 中先松开抓着的任务物，再点 `Restore current step to default`。这会把门与三件任务物摆回**当前步骤可重新操作的位置**，清除本步骤已累计的部分证据；已完成步骤、当前方案与 `RunId` 保留，XR Rig 不移动。例如 TakeOut 显示 1/2 但另一个物品遗失时，点恢复后两件回到上方 Freezer，计数回到 0/2，可重新取出。Approach/Open 从关门、源物在 Freezer 开始；TakeOut/TemporaryPlacement 从开门、源物在 Freezer 开始；Reorganise/PutBack 从两件在紫色临放区、麦片盒在原位开始；Close 从三件在 Fresh、门开开始；Leave 从三件在 Fresh、门关开始。恢复动作不自动产生完成证据；失败时步骤进度不清空，可按错误提示重试。
- **没有任意“上一步”**：物体位置和八步证据不可简单倒放。`Reset current task to step 1` 重新开始**当前方案**；这不撤销已保存的其他方案。`Show` 只预览，不是开始/完成任务。若步骤没有前进，先核对面板提示中的对象、区域、释放或门开度。
- **Skip**：在 `Skip reason` 输入原因（原先默认 `Comfort stop` 只是示例理由），点 `Skip challenge (then submit feedback)`。本方案任务结果变为 Skipped，随后仍须在 `FeedbackPending` 点提交并等保存，才能到下一个方案。已跳过的方案不能作为最终偏好；若两方案都跳过，使用 `Finish with no preference (all skipped)`。跳过不是安全退出。
- **Pause/Resume**：暂停后点击 `Resume` 回到原状态，不回退步骤。`Retry failed operation` 只针对确实失败的场景/保存操作；没有失败时不需要点。保存为 Pending 时等待回执或超时反馈。
- **Safe exit**：任何未终止阶段可点 `Safe exit (abort this session)`，会把当前会话标为 `Exited / Aborted`，这是**终态**，不能在同一会话 Resume，也不能继续 B1/B2。等最终保存不再 Pending 后，点 `Start NEW test session (reload scene)` 重新加载场景并生成新匿名 Session ID；这会从 Setup 开始，不恢复上一轮。若退出保存失败，面板会显示 Failed，旧会话不保证保存成功；新会话也不会恢复旧数据。`Finished` 同样可用这个按钮再测一轮。

## 后续开发者：模块边界与接入

| 所有者 | 本轮位置与交接点 | 后续正式实现 |
| --- | --- | --- |
| Core | `Simulation/JourneyContracts.cs`、`JourneyCoordinator.cs`、`ChallengeTaskTracker.cs`；`Runtime/Core/JourneyRuntimeHost.cs`、`RuntimeBenchmarkAdapter.cs`、`TaskEvidenceBridge.cs` | 维持唯一状态、B1/B2 切换、八步证据与场景/保存回执配对；不要让 UI/场景直接推进状态。 |
| Feedback | `Runtime/Debug/MockFeedbackAdapter.cs` 实现 `IFeedbackPort` | 独立实现正式表单及字段校验，向 Host 提交 `FeedbackDraft`；不要修改已冻结 DTO 而不先修订契约。 |
| UI / Orientation | `Runtime/Debug/JourneyDebugUI.cs` 实现 `IJourneyView` | 独立实现正式流程、分步提示、拒绝/错误和可访问交互；临时 OnGUI 不做产品 UI。 |
| Storage | `Runtime/Debug/MockStorageAdapter.cs` 实现 `ISessionStore` | 匿名本地持久化、读回验证、失败/恢复；当前 Mock 仅内存。 |
| Conflict / Safety | Host 的 `IConflictSource` / `ISafetySource` 端口预留 | 独立来源报告客观通知；Yellow 在相应反馈记入后成为 Amber，Red 独立提示，不阻断安全退出。 |

场景接入 Prefab 是 `Assets/_Project/Prefabs/Core/PREF_Core_PlayValidation.prefab`，共享场景只作最小挂载；两个运行时方案 Prefab 位于同目录。冰箱安装位保留原场景的 180° Y 朝向，新增方案也须使 Fresh 门朝向室内。独立功能分支开发正式模块，通过上述端口替换 Mock；共享 Scene/Prefab 变动逐项审查。不可改 `Assets/Samples/**`，也不可把编辑器 `XR Apartment → Benchmark → Apply …` 当作运行时 B1/B2 切换。模型现仍用 F2 双门代理；内部层板和金色区只供 Core Play 验证，正式物品/区域、模型形态、参数及 Quest Link 体验须由对应负责人确认。

收起/展开只改变临时 **屏幕 OnGUI 调试面板**的可见性，不修改 `JourneySnapshot`、任务证据或会话状态；目前用 Game 视图中的按钮操作，尚不是头显手柄可直接呼出的正式世界空间 UI。正式 UI 负责人应沿用 `IJourneyView` 快照与 Host 命令接入，单独设计 XR 可达性与视觉规范。

## 验证口径

运行 Unity EditMode、PlayMode 测试与 `StandaloneWindows64` 构建。`Assets/_Project/Tests/PlayMode/CorePlayWiringSmokeTests.cs` 通过真实场景物体、门体进度、区域和 B1/B2 切换验证两轮八步，并检查运行时冰箱朝向、菜单收起时任务摘要随状态变化与退出后的新会话入口；门外冰箱可见性测试需要图形设备，批处理运行 PlayMode 时不要加 `-nographics`。它不模拟人手柄抓取。测试/构建通过后，仍须按上文由人进行 Game 视图和 Quest Link 试跑，并单独确认正式 Feedback、UI、Conflict、Storage 与 F2 模型。
