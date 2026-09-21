# XR-Apartment-Group8

COMP5424 Scenario C 的课程小组项目：一个**厨房设计评审原型**（Kitchen Design Review），不是通用 VR 公寓漫游。评审者在单一代表性厨房里比较「冰箱 × 柜体」的三种组合，通过相同的日常任务理解空间取舍，并提交可定位的结构化反馈。产品定义、范围与验收以仓库外的 **Lean PRD v0.1** 为唯一权威（见 §12）。

> **给新组员：先看「clone 后第一次打开」和「版本控制规范」两节，再动 Unity。**

---

## 1. 引擎与工具链

| 项 | 值 |
| --- | --- |
| Unity 版本 | **2022.3.62f3**（`C:\Program Files\Unity\Hub\Editor\2022.3.62f3`），**锁死，不要升级** |
| 渲染管线 | Built-in RP。PRD 没有要求更换管线；迁移 URP 属于团队另行决策（会牵动全部材质与构建验证），不在 MVP 路径上 |
| 运行环境 | **Meta Quest 3 经 Quest Link 连 Windows PC**（PRD §3.3、TECH-02）→ 构建目标 `StandaloneWindows64`。没头显的组员用 XR Device Simulator 在编辑器里跑 |
| 会话形态 | 单一参与者、约 20 分钟、自导（不依赖引导员持续讲解）、有限实体 play area + Teleport 覆盖虚拟厨房（PRD §3.3、D09） |
| 输入系统 | 以 New Input System 为准。实测 `ProjectSettings.asset` 里 `activeInputHandler: 1`，即 **Both（旧 Input Manager 同时启用）**；若要收紧为「仅新系统」需改为 `2`，会重启生效并可能影响用到 `UnityEngine.Input` 的第三方资源 |
| 数据与记录 | 匿名本地 JSON/CSV（PRD §11.3、D13）。**不建账号、不接云端数据库**，多人协作与云端已明确 Deferred |
| 模型来源 | Blender → Unity，统一米制 `1 unit = 1 m`（PRD §7.1、TECH-01），由 E 维护几何与参数登记 |
| 脚本后端 | Windows: Mono（MVP 用这个）。Android 的 IL2CPP / ARM64 / Min SDK 29 / Vulkan+GLES3 工程里已配好，但**一体机出包不在 Phase 2 MVP 范围**，见 §8 |
| 版本控制 | Git + **Git LFS**（必装，见 §5） |

### Packages（`Packages/manifest.json`）

| 包 | 版本 | 说明 |
| --- | --- | --- |
| `com.unity.inputsystem` | 1.19.0 | New Input System |
| `com.unity.xr.management` | 4.7.0 | XR Plug-in Management |
| `com.unity.xr.openxr` | 1.17.1 | OpenXR 加载器（Windows 与 Android 均已启用） |
| `com.unity.xr.interaction.toolkit` | 3.5.0 | XRI，交互框架（Grab / Teleport / Locomotion） |
| `com.coplaydev.unity-mcp` | 10.2.0 | MCP for Unity，让 AI 编码助手直接操作编辑器（走 OpenUPM scope `com.coplaydev`） |
| `com.unity.toolchain.win-x86_64-linux-x86_64` | 2.0.11 | Linux 交叉编译工具链（本项目暂未用到，保留） |

XRI **Starter Assets** 已按 Unity 的路径约定拷贝进仓库：
`Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/`（XR Origin 预制体、`XRI Default Input Actions`、传送区/锚点、可交互示例）。队友 clone 后**无需**再手动 `Import Sample`，也**不要改动这个目录**（见 §6）。

---

## 2. clone 后第一次打开

```bash
git lfs install                      # 每台机器一次；跳过会导致拉到 LFS 指针文本而不是真资源
git clone https://github.com/goAheadTopPaper/XR-Apartment-Group8.git
cd XR-Apartment-Group8
git lfs pull                         # 确认二进制资源已下载
```

1. Unity Hub → **Add project from disk** → 选择仓库根目录 → 用 **2022.3.62f3** 打开（不要用别的版本，否则 `ProjectVersion.txt` 会被改写、`Library/` 需全量重建）。
2. 首次会联网解析 UPM 依赖（含 OpenUPM 上的 `com.coplaydev.unity-mcp`）并重建 `Library/`，几分钟属正常。
3. 打开 `Assets/_Project/Scenes/Main.unity`，接好头显（SteamVR 需先在后台运行）按 Play。
4. 若报 `The following build scene(s) could not be loaded` 或 XRI 相关 missing script：先 `git lfs pull`，再 `Assets → Reimport All`。

