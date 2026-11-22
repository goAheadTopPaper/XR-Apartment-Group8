# Mzh 厨房设计迁移到 Hzc

来源固定为 `origin/feature/mzh` 的提交 `1f501b310e6d21f9e817644f785e752605e6296d`，场景为 `Assets/PhysicsSystem/Scenes/KitchenTest.unity`。`TestRange` 是另一个物理测试场，未作为厨房设计来源。两个分支没有共同祖先，本次按资产依赖迁移，没有合并工程设置或提交历史。

## 版本边界

| 项目 | Hzc 保留的基线 | Mzh 来源 |
| --- | --- | --- |
| Unity | 2022.3.62f3 | 6000.6.2f1 |
| 渲染 | Built-in | URP 17.6 |
| XRI | 3.5.0 | 3.6.1 |
| 输入 | New Input System，`activeInputHandler: 1` | 不迁移控制器与输入配置 |
| 构建 | StandaloneWindows64 | 不迁移来源的 Build Settings |

## 迁入的内容

- `Assets/_Project/Art/Kitchen/Meshes/`：18 个来源 FBX；原始二进制及导入 GUID 保留。
- `Assets/_Project/Art/Kitchen/Textures/`：11 个来源材质实际引用的 27 张贴图，保留导入设置和 GUID。
- `Assets/_Project/Art/Kitchen/Materials/`：把来源的颜色、BaseMap、平铺、法线、金属/平滑度映射为 Standard 材质；来源的默认白材质改为本项目的 `MAT_Kitchen_White`。没有迁移 URP 编辑器子资源或 Shader GUID。
- `PREF_Kitchen_Environment`：保留来源厨房、墙窗门、天花和静态餐桌/沙发背景的摆放。来源 WhiteBox 下 15 个保留对象的局部位置、旋转和缩放一致；沙发包含的 5 个模型也保留其局部变换。
- `SCN_Kitchen_Review`：唯一启用的构建场景。环境、benchmark、菜单、传送区、冰箱和锅/杯均使用 Prefab 实例。

未迁入树木包、TestRange、来源 PD 物理跟随/FixedJoint 抓取、来源 XR 配置、参考图片/人体标尺、测试方块、URP Volume、反射烘焙和 Unity 6 Adaptive Probe Volume 数据。手部模型与动画的后续适配见下节。静态背景不新增评审任务；传送区域只设在厨房。

## 接入 Hzc 交互时的适配

来源 `KitchenTest` 没有可用的 F2/F3/F4 冰箱，也没有对应槽位。保留 Hzc 的冰箱代理、同侧铰链、运动学开合、锅/杯、菜单，以及数据和纯逻辑程序集。

原台面延伸到厨房入口端，直接放入冰箱会重叠。新增派生网格 `MESH_Kitchen_Counter_WithFridgeSlot.asset`，将入口端截到世界坐标 `x=-2.29`，为代理留出 `0.838m` 槽位；台面其余布局和原 FBX 保留。派生网格同时用于渲染和 MeshCollider。冰箱朝向厨房内部，B1 门板前缘相对台面前缘突出 `0.10m`；B2/B3 使用围板包覆，围板前缘与门板对齐。

`ASM_KitchenParameters` 已替换旧灰盒登记：源台面射线测高为 `1.00m`，厨房该位置天花为约 `2.6801m`；槽位、代理尺寸和开合行程均注明来源/适配。**这些是模型内的 prototype assumptions，不是客户尺寸、通行合规或 reach 结论。**

旧 `Main.unity`、旧灰盒环境、两个房间生成脚本和 6 个失去引用的环境材质已移除。`KitchenBenchmarkMenu` 仅打开场景与切换 B1/B2/B3，不再生成房间或覆盖美术设计。后续通过 Prefab 编辑布局；不要恢复旧生成器。

## 手部交互适配

来源 `Physics Rig` 是手柄驱动的骨骼手：`AnimateHandOnInput` 把握把/扳机映射到 Animator；`PhysicsHandTracking` 用 Rigidbody PD 跟随（还引用 Unity 6 的 `linearVelocity`）；`GrabPhysics` 用 FixedJoint 抓取；`TrackingHandVisualBlend` 显示追踪位置虚影。没有摄像头裸手识别。

