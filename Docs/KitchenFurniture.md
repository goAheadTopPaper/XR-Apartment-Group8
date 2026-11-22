# 厨房家具与餐区模型

2026-10-30，按用户确认的浅橡木、暖白金属风格制作冰箱、餐桌、餐椅和吊灯。入口仍为 `XR Apartment → Cooking → Open Cooking Kitchen`；`Apply Reviewed Furniture` 可以重新应用本批家具，完整重建烹饪工作台也会接入它们。

## 场景与使用

独立烹饪场景 `SCN_Kitchen_Cooking` 的餐区包含一张餐桌、四把配套椅子和一盏暖色吊灯。原环境的餐桌与两盏旧吊灯只在这个场景的实例上停用。原厨房内切配、打蛋、炒菜、水站和食材位置继续保留；冰箱继续用原来的固定铰链开门、储物和隔门取物限制。

冰箱外观包括直板搪瓷箱体、圆角门板、独立不锈钢把手、门封、通风格栅、可调脚、内胆、层板和门内收纳格。下部保鲜盒与门内收纳格是本轮的模型细节，不新增可拉抽屉或自动补货行为。

| 资产 | 制作尺寸（米） | Unity Prefab |
| --- | --- | --- |
| 浅橡木餐桌 | 1.50 × 0.85，桌面高 0.758 | `Prefabs/Furniture/PREF_Furniture_DiningTable` |
| 配套餐椅 | 0.49 × 0.53 × 0.83 | `Prefabs/Furniture/PREF_Furniture_DiningChair` |
| 暖白吊灯 | 灯罩直径 0.40，含吊线与顶盘 | `Prefabs/Furniture/PREF_Furniture_Pendant` |
| 烹饪冰箱 | 沿用原 0.72 米宽、1.81 米高及门轴 / 层板位置 | `Prefabs/Cooking/Recipe/PREF_Cooking_Fridge` |

这些是 `ASM_KitchenParameters` 中的 prototype assumption。家具生成结果不作为客户尺寸或正式空间评审证据。

## 实际生成与修整

内置 Image Gen 制作并保存单件参考图，用户确认后使用官方 Comfy Cloud 的现有单图 Pixal3D 工作流。新增 **4 次实际三维任务**，累计 **24 次**。母版 GLB、独立 PBR、预览、工作流、随机种子、参考提示词与校验均保留在 `ArtSource/KitchenAssets/furniture_*/v001/`、`References/` 与 `Manifests/`。费用未能逐任务核实，保留 `null`。

| 资产 | Cloud 任务 ID | 云端耗时 | 使用情况 |
| --- | --- | ---: | --- |
| 餐桌 | `6a0985b7-738e-45c3-bd9c-0aaaffa6f88a` | 44.15s | 保留生成网格和木纹，校正倾斜、比例与桌面 |
| 餐椅 | `b896a67e-5393-492b-aacf-16ee35124d71` | 40.49s | 保留生成网格和木纹，统一朝向与尺度 |
| 吊灯灯罩 | `41ba5179-bf96-4ed3-ac6b-5167509ed44c` | 47.70s | 保留生成木质顶帽；灯罩、吊线与顶盘本地重建 |
| 单门冰箱 | `12187f5e-e184-4f0b-91a7-3d520a308282` | 60.08s | 原始整机平面褶皱、贴图拉痕未通过外观检查；精确板件和机械细节本地重建 |

图生三维对木纹、圆润椅背等外观有效，但不能直接保证大平面、空腔或活动部件正确。没有把失败的原始冰箱和灯罩直接作为合格成品。冰箱当前的整洁箱体与门体是按确认参考制作的本地硬表面模型；不能称为未经修整的 Cloud 生成物。

`Tools/ComfyCloud/blender_furniture_visuals.py` 输出 `Recipe/v004_furniture/`，保留生成桌椅的 UV，并制作各件的两档模型；冰箱 / 灯具按材质合并同类零件，减少逐格栅、逐支架绘制。Unity 使用 Built-in Standard，独立材质与贴图位于 `Art/Cooking/Furniture/`。桌椅和吊灯为固定场景物，碰撞不随渲染 LOD 消失；冰箱只替换可见模型，沿用已有交互与碰撞代理。

家具制作集中在 `KitchenFurnitureAuthoring`，继续作为 `KitchenCookingAuthoring` 的 partial，复用公共资源、Prefab 生命周期和原有门体逻辑。旧 Blender 菜谱脚本增加直接执行入口保护，作为几何工具导入时不再重新生成以前的来源资产。

## 验证

保存后重新加载、尺寸 / LOD / 材质 / 碰撞检查、Unity 画面和回归结果见 `ArtSource/KitchenAssets/Validation/Furniture/`。只重烘焙独立烹饪场景。正式评审场景、原厨房环境 Prefab、Packages、XR 与 Build Settings 保持原样。

本批 Python 12/12、5 组实际家具网格的远近模型 / 空腔 / UV / 有限坐标检查通过；Unity EditMode **145/145**、PlayMode **50/50** 通过。新增通行检查使用高 1.7 米、半径 0.2 米的真实 CharacterController，从餐桌两端绕行并经过厨房前方通道，三个目标点的水平误差均小于 0.025 米。原有真实 XRI 烹饪流程继续完成 50mL 蛋液、16 块 / 120g 番茄入锅、成菜、烧焦和重置；接水检查也继续通过。

通行角色尺寸和路线坐标已登记在 `ASM_KitchenParameters`。按登记参数单独复测 **1/1** 通过；测试隔离其他场景时保留测试运行器，避免单独执行时停住。正式评审与工程配置共 63 个受保护文件的校验保持一致。

桌椅四脚均校正落地，远档餐椅的最大脚底偏差约 0.63 毫米；吊灯顶盘匹配场景中 3 米的天花板。桌面实体支撑高 0.758 米，桌下中央不被整物凸包堵住。八组 LOD 的面数降低且碰撞保留；两档实际 Unity 画面分别为 `furniture-unity-studio.png`、`furniture-unity-lod1.png`。整机 GLB 为 `Recipe/v004_furniture/refrigerator.glb`，保留独立门轴节点；可运行交互仍由 Unity Prefab 提供。

Windows Player、Quest Link 手感和整帧 VR 性能尚未验收。
