# 番茄与熟蛋模型精修

用户指出上一版成菜过于简陋后，重新生成了番茄切片和熟蛋块，并集成进现有完整菜谱。参考图由内置 Image Gen 制作；两件三维模型实际通过官方 Comfy Cloud 的 Pixal3D 生成。当前游戏画面见 `ArtSource/KitchenAssets/Validation/FoodVisuals/`，该目录保留替换前后的 Unity 原始渲染截图。

## 实际来源

| 模型 | Cloud 任务 ID | 云端耗时 | 参考 / 原始输出 |
| --- | --- | ---: | --- |
| 带果肉、籽与胶质的番茄切片 | `a85e7bdd-21ac-4286-a904-bbbd4cd317db` | 59.60s | `References/tomato_slice_single_v001.png`、`tomato_slice/v001/download/master.glb` |
| 带自然褶皱和不规则边缘的熟蛋块 | `691f27f7-8e38-47e1-88d7-65d5fe2b828f` | 56.07s | `References/scrambled_egg_single_v001.png`、`scrambled_egg/v001/download/master.glb` |

路径相对 `ArtSource/KitchenAssets/`。工作流 JSON、独立五张 PBR、Cloud 文件校验和和参数在各自版本目录及 `Manifests/` 中保留。本次新增两个单图任务，连同前一批共 **12 个实际完成任务**；没有把本地制作或参考图片计作三维生成任务。逐任务积分扣费尚未核实，费用字段保持 `null`。

两张参考图的完整提示词保存在 `ArtSource/KitchenAssets/References/FoodVisual_PROMPTS.md`；使用内置 Image Gen，没有调用需要 API 密钥的 CLI 回退。

## 集成方式

- 完整番茄继续使用原有实际 Pixal3D 模型。切开后的可见网格来自新生成的切片，再派生为小块，保留种籽腔和原 UV。新切面使用生成参考图的果肉区域。加工脚本为 `Tools/ComfyCloud/blender_food_visuals.py`。
- 云端切片有多余的侧面厚度，本地加工把网格烘焙到现有四片 / 十六块交互代理的包围范围内，保持米制坐标。原有碰撞网格、质量、有限份量和菜谱动作引用保持原实现；这些经过适配的可见体积不作为食材质量或空间评审证据。
- 熟蛋块由实际生成网格减面而来，替换上一版的程序化黄色块。池化 12 件，近景每件 4,000 三角形，远景每件 1,200；两个精度均使用生成的底色、法线、粗糙度和遮蔽贴图。软褶皱网格用于呈现，不用于动态碰撞、碎裂或食材体积计算。
- 熟蛋和番茄的原始贴图颜色保留，熟化时只做轻微暖色变化；烧焦时两个精度都会变暗。锅内托管的番茄以切面朝上呈现，翻炒继续由真实铲头运动驱动。
- 运行时仍使用 `KitchenPanContents` 与 `CookableIngredient`。制作工具 `KitchenFoodVisualAuthoring` 是原 `KitchenCookingAuthoring` 的 partial 文件，复用既有导入器和 Prefab 编辑生命周期。

## 打开与维护

用 `XR Apartment → Cooking → Open Cooking Kitchen` 打开已有场景，再点 Play。菜谱操作见 [TomatoEggRecipe.md](TomatoEggRecipe.md)。

`XR Apartment → Cooking → Apply Generated Food Visuals` 只更新生成食材的模型、材质与煎锅呈现；不会重建桌面摆放或光照。完整 `Rebuild Authored Workbench` 同样接入这套食材，避免重建后恢复简陋版本。新资源位于 `Art/Cooking/FoodVisuals/` 及 `Art/Generated/{tomato_slice,scrambled_egg}_v001/`。

模型、材质和纹理经过保存后重载检查，4 片 / 16 块番茄与 12 件双精度熟蛋引用齐全。最终测试数字、完整动作结果、局部呈现采样和图片校验见 `ArtSource/KitchenAssets/Validation/FoodVisuals/validation.json`；完整诊断留在忽略入库的 `Logs/FoodVisualUpgrade/`。Windows Player、Quest Link 手感和 VR 帧率仍须真实设备验证。

厨房布置和厨具随后继续精修，最新布局、火焰和来源见 [KitchenRoomAndCookware.md](KitchenRoomAndCookware.md)。本页保留该版食材的原始验收记录。
