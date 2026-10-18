# XR Apartment — Kitchen Design Review

COMP5424 Scenario C 的厨房设计评审原型。参与者在同一个厨房里完成标准化日常任务，体验并比较两个预设方案：

- **B1**：Proud + F2
- **B2**：Flush-oriented + F2

项目的运行时评审流程目前只配置 B1、B2。它不是通用公寓漫游器、自由装修工具、合规认证工具或云端服务；本地几何尺寸均属于原型假设，不代表客户事实或法规结论。

产品范围与验收以 [Lean PRD v0.3 Final](Docs/COMP5424_Scenario_C_Lean_PRD_CN_Final.docx) 为准。团队接口与实现限制见 [模块接口契约](Docs/Module_Interface_Contract.md)，AI 编码助手专用规则见 [AGENTS.md](AGENTS.md)。

> **第一次进入项目？先看 [厨房设计评审使用手册](Docs/User_Manual_CN.md)**：从启动、姿势校准和控制器练习，到 B1/B2 任务、反馈、暂停恢复与安全退出。

## 快速开始

### 环境

| 项目 | 配置 |
| --- | --- |
| Unity | **2022.3.62f3**，不要升级引擎 |
| 渲染 | Built-in Render Pipeline，Linear Color Space |
| 输入 | 仅新 Input System（`activeInputHandler: 1`） |
| XR | OpenXR + XR Interaction Toolkit 3.5.0 |
| 目标体验 | Windows PC 构建，通过 **Quest 3 + Quest Link** 使用 |
| 场景 | `Assets/_Project/Scenes/SCN_Kitchen_Review.unity` |
| 数据 | 写入运行电脑的本地 CSV，不需要账号、服务器或网络服务 |

### 克隆并打开

```bash
git lfs install
git clone https://github.com/goAheadTopPaper/XR-Apartment-Group8.git
cd XR-Apartment-Group8
git lfs pull
```

用 Unity Hub 打开仓库根目录，并选择 **2022.3.62f3**。UPM 首次解析完成后，在 Unity 菜单选择 **XR Apartment → Open Kitchen Review Scene**；也可直接打开上表所列场景。`ProjectSettings/EditorBuildSettings.asset` 中已启用该场景。

编辑器无头显测试可使用项目随附的 XR Interaction Simulator；进入 Play 后查看模拟器提示（`Y`）。模拟器适合检查交互逻辑，不可代替 Quest Link 的空间、舒适度或设备验收。

## 当前实现

### 评审旅程

场景中的 `JourneyRuntimeHost` 根据 Prefab 中配置的计划运行单一会话；B1、B2 使用相同的任务物品、语义区域和动作链，只切换 benchmark 槽位内的方案实例。任务证据由场景适配器收集，流程状态由纯 C# `JourneyCoordinator` 独占管理。

```text
Setup → Orientation → Explore
      → ChallengeActive → FeedbackPending（每个方案各自进行）
      → ComparisonSummary → 保存并验证 → Finished
```

挑战的固定八步为：

1. Approach（接近冰箱）
2. Open（打开指定冰箱门）
3. TakeOut（取出原有物品）
4. TemporaryPlacement（临时放置）
5. Reorganise（整理物品）
6. PutBack（放回任务物品）
7. Close（关闭指定冰箱门）
8. Leave（离开操作区）

当前会话计划配置 3 件物品（番茄、洋葱、麦片盒）。传感器与交互证据按稳定对象 ID、语义角色、方案及任务代次核验；状态机拒绝不符合当前步骤或目标的证据。探索预览不作为正式挑战证据。

挑战完成或跳过后，参与者可在世界空间反馈表单中提交当前方案反馈。完成任务时必填 **Decision、Main category、Importance**；体验标签与 Everyday impact 为可选。跳过时使用已记录的跳过原因。表单不收集自由文本说明。两个方案均已解决后，若至少一个方案完成，最终比较界面要求从已完成方案中选择偏好并填写取舍说明；若跳过一个方案，可使用界面提供的跳过背景快捷填充。若两个方案都跳过，则会话结果为 Incomplete，偏好可以留空。

