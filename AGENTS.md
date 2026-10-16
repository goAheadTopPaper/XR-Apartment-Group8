# AGENTS.md — 给 AI 编码助手的项目指令

面向在本仓库工作的 agent 会话。人类向说明看 `README.md`，**产品与需求的唯一权威是 Lean PRD v0.1（见 README §12）**。本文件只收必须遵守的约束和已验证的工具入口。

## 这个项目是什么

COMP5424 Scenario C 的**厨房设计评审原型**：在单一代表性厨房里，让 future-resident proxy 比较「冰箱 archetype（F2/F3/F4）× 柜体策略（Proud / Flush-oriented）」的三种 benchmark 组合（B1=Proud+F2、B2=Flush+F2、B3=Flush+F4），执行相同的日常动作链，接受即时非阻塞的几何冲突提示，最后提交可定位的结构化反馈。

**不是**通用 VR 公寓漫游、不是装修器、不是营销展示、不是合规认证工具。产品关系是 `建筑师提出方案 → 评审者体验 → 系统捕获反馈 → 建筑师修订`。

## 硬性约束

- **引擎锁死 Unity 2022.3.62f3**。不要升级、不要提议升级到 Unity 6、不要安装 `com.unity.ai.assistant*`。官方内置 MCP 要求 Unity 6，本项目改用社区方案 MCP for Unity 10.2.0（支持 2021.3–6.x），这是已定决策。
- **构建目标是 `StandaloneWindows64`**。PRD 批准的环境是 Meta Quest 3 **经 Quest Link 连 PC**（TECH-02），一体机 APK 不在 MVP 内。**不要 Switch Platform 到 Android**：会触发全量 reimport 并改写 `EditorBuildSettings.asset` / `GraphicsSettings.asset`，影响全组。
- **不要扩张范围**。完整公寓、颜色/材质定制、真实重量与复杂物理、电线插座、多人同场、云端后台均已在 PRD §5 标为 Deferred/Rejected（D02/D03/D13）。范围膨胀是首要风险 R01。
- **不要把自由编辑当成方案**。方案切换是预设 configuration switching（D03），不是任意搬动家具。
- **不要修改 `Assets/Samples/**`**。整目录是 XRI 官方示例，升级时整目录替换。需要改输入配置或预制体时**复制**到 `Assets/_Project/Settings/` 或 `Prefabs/` 再改。
- **空间主张必须有登记参数支撑**（MOD-01）。尺寸 / clearance / reach 结论只能对 `Assumption Register` 里的 prototype assumption 成立，不得写成 client fact。
- **提交、推送、切构建目标、改写已推送历史**这类影响全组状态的操作，先征求用户同意；一次授权不代表长期授权。
- 不要把 keystore、口令、`.qoder/settings.local.json`、日志、崩溃转储写进任何提交。`.gitignore` 已拦截，但 `git add -f` 会绕过它。

## 当前真实状态（改设置前先看这里，别照旧文档假设）

