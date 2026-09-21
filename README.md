# XR-Apartment-Group8

多人协作的 VR 公寓交互项目（课程小组 8）。目标是在 PC VR 上开发调试、最终出 Meta Quest 一体机包。

> **给新组员：先看「clone 后第一次打开」和「版本控制规范」两节，再动 Unity。**

---

## 1. 引擎与工具链

| 项 | 值 |
| --- | --- |
| Unity 版本 | **2022.3.62f3**（`C:\Program Files\Unity\Hub\Editor\2022.3.62f3`），**锁死，不要升级** |
| 渲染管线 | Built-in RP（URP 迁移见 §9） |
| 目标平台 | PC VR（OpenXR / SteamVR）开发调试 + Meta Quest 一体机出包 |
| 输入系统 | 以 New Input System 为准。实测 `ProjectSettings.asset` 里 `activeInputHandler: 1`，即 **Both（旧 Input Manager 同时启用）**；若要收紧为「仅新系统」需改为 `2`，会重启生效并可能影响用到 `UnityEngine.Input` 的第三方资源 |
| 脚本后端 | Windows: Mono；Android: IL2CPP / ARM64，Min SDK 29，Graphics API `Vulkan + OpenGLES3` |
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
    Settings/          项目自有配置资产（自定义 InputActions、Interaction Layers 等）
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
- **Prefab 优先**：可交互物（门、灯、家具、手持物）做成 Prefab 放进 `Assets/_Project/Prefabs/`，场景里只放实例。改 Prefab 一个提交能让全部实例生效，比在场景里逐个改更容易review、也更不容易冲突。
- **场景拆分**：客厅 / 卧室 / 厨房 / 卫生间 / 阳台各自独立子场景，运行时按 additive 加载。`.unity` 是整体序列化的文本，**两人同时改同一个场景必然冲突且无法手工合并**，这是唯一可靠的解法。
- **不要改 `Assets/Samples/**`**：整目录是官方示例，需要改其中的输入配置或预制体时，**复制**到 `Assets/_Project/Settings/` 或 `Prefabs/` 再改（升级 XRI 时 `Samples/` 会整目录替换）。
- **单位约定**：FBX 导入 `1 unit = 1 m`，人物眼睛高度约 1.6–1.7 m。家具、门洞尺寸按真实米制建模，否则抓取和传送的落点全部会错。
- **命名**：`前缀_类型_描述`，例如 `PREF_Door_Front`、`MAT_Kitchen_Cabinet`、`TEX_Wall_Paint_01`、`SCN_LivingRoom`。同一概念用同一前缀，方便搜索。
- 遇到 `ProjectSettings/`（尤其 `TagManager.asset` 的 Interaction Layers、`EditorBuildSettings.asset`）冲突时，优先在群里对齐后由一人重设，不要手工拼接 YAML。

---

## 7. PC VR 调试

1. 后台启动 SteamVR（或对应头显的运行时）。
2. 打开 `Assets/_Project/Scenes/Main.unity` → Play。
3. 验收标准：手柄能抓取示例 Cube（触发键）、能通过 Teleport Area 上的控制器射线传送。
4. 没头显的组员：`Window → Package Manager → XR Interaction Toolkit → Samples → XR Device Simulator` 按需导入，用键鼠模拟手柄（只用于摆场景，交互手感仍需真机验证）。

---

## 8. Quest 出包

1. `File → Build Settings → Android → Switch Platform`（需已安装 Android Build Support + SDK/NDK/OpenJDK）。
2. `Player Settings` 保持：IL2CPP、ARM64、Min SDK 29、Graphics API `Vulkan + OpenGLES3`。
3. OpenXR 设置里按需启用 `Meta Touch Controller Profile`，需要手势识别再加 `Hand Tracking` feature：`Project Settings → XR Plug-in Management → Android tab → OpenXR`。
4. 签名：`Project Settings → Player → Android → Publishing Settings` 里指向 keystore。**keystore 文件和口令都不要放进仓库**——放在仓库外的本机路径（例如 `C:\Users\<你>\unity-keystores\`），口令交给组长保管；`.gitignore` 已经拦截 `*.keystore` / `*.jks`。
5. Build 出 APK 后 `adb install -r <apk>`，在「Unknown Sources / 未知来源应用」中启动。
6. 需要 OVR 性能工具或上架商店时，另行安装 Meta XR SDK（Asset Store，需登录账号，只能手动装）。
7. 性能目标：Quest 端稳定 **72 fps**。用 `Window → Analysis → Profiler` 或 Meta OVR Metrics Tool 实测，不要只看编辑器帧率。

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

- `Assets/_Project/Scenes/Main.unity` 是 XRI Starter Assets 的初始布局（XR Origin + EventSystem + Ground + Teleport Area/Anchor + 可抓取 Cube），**还没有公寓场景内容**。
- `Assets/_Project/Scripts/`、`Prefabs/`、`Materials/`、`Settings/`、`XR/` 目前都是空的。
- 控制台 0 error。

### 下一步（P0 → P2）

**P0**
- OpenXR Profile 落地：Android 端启用 Meta Touch Controller Profile（要手势识别再加 Hand Tracking），Windows 端按组里实际头显勾选。验收：PC 接手柄 Play 能抓取 + 传送。
- Quest 出包实测一次：切 Android → Build APK → `adb install -r` → 真机跑到 72 fps（含 Vulkan 兼容性验证）。

**P1**
- 把 `XRI Default Input Actions` 复制为项目自有 asset 放 `Assets/_Project/Settings/` 再改；建立 Interaction Layers：`Wall / Furniture / Door / Item / Player`。
- 公寓场景骨架：客厅/卧室/厨房/卫生间/阳台按真实米制白模（可用 ProBuilder），拆成 additive 子场景。
- 核心可交互 Prefab：门（旋转/推拉）、灯与电器开关、可抓取+投掷物品、传送点与锚点布局。
- 光照与性能基线：静态 Lightmap 烘焙（Progressive）、Reflection Probe、LOD 与 overdraw 控制，采一次 Profiler 记录瓶颈。

**P2**
- Editor 冒烟测试（`Tests/Editor`）：断言双端 `activeLoaders` 含 `OpenXRLoader`、`activeInputHandler == 1`、`Main` 在 Build Settings 第 0 位——防止队友误改回退。
- 可选：URP 迁移（Quest 端更省，但要改材质）、Meta XR SDK（需登录 Unity 账号手动装）。

### 未决问题（需要拍板）

- **是否需要多人同场联网？** 这会决定架构（网络方案 + 交互状态同步），越晚定改动越大。
- 组里实际有哪些头显？决定 Windows 端 OpenXR Profile 与测试分工。