流程还提供暂停/恢复、从当前步骤恢复、重置当前任务、重放当前方案、返回物品、重新定位、跳过挑战、保存失败重试和安全退出。安全退出与“评审完成”不同，不代表保存已成功。

### 场景交互与演示功能

- XRI 控制器驱动移动、传送、抓取与门体/抽屉交互；门和抽屉使用运动学逻辑，不使用物理铰链驱动。
- `PREF_XR_KitchenRig` 提供手柄驱动的手部模型与握持动画；球形手掌代理可停靠在静态表面。**没有**摄像头裸手追踪、逐指碰撞或自适应握姿。
- 厨房水站支持接水与倾倒：有限容器水量、重力水流、受限晃动与水花。实现采用有界水流分段，不是 SPH/FLIP 流体；没有浮力、重量反馈或地面积水。
- 厨房环境、道具、日光、天空和烘焙光照保存在 Prefab 与资源中。修改静态环境、太阳或发光材质后，需要重新烘焙光照。
- 场景菜单提供演示用的回到起点、暂停与移动方式控制；水系统可通过编辑器菜单重置。它们不改变正式评审流程的状态所有权。

资源与维护说明：

- [厨房迁移与场景来源](Docs/MzhKitchenMigration.md)
- [厨房道具布置](Docs/KitchenDressing.md)
- [天空与倒水交互](Docs/KitchenWaterAndSky.md)

## 数据保存

运行时通过 `LocalCsvStorageAdapter` 把匿名会话保存到：

```text
Application.persistentDataPath/ReviewRecords/sessions.csv
```

Windows 默认位于当前用户的 `AppData/LocalLow` 下；具体完整路径可在 Unity 中查看存储组件的 **Log CSV Storage Path** 菜单项。

CSV 一行对应一个会话。逐方案反馈等检查点会更新该会话的行，最终比较也写回同一行。写入后执行读回验证；失败时流程保留在当前阶段并允许重试，不把失败伪装成成功。存储格式包含可读摘要和完整 `SessionRecord` JSON，支持中文与 CSV 转义。

数据结构、写入保证和故障处理细节见 [本地 CSV 存储说明](Docs/LocalCsvStorage.md)。

## 代码结构

```text
Assets/_Project/
  Art/                 厨房模型、材质与贴图
  Prefabs/             场景环境、benchmark、交互物与 UI Prefab
  Scenes/              SCN_Kitchen_Review（构建入口）
  Scripts/
    Data/               benchmark、参数、反馈分类与会话记录
    Simulation/         流程状态机、任务追踪、契约与运动学逻辑
    Runtime/             Unity 场景适配、XR 交互、UI、水与 CSV 存储
  Settings/             benchmark 注册表、方案、模型假设与交互参数
  Tests/
    EditMode/            纯逻辑、数据、Prefab/场景契约测试
    PlayMode/            场景接线与运行时旅程测试
Packages/                UPM manifest 与 lock
ProjectSettings/         Unity 工程及构建场景设置
Docs/                    接口、验收和功能维护文档
```

程序集依赖方向为 `XRApartment.Data ← XRApartment.Simulation ← XRApartment.Runtime`。`Simulation` 不依赖 Unity Engine；Core/流程状态只能通过 `JourneyRuntimeHost` 命令入口推进。

主要入口：

- 流程与适配：[JourneyRuntimeHost.cs](Assets/_Project/Scripts/Runtime/Core/JourneyRuntimeHost.cs)、[JourneyCoordinator.cs](Assets/_Project/Scripts/Simulation/JourneyCoordinator.cs)
- 证据采集：[RuntimeBenchmarkAdapter.cs](Assets/_Project/Scripts/Runtime/Core/RuntimeBenchmarkAdapter.cs)、[TaskEvidenceBridge.cs](Assets/_Project/Scripts/Runtime/Core/TaskEvidenceBridge.cs)
- 反馈与比较：[FeedbackFormView.cs](Assets/_Project/Scripts/Runtime/Feedback/FeedbackFormView.cs)、[JourneyPhaseFiveView.cs](Assets/_Project/Scripts/Runtime/UI/JourneyPhaseFiveView.cs)
- 本地存储：[CsvSessionStore.cs](Assets/_Project/Scripts/Runtime/Storage/CsvSessionStore.cs)

