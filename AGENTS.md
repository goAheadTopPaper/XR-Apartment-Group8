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

- `activeInputHandler: 1` = **Both**（旧 Input Manager 与新 Input System 同时启用）。收紧为「仅新系统」是 `2`。**不要因为"看起来应该是 2"就顺手改**。
- PRD §12 的 25 条 Approved 需求（FR/UX/ACC/SAFE/MOD/TECH/EVAL）在 Unity 侧 **0 条已实现**。
- `Assets/_Project/` 下除 `Scenes/Main.unity` 外全是空目录（`.gitkeep` 占位）：没有脚本、没有 asmdef、没有 Prefab、没有自定义 `.inputactions`、没有测试程序集。
- `Main.unity` 仍是 Starter Assets 初始布局（XR Origin + EventSystem + Ground + Teleport Area/Anchor + 可抓取 Cube），厨房灰盒还没建。
- `ProjectSettings/TagManager.asset` 层 3–31 仍全空；XRI `InteractionLayerSettings` 目前只有 `Teleport` 一层（Starter Assets 导入时加的）。
- OpenXR 的 **控制器 interaction profile 已为 StandaloneWindows64 启用**（Oculus Touch / Meta Quest Touch Plus / Touch Pro / KHR Simple），Android 侧有意保持全关。**但本机没装任何 XR 运行时**（无 Meta Horizon/Oculus 桌面端、无 SteamVR），Play 模式实测 `XRSettings.enabled=False`、`isDeviceActive=False`——能进普通模式跑，进不了 XR。这条限制解除前，任何「手柄有没有反应」的结论都不可验证。
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