- `activeInputHandler: 1` = **仅新 Input System**（Unity 的取值映射是 0=旧 / 1=仅新 / 2=Both，与 `Assets/_Project/Editor/XRProjectBootstrap.cs` 的注释一致）。**不要因为"看起来应该是 2"就顺手改**。
- PRD §12 的 25 条 Approved 需求（FR/UX/ACC/SAFE/MOD/TECH/EVAL）尚未完成端到端验收；已有模块与完整需求验收需区分。
- `Assets/_Project/Scripts/` 三个程序集边界：`XRApartment.Data`（枚举 / benchmark 定义与注册表 / 模型参数 SO / 会话记录 schema）、`XRApartment.Simulation`（纯 C#，`noEngineReferences`，八态状态机、挑战动作链、运动学缓动 `MotionKinematics`）、`XRApartment.Runtime`（`ReviewElement` 标注 + `KinematicSwingDoor` / `KinematicLinearDrawer` 运动学门体抽屉 + `BenchmarkSlotMarker` + `KitchenMenuController` 世界空间菜单，已引用 XRI 3.5 / XR CoreUtils / uGUI）。冰箱平开门统一**同侧铰链**（靠台面一侧），门扇向房间中央摆出——异侧会让两扇门向玩家两侧张开，不符合双门形态（玩家实测反馈）。`Assets/_Project/Tests/EditMode/` 在原有 25 条断言之外新增 `MotionKinematicsTests`（7 条），用 `run_tests` 无需头显即可全绿。
- 运行时按 `Runtime/Review`、`Runtime/Interaction`、`Runtime/Water` 分组，类名、命名空间、脚本 GUID 与三个程序集不变。项目作者工具统一在 `Assets/_Project/Editor/`：`KitchenAuthoring` 负责公共路径、资源加载、场景检查和 Prefab 编辑生命周期；`KitchenGrabAuthoring` 是抓取配置的唯一入口，`KitchenLightingAuthoring` 负责天空，`WaterVesselMeshBuilder` 负责容器网格。新增模块前先看 `Docs/CodeStructure.md`，不要恢复根目录 `Assets/Editor` 或新增一套通用服务框架。
- 厨房已迁移为 `feature/mzh@1f501b3` 的 `KitchenTest` 设计：`Assets/_Project/Scenes/SCN_Kitchen_Review.unity`。用 `XR Apartment/Open Kitchen Review Scene` 打开；环境由 `PREF_Kitchen_Environment` 保存，来源模型/贴图和适配后的 Standard 材质在 `Assets/_Project/Art/Kitchen/`。**旧 `KitchenSceneBootstrap` / `VRSceneBootstrap` 已删除，不要恢复代码生成的灰盒**。原始 FBX 不变，台面入口端用派生网格留出 0.838m 冰箱槽位；此改动和实测台面高度等已更新到 `Settings/ASM_KitchenParameters.asset`（全为 prototype assumption，MOD-01）。详见 `Docs/MzhKitchenMigration.md`。
- `Prefabs/` 保留 `PREF_Fridge_F2/F3/F4`（双门/三门/四门十字代理）与 `PREF_Prop_Pot/Cup`；新增厨房环境、benchmark 槽位、菜单和传送区 Prefab。B1/B2/B3 继续由 `KitchenBenchmarkMenu` 的 `XR Apartment/Benchmark/Apply …` 切换（编辑器与 Play 均可），只换 `SLOT_Fridge` 的实例和 Flush 围板。mzh 场景未提供可用冰箱方案，因此这些交互代理仍为 Hzc 原有版本，不能声称来自 mzh。
- 仍然没有：几何冲突检测（SAFE-01 的 warning/fade 与 Red→Amber 两套语义）、反馈 UI、本地记录写盘。
- 旧 `Main.unity` 已删除；Build Settings 只启用 `SCN_Kitchen_Review`（第 0 位）。保留 Unity 2022.3.62f3 / Built-in / XRI 3.5，不引入 mzh 的 Unity 6 / URP / XRI 3.6.1 / 物理手控制器、烘焙数据和测试场。
- `ProjectSettings/TagManager.asset` 层 3–31 仍全空；XRI `InteractionLayerSettings` 目前只有 `Teleport` 一层（Starter Assets 导入时加的）。
- 无头显键鼠测试已配置：`Assets/_Project/XR/InteractionSimulator/` 是 XRI 3.5.0 的 XR Interaction Simulator 项目副本，TMP Essentials 在 `Assets/TextMesh Pro/`。`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 自动创建已开启且仅限 Editor，进入 Play 后点击 Game 即可用 WASD / 鼠标操作，Y 显示提示。真实头显在编辑器测试前关闭自动模拟器；构建仍保持 Windows64。不要为测试再次导入同 GUID 的 Simulator Sample，也不要升级 Unity。
- `PREF_XR_KitchenRig` 已替换场景中的 Starter Rig 实例。`PREF_Hand_Left/Right` 使用 mzh 的右手模型镜像与四个 Grip/Trigger 动画；`ControllerHandPresentation` 负责预握/握持提示和追踪虚影，`KinematicPalmSolver` 只让球形手掌代理停靠静态表面，不施加物理力或改变输入坐标。参数由 `SET_HandInteraction` 引用 `ASM_KitchenParameters` 的 `hand_*` 项。不是摄像头裸手追踪，也没有逐指碰撞/自适应握姿。锅杯保持 XRI 抓取，改用动态接触点；门/抽屉保持原有运动学逻辑。45 条 EditMode 测试通过；尚未完成 Quest Link 实测。
- 锅杯及新增食品共 11 件可抓物使用 `CollisionConstrainedGrabTransformer` / `HeldPropSweep`，按整个物体的保守定向包围盒扫掠平移与旋转；受阻时实物/实手停住，虚影透过遮挡显示，释放抑制虚假抛掷。配置入口为 `KitchenGrabAuthoring`；重建道具时必须保留此调用，不要换成未经约束的 XRI 跟随或在约束后添加插值。`COL_GrabSweepProbe` 必须禁用且不加入 XRI colliders；接触余量读取登记项 `grab_contact_skin`。详见 `Docs/HeldPropCollision.md`。当前 77 条 EditMode、8 条 PlayMode 通过，未做 Quest Link 真机验收。
- 光照使用 `PREF_Lighting_KitchenDaylight`、`MAT_Sky_KitchenDaylight`、`LGT_KitchenDaylight.lighting`、`PP_KitchenDaylight`；Built-in + **Linear**，官方 Post Processing 3.2.2 提供固定 ACES/Bloom，不要照旧改回 Gamma。太阳与天光在三种 benchmark 间保持一致。静态环境已生成 UV2，烘焙数据在 `Scenes/SCN_Kitchen_Review/`；移动门体/锅杯/手部/benchmark 围板不参与静态烘焙。改环境/太阳/发光材质需重烘焙；不要复制 Unity 6 的 LightingData/APV。二进制 `LightingData.asset` 已按精确路径走 LFS，其他 YAML `.asset` 继续走文本。
- 天空后续按用户选择改为 AllSky Free 1.2.0 的 `Cold Sunset Equirect`，仅引入这一张原始图和导入 GUID；`MAT_Sky_KitchenDaylight` 使用 `Skybox/Cubemap`。水默认使用 MIT 来源的 NVJOB Water Specular，资源与许可证在 `Assets/ThirdParty/`。`SET_WaterInteraction` 引用 `ASM_KitchenParameters` 的 `water_*` 项。`PREF_Water_KitchenStation` 提供龙头和排水盆，锅杯已改为开口模型；水量、重力轨迹和受限晃动由项目脚本计算，**不是 SPH/FLIP 流体、浮力或重量反馈**。不要挂载供应商的平面反射相机脚本，不导入其演示场景。见 `Docs/KitchenWaterAndSky.md`。
- Kitchen Props Free / Food Pack Mixed 的精选源资源在 `Assets/ThirdParty/`，16 个归一化道具 Prefab 在 `_Project/Prefabs/Props/`，公共布置为 `PREF_Kitchen_Dressing`，位于方案槽位之外。9 件新增食品/包装及空马克杯可抓取；新马克杯和果汁盒不参与倒水，保留原有水杯/锅。尺寸按 `ASM_KitchenParameters` 的 `prop_*` 登记；固定摆件参加 GI，改位置后需重烘焙。`KitchenPropsAuthoring` 只重建摆设，不重建房间；配置会恢复默认摆放。详见 `Docs/KitchenDressing.md`。
- OpenXR 的 **控制器 interaction profile 已为 StandaloneWindows64 启用**（Oculus Touch / Meta Quest Touch Plus / Touch Pro / KHR Simple），Android 侧有意保持全关。
- 此前 Windows 测试机的活动 OpenXR 运行时为 Virtual Desktop（`HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` 指向 `C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`，runtime 名 `VirtualDesktopXR (Bundled)`）。该次未连接是因为 VDServer 未运行、头显未连接；这个历史状态不代表当前执行主机的运行时，排查前先确认主机与连接方式。
- 2026-10-16 在 Mac 编辑器实测 Play：自动初始化已开、OpenXR Loader 已配置，但无活动 Loader、XR 显示子系统或真实设备，输入只有模拟头显/手柄。VD 的 Mac 连接只提供桌面串流，PCVR 串流需要 Windows（见 README §7）。真机直接 Play 需在 Windows 运行编辑器，先连接对应运行时和头显，再关闭自动 XR 模拟器；不要通过切 Android、修改 Input Handler 或重复安装包来修复此平台限制。
- **注意与 PRD 的偏差**：§3.3 / §14.1 / TECH-02 写的批准环境是「Quest 3 经 Quest Link」，用 VD 串流属于换链路，需 A/F 确认是改文档还是正式评价仍走官方 Link。别把 VD 的结果当成满足 TECH-02 验收。
- 编译目标当前是 StandaloneWindows64，双端 OpenXR Loader 均已配置，控制台 0 error。

## 必须入库 / 绝不入库

**必须提交**：`Assets/**/*.meta`（与资源同一个提交）、`ProjectSettings/`、`Packages/manifest.json` + `packages-lock.json`、`Assets/XR/`、`Assets/XRI/`（这两个目录是 ProjectSettings 引用到的实际对象，漏提交会让队友加载器列表为空）。

**绝不提交**：`Library/ Temp/ Logs/ UserSettings/ Build/ Builds/ .utmp/`、`*.apk *.aab *.unitypackage`、`*.keystore *.jks *.pem *.key .env secrets.json`、`*.log *.dmp *.crash sysinfo.txt`、IDE 生成物、`.qoder/settings.local.json`。

Unity 会丢弃无资源的空文件夹，需要在仓库里保留的空目录放一个 `.gitkeep`。

## Unity MCP 工具

47 个 `mcp__unity__*` 工具。**前提是 Unity 编辑器处于打开状态**，Bridge 在 `127.0.0.1:6400`。

- 工具报 `No Unity Editor instances found` 时，先确认编辑器是否关着（`tasklist | grep -i unity`、`netstat -ano | grep 6400`），**不要怀疑包版本或配置**。编辑器正常退出后 `Temp/` 会被删除且无 `UnityLockfile`，可用来区分正常关闭与崩溃。
- 改场景 / 预制体 / 工程设置优先用这些工具，而不是手写 YAML：`manage_scene`（`get_hierarchy` 支持 `parent` + `max_depth` 逐层展开，根节点 `childCount` 会截断）、`manage_gameobject`、`manage_components`、`manage_prefabs`、`manage_build`、`manage_physics`、`manage_probuilder`、`execute_code`。
- 写完脚本用 `refresh_unity`（`mode=force`）触发导入，再用 `read_console`（`action=get`, `types=["error"]`）确认 0 error。域重载期间工具调用会失败，等 `ready` 再试。
- 无头显时逻辑验证优先走 `run_tests` + `get_test_job`：把状态机、冲突判定、任务进度、数据 schema 抽成不依赖 XR 设备的纯逻辑层，能在编辑器测试里跑完。
- 注册位置：MCP server 写在**用户级** `~/.qoder/settings.json` 的 `mcpServers` 里。IDE 的 agent 会话不读仓库内的 `.qoder/settings.local.json`，写在那儿不生效。
- batchmode 命令行（README §10）与打开的编辑器互斥，跑之前先关编辑器。`-logFile` 的目录名不能以点开头，用 `Logs/`。

## 修改约定

- 提交信息用中文 + Conventional Commits 前缀，正文写清「为什么」而不是「改了什么」，并**引用被满足的 PRD 需求 ID**，例如 `feat(review): 加入八态会话状态机 [FR-11 FR-12]`。这是 F 做证据追溯、A 维护 traceability 的依据。
- **不要新造需求/决策编号**。沿用 PRD 稳定 ID：`FR-*`、`UX-*`、`ACC-*`、`SAFE-01`、`MOD-01`、`TECH-*`、`EVAL-*`、`D01…D20`、`R01…R11`、`EQ1…EQ6`。
- 一次提交只含一个关注点，按依赖顺序排：VCS 配置 → 引擎/厂商设置资产 → vendored 资源 → 项目内容 → 文档。
- 可交互物一律做成 Prefab 放 `Assets/_Project/Prefabs/`，场景里只放实例。benchmark 之间切换预制体实例，不改场景。
- **三个 benchmark 必须跑同一条动作链**（FR-03 / D07）。可交互目标按**语义角色**解析（`FridgeDoor` / `FridgeDrawer` / `CounterSurface` / `StorageZone`），不要按 B1/B2/B3 写死对象名或任务分支，否则比较失去可解释性。
- 门体与抽屉用运动学旋转 + 扫掠体检测，参数从登记表读取（FR-04）；**不要用 HingeJoint 物理驱动**，那会引入不可复现的结果并撞上 R04。
- Safety collision（边界 warning/fade）与 Design conflict（Red→Amber 几何标红）是两套语义，不得复用同一套视觉（SAFE-01、§10.1）。
- 命名 `前缀_类型_描述`：`PREF_Fridge_F2`、`PREF_Cabinet_Proud_Wall`、`MAT_Kitchen_Counter`、`SCN_Kitchen_Review`。
- 新增需要走 LFS 的后缀时改 `.gitattributes`；已经提交过的文件需要 `git lfs migrate`，属于影响全组的动作，先问。

## 待确认（尚未冻结，别当成既定事实）

- Unity 侧模块划分与程序集（asmdef）方案：状态机 / 配置注册 / 参数登记 / 冲突检测 / 反馈捕获 / 本地记录六块，以及是否单列一个不依赖 XR 的纯逻辑层。等 D 确认后再建目录。
- 厨房灰盒由谁先搭（D 用 ProBuilder 按登记参数占位，还是等 E 的 Blender 模型）。
- 模型参数登记用 ScriptableObject 还是 CSV 表（涉及 E 手填与 F 取数）。
- PRD 与作业文档是否收进仓库 `Docs/`（目前只在各人本机，队友和 agent 读不到）。
