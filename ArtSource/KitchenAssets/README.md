# 厨房资产源文件

生成与使用流程见 `../../Docs/KitchenAssetPipeline.md`。

- `References/`：已确认风格板、单件图和生成提示记录。
- `Manifests/`：每件资产的输入、版本、随机种子、Cloud 任务与验收，`pilot_runs.csv` 为汇总。
- `<asset>/v001/download/`：原始 Cloud GLB 与预览；`TexturesSource/` 是原始独立五图。
- `prepared*/`：米制归一化母版、游戏 GLB / FBX、游戏贴图、派生碎片与预览。
- `tomato/v002/prepared_recipe/`：从已确认 v001 派生的四片 → 十六块两级切分，保留外表面 UV、独立断面材质和每级体积检查；v001 不覆盖。
- `Recipe/v001/`：本地 Blender 制作的冰箱、独立门板、锅铲、切菜板和熟蛋块 GLB / FBX，`model_provenance.json` 记录来源及校验和，不计为 Cloud 任务。
- `tomato_slice/v001/`、`scrambled_egg/v001/`：本次新增的两个真实 Pixal3D 食材任务、原始 GLB、PBR、参考图和处理结果。
- `Recipe/v002_food_visuals/`：从新增生成模型派生的四片 / 十六块番茄和双精度熟蛋网格。当前菜谱采用此版本的可见网格，原交互碰撞与份量不变；`Recipe/v001/` 中程序化熟蛋保留为来源历史，不再作为当前菜谱美术。
- 人物的 `inspection/` 是绑定前基线，`rigged_v008/` 是当前身体动作实验；衣物拓扑仍待精修，面部口型未做。
- 柜体的 `inspection_aligned/` 是归正的生成代理，`authored_v003/` 是空心模块与铰链门；`calibrated_final/` 是同一模块的四视角输入。
- `Validation/`：精选验证摘要和 Unity Play 截图。完整日志、ZIP 和被替代的中间模型留在忽略入库的 `Logs/KitchenAssetPipeline/`。
- `Validation/Recipe/`：完整西红柿鸡蛋流程的动作、份量、测试摘要和 Unity 渲染截图。操作见 `../../Docs/TomatoEggRecipe.md`，诊断日志在忽略入库的 `Logs/TomatoEggRecipe/`。
- `Validation/FoodVisuals/`：模型精修后的当前菜谱截图、旧版对照、Cloud 画面和验证记录，使用说明见 `../../Docs/FoodVisualUpgrade.md`。

临时算法夹具不充当实际生成样例。生成失败、未知费用与尚未通过的美术 / VR 验收需如实保留。所有尺度均为原型制作假设。