---

## 3. 目录结构

```
Assets/
  _Project/            ★ 项目自有内容，日常开发都在这里
    Scenes/            场景（Main.unity 是启动场景，Build Settings 第 0 位）
    Prefabs/           预制体（优先在预制体里改，不要直接改场景里的实例）
    Scripts/           运行时脚本
    Materials/         材质
    Settings/          项目自有配置资产（自定义 InputActions、benchmark 组合定义、模型参数登记表）
    XR/                自定义 XR Provider / 交互层配置
  Editor/              编辑器工具脚本（XRProjectBootstrap / VRSceneBootstrap）
  XR/                  ★ XR Plug-in Management 生成的资产（Loader 列表、OpenXR Package Settings）
  XRI/                 ★ XRI 生成的设置资产（Interaction Layer Mask / Runtime / Simulator Settings）
  Samples/             XRI Starter Assets（只读参考，勿改动，升级时整目录替换）
ProjectSettings/       工程设置（版本控制）
Packages/              manifest.json + packages-lock.json（都要提交）
UserSettings/ Logs/ Temp/ Library/   ← 本机私有，已 gitignore，不要提交
```

★ 标记的三个 `Assets/` 子目录**必须入库**：`Assets/XR/` 与 `Assets/XRI/` 下的 `.asset` 是 `ProjectSettings/EditorBuildSettings.asset` 和 XRI 运行时引用的实际对象，删掉或漏提交会让队友打开工程后加载器列表为空、交互层丢失。

---

## 4. 版本控制规范

`.gitignore` 已配好，规则汇总如下；提交前用 `git status` 复核。

**必须提交**
- `Assets/**/*.meta`（与它的资源**同一个提交**；只提交资源不提交 `.meta` 会让队友 GUID 错乱、引用断裂）
- `ProjectSettings/`、`Packages/manifest.json`、`Packages/packages-lock.json`
- `Assets/XR/`、`Assets/XRI/`、`Assets/Samples/`

**绝不提交**
- `Library/`、`Logs/`、`Temp/`、`UserSettings/`、`obj/`、`Build/`、`Builds/`、`.utmp/`
- 构建产物：`*.apk`、`*.aab`、`*.app`、`*.unitypackage`
- **密钥与签名材料**：`*.keystore`、`*.jks`、`*.p12`、`*.pem`、`*.key`、`.env`、`secrets.json`、`credentials.json`、`google-services.json`（Quest 出包的签名口令请放在仓库外的本机文件里，见 §8）
- 崩溃转储与诊断输出：`*.log`、`*.dmp`、`*.crash`、`sysinfo.txt`、`MemoryCaptures/`、`Recordings/`
- IDE 生成物：`*.sln`、`*.csproj`、`.vs/`、`.idea/`、`.vscode/`
- 各人本地的 Qoder 配置：`.qoder/settings.local.json`（含本机绝对路径，不共享；`.qoder/` 下的 rules / repowiki 等共享内容照常入库）

**其他**
- Unity 会丢弃无资源的空文件夹。需要在仓库里占位的空目录（如 `Assets/_Project/Scripts/`）放一个 `.gitkeep`。
- 已经误提交过的文件，`git rm --cached <file>` 只取消跟踪、保留本地文件。
- **如果密钥已经被推送过，改 .gitignore 不足以撤销**：需要重写历史并强制推送，属于影响全组的操作，必须先在群里确认再做。

---

## 5. Git LFS

`.gitattributes` 已配置：可读的 Unity YAML（`.unity` / `.prefab` / `.asset` / `.mat` / `.meta`）与 C# 保持**文本**以便看 diff 和合并；不可读的二进制美术资源走 **LFS**。

走 LFS 的类型：贴图（`png jpg tga tif psd exr hdr dds ktx2 webp svg …`）、模型（`fbx obj blend ma mb max abc usd* glb/gltf`）、音频（`wav mp3 ogg flac aac …`）、视频（`mp4 mov webm …`）、字体（`ttf otf`）、二进制包（`dll so a dylib zip 7z`）。

常用命令：