## 测试与验收

在 Unity Test Runner 中分别运行 EditMode 与 PlayMode 测试。最近一次验证结果（2026-10-18）：

| 测试模式 | 通过 | 失败 |
| --- | ---: | ---: |
| EditMode | 120 | 0 |
| PlayMode | 18 | 0 |

PlayMode 覆盖场景启动、B1/B2 任务流程、比较输入交互、XR 射线/编辑器鼠标射线目标、任务区传感器及恢复菜单。EditMode 覆盖流程状态、反馈校验、CSV、运动学、水量与场景/Prefab 完整性。

以上是编辑器自动化测试结果，不等于 Windows Player 构建验证，也不等于 Quest 3 + Quest Link 端到端验收。正式设备验收仍须按 PRD 的目标环境执行。

流程与联合验收步骤见 [Core Review Playbook](Docs/Core_Review_Playbook.md) 和 [场景接线说明](Docs/Core_Scene_Wiring.md)；阶段五的详细检查清单见 [Phase 5 Manual Acceptance](Docs/UI/Phase5_Manual_Acceptance.md)。

## 当前限制与待验证项

- 构建与体验目标是 **StandaloneWindows64 + Quest Link**；本项目不以 Android APK/Quest 一体机运行作为当前交付目标。不要为方便测试切换 Android 平台。
- 运行时配置只绑定 B1、B2。注册表或资源中存在其他方案，不代表它们已纳入当前正式评审旅程；B3/F4、F3 Explore-only 与位置 marker 按 PRD 属后续范围。
- 当前 F2 代理为上下两扇门，而 PRD Final §6.2 要求大型单开门；F3/F4 代理形态也与 PRD 定义不完全一致，且属于后续范围。修正和验收须逐件核对门数、铰链、开启方向、碰撞与登记参数。Prefab 存在或自动化测试通过不代表模型已通过设计验收。
- 流程契约包含冲突与安全通知接口，但当前核心 Prefab 未绑定 `conflictSource` / `safetySource`，仓库中也没有相应运行时检测实现。因此，**客观几何冲突提示、Yellow→Amber 反馈联动和独立 Red 安全提示尚未完成**。
- CSV 读回验证和自动化测试不提供跨启动的会话恢复 UI。
- Quest Link 真机试跑、体验尺度判断和完整构建验收仍未完成；不应把编辑器模拟结果表述为头显验证。

所有空间、尺寸、净空与触及范围结论必须标注为登记表支持的 prototype assumption，不能写成客户事实。需求状态与验收基线见 PRD Final 和 [模块接口契约](Docs/Module_Interface_Contract.md)。

## 协作与版本控制

- 功能开发使用 `feature/<name>` 分支并通过 PR 合并；影响共享分支的提交、推送、构建平台变更或历史改写，先与团队确认。
- Unity 版本、方案切换和模块责任边界遵守 [AGENTS.md](AGENTS.md) 与 [Module Interface Contract](Docs/Module_Interface_Contract.md)。
- **不要修改 `Assets/Samples/**`**；需要定制时复制到 `Assets/_Project/`。
- `Assets/**/*.meta` 必须与对应资源一起提交。`Assets/XR/`、`Assets/XRI/`、`ProjectSettings/`、`Packages/manifest.json` 和 `Packages/packages-lock.json` 属于项目配置的一部分。
- 二进制美术资源按仓库 `.gitattributes` 使用 Git LFS；新克隆后执行 `git lfs pull`。
- 不提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、构建产物、签名密钥、口令、日志或本地 `.qoder/settings.local.json`。
