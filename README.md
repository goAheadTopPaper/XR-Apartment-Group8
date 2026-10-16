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
| 输入系统 | **仅 New Input System**，`activeInputHandler: 1`（0=旧 / 1=仅新 / 2=Both），不要为迁移场景修改此值 |
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
| `com.unity.postprocessing` | 3.2.2 | Built-in 后处理：固定 ACES 色调映射与轻度 Bloom |
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
3. 用菜单 `XR Apartment → Open Kitchen Review Scene` 打开 `Assets/_Project/Scenes/SCN_Kitchen_Review.unity`，接好 Quest Link 后按 Play。它是唯一启用的构建场景。
4. 若报 `The following build scene(s) could not be loaded` 或 XRI 相关 missing script：先 `git lfs pull`，再 `Assets → Reimport All`。

---

## 3. 目录结构

```
Assets/
  _Project/            ★ 项目自有内容，日常开发都在这里
    Art/Kitchen/       从 mzh KitchenTest 迁入的模型/贴图，以及 Built-in 材质
    Scenes/            SCN_Kitchen_Review.unity（唯一启动场景，第 0 位）
    Prefabs/           预制体（优先在预制体里改，不要直接改场景里的实例）
    Scripts/
      Data/            benchmark / 参数登记 / 反馈记录定义
      Simulation/      无引擎引用的状态机、动作链、运动学与水量算法
      Runtime/
        Review/        评审标记、方案槽位、世界空间菜单
        Interaction/   门/抽屉、手部呈现、持物防穿透
        Water/         接水、容器、水流、水面与水花
    Editor/            项目作者工具：方案、道具、抓取、水体、天空、XR 初始化
    Tests/             EditMode / PlayMode 回归测试
    Materials/         材质
    Settings/          项目自有配置资产（自定义 InputActions、benchmark 组合定义、模型参数登记表）
    XR/                自定义 XR Provider / 交互层配置
  XR/                  ★ XR Plug-in Management 生成的资产（Loader 列表、OpenXR Package Settings）
  XRI/                 ★ XRI 生成的设置资产（Interaction Layer Mask / Runtime / Simulator Settings）
  Samples/             XRI Starter Assets（只读参考，勿改动，升级时整目录替换）
ProjectSettings/       工程设置（版本控制）
Packages/              manifest.json + packages-lock.json（都要提交）
UserSettings/ Logs/ Temp/ Library/   ← 本机私有，已 gitignore，不要提交
```

★ 标记的三个 `Assets/` 子目录**必须入库**：`Assets/XR/` 与 `Assets/XRI/` 下的 `.asset` 是 `ProjectSettings/EditorBuildSettings.asset` 和 XRI 运行时引用的实际对象，删掉或漏提交会让队友打开工程后加载器列表为空、交互层丢失。

各模块职责、修改入口和依赖方向见 [代码结构与维护入口](Docs/CodeStructure.md)。编辑器工具与运行时代码分开，现有三个运行时程序集保持不变。

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

构建目标固定 `StandaloneWindows64`：Windows PC 渲染画面，Quest 3 提供头部与手柄追踪（PRD TECH-02）。直接 Play 联动头显时，Unity 编辑器也需要在 Windows PC 上运行。

