# AGENTS.md — 给 AI 编码助手的项目指令

面向在本仓库工作的 agent 会话。人类向说明看 `README.md`。**Phase 2 产品范围与验收以 `Docs/COMP5424_Scenario_C_Lean_PRD_CN_Final.docx`（Lean PRD v0.3 Final，2026-10-03）为准**；Final Proposal 是该 PRD 的实施来源，冰箱 archetype 定义以 PRD Final §6.2 为准。本文是操作摘要，冲突时核对 PRD Final；需求追溯见 README §12。

## 这个项目是什么

COMP5424 Scenario C 的**厨房设计评审原型**：在单一代表性厨房里，让 future-resident proxy 通过相同的日常动作链比较 P0 的 B1=Proud+F2 与 B2=Flush+F2。B3=Flush+F4、F3 Explore-only 预览和位置 marker 为 P1，待主旅程稳定后再纳入；F3 不产生正式比较证据。P0 包括即时非阻塞的客观几何冲突提示、逐组合反馈与匿名本地记录。

**不是**通用 VR 公寓漫游、不是装修器、不是营销展示、不是合规认证工具。产品关系是 `建筑师提出方案 → 评审者体验 → 系统捕获反馈 → 建筑师修订`。

## 硬性约束

- **引擎锁死 Unity 2022.3.62f3**。不要升级、不要提议升级到 Unity 6、不要安装 `com.unity.ai.assistant*`。官方内置 MCP 要求 Unity 6，本项目改用社区方案 MCP for Unity 10.2.0（支持 2021.3–6.x），这是已定决策。
- **构建目标是 `StandaloneWindows64`**。PRD 批准的环境是 Meta Quest 3 **经 Quest Link 连 PC**（TECH-02），一体机 APK 不在 MVP 内。**不要 Switch Platform 到 Android**：会触发全量 reimport 并改写 `EditorBuildSettings.asset` / `GraphicsSettings.asset`，影响全组。
- **不要扩张 P0 范围**。B3/F4、F3 Explore-only 和位置 marker 为 P1；预设颜色方案及代表性线缆/插座为 P2（FR-13/14）；完整公寓、真实重量与复杂物理、多人同场和云端后台不进入 P0。范围膨胀是风险 R01。
- **不要把自由编辑当成方案**。方案切换是预设 configuration switching（D03），不是任意搬动家具。
- **不要修改 `Assets/Samples/**`**。整目录是 XRI 官方示例，升级时整目录替换。需要改输入配置或预制体时**复制**到 `Assets/_Project/Settings/` 或 `Prefabs/` 再改。
- **空间主张必须有登记参数支撑**（MOD-01）。尺寸 / clearance / reach 结论只能对 `Assumption Register` 里的 prototype assumption 成立，不得写成 client fact。
- **提交、推送、切构建目标、改写已推送历史**这类影响全组状态的操作，先征求用户同意；一次授权不代表长期授权。
- 不要把 keystore、口令、`.qoder/settings.local.json`、日志、崩溃转储写进任何提交。`.gitignore` 已拦截，但 `git add -f` 会绕过它。

## 当前真实状态（改设置前先看这里，别照旧文档假设）