```bash
git lfs install --with-git   # 新机器初始化（每位组员一次）
git lfs ls-files -s          # 查看当前走 LFS 的文件及大小
git lfs pull                 # 拉取全部 LFS 对象
git lfs checkout             # 指针文件还原为真实内容
```

注意：
- 首次启用 LFS 时仓库里只有 Starter Assets 的约 **7 MB**（28 个文件，最大的是 `Concrete_*.tif` 2 MB）。这些文件目前还是未跟踪状态，**首次提交时会自动走 LFS**，不需要历史迁移；提交后用 `git lfs ls-files` 核对数量应为 28。
- **美术资源进来后请第一时间确认 LFS 是否生效**：`git lfs ls-files` 能看到该文件＝对了；如果仓库体积/流量涨得异常，多半是有人在没装 LFS 的机器上提交了二进制。LFS 配额与仓库可见性有关，资源量大之前先跟管理员确认一次。
- 新增需要走 LFS 的后缀，改 `.gitattributes` 后必须 `git lfs migrate import --include="*.xxx" --include-ref=refs/heads/<branch>` 处理已经提交过的文件——先和组长说，别独自做。

---

## 6. 协作流程

- **分支**：`main` 保持随时可运行，功能开发走 `feature/<名字>`（当前有 `feature/hzc`、`feature/xjx`、`feature/mzh`、`feature/ycy`、`feature/yzk`、`feature/zh`）。合并用 PR，不要直接 push `main`。
- **Prefab 优先**：冰箱 archetype（F2/F3/F4）、两类柜体策略（Proud / Flush-oriented）、可抓取物品、反馈面板各自做成 Prefab 放 `Assets/_Project/Prefabs/`，场景里只放实例。benchmark 之间靠切换实例，不靠改场景。
- **不要扩回完整公寓**：PRD 已把完整公寓、颜色定制、真实重量物理、电线/插座、多人联网列为 Deferred/Rejected（D02/D03、§5）。范围再次膨胀是首要风险 R01。
- **不要改 `Assets/Samples/**`**：整目录是官方示例，需要改其中的输入配置或预制体时，**复制**到 `Assets/_Project/Settings/` 或 `Prefabs/` 再改（升级 XRI 时 `Samples/` 会整目录替换）。
- **单位与尺寸**：FBX 导入 `1 unit = 1 m`，人物眼高约 1.6–1.7 m。**任何尺寸、clearance、reach 的主张都必须能对上登记的模型参数**（MOD-01、§7.2）；「看起来够宽」不是证据，视觉印象不能作为空间结论的依据。
- **命名带语义角色，不带 benchmark 编号**：`PREF_Fridge_F2`、`PREF_Cabinet_Proud_Wall`、`MAT_Kitchen_Counter`、`SCN_Kitchen_Review`。可交互物要能通过语义角色被找到（如 `FridgeDoor` / `FridgeDrawer` / `CounterSurface` / `StorageZone`），**不要按 B1/B2/B3 分别命名对象**——FR-03 要求三个 benchmark 执行完全相同的动作链，目标一旦按组合写死，这条就无法满足。
- 遇到 `ProjectSettings/`（尤其 `TagManager.asset`、`EditorBuildSettings.asset`）冲突时，优先在群里对齐后由一人重设，不要手工拼接 YAML。

---

## 7. 运行与调试（Quest Link）

构建目标固定 `StandaloneWindows64`——Quest 3 只当显示器，画面由 PC 渲染（PRD TECH-02 批准的就是这个环境）。

1. 编辑器里 `File → Build Settings` 确认平台是 **Windows, Mac, Linux → Standalone Windows64**，场景 `Assets/_Project/Scenes/Main.unity` 在第 0 位。
2. Quest 3 上启动 Link（有线优先，无线对带宽敏感），连到本机；Meta Horizon 里允许连接。
3. Unity 编辑器按 **Play**，头显即显示画面。当前工程的最小验收：控制器能抓取示例 Cube、能沿 Teleport Area 传送。
4. **没头显的组员**：`Window → Package Manager → XR Interaction Toolkit → Samples → XR Device Simulator`，用键鼠模拟手柄摆场景；但空间尺度、通行和舒适判断必须最后在头显里验，模拟器给不了 1:1 尺度感。
5. 不要引入 SteamVR / WMR 等其他运行时路径作为测试基线——PRD 只批准了 Quest 3 + Quest Link，多一条链路就多一套 OpenXR Profile 和风险。

