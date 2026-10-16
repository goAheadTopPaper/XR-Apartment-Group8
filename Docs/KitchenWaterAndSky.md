# 厨房天空与倒水交互

使用 Unity 2022.3.62f3 / Built-in / Linear / XRI 3.5；构建目标仍为 StandaloneWindows64。

## 资源选择

- AllSky Free 1.2.0，商店产品 146014，rpgwhitelock：从账号已拥有的资源经 Unity Package Manager 下载。只取 `Cold Sunset Equirect.png` 和其导入 GUID；未导入其他九种天空、演示场景或脚本。该图是蓝天、薄云和低角度暖日光，适合当前固定日间厨房。`MAT_Sky_KitchenDaylight` 使用 Built-in `Skybox/Cubemap`。太阳、曝光和 benchmark 的公共光照设置保留。
- NVJOB Water Shaders V2.0：从作者 MIT 仓库的固定提交 `34f30d75568b2a6828f769f20ee007eb6700fc98` 取着色器、法线贴图和材质面板依赖。保留 GUID、贴图和 C# 源码；着色器有小范围 PC VR 适配：用 `UNITY_DECLARE_DEPTH_TEXTURE` 声明双眼深度纹理，并以 shader model 3.5 支持纹理数组。详见 `Assets/ThirdParty/NVJOBWaterV2/PROVENANCE.txt` 和许可证文件；仍需头显验证。
- `SET_WaterInteraction` 引用默认 `MAT_Water_Kitchen`，使用 NVJOB Water Specular。厨房的厘米级水深使用较快的深度淡化；关闭海岸泡沫、视差与额外平面反射相机，使用已有室内烘焙反射。项目系统统一驱动波纹；供应商的 `NVWaterShaders` 类型只供其材质编辑器引用，未挂到场景。

## 用户选定的水交互范围

此次明确选择“可倒水交互”：容器可以接水、倾倒，水面按重力保持水平并在移动后晃动，落水有水花。采用开放圆柱容器的静水近似、阻尼晃动和重力抛物线水流，不是 SPH/FLIP 连续流体求解器。NVJOB 负责表面外观，不计算液体物理。

- 杯、锅与接水盆保存实际水量（mL）；容积由登记的内半径和高度计算。填满后不会继续增加水量，空杯不能倒出水。
- 倾斜时按最低杯口高度计算可保留水量；超过杯口的部分以有限速率流出。水流按重力轨迹移动，穿过其他容器开口时被接住，先撞到杯壁、台面等物体则产生水花并计为溅出。
- 水在空中也有体积记录；256 个固定容量水流分段复用，不为每滴水创建 Rigidbody/GameObject。池满时保留源容器的水量；视觉水花不参与体积计算。
- 接水盆自动排水，排出的水量单独记录。台面落水计为溅出，没有地面积水、湿滑、伤害、浮力或液体重量反馈。
- 容器表面是重力平面与圆柱内部的几何交线；晃动在容器内受限。锅、杯模型改为开口薄壁，使用底部和分段杯壁碰撞体，保留原 XRI 抓取配置与锅柄。没有引入另一套物理抓取系统。
- 杯使用低饱和蓝灰陶瓷，锅与水盆使用金属，帮助在明亮台面前读出容器和透明水流；没有增加材质选择功能。
- 新增独立 `PREF_Water_KitchenStation`，放在原台面上，不切割或重建 mzh 的厨房网格。其位置和容器尺寸均登记在 `ASM_KitchenParameters` 的 `water_*` 参数中，属于 prototype assumptions，不是客户尺寸或合规结论。
- 所有水物体保持动态且不参加静态 GI 烘焙；三种 benchmark 共用同一套水材质、容器和龙头。

## 使用与维护

1. 打开 `SCN_Kitchen_Review`，进入 Play。
2. 选择龙头右侧蓝色拨杆开关水；把杯或锅移到出水口下方接水。
3. 倾斜杯或锅，将水倒进另一个容器或接水盆。
4. 原暂停菜单会停止水量、重力水流和晃动的推进。
5. 要重复演示，使用菜单 `XR Apartment/Water/Reset Water (Play)`。它关闭龙头、清空在途水和统计、恢复各容器的初始水量；不移动用户拿着的物体。

`XR Apartment/Water/Configure Kitchen Water` 只配置水资源、两个现有锅杯 Prefab 和水站，不生成房间。修改水参数后执行它可重建对应容器。`XR Apartment/Lighting/Apply AllSky Free Daylight` 重新应用所选天空；更换天空后必须重新烘焙 GI 和反射，再更新水材质引用的室内反射纹理。

键鼠模拟器可测试选择、抓取与容器姿态；Quest 3 经 Quest Link 的双眼显示、抓握舒适度和帧率仍需 Windows 真机验证。

## 编辑器验证（2026-10-16）

- EditMode 62/62 通过：包含水量转移/满杯/空杯/非法输入、长序列守恒、倾斜保留量、表面高度和场景资源绑定。完整性测试显式检查厨房场景的 RenderSettings，避免误读 Test Runner 临时场景的默认天空。
- 连续执行两次水配置后，场景仍只有 1 个水系统、1 个龙头和 3 个容器。修复了早期重复配置产生重复水站的问题。
- Play 组件 API：通过 XRI Interaction Manager 的选择事件成功开关龙头、抓取并释放杯；杯注册 17 个抓取碰撞体。不是用真实头显/手柄执行的验证。
- 龙头把杯填至 341.378 mL 的登记几何容积。倾倒测试中杯流出 341.378 mL，锅接到 336.826 mL，另有约 4.552 mL 溅出，在途水清零后相符。
- 一个受控的 20 步倾倒快照中，杯约 113.793 mL、锅约 404.953 mL、在途水约 40.965 mL、溅出为零，总量约 559.711 mL，与初始总量一致。该快照只用于拍摄可见水流，物体姿态由验证代码设置。
- 龙头流入接水盆时，注入、盆内、在途与排水量核对一致；暂停后注入量、在途量和排水量差值均为零。
- Play 切换 B1/B2/B3 后，天空材质和水系统实例相同，仍有一张公共 lightmap。
- AllSky 更换后重新烘焙：1 张 lightmap、72 个 Light Probes、2 个室内反射探针。重开保存场景后天空贴图、水组件、抓取启用状态和原始锅杯位置保留。
- 两个 NVJOB 水着色器和项目水花着色器在本机报告支持，ShaderUtil 消息为零，控制台无 error。当前是 macOS Metal 编辑器验证，不等于 Windows D3D11 / Quest 双眼验证。
- Profiler 已采样：水流开启的最后一次编辑器样本为 CPU 5.36 ms、GPU 4.44 ms。初始和关闭水渲染的基线也出现过约 19 ms GPU 样本，故这些是编辑器诊断数据，不是持续帧率承诺或 Quest Link 验收。

本机预览和过程证据在忽略入库的 `Logs/WaterVerification/`；不把日志、截图缓存或下载的 `.unitypackage` 提交到仓库。