- `activeInputHandler: 1` = **仅新 Input System**（Unity 的取值映射是 0=旧 / 1=仅新 / 2=Both，与 `Assets/Editor/XRProjectBootstrap.cs` 的注释一致）。**不要因为"看起来应该是 2"就顺手改**。
- PRD Final §12 的需求包含 P0/P1/P2；P0 主旅程尚未完成端到端验收，现有模块不能视作需求已验收。按 Final 的优先级、状态和验收条件追踪各 ID。
- `Assets/_Project/Scripts/` 三个程序集边界：`XRApartment.Data`（枚举 / benchmark 定义与注册表 / 模型参数 SO / 会话记录 schema）、`XRApartment.Simulation`（纯 C#，`noEngineReferences`，八态状态机、挑战动作链、运动学缓动 `MotionKinematics`）、`XRApartment.Runtime`（`ReviewElement` 标注 + `KinematicSwingDoor` / `KinematicLinearDrawer` 运动学门体抽屉 + `BenchmarkSlotMarker` + `KitchenMenuController` 世界空间菜单，已引用 XRI 3.5 / XR CoreUtils / uGUI）。现有门体的同侧铰链安排属于旧代理实现；修正 F2/F3/F4 时须逐件按 PRD Final §6.2 和登记参数核对门数、铰链、开启方向及碰撞。`Assets/_Project/Tests/EditMode/` 在原有 25 条断言之外新增 `MotionKinematicsTests`（7 条），用 `run_tests` 无需头显即可全绿。
- 厨房已迁移为 `feature/mzh@1f501b3` 的 `KitchenTest` 设计：`Assets/_Project/Scenes/SCN_Kitchen_Review.unity`。用 `XR Apartment/Open Kitchen Review Scene` 打开；环境由 `PREF_Kitchen_Environment` 保存，来源模型/贴图和适配后的 Standard 材质在 `Assets/_Project/Art/Kitchen/`。**旧 `KitchenSceneBootstrap` / `VRSceneBootstrap` 已删除，不要恢复代码生成的灰盒**。原始 FBX 不变，台面入口端用派生网格留出 0.838m 冰箱槽位；此改动和实测台面高度等已更新到 `Settings/ASM_KitchenParameters.asset`（全为 prototype assumption，MOD-01）。详见 `Docs/MzhKitchenMigration.md`。
- `Prefabs/` 保留 `PREF_Fridge_F2/F3/F4` 现有交互代理与 `PREF_Prop_Pot/Cup`；新增厨房环境、benchmark 槽位、菜单和传送区 Prefab。**现有代理与 PRD Final §6.2 的形态不符**：F2 现为上下两扇门，Final 要求大型单开门；F3 现有两扇门加抽屉，Final 要求左右双开门；F4 现有两扇门加两个抽屉，Final 要求上部双门加一个下部抽屉。P0 先修 F2，F3/F4 随 P1 修。当前 `KitchenBenchmarkMenu` 的 `XR Apartment/Benchmark/Apply …` 是编辑器制作/调试入口，不满足构建后的运行时方案切换；现有切换主要替换 `SLOT_Fridge` 实例和 Flush 围板。mzh 场景未提供可用冰箱方案，这些交互代理仍为 Hzc 原有版本，不能声称来自 mzh。
- 仍然没有：客观几何冲突检测与 Yellow→Amber 设计提示（FR-06）、独立的 Red 安全提示（SAFE-01）、反馈 UI、本地记录写盘。
- 旧 `Main.unity` 已删除；Build Settings 只启用 `SCN_Kitchen_Review`（第 0 位）。保留 Unity 2022.3.62f3 / Built-in / XRI 3.5，不引入 mzh 的 Unity 6 / URP / XRI 3.6.1 / 物理手控制器、烘焙数据和测试场。
- `ProjectSettings/TagManager.asset` 层 3–31 仍全空；XRI `InteractionLayerSettings` 目前只有 `Teleport` 一层（Starter Assets 导入时加的）。
- 无头显键鼠测试已配置：`Assets/_Project/XR/InteractionSimulator/` 是 XRI 3.5.0 的 XR Interaction Simulator 项目副本，TMP Essentials 在 `Assets/TextMesh Pro/`。`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 自动创建已开启且仅限 Editor，进入 Play 后点击 Game 即可用 WASD / 鼠标操作，Y 显示提示。真实头显在编辑器测试前关闭自动模拟器；构建仍保持 Windows64。不要为测试再次导入同 GUID 的 Simulator Sample，也不要升级 Unity。
- `PREF_XR_KitchenRig` 已替换场景中的 Starter Rig 实例。`PREF_Hand_Left/Right` 使用 mzh 的右手模型镜像与四个 Grip/Trigger 动画；`ControllerHandPresentation` 负责预握/握持提示和追踪虚影，`KinematicPalmSolver` 只让球形手掌代理停靠静态表面，不施加物理力或改变输入坐标。参数由 `SET_HandInteraction` 引用 `ASM_KitchenParameters` 的 `hand_*` 项。不是摄像头裸手追踪，也没有逐指碰撞/自适应握姿。锅杯保持 XRI 抓取，改用动态接触点；门/抽屉保持原有运动学逻辑。45 条 EditMode 测试通过；尚未完成 Quest Link 实测。
- 光照使用 `PREF_Lighting_KitchenDaylight`、`MAT_Sky_KitchenDaylight`、`LGT_KitchenDaylight.lighting`、`PP_KitchenDaylight`；Built-in + **Linear**，官方 Post Processing 3.2.2 提供固定 ACES/Bloom，不要照旧改回 Gamma。太阳与天光在三种 benchmark 间保持一致。静态环境已生成 UV2，烘焙数据在 `Scenes/SCN_Kitchen_Review/`；移动门体/锅杯/手部/benchmark 围板不参与静态烘焙。改环境/太阳/发光材质需重烘焙；不要复制 Unity 6 的 LightingData/APV。二进制 `LightingData.asset` 已按精确路径走 LFS，其他 YAML `.asset` 继续走文本。
- 天空后续按用户选择改为 AllSky Free 1.2.0 的 `Cold Sunset Equirect`，仅引入这一张原始图和导入 GUID；`MAT_Sky_KitchenDaylight` 使用 `Skybox/Cubemap`。水默认使用 MIT 来源的 NVJOB Water Specular，资源与许可证在 `Assets/ThirdParty/`。`SET_WaterInteraction` 引用 `ASM_KitchenParameters` 的 `water_*` 项。`PREF_Water_KitchenStation` 提供龙头和排水盆，锅杯已改为开口模型；水量、重力轨迹和受限晃动由项目脚本计算，**不是 SPH/FLIP 流体、浮力或重量反馈**。不要挂载供应商的平面反射相机脚本，不导入其演示场景。见 `Docs/KitchenWaterAndSky.md`。
- Kitchen Props Free / Food Pack Mixed 的精选源资源在 `Assets/ThirdParty/`，16 个归一化道具 Prefab 在 `_Project/Prefabs/Props/`，公共布置为 `PREF_Kitchen_Dressing`，位于方案槽位之外。9 件新增食品/包装及空马克杯可抓取；新马克杯和果汁盒不参与倒水，保留原有水杯/锅。尺寸按 `ASM_KitchenParameters` 的 `prop_*` 登记；固定摆件参加 GI，改位置后需重烘焙。`KitchenPropsAuthoring` 只重建摆设，不重建房间；配置会恢复默认摆放。详见 `Docs/KitchenDressing.md`。
- OpenXR 的 **控制器 interaction profile 已为 StandaloneWindows64 启用**（Oculus Touch / Meta Quest Touch Plus / Touch Pro / KHR Simple），Android 侧有意保持全关。
- 本机 **活动的 OpenXR 运行时是 Virtual Desktop**（`HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` 指向 `C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`，runtime 名 `VirtualDesktopXR (Bundled)`）。SteamVR 也已安装但不应与 VD 抢运行时。
- Play 模式实测 `XRSettings.enabled=False`、`isDeviceActive=False` 的原因是 **VDServer 未运行、头显未连接**，不是缺运行时。要验手柄与画面：先启动 PC 端 Virtual Desktop Streamer 并让 Quest 3 连上，再进 Play。
- **注意与 PRD Final 的偏差**：TECH-02 的批准环境是「Quest 3 经 Quest Link」，VD 串流可作调试，但其结果不满足 TECH-02 验收；正式验收仍须走 Quest Link。
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

- 开发或修改会话流程、方案切换、任务事件、UI、冲突提示、反馈或本地记录时，先读 `Docs/Module_Interface_Contract.md`；它定义模块交接字段、状态推进和联合测试，变更共享接口须先更新该契约。
- 提交信息用中文 + Conventional Commits 前缀，正文写清「为什么」而不是「改了什么」，并**引用被满足的 PRD 需求 ID**，例如 `feat(review): 加入八态会话状态机 [FR-11 FR-12]`。这是 F 做证据追溯、A 维护 traceability 的依据。
- **不要新造需求/决策编号**。沿用 PRD 稳定 ID：`FR-*`、`UX-*`、`ACC-*`、`SAFE-01`、`MOD-01`、`TECH-*`、`EVAL-*`、`D01…D20`、`R01…R11`、`EQ1…EQ6`。
- 一次提交只含一个关注点，按依赖顺序排：VCS 配置 → 引擎/厂商设置资产 → vendored 资源 → 项目内容 → 文档。
- 可交互物一律做成 Prefab 放 `Assets/_Project/Prefabs/`，场景里只放实例。benchmark 之间切换预制体实例，不改场景。
- **所有已实现的 Challenge benchmark 必须跑同一条动作链**（FR-03 / D07）；P0 是 B1/B2，B3 仅在 P1 纳入后适用。可交互目标按**语义角色**解析（`FridgeDoor` / `FridgeDrawer` / `CounterSurface` / `StorageZone`），不要按组合名写死任务分支。
- 门体与抽屉用运动学旋转 + 扫掠体检测，参数从登记表读取（FR-04）；**不要用 HingeJoint 物理驱动**，那会引入不可复现的结果并撞上 R04。
- Safety collision（边界 warning/fade，Red）与 Design conflict（客观几何冲突 Yellow→提交反馈后 Amber）是两套语义，均附文字/图标，不得复用同一套视觉（SAFE-01、FR-06）。
- 命名 `前缀_类型_描述`：`PREF_Fridge_F2`、`PREF_Cabinet_Proud_Wall`、`MAT_Kitchen_Counter`、`SCN_Kitchen_Review`。
- 新增需要走 LFS 的后缀时改 `.gitattributes`；已经提交过的文件需要 `git lfs migrate`，属于影响全组的动作，先问。

## 待确认（尚未冻结，别当成既定事实）

- 作业文档是否收进仓库 `Docs/`（PRD Final 已入库）。
- F2 代表性几何参数、冲突阈值和 Ready for Evaluation 联合检查结果，以 Assumption Register、模型版本和实际证据冻结；不得凭现有 Prefab 反推 PRD 定义。