---

## 8. 一体机出包（当前不做）

**MVP 不需要 Android/APK**：PRD §14.1 与 TECH-02 只要求 Quest Link 环境。工程里 Android 的 IL2CPP / ARM64 / Min SDK 29 / Vulkan+GLES3 已配好，是给后续可能的一体机版本预留的。

- **不要顺手 Switch Platform**：切到 Android 会触发全量 reimport 并改写 `EditorBuildSettings.asset` / `GraphicsSettings.asset`，属于影响全组状态的动作（见 §12 的「先问人」约定）。
- 将来真要出一体机包时：keystore 文件与口令**绝不入库**，放在仓库外的本机路径（例如 `C:\Users\<你>\unity-keystores\`），口令交组长保管——`.gitignore` 已拦 `*.keystore` / `*.jks` / `*.pem` / `*.key`。
- 需要 OVR 性能工具或上架商店时另行安装 Meta XR SDK（Asset Store，需登录 Unity 账号，只能手动装）。
- PRD 没有设具体 fps 指标；舒适度与性能作为 PRD §9.3（异常与恢复）和 §13（评价计划）的观察项记录，不要用「跑到 72fps」这类文档里没有的目标当验收条件。

---

## 9. AI 编码助手接入（MCP）

- Unity 官方的 `com.unity.ai.assistant` 内置 MCP **要求 Unity 6（6000.0）以上**；本项目走社区方案 **MCP for Unity（CoplayDev，支持 2021.3–6.x）**。这是已定决策，不要再提议升级引擎或安装 `com.unity.ai.assistant*`。
- 编辑器内：`Window → MCP for Unity → Auto-Setup`；若 Unity Bridge 显示 Stopped，点 `Start Bridge`。
- 服务端：`uvx --from mcpforunityserver==10.2.0 mcp-for-unity`，需要 [uv](https://docs.astral.sh/uv/)（`uvx --version` 可用即可）。
- MCP server 要写在**用户级** `~/.qoder/settings.json`（Windows 上是 `C:\Users\<你>\.qoder\settings.json`）的 `mcpServers` 里，示例：

```json
{
  "mcpServers": {
    "unity": {
      "command": "C:\\Users\\<你>\\AppData\\Local\\Microsoft\\WinGet\\Packages\\astral-sh.uv_Microsoft.Winget.Source_8wekyb3d8bbwe\\uvx.exe",
      "args": ["--from", "mcpforunityserver==10.2.0", "mcp-for-unity"]
    }
  }
}
```

（`command` + `args` 即 stdio，无需写 `type` 字段。）

**上面这段 JSON 要放进 `~/.qoder/settings.json`（用户级）才会生效。** IDE 的 agent 会话不读仓库内的 `.qoder/settings.local.json`——写在项目里会发现工具列表纹丝不动；那份文件只给终端 `qodercli` 用。不想动全局配置的话，走 IDE 自己的 MCP 设置界面添加也可以，效果相同。改完需要**新开一个会话**才会加载。

- 用 IDE 会话时执行 `/mcp reload` 可能只是被当成普通消息发出去，不一定真重载；`/mcp` 可查看已连上的 server。Unity 编辑器必须处于打开状态，Bridge（`127.0.0.1:6400`）才能响应工具调用。
- 若工具报 `No Unity Editor instances found`：先确认编辑器还开着（`tasklist | grep -i unity`、`netstat -ano | grep 6400`），这几乎总是编辑器关了或正在域重载，不是配置问题。
- 给 agent 的硬约束汇总在仓库根的 **`AGENTS.md`**（会随仓库共享，队友的 agent 会话会自动读取）：引擎版本、`Assets/Samples/**` 只读、必须入库/绝不入库清单、当前设置的真实值。改这些约定时同步改它。

---

## 10. 批量配置命令（可重复执行，幂等）

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe"
PROJ="D:/codeWork/XR-Apartment-Group8"

# 写入 XR 加载器 / Input System / Android 编译设置
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
  -executeMethod XRApartment.EditorTools.XRProjectBootstrap.Configure \
  -logFile "$PROJ/Logs/bootstrap.log"

# 重建启动场景
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
  -executeMethod XRApartment.EditorTools.VRSceneBootstrap.CreateMainScene \
  -logFile "$PROJ/Logs/scene.log"
```

注意：Unity 会拒绝名字以点开头的 `-logFile` 目录（如 `.unity-logs`），日志请放在 `Logs/` 下。batchmode 前**先关掉编辑器**，否则抢工程锁会失败。

---

## 11. 当前状态

- `Assets/_Project/Scenes/Main.unity` 是 XRI Starter Assets 的初始布局（XR Origin + EventSystem + Ground + Teleport Area/Anchor + 可抓取 Cube）——只是「能进 VR」的最小外壳。
- **厨房、冰箱/柜体方案、会话状态机、冲突提示、反馈与本地记录全部还没有**；`Assets/_Project/` 下除 `Scenes/Main.unity` 外都是空目录（靠 `.gitkeep` 占位）。
- 工程设置与版本控制已冻结入库，控制台 0 error。
- 对照 PRD §12 的 25 条 Approved 需求，Unity 侧目前 **0 条已实现**。

### 下一步（对齐 PRD §16 里程碑）

| PRD 周次 | 工作 | Owner | 完成判据 |
| --- | --- | --- | --- |
| Week 4 | Quest Link 环境下跑通可执行 build；灰盒厨房比例 | D/E/F | 目标设备能启动并进主旅程（TECH-02）；尺寸抽查与登记一致（TECH-01） |
| Week 5 | 厨房、两类柜体、F2/F3/F4 与参数登记 | E | B1-B3 几何可切换；Assumption Register 完整（FR-02、MOD-01） |
| Week 5-6 | 移动、门体/抽屉、抓取、重置、八态状态机 | D | 主动作链可运行且可恢复（FR-01/03/04/05/11） |
| Week 6 | Explore/Challenge、即时冲突提示、反馈 UI | C/D | 三个 benchmark 能完成并保存记录（FR-06/07/08/09/10、SAFE-01） |
| Week 7 | 内部试跑、稳定 build、Phase 2 提交 | F/D | 20 分钟主旅程稳定；限制有记录（UX-03） |

可并行推进的：把 `XRI Default Input Actions` 复制进 `Assets/_Project/Settings/` 再改（**不要动 Samples**）、ACC-01/02 的姿态切换与远距选择、以及 §12 说的需求 ID 追溯习惯。

### 开放实现参数（PRD §22 表 31，不要提前当成已定）

柜体/通道/gap 的具体数值（E，冻结于 gray-box Ready Gate）、门体提示触发阈值（D/E）、反馈面板最终形态（C）、结构化选项措辞（B/C）、物品种类与数量（C/E，每组 3-5 件等价）、稳定 build 版本号（D/F）。

---

## 12. 权威文档与需求追溯

- **唯一权威是 Lean PRD v0.1（2026-09-19，状态 Team Decision Baseline）**，配合 Scenario C 作业说明与虚拟客户答复 Q1-Q7。README 与 `AGENTS.md` 只是操作向摘要，**与 PRD 冲突时以 PRD 为准**，并请顺手把摘要改对。
- 目前这些文档只存在各人本机（微信文件目录）。建议收进仓库 `Docs/` 并走 LFS（`.docx` 是 zip 二进制），否则队友和 agent 会话只能凭二手描述工作，PRD 里那张参数登记表也无从追溯。
- **状态词不要混用**：`Confirmed`（课程/场景/客户明确事实）｜`Team Decision`（团队冻结的产品选择）｜`Assumption`（须登记 Owner 与验证方法）｜`Open`｜`Deferred`｜`Rejected`。凡空间表述都要标成 prototype assumption，不得冒充 client fact（MOD-01、风险 R02，由 A 做 claims review）。
- **ID 稳定且唯一**：需求 `FR-01…FR-12`、`UX-01…UX-04`、`ACC-01/02`、`SAFE-01`、`MOD-01`、`TECH-01/02`、`EVAL-01…03`，决策 `D01…D20`，风险 `R01…R11`，评价问题 `EQ1…EQ6`。不要新造并行编号体系。
- **提交信息引用被满足的需求 ID**，例如：

```
feat(review): 加入八态会话状态机 [FR-11 FR-12]
fix(conflict): 门体扫掠检测改为读取登记的开门角参数 [FR-04 MOD-01]
```

  这是 F 做证据追溯、A 维护 traceability matrix 最省事的做法，也正是 PRD §15.2 所说「用 Git commit / issue / PR 区分个人贡献」的直接来源。
