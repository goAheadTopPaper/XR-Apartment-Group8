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

- `activeInputHandler: 1` = **仅新 Input System**（Unity 的取值映射是 0=旧 / 1=仅新 / 2=Both，与 `Assets/_Project/Editor/XRProjectBootstrap.cs` 的注释一致）。**不要因为"看起来应该是 2"就顺手改**。
- PRD Final §12 的需求包含 P0/P1/P2；P0 主旅程尚未完成端到端验收，现有模块不能视作需求已验收。按 Final 的优先级、状态和验收条件追踪各 ID。
- `Assets/_Project/Scripts/` 三个程序集边界：`XRApartment.Data`（枚举 / benchmark 定义与注册表 / 模型参数 SO / 会话记录 schema）、`XRApartment.Simulation`（纯 C#，`noEngineReferences`，八态状态机、挑战动作链、运动学缓动 `MotionKinematics`）、`XRApartment.Runtime`（`ReviewElement` 标注 + `KinematicSwingDoor` / `KinematicLinearDrawer` 运动学门体抽屉 + `BenchmarkSlotMarker` + `KitchenMenuController` 世界空间菜单，已引用 XRI 3.5 / XR CoreUtils / uGUI）。现有门体的同侧铰链安排属于旧代理实现；修正 F2/F3/F4 时须逐件按 PRD Final §6.2 和登记参数核对门数、铰链、开启方向及碰撞。`Assets/_Project/Tests/EditMode/` 在原有 25 条断言之外新增 `MotionKinematicsTests`（7 条），用 `run_tests` 无需头显即可全绿。
- 厨房已迁移为 `feature/mzh@1f501b3` 的 `KitchenTest` 设计：`Assets/_Project/Scenes/SCN_Kitchen_Review.unity`。用 `XR Apartment/Open Kitchen Review Scene` 打开；环境由 `PREF_Kitchen_Environment` 保存，来源模型/贴图和适配后的 Standard 材质在 `Assets/_Project/Art/Kitchen/`。**旧 `KitchenSceneBootstrap` / `VRSceneBootstrap` 已删除，不要恢复代码生成的灰盒**。原始 FBX 不变，台面入口端用派生网格留出 0.838m 冰箱槽位；此改动和实测台面高度等已更新到 `Settings/ASM_KitchenParameters.asset`（全为 prototype assumption，MOD-01）。详见 `Docs/MzhKitchenMigration.md`。
- `Prefabs/` 保留 `PREF_Fridge_F2/F3/F4` 现有交互代理与 `PREF_Prop_Pot/Cup`；新增厨房环境、benchmark 槽位、菜单和传送区 Prefab。**现有代理与 PRD Final §6.2 的形态不符**：F2 现为上下两扇门，Final 要求大型单开门；F3 现有两扇门加抽屉，Final 要求左右双开门；F4 现有两扇门加两个抽屉，Final 要求上部双门加一个下部抽屉。P0 先修 F2，F3/F4 随 P1 修。当前 `KitchenBenchmarkMenu` 的 `XR Apartment/Benchmark/Apply …` 是编辑器制作/调试入口，不满足构建后的运行时方案切换；现有切换主要替换 `SLOT_Fridge` 实例和 Flush 围板。mzh 场景未提供可用冰箱方案，这些交互代理仍为 Hzc 原有版本，不能声称来自 mzh。
- 主线已接入 Core 运行时旅程、世界空间 UI、正式反馈表单和本地 CSV 保存。P0 会话计划为 B1/B2；B3 继续保留作者预览入口。客观几何冲突检测与 Yellow→Amber 设计提示（FR-06）、独立的 Red 安全提示（SAFE-01）仍未接入。
- 旧 `Main.unity` 已删除；按用户 2026-10-31 的主场景选择，Build Settings 以城市窗景 `SCN_Kitchen_Cooking` 为第 0 位入口，`SCN_Kitchen_Review` 为第 1 位正式评审场景。保留 Unity 2022.3.62f3 / Built-in / XRI 3.5，不引入 mzh 的 Unity 6 / URP / XRI 3.6.1 / 物理手控制器、烘焙数据和测试场。
- `ProjectSettings/TagManager.asset` 层 3–31 仍全空；XRI `InteractionLayerSettings` 目前只有 `Teleport` 一层（Starter Assets 导入时加的）。
- 无头显键鼠测试已配置：`Assets/_Project/XR/InteractionSimulator/` 是 XRI 3.5.0 的 XR Interaction Simulator 项目副本，TMP Essentials 在 `Assets/TextMesh Pro/`。`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 自动创建已开启且仅限 Editor，进入 Play 后点击 Game 即可用 WASD / 鼠标操作，Y 显示提示。真实头显在编辑器测试前关闭自动模拟器；构建仍保持 Windows64。不要为测试再次导入同 GUID 的 Simulator Sample，也不要升级 Unity。
- `PREF_XR_KitchenRig` 已替换场景中的 Starter Rig 实例。`PREF_Hand_Left/Right` 使用 mzh 的右手模型镜像与四个 Grip/Trigger 动画；`ControllerHandPresentation` 负责预握/握持提示和追踪虚影，`KinematicPalmSolver` 只让球形手掌代理停靠静态表面，不施加物理力或改变输入坐标。参数由 `SET_HandInteraction` 引用 `ASM_KitchenParameters` 的 `hand_*` 项。不是摄像头裸手追踪，也没有逐指碰撞/自适应握姿。锅杯保持 XRI 抓取，改用动态接触点；门/抽屉保持原有运动学逻辑。45 条 EditMode 测试通过；尚未完成 Quest Link 实测。
- 光照使用 `PREF_Lighting_KitchenDaylight`、`MAT_Sky_KitchenDaylight`、`LGT_KitchenDaylight.lighting`、`PP_KitchenDaylight`；Built-in + **Linear**，官方 Post Processing 3.2.2 提供固定 ACES/Bloom，不要照旧改回 Gamma。太阳与天光在三种 benchmark 间保持一致。静态环境已生成 UV2，烘焙数据在 `Scenes/SCN_Kitchen_Review/`；移动门体/锅杯/手部/benchmark 围板不参与静态烘焙。改环境/太阳/发光材质需重烘焙；不要复制 Unity 6 的 LightingData/APV。二进制 `LightingData.asset` 已按精确路径走 LFS，其他 YAML `.asset` 继续走文本。
- 天空后续按用户选择改为 AllSky Free 1.2.0 的 `Cold Sunset Equirect`，仅引入这一张原始图和导入 GUID；`MAT_Sky_KitchenDaylight` 使用 `Skybox/Cubemap`。水默认使用 MIT 来源的 NVJOB Water Specular，资源与许可证在 `Assets/ThirdParty/`。`SET_WaterInteraction` 引用 `ASM_KitchenParameters` 的 `water_*` 项。`PREF_Water_KitchenStation` 提供龙头和排水盆，锅杯已改为开口模型；水量、重力轨迹和受限晃动由项目脚本计算，**不是 SPH/FLIP 流体、浮力或重量反馈**。不要挂载供应商的平面反射相机脚本，不导入其演示场景。见 `Docs/KitchenWaterAndSky.md`。
- Kitchen Props Free / Food Pack Mixed 的精选源资源在 `Assets/ThirdParty/`，16 个归一化道具 Prefab 在 `_Project/Prefabs/Props/`，公共布置为 `PREF_Kitchen_Dressing`，位于方案槽位之外。9 件新增食品/包装及空马克杯可抓取；新马克杯和果汁盒不参与倒水，保留原有水杯/锅。尺寸按 `ASM_KitchenParameters` 的 `prop_*` 登记；固定摆件参加 GI，改位置后需重烘焙。`KitchenPropsAuthoring` 只重建摆设，不重建房间；配置会恢复默认摆放。详见 `Docs/KitchenDressing.md`。
- OpenXR 的 **控制器 interaction profile 已为 StandaloneWindows64 启用**（Oculus Touch / Meta Quest Touch Plus / Touch Pro / KHR Simple），Android 侧有意保持全关。
- 此前 Windows 测试机的活动 OpenXR 运行时为 Virtual Desktop（`HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` 指向 `C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`，runtime 名 `VirtualDesktopXR (Bundled)`）。该次未连接是因为 VDServer 未运行、头显未连接；这个历史状态不代表当前执行主机的运行时，排查前先确认主机与连接方式。
- 2026-10-16 在 Mac 编辑器实测 Play：自动初始化已开、OpenXR Loader 已配置，但无活动 Loader、XR 显示子系统或真实设备，输入只有模拟头显/手柄。VD 的 Mac 连接只提供桌面串流，PCVR 串流需要 Windows（见 README §7）。真机直接 Play 需在 Windows 运行编辑器，先连接对应运行时和头显，再关闭自动 XR 模拟器；不要通过切 Android、修改 Input Handler 或重复安装包来修复此平台限制。
- **注意与 PRD Final 的偏差**：TECH-02 的批准环境是「Quest 3 经 Quest Link」，VD 串流可作调试，但其结果不满足 TECH-02 验收；正式验收仍须走 Quest Link。
- 编译目标当前是 StandaloneWindows64，双端 OpenXR Loader 均已配置。2026-10-27 本地整合 main，编译与 EditMode 133/133、PlayMode 28/28 通过；这台 Mac 缺少 Windows Build Support，本次未做 Windows Player 构建或 Quest Link 验收。

- 运行时按 `Runtime/Review`、`Runtime/Interaction`、`Runtime/Water`，主线新增 `Core`、`Feedback`、`Storage`、`UI` 和 `Debug` 分组，类名、命名空间、脚本 GUID 与三个程序集不变。项目作者工具统一在 `Assets/_Project/Editor/`：`KitchenAuthoring` 负责公共路径、资源加载、场景检查和 Prefab 编辑生命周期；`KitchenGrabAuthoring` 是抓取配置的唯一入口，`KitchenLightingAuthoring` 负责天空，`WaterVesselMeshBuilder` 负责容器网格。新增模块前先看 `Docs/CodeStructure.md`，不要恢复根目录 `Assets/Editor` 或新增一套通用服务框架。

- 锅杯及新增食品共 11 件可抓物使用 `CollisionConstrainedGrabTransformer` / `HeldPropSweep`，按整个物体的保守定向包围盒扫掠平移与旋转；受阻时实物/实手停住，虚影透过遮挡显示，释放抑制虚假抛掷。配置入口为 `KitchenGrabAuthoring`；重建道具时必须保留此调用，不要换成未经约束的 XRI 跟随或在约束后添加插值。`COL_GrabSweepProbe` 必须禁用且不加入 XRI colliders；接触余量读取登记项 `grab_contact_skin`。详见 `Docs/HeldPropCollision.md`。整合前 77 条 EditMode、8 条 PlayMode 通过；2026-10-27 整合 main 后 133 条 EditMode、28 条 PlayMode 通过，未做 Quest Link 真机验收。

- 2026-10-28 用户已确认参考板，厨房物品优先。Comfy Cloud 实际完成 9 个单图 Pixal3D 和 1 个四视角 Pixal3D 任务；7 件厨房物品、Generic 身体绑定实验、明确尺寸的柜体模块共 9 类 Prefab 位于 `Prefabs/Generated/`。两份可复用工作流、三类预设、参数绑定、原始模型 / 独立 PBR / 预览 / 任务编号与实测表分别在 `Tools/ComfyCloud/`、`ArtSource/KitchenAssets/`。四视角柜体虽然实际生成并通过临时 FBX 导入，但褶皱外观不合格，已排除厨房；不要把技术导入通过写成美术验收。
- `SCN_Kitchen_Cooking.unity` 是用户新要求的独立烹饪样例，用 `XR Apartment/Cooking/Open Cooking Kitchen` 打开，不接正式 Core / 反馈 / CSV。保留原水站锅杯，新增制作工具管理的烹饪桌、碗、煎锅与搅拌器。支持盘子 8 块破碎、果蔬预切、打蛋 / 搅拌 / 倾倒 / 受热变熟与变焦 / 重置；不是任意实时切割、SPH / FLIP 或热传导。`CookingProgress` / `EggBatch` 是纯逻辑，`Runtime/Cooking/` 管适配，`KitchenCookingAuthoring` 管制作；所有可抓物继续用统一 `KitchenGrabAuthoring`。切片保留供烹饪，非食品碎片到期回收；切分时子物品继承完整件的 XR 管理器，不能依赖多场景下的默认管理器。
- 人物为 20 骨骼 Generic 实验，4 个基础身体动作、实际网格变形和 UV 接缝检查通过，肩部 / 衣物仍需生产级拓扑精修，面部口型未实现。柜体是本地制作的空心结构、层板及独立铰链门，名义 0.6×0.6×1m；生成代理不作空间证据。详细状态见 `Docs/KitchenAssetPipeline.md` 和 `ArtSource/KitchenAssets/Validation/`。
- 烹饪场景单独使用 `Art/Cooking/LGT_KitchenCooking.lighting` 与 `Scenes/SCN_Kitchen_Cooking/` 烘焙结果；修改静态环境后只重烘焙此场景，不复用或覆盖正式评审的旧摆件阴影。Mac 批处理入口使用 Gaussian 滤波，避免本轮 OIDN 降噪的内存压力；该场景的二进制 `LightingData.asset` 按精确路径走 LFS。
- 此扩展验证：Python 12/12、Blender 7/7、EditMode 138/138、PlayMode 42/42；独立烘焙后隔离场景复测 1/1（支撑、真实 XRI 抓取 / 打蛋、50mL 保留、重置）通过，19 个原场景 / 光照文件校验不变。正式评审场景、Build Settings、Packages 和 XR 输入配置未改；新增空间尺度登记在 `ASM_KitchenParameters`，都是 prototype assumption。Windows Player / Quest Link 尚未验收。临时模型与日志已归档 `Logs/KitchenAssetPipeline/`，不入库；本轮内容尚未提交或推送。
- 同日用户要求的完整西红柿鸡蛋流程已在 `SCN_Kitchen_Cooking` 实现：冰箱储物 / 关门禁止隔门取物、番茄四片再切十六块、磕蛋入碗 / 搅拌、开火放锅 / 倾倒蛋液、凝固成有体积的蛋块 / 实际铲头翻炒、从板或备料碗重力倒入番茄、成菜 / 烧焦 / 全部重置。番茄 v002 派生自实际 Pixal3D v001；冰箱、锅铲、切菜板和熟蛋模型本地 Blender 制作，不算新增 Cloud 任务。`KitchenRecipeAuthoring` 是 `KitchenCookingAuthoring` 的 partial 制作文件；纯逻辑 `TomatoEggRecipe` 与 Runtime/Cooking 各组件保持职责分离，不接正式 Core。详情 `Docs/TomatoEggRecipe.md`。
- 菜谱使用 `SET_TomatoEggCooking` 的 12 / 90 秒配置，保留旧独立资产的 12 / 32 秒。小块在板 / 碗拿起时承载，倾斜后恢复物理，进入锅口后按实际份量交给池化呈现，不支持任意切面或真实多相流体；追踪虚影不算切菜 / 翻炒。所有可抓物继续走 `KitchenGrabAuthoring`，只收同一刚体实体碰撞体；切分 / 倾倒释放 / 重置通过 XRI `SetTargetPose` 同步，避免延迟姿态覆盖。冰箱沿用固定铰链运动学门，开启可选扫掠探针，原门无配置时行为保持原样。
- 菜谱最终回归 EditMode **145/145**、PlayMode **47/47**（新增场景检查 5/5）通过；实际完整 XRI 流程得到 50mL 蛋液、14 块 / 110.22831g 番茄入锅，成菜、变焦、重置通过。70 帧局部锅中呈现均值 0.01105ms，仅为编辑器 CPU 局部采样，不是 VR 帧率。19 个原评审场景 / 光照校验一致，工程 / 包 / XR / Build Settings 无改动。精选证据 `ArtSource/KitchenAssets/Validation/Recipe/`，完整日志 `Logs/TomatoEggRecipe/` 不入库；Windows Player / Quest Link 仍未验收，内容仍未提交或推送。
- 2026-10-29 食材视觉精修：实际新增两个单图 Pixal3D 任务（番茄切片 `a85e7bdd-21ac-4286-a904-bbbd4cd317db`、熟蛋 `691f27f7-8e38-47e1-88d7-65d5fe2b828f`，生成于前一晚），总任务数 **12**。当前菜谱可见切片 / 小块和凝固蛋块已替换为生成网格及独立 PBR；`Recipe/v001` 的程序化熟蛋仅留作来源历史。新母版 / 游戏版在 `tomato_slice/v001/`、`scrambled_egg/v001/`，适配网格在 `Recipe/v002_food_visuals/`，Unity 资源在 `Art/Cooking/FoodVisuals/`。保留既有交互层级、碰撞和食材数量；生成切片只适配可见范围，不用可见体积反推质量。
- `KitchenFoodVisualAuthoring` 继续作为 `KitchenCookingAuthoring` 的 partial，菜单 `Apply Generated Food Visuals` 只更新食材美术；完整重建也会接入此版本。12 件熟蛋池化呈现，每件近档 4000 / 远档 1200 三角形，两档均保留生成贴图和烧焦变色。锅中番茄按制作指定的切面朝上呈现，实际铲头仍驱动翻炒。此版 EditMode **145/145**、PlayMode **47/47**，完整流程 **50mL 蛋液、16 块 / 120g 番茄** 入锅至完成、烧焦和重置通过；70 帧锅中呈现局部 CPU 均值约 0.01656ms，不是整帧 / VR 性能。详细来源和当前画面见 `Docs/FoodVisualUpgrade.md`、`ArtSource/KitchenAssets/Validation/FoodVisuals/`。原评审场景与工程设置未动；仍未提交 / 推送，也未做 Windows Player / Quest Link 验收。

- 2026-10-29 已将 `SCN_Kitchen_Cooking` 的烹饪物品从餐桌搬进厨房小房间：左侧水槽 / 锅杯，中间切配，右侧燃气灶，食材仍在冰箱。实际新增 8 个单图 Pixal3D 任务，累计 20 次；生成厨具和冰箱门配合 Kitchen Props Free 陶瓷碗 / 杯。生成箱体大平面未通过外观检查，当前外壳 / 内胆为本地硬表面重建；水槽和弯管龙头也本地制作，不能计作 Cloud 生成。加工版本 `Recipe/v003_cookware/`、Unity `Art/Cooking/Cookware/`，来源 / 截图 / 验证见 `Docs/KitchenRoomAndCookware.md` 和 `Validation/KitchenRoom/`。`KitchenCookwareAuthoring` 仍是原制作类的 partial；水站、锅杯用烹饪专用副本，正式评审资产不改。`KitchenBurnerFlame` 独立响应热源开关，空炉可点火，关火 / 禁用 / 重置立即熄灭；蒸汽与食物反馈分开。最新 EditMode 145/145、PlayMode 49/49；完整菜谱 50mL 蛋液、16 块 / 120g 番茄及真实接水通过。两项旧抓取测试补上渲染输入采样等待，强制物理追赶复现由 0/2 变为 2/2，运行时抓取与断言不放宽。19 个受保护评审文件、Packages / XR / Build Settings 未改；尚未提交 / 推送，Windows Player / Quest Link 未验收。

- 厨房扩展的提交按版本控制、生成流程与来源、交互代码、美术资源、场景配置、测试和文档分类，目标分支为 `feature/hzc`。上面各阶段的“尚未提交 / 推送”为当时的历史状态，当前同步状态以 Git 为准；分支推送不代表 Windows Player / Quest Link 已验收。

- 2026-10-30 用户确认浅橡木 / 暖白家具外观后，实际新增 4 次单图 Pixal3D，累计 24 次。`KitchenFurnitureAuthoring`（原制作类 partial）在独立烹饪场景替换餐桌、四把椅子、吊灯及冰箱可见模型；原环境 Prefab 不改。桌椅保留生成网格 / PBR，校正桌面与四脚接地；灯具保留生成木质顶帽，空心灯罩 / 吊线 / 顶盘本地制作；整机冰箱原始大平面 / 贴图不合格，当前精确箱体 / 门体 / 把手 / 层板 / 门内格为本地硬表面重建，沿用旧交互和碰撞。不得称为未经修整的 Cloud 成品。资源 `Art/Cooking/Furniture/`、`Prefabs/Furniture/`，加工 `Recipe/v004_furniture/`，来源和证据见 `Docs/KitchenFurniture.md`、`Validation/Furniture/`。只重烘焙烹饪场景。最新 EditMode 145/145、PlayMode 50/50，包含真实角色胶囊绕行餐桌 / 厨房前方通道及原完整菜谱；正式评审、Packages / XR / Build Settings 未改。Windows Player / Quest Link 未验收；家具集成与后续清理的提交 / 同步状态以 Git 为准。

2026-10-30 家具集成之后清理：Unity 依赖与代码路径核对后删除 98 项无用导入资产（66 张纹理、27 个材质、3 个旧模型、2 份一次性诊断脚本），约 86.48 MiB，原始 ArtSource / 工作流 / 授权保留。普通与特殊资产导入共用 `KitchenPreparedAssetImporter.ImportFiles`，只保留 FBX 依赖与清单 PBR；当前冰箱由家具工具直接应用，旧中间外观和旧熟蛋不再自动导入。291 个场景 / Prefab / 登记参数 / 工程与 XR 配置校验不变，Runtime 源码未改。实际刀具、人物、柜体重导入和两次冰箱外观应用检查通过。EditMode 145/145；最新整批 PlayMode **49/50**，完整菜谱切片曾失败、同一最终源码下单独重跑 **1/1** 通过，批量稳定性未解决。独立搅蛋测试确认转移后停用蛋壳夹具，避免碎壳碰撞干扰；原断言不变。详情 `Docs/ProjectCleanup.md`，诊断与恢复备份在忽略的 `Logs/ProjectCleanup/`；提交 / 同步状态以 Git 为准。

2026-10-31 整屋重置与城市窗景：独立烹饪场景的台面按钮为 `RESET ROOM`，`PREF_Cooking_RoomReset` 缓存同场景外部抓取物与水体，订阅 `KitchenCookingStation.ResetStarting`，先归位外部物品，再由工作台恢复食材 / 菜谱 / 冰箱。水流通过 `KitchenWaterSystem.ClearFlow` 清空后只复位本场景容器；暂停、连续触发、XRI 取消与再抓、其他场景隔离已验证，玩家不传送。新增 `KitchenRoomAuthoring` partial 与完整重建接入；城市天空为 CC0 的 Poly Haven Joburg Central Sunset 4K EXR（固定旋转 120°），原文件与来源在 `Assets/ThirdParty/PolyHaven/JoburgCentralSunset/`。只改烹饪天空与其独立烘焙，正式评审环境 / AllSky / Packages / XR / Build Settings 未改。EditMode 145/145，整批 PlayMode 51/52（旧完整菜谱放锅检查失败），重置最终专项 2/2 通过，未解决旧物理操作测试波动。226 个受保护原文件一致；证据和限制见 `Docs/RoomResetAndCity.md`、`Validation/RoomResetAndCity/`。Windows / Quest Link 未验收；此处为当时状态，后续同步以 Git 为准。

2026-10-31 键盘穿模修复：原 XRI 模拟器的 FPS 平移直接写模拟追踪位置，绕过角色移动碰撞；柜体 / 墙面碰撞体有效。项目副本改用 `Runtime/Interaction/KitchenInteractionSimulator` 派生组件，只把 FPS 键盘 / 滚轮走路交给现有 `XRBodyTransformer` 和 `CharacterControllerBodyManipulator`，保留旋转、点击与独立设备姿态编辑；自动模拟器仍仅 Editor，真实追踪头部不钳制。模拟器 Prefab 仅换脚本 GUID，原字段完整保留；未改角色尺寸、家具碰撞、场景、烘焙、包或 XR 配置。实际模拟器输入回归 4/4，EditMode 145/145、整批 PlayMode 56/56，包含抓取 / 重置 / 完整菜谱；本次菜谱通过不代表旧物理操作波动已解决。617 个受保护原文件一致，详见 `Docs/KeyboardLocomotion.md`；诊断日志在忽略的 `Logs/KeyboardCollision/`。这两项当时未提交 / 推送；后续随移动模式一起整合，实际同步状态以 Git 为准，Windows / Quest Link 未验收。

2026-10-31 玩家移动设置：共享 `PREF_XR_KitchenRig` 接入 `KitchenLocomotionSettings` 和内嵌 `PREF_UI_LocomotionSettings`。左手 Y / Menu、键盘 F8 打开面板，初次默认瞬移，PlayerPrefs 记住选择；平移时左摇杆按视线方向走路，右摇杆两种模式下都保留 45° 分段转向和瞬移。F8 避开模拟器原有 M 菜单模拟。打开设置仅暂时阻止移动，不暂停或操作 Core。原瞬移区域的 Default 掩码已改为现有第 31 位 Teleport，并给真实 `SM_Ground` 增加 `KitchenTeleportationArea`；`KitchenTeleportationProvider` 检查支撑、胶囊落点及直线路径，确认前重查，取消遗留请求。没有导航网格绕行、自由攀爬或真实头部碰撞钳制；不替代 SAFE-01。作者入口 `KitchenLocomotionAuthoring.Configure`，旧菜单转交同一偏好状态，Runtime 不依赖 Samples 程序集。最终 EditMode 145/145、PlayMode 61/61，移动检查 5/5、两个场景接线 / missing script / 四个 Prefab 重复配置检查通过。614 个受保护文件不变，只改 3 个预期共享 Prefab，未重写场景或烘焙。固定落点 256 次局部 CPU 均值约 0.00237ms / 最大 0.0173ms / 分配 0B，不是 VR 帧率。详见 `Docs/LocomotionModes.md` 与 `Validation/Locomotion/`；Windows Player / Quest Link 未验收。用户已授权将移动设置连同之前的房间重置、城市窗景和键盘防穿模分类提交并推送到 `feature/hzc`；当前实际同步状态以 Git 为准。

2026-10-31 主场景配置：用户选择带城市建筑天空盒的 `SCN_Kitchen_Cooking` 为主场景。`KitchenRoomAuthoring.ConfigureMainScene` 将其放在构建列表第 0 项，保留正式评审为第 1 项；`Open Main Kitchen` 打开主厨房并设置本机会话 Play 入口。Unity 的 Play 覆盖不会自动跨编辑器会话保存，因此启动时按首个构建场景恢复一次，SessionState 防止后续脚本重编译覆盖显式选择；`Open Kitchen Review Scene` 清除 Play 覆盖以便正式评审调试。新开 Unity 2022 编辑器后的恢复、评审菜单清除及会话保护、实际从评审编辑状态进入城市厨房 Play（buildIndex=0、城市天空、Rig 与整屋重置均存在）已检查，控制台 0 error。场景、Prefab、烘焙、Packages、XR 与其余工程设置未改，仅 EditorBuildSettings 调整场景顺序；Windows64 目标保留。该主场景配置已获用户本次提交与推送授权，实际同步状态以 Git 为准；不改变烹饪与正式 Core / 反馈 / CSV 的职责，也不代表 Windows Player 或 Quest Link 验收。

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