**Mac 的平台限制**：Virtual Desktop 的 Mac 连接提供桌面串流；PCVR 串流需要 Windows，不能将 Quest 追踪接入 Mac 上的 Unity Play（[VD 官方说明](https://www.vrdesktop.net/)）。Mac 上继续使用下方的键鼠模拟器；把构建目标设为 Windows 不会改变编辑器所在的平台。

1. 编辑器里 `File → Build Settings` 确认平台是 **Windows, Mac, Linux → Standalone Windows64**，场景 `Assets/_Project/Scenes/SCN_Kitchen_Review.unity` 在第 0 位。
2. Quest 3 上启动 Link（有线优先，无线对带宽敏感），连到本机；Meta Horizon 里允许连接。
3. 确认当前 OpenXR 运行时与连接方式一致：Quest Link 使用 Meta 的运行时；Windows VD 调试使用其对应的运行时，并先启动 Streamer、让头显连接该 PC。
4. 真机 Play 前，在 `Project Settings → XR Plug-in Management → XR Interaction Toolkit` 取消 **Use XR Interaction Simulator in scenes**，避免模拟输入同时进入。然后按 **Play**，检查传送、锅/杯抓取、冰箱门/抽屉，以及回到起点/暂停/平滑移动菜单；B1/B2/B3 从 `XR Apartment → Benchmark → Apply …` 切换。
5. **没头显的组员（Mac / Windows）**：项目已配置 **XR Interaction Simulator**，进入 Play 后自动创建虚拟头显和两只手柄。先点击 **Game** 画面获得键盘焦点，再按下表操作；不需要再导入一份 Simulator Sample。TMP Essentials 也已随项目提供。
6. 正式验收仍使用 Quest 3 + Quest Link；Windows VD 可用于开发调试，其结果不等于通过 TECH-02。不要引入其他运行时路径作为测试基线。

### 无头显时的键鼠操作

| 操作 | 默认按键 |
| --- | --- |
| 移动 | W / A / S / D |
| 升高 / 降低当前模拟设备 | E / Q |
| 转动视角 | 按住鼠标右键移动，或方向键 |
| 抓取 / 释放锅杯 | 鼠标指向物体，按住 / 松开左键 |
| 选择门体、点击菜单 | 鼠标指向目标后点击左键 |
| 全身 / 单设备模式切换 | Tab |
| 选择左 / 右手柄 | `[` / `]` |
| 手指握合 / 食指扳机（默认右手） | 按住 G / T；左手组合键见 Y 提示 |
| 操作提示面板 | Y |

模拟器原样复制自 XRI 3.5.0，位于 `Assets/_Project/XR/InteractionSimulator/`；未改动 `Assets/Samples/`。`Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset` 已开启自动创建与 **Instantiate In Editor Only**，正式 Windows 构建不会自动创建模拟器。使用真实头显在编辑器测试时，先在 `Project Settings → XR Plug-in Management → XR Interaction Toolkit` 取消 **Use XR Interaction Simulator in scenes**，避免同时输入。

键鼠可以检查移动、抓取、开合和菜单逻辑；空间尺度、通行与舒适度仍需要 Quest 3 + Quest Link 实测。模拟器的可选裸手模式不在本项目测试路径中，使用默认的虚拟手柄即可。

### 当前手部交互

`PREF_XR_KitchenRig` 使用 mzh 的手模型、贴图及 Idle / Grip / Trigger / Fist 动画，保留原 XRI 输入、菜单、传送与抓取。握把和扳机控制手指，悬停时轻微预握，拿住锅杯时提供握合与淡青色提示；锅杯使用接触位置的动态抓取点。手掌通过球形代理停靠静态台面/墙面，手柄继续越过表面时显示追踪位置的半透明虚影，收回后消失。接触参数来自 `ASM_KitchenParameters`，是虚拟手调参假设。

这版借鉴 Alyx 的握持与接触反馈；目前是**手柄驱动动画**，未接入摄像头裸手识别、逐指碰撞或物体形状自适应握姿。冰箱仍用原有运动学交互，三种 benchmark 的动作链相同。来源与验证见 [迁移说明](Docs/MzhKitchenMigration.md)。

### 天空与日光

厨房使用 `PREF_Lighting_KitchenDaylight`：AllSky Free 的 Cold Sunset 天空、mzh 的低角度暖色阳光、烘焙间接光/接触阴影、室内反射和给动态手部/锅杯使用的 Light Probes。工程颜色空间为 **Linear**，渲染仍是 Built-in。`PP_KitchenDaylight` 保存固定 ACES/Bloom；没有自动曝光，B1/B2/B3 共用同一套光照。

太阳、探针位置在光照 Prefab，天空在 `Art/Lighting/MAT_Sky_KitchenDaylight.mat`，烘焙设置在 `Settings/LGT_KitchenDaylight.lighting`。调整静态环境、太阳或发光材质后，用 Lighting 窗口的 **Generate Lighting** 重烘焙；生成的 `Scenes/SCN_Kitchen_Review/` 及 `.meta` 需要与场景一起共享。薄吊灯使用 Light Probes，冰箱/门体/锅杯/手部不参与静态烘焙，避免切换方案时留下旧方案的阴影。

### 接水与倒水

默认容器水面使用 **NVJOB Water Shaders V2**；水量与重力水流由项目脚本负责。进入 Play 后，选择水站的蓝色龙头拨杆开关水，把杯或锅放到出水口下方接水，再倾斜倒入另一容器。开口锅杯保留 XRI 抓取，水面按重力保持水平并在移动后晃动，落水有水花；接水盆自动排水。

这是有限容器的近似液体交互，没有引入 SPH 流体、浮力、重量变化或地面积水。暂停菜单会停止水的推进；`XR Apartment/Water/Reset Water (Play)` 关闭龙头并恢复初始水量。资源来源、登记参数和维护方法见 [KitchenWaterAndSky.md](Docs/KitchenWaterAndSky.md)。

### 餐具与食品

Kitchen Props Free 和 Food Pack Mixed 的精选资源已布置到公共 `PREF_Kitchen_Dressing`：窗边餐具收纳、备餐盘与调料、烤面包机，以及已有餐桌上的早餐和水果。9 件新增食品/包装和空马克杯支持 XRI 抓取；新马克杯、果汁盒暂不支持倒液体，接水仍使用原有杯和锅。三种 benchmark 共用同一布局。来源清单、登记尺寸和维护方法见 [KitchenDressing.md](Docs/KitchenDressing.md)。

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

# 确保 Standalone OpenXR / New Input System 和项目目录
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" \
  -executeMethod XRApartment.EditorTools.XRProjectBootstrap.Configure \
  -logFile "$PROJ/Logs/bootstrap.log"

```

注意：Unity 会拒绝名字以点开头的 `-logFile` 目录（如 `.unity-logs`），日志请放在 `Logs/` 下。batchmode 前**先关掉编辑器**，否则抢工程锁会失败。

初始化会保留已有 Loader，仅在缺少 OpenXR 时补上；不切换当前构建目标，也不改写 Android 设置。

厨房现在由场景和 Prefab 保存设计，**不再通过代码生成房间**。在 `PREF_Kitchen_Environment` 编辑环境，在 `PREF_Kitchen_Benchmark` 编辑槽位/围板，交互物继续编辑各自 Prefab。旧 `VRSceneBootstrap` / `KitchenSceneBootstrap` 已移除，避免覆盖迁入的布局。来源、材质适配和冰箱槽位的改动见 [迁移说明](Docs/MzhKitchenMigration.md)。

---

## 11. 当前状态

- `SCN_Kitchen_Review` 使用 `feature/mzh` 的 `KitchenTest` 厨房设计，保留建筑背景；评审交互和传送区集中在厨房。原灰盒已被替换，旧 `Main.unity` 和无用材质已移除。
- 保留 Hzc 的数据层、纯逻辑会话状态机/动作链、运动学门体/抽屉、F2/F3/F4 代理、B1/B2/B3 注册表、可抓取锅/杯与世界空间菜单。
- 迁移保持 Unity 2022.3.62f3、Built-in、XRI 3.5 和 Windows64；手模型/动画适配到项目自己的 XRI Rig，没有引入 mzh 的 Unity 6/URP 工程配置、PD 物理跟随或 FixedJoint 抓取。
- 天空/光照已按 mzh 的暖日光样式重建，使用 Linear、固定 ACES/Bloom 和本项目重新生成的烘焙数据；不复用 Unity 6 的 LightingData/APV。
- 几何冲突检测、Safety warning/fade、反馈 UI、本地记录写盘仍未实现。逻辑测试和编辑器场景检查不能替代 Quest 3 + Quest Link 的端到端验收。

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