现在迁入 `SK_BasicHand_R.fbx`、4 张贴图和 `R_Idle/R_Grip/R_Trigger/R_Fist` 四段动画，原始文件二进制保留。左手按来源方式镜像右手网格，模型位置/旋转保留来源校准。材质重建为 Built-in Standard，Animator Controller 由 Unity 2022 API 创建为 Grip/Trigger 二维混合。资产位于 `Assets/_Project/Art/Hands/`。

`PREF_XR_KitchenRig` 是项目自己的完整 XRI Rig，未修改 Samples；两个 `PREF_Hand_*` 取代可见手柄模型，保留原 NearFar/Poke、射线、输入、震动、菜单与传送。`ControllerHandPresentation` 让握把/扳机平滑驱动手指，悬停时预握，持物时握合并增加淡青色提示。锅/杯用 XRI 动态附着点保留接触处与拿取朝向，未引入第二套抓取系统。

`KinematicPalmSolver` 用非分配球形扫掠/滑动和初始穿透恢复来呈现静态台面/墙面接触。它跳过自身 Rig、当前持物、Trigger 和 Rigidbody 物体；不推物体、不控制输入、不作为 reach 或冲突检测。Unity 2022 计算穿透要求启用 probe，因此只在同步查询中临时启用，`finally` 中关闭，不留在物理模拟里。实体手停住后，追踪偏移超过 0.08m 显示虚影，退至 0.04m 内隐藏；这些与 0.045m 代理半径等均登记为虚拟手调参假设。

