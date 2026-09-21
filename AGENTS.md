# AGENTS.md — 给 AI 编码助手的项目指令

面向在本仓库工作的 agent 会话。人类向的完整说明看 `README.md`，本文件只收**必须遵守的约束**和**已验证的工具入口**。

## 硬性约束

- **引擎锁死 Unity 2022.3.62f3**。不要升级、不要提议升级到 Unity 6、不要安装 `com.unity.ai.assistant*`。这是明确决定：官方内置 MCP 要求 Unity 6，本项目改用社区方案 MCP for Unity 10.2.0（支持 2021.3–6.x）。
- **不要修改 `Assets/Samples/**`**。整目录是 XRI 官方示例，升级 XRI 时整目录替换。需要改其中的输入配置或预制体时，**复制**到 `Assets/_Project/Settings/` 或 `Prefabs/` 再改。
- **提交、推送、切构建目标（Switch Platform）、改写已推送历史**这类影响全组状态的操作，先征求用户同意，一次授权不代表长期授权。
- 不要把 keystore、口令、`.qoder/settings.local.json`、日志、崩溃转储写进任何提交。`.gitignore` 已拦截，但 `git add -f` 会绕过它。

## 当前真实状态（改设置前先看这里，别照旧文档假设）

- `activeInputHandler: 1` = **Both**（旧 Input Manager 与新 Input System 同时启用）。收紧为「仅新系统」是 `2`。**不要因为"看起来应该是 2"就顺手改**，会影响第三方资源。
- XRI 的 Interaction Layers（`Wall / Furniture / Door / Item / Player`）尚未定义：`ProjectSettings/TagManager.asset` 层 3–31 全空。
- `Assets/_Project/` 下除 `Scenes/Main.unity` 外均为空目录（靠 `.gitkeep` 占位），自有运行时代码还没有。
- 启动场景 `Assets/_Project/Scenes/Main.unity` 是 Starter Assets 初始布局，还没有公寓场景内容。

## 必须入库 / 绝不入库

**必须提交**：`Assets/**/*.meta`（与资源同一个提交）、`ProjectSettings/`、`Packages/manifest.json` + `packages-lock.json`、`Assets/XR/`、`Assets/XRI/`（这两个目录是 ProjectSettings 引用到的实际对象，漏提交会让队友加载器列表为空）。

**绝不提交**：`Library/ Temp/ Logs/ UserSettings/ Build/ Builds/ .utmp/`、`*.apk *.aab *.unitypackage`、`*.keystore *.jks *.pem *.key .env secrets.json`、`*.log *.dmp *.crash sysinfo.txt`、IDE 生成物、`.qoder/settings.local.json`。

Unity 会丢弃无资源的空文件夹，需要在仓库里保留的空目录放一个 `.gitkeep`。

## Unity MCP 工具

47 个 `mcp__unity__*` 工具。**前提是 Unity 编辑器处于打开状态**，Bridge 在 `127.0.0.1:6400`。

- 工具报 `No Unity Editor instances found` 时，先确认编辑器是否关着（`tasklist | grep -i unity`、`netstat -ano | grep 6400`），**不要怀疑包版本或配置**。编辑器正常退出后 `Temp/` 会被删除且无 `UnityLockfile`，可用来区分正常关闭与崩溃。
- 改场景 / 预制体 / 工程设置优先用这些工具，而不是手写 YAML：`manage_scene`（`get_hierarchy` 支持 `parent` + `max_depth` 逐层展开，根节点 `childCount` 会截断）、`manage_gameobject`、`manage_components`、`manage_prefabs`、`manage_build`、`manage_physics`、`manage_probuilder`、`execute_code`。
- 写完脚本用 `refresh_unity`（`mode=force`）触发导入，再用 `read_console`（`action=get`, `types=["error"]`）确认 0 error。域重载期间工具调用会失败，等 `ready` 再试。
- 注册位置：MCP server 写在**用户级** `~/.qoder/settings.json` 的 `mcpServers` 里。IDE 的 agent 会话不读仓库内的 `.qoder/settings.local.json`，写在那儿不生效。
- batchmode 命令行（README §10）与打开的编辑器互斥，跑之前先关编辑器。`-logFile` 的目录名不能以点开头，用 `Logs/`。

## 修改约定

- 提交信息用中文 + Conventional Commits 前缀，正文写清「为什么」而不是「改了什么」。
- 一次提交只含一个关注点，按依赖顺序排：VCS 配置 → 引擎/厂商设置资产 → vendored 资源 → 项目内容 → 文档。
- 可交互物一律做成 Prefab 放 `Assets/_Project/Prefabs/`，场景里只放实例。客厅/卧室/厨房/卫生间/阳台拆成 additive 子场景——`.unity` 是整体序列化文本，两人同改一个场景必然冲突且无法手工合并。
- FBX 导入按 `1 unit = 1 m`，人物眼高 1.6–1.7 m。
- 命名 `前缀_类型_描述`：`PREF_Door_Front`、`MAT_Kitchen_Cabinet`、`TEX_Wall_Paint_01`、`SCN_LivingRoom`。
- 新增需要走 LFS 的后缀时改 `.gitattributes`；已经提交过的文件需要 `git lfs migrate`，属于影响全组的动作，先问。

## 未决事项

- 是否需要多人同场联网？决定架构（网络方案 + 交互状态同步），越晚定改动越大。
- 组里实际有哪些头显？决定 Windows 端 OpenXR Profile 勾选与测试分工。