借鉴 [Alyx 官方说明](https://www.half-life.com/en/alyx/) 中对手指追踪/扳机式控制器的兼容，以及 [Valve 交互设计访谈](https://www.uploadvr.com/valve-interview-half-life-alyx/) 对可触碰物体的讨论。当前仅实现手势、握持提示和球形手掌接触；没有逐指碰撞、贴合不同物体的握姿、完整物理手或裸手识别。没有引入远距吸取，以免改变三种方案的触达比较。

后续针对用户发现的锅穿桌问题，所有活动道具增加整体扫掠，持物实手跟随受阻姿态并显示透过遮挡的追踪虚影；实现、验证与保守包围盒的限制见 [HeldPropCollision.md](HeldPropCollision.md)。

## 天空与光照适配

来源 `KitchenTest` 使用 Unity 默认程序天空，太阳旋转 `(15°,60°,0°)`、暖白色 `(1,0.95686,0.83922)`、强度 5、间接光倍率 4；`StandardLightingSettings` 启用两次反弹、0.5m 烘焙 AO。两个室内反射探针分别在 `(-3.5,1.75,1.75)` 与 `(-3,1.75,-0.75)`。`GeneralSceneProfile` 开启 ACES 与 Bloom（强度 2、阈值 1），Motion Blur/Vignette 关闭。来源项目为 Linear。这些是从该提交的场景/设置中读出的值，不代表另一场演示一定采用了同一版本。

当前项目据此重建静态日光环境：

- `PREF_Lighting_KitchenDaylight`：沿用太阳角度/色调和两个反射探针的位置。太阳使用 Mixed + Soft Shadows，保留门/手部的实时阴影；静态环境烘焙间接光，动态对象使用 72 个 Light Probes。反射探针改为带盒投影的 256px 烘焙探针。
- `MAT_Sky_KitchenDaylight`：Builtin Procedural Sky，太阳与场景主光关联，主相机使用 Skybox 清屏；窗外不再是平面背景色。固定日间环境，不增加昼夜/天气切换。
- `PP_KitchenDaylight`：官方 Built-in Post Processing 3.2.2 的 ACES/Bloom。曝光固定为 -0.7 EV，控制亮处的过曝；没有自动曝光、景深、运动模糊或屏幕暗角。MSAA 保留，Bloom 使用 fast mode。后处理挂在项目 Rig 的主相机，未修改 Samples。
- `LGT_KitchenDaylight.lighting`：Progressive GPU、32 texels/m、两次反弹、0.5m 仅作用于间接光的烘焙 AO、Directional Lightmaps、Shadowmask。灯带材质恢复 `_EMISSION` 与 BakedEmissive，使用来源 HDR 发光颜色。
- 工程改用 Linear 与线性灯光强度，保留 Built-in / Unity 2022 / Windows64。当前 Ultra 的阴影距离收至 25m，集中在室内。

来源 FBX 的 UV2 为空，因此在本项目导入设置里生成烘焙 UV（FBX 原始字节不变）。派生台面单独展开并增加图表间距；两根薄吊灯使用 Light Probes，避免细小面在 lightmap 中互相污染。环境静态标记包含 GI/ReflectionProbeStatic；冰箱、围板、门体、锅杯、手部保持动态，不烘焙进固定环境。三种 benchmark 共用同一套光照/曝光。

烘焙产物保存于 `Assets/_Project/Scenes/SCN_Kitchen_Review/`，没有复制 Unity 6 的 LightingData 或 Adaptive Probe Volume。新的原生二进制 `LightingData.asset` 按精确路径走 LFS；EXR/PNG 沿用已有 LFS 规则，`.lighting` 保留文本。修改静态几何、太阳或发光材质后必须重新 Generate Lighting，反射与间接光不会在运行时逐帧重算。

## 验证与限制

本次验证结果：

- EditMode：45/45 通过，含 5 个场景完整性测试及 8 个手掌求解测试；覆盖启动/手部引用、资源/材质兼容、碰撞网格、台面/槽位登记、快速扫掠、滑动、初始穿透恢复和应忽略的碰撞体。
- 场景校验：0 缺失脚本、0 损坏 Prefab；删除资源没有遗留 GUID 引用，资产和目录均有对应 `.meta`。
- 20 个保留的来源对象/沙发子对象变换已比对；18 个 FBX 与 27 张贴图的二进制 SHA-256 与来源一致。
- B1/B2/B3 在 Edit 模式切换后保存并重新打开，冰箱、围板及槽位位置均保留；环境对象不随预设切换。
- Play 模式：B1/B2 的两扇门到达开启位置；B3 的两扇门和两个抽屉可开启并关闭，抽屉行程为 0.35m。暂停/恢复、回到起点、平滑移动开关有效，厨房传送区已注册碰撞体；控制台 0 error。这些通过组件 API 检查，不构成头显/手柄验收。
- 手部 Play 检查通过模拟器输入注入与组件 API：Grip/Trigger 均到达 1，中指骨骼相对初始姿态改变约 105°；NearFar 实际悬停锅后，用模拟握把选中、抬升约 0.27m 并释放。追踪点压到台面下方时，实体代理停在约 1.047m、偏移约 0.197m 且虚影可见；收回后偏移归零且虚影隐藏；Rig 位移 0.8m 或转向 45° 后双手偏移归零。重新打开保存的场景后，两手引用和四段动画仍有效。不是通过真实手柄完成的测试。
- 光照适配后重新运行 EditMode，45/45 通过，场景校验 0 缺失脚本/损坏 Prefab，控制台 0 error。最终烘焙得到一张 1024px Directional Lightmap、Shadowmask、72 个 Light Probes、两张 256px 室内反射及默认环境反射。台面 UV 重叠已修正，最终烘焙未再报告 UV overlap；厨房窗边、冰箱侧、背景房间均已截图检查。保存重载后天空、探针、后处理与烘焙数据保留；Play 切换 B1/B2/B3 时主光/天空/lightmap 引用一致，benchmark 对象未进入静态烘焙，双手仍能响应模拟握把/扳机。头显中的曝光观感、舒适度和帧率仍需 Quest Link 实测。

键鼠模拟器所需 TMP Essentials 字体、设置、Shader 和相关许可证已从忽略规则中移出，随项目共享；旧版 PDF 手册仍忽略。进入 Play 点击 Game，G/T 检查手势，鼠标左键检查选择/抓取，Y 查看完整键位。

本机为 macOS，未连接 Quest 3。编辑器验证不能代替 Windows + Quest Link 的控制器、空间舒适度和 TECH-02 验收。Safety warning/fade、Design conflict、反馈 UI 与本地记录写盘仍未实现。

## 来源清单

后续按用户选择接入的 AllSky Free 天空及 NVJOB 水面/倒水交互见 [KitchenWaterAndSky.md](KitchenWaterAndSky.md)。这些是迁移之后的项目扩展，不来自 mzh 场景。

完整提交：`1f501b310e6d21f9e817644f785e752605e6296d`。

来源场景中的对象与资产路径保留在环境 Prefab 和导入模型名称中；美术二进制按本项目 `.gitattributes` 走 Git LFS。
