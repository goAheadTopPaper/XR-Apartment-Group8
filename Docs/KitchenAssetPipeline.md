# Comfy Cloud → Blender → Unity 厨房资产流程

2026-10-28 的样例使用已确认的写实、简洁风格。厨房优先：盘子、番茄、胡萝卜、苹果、柠檬、鸡蛋、厨刀均来自实际 Comfy Cloud Pixal3D 任务；人物与柜体另作绑定、模块化样例。所有参考图、任务编号、参数、原始输出与处理结果都保留在 `ArtSource/KitchenAssets/`。

截至 2026-10-30，实际 Cloud 任务累计 24 次。最新桌椅、吊灯与烹饪冰箱的生成结果、硬表面修整和 Unity 验证见 [KitchenFurniture.md](KitchenFurniture.md)；本页下方的早期批次验证保留其当时状态。

## 打开和使用

- Unity 菜单 `XR Apartment/Cooking/Open Cooking Kitchen` 打开 `Assets/_Project/Scenes/SCN_Kitchen_Cooking.unity`，进入 Play。
- 选择冰箱把手开门，再取出层板上的蔬菜、水果或鸡蛋；关门时不能隔门抓取。容器和刀具位于备料桌上。
- 抓住鸡蛋，放在碗口上方按触发键，或在碗口磕开。没有接住的蛋液记为洒出；两半蛋壳向两侧分开。
- 抓住搅拌器，在蛋液里移动尖端，蛋黄与蛋清逐渐混合。倾斜碗，把蛋液倒进煎锅。
- 选择 `STOVE` 加热并把煎锅平放在炉面；当前菜谱持续受热 12 秒变熟、90 秒变焦。拿离热区或关闭炉灶停止累计受热。原有独立资产测试配置仍是 12 / 32 秒。
- 抓住厨刀，接触并向下切果蔬。当前是预先制作的切分变体；番茄先分为四片，抬刀后逐片再切成小块。每片 / 块仍可抓取、加热。用锅铲实际翻动已凝固的鸡蛋，再倾倒番茄块并翻炒成菜。
- 盘子碰撞达到阈值后碎成 8 块；碎片和蛋壳到期回收。食品切片保留供烹饪使用。`RESET` 恢复同一批物体、位置和内容物。
- 原有龙头、水杯、锅和排水盆也放在此厨房内；水系统使用原有容器，蛋液使用新的搅拌碗与煎锅，两套数量分开处理。

此场景是额外的厨房交互样例。正式评审场景、Core 旅程、反馈与 CSV 接口不接入烹饪事件，Build Settings 的启动场景不变。Windows 编辑器可手动打开本场景测试；连接真实头显前按 README §7 关闭自动模拟器。macOS 的键鼠与逻辑测试不代表 Quest Link 验收。

随后已扩展为完整的[西红柿鸡蛋流程](TomatoEggRecipe.md)：开冰箱取食材、两级切配、磕蛋搅拌、放锅开火、倒蛋液、凝固与铲炒、倾倒番茄、翻拌成菜。场景目前以此流程布置；番茄使用从实际生成 v001 派生的 v002，两级切分得到 4 片 / 16 块。冰箱、锅铲、切菜板和熟蛋模型为本地 Blender 制作，来源在 `Recipe/v001/`，本次没有新增 Cloud 任务。独立资产的 12 / 32 秒配置保留，菜谱使用单独的 `SET_TomatoEggCooking`（12 / 90 秒）。

当前食材美术已再次精修：[番茄切面与熟蛋](FoodVisualUpgrade.md) 实际新增两个 Pixal3D 任务，可见切片 / 小块和凝固蛋块均已替换为生成网格与贴图；上一版程序化熟蛋不再用于菜谱。原始下载在 `tomato_slice/v001/`、`scrambled_egg/v001/`，加工结果在 `Recipe/v002_food_visuals/`。前一段描述的是首版来源，当前总任务数为 12。

烹饪场景使用独立的 `LGT_KitchenCooking.lighting` 和 `Scenes/SCN_Kitchen_Cooking/` 烘焙结果，避免沿用已移除摆件的阴影。修改静态环境或光源后，在此场景重新 Generate Lighting；不要覆盖正式评审场景的烘焙文件。Mac 批处理烘焙入口 `KitchenCookingAuthoring.BakeLightingBatch` 保留采样数，使用 Gaussian 滤波以避开本轮降噪阶段的内存压力。

## 可复用交付

| 内容 | 文件 / 位置 |
| --- | --- |
| 单图 Pixal3D / TRELLIS.2 切换图 | `Tools/ComfyCloud/workflows/KitchenAssets_v1_Single_Pixal_Trellis.json` |
| 四视角 Pixal3D 图 | `Tools/ComfyCloud/workflows/KitchenAssets_v1_Multiview_Pixal.json` |
| 三类、三档质量预设 | `Tools/ComfyCloud/presets/{prop,character,building}.json` |
| 每件资产的输入、种子、尺度、任务与验收记录 | `ArtSource/KitchenAssets/Manifests/*.json` |
| 实测表 | `ArtSource/KitchenAssets/Manifests/pilot_runs.csv` |
| 原图、工作流、下载母版、五张源 PBR、处理结果 | `ArtSource/KitchenAssets/<asset>/v001/` |
| Unity 原生模型桥接与材质 | `Assets/_Project/Art/Generated/` |
| 可交互 Prefab | `Assets/_Project/Prefabs/Generated/` |
| 本地制作的碗、煎锅、搅拌器、炉灶与提示 | `Assets/_Project/Prefabs/Cooking/`、`Art/Cooking/` |

GLB 保留为通用母版 / 游戏模型；Unity 2022 通过实际测试过的 FBX 桥接导入，不依赖额外 glTF 包。原始下载与归一化母版分别保留。生成的 ZIP、临时检查模型和完整日志位于忽略入库的 `Logs/KitchenAssetPipeline/`；精选验证摘要和图片位于 `ArtSource/KitchenAssets/Validation/`。

## 从新需求到生成

1. 文字先转成参考图，确认外观后，将清单的 `design_review.approved` 与确认依据填好。不要把风格板直接当作一个 3D 物体输入。
2. 复制一份资产 manifest，设置名称、类别、输入方式、质量、种子、尺度和制作配方；填写参考图并计算 SHA256。
3. 用 `prepare_asset.py` 生成该资产的工作流，导入 Comfy Cloud，上传对应原图。各资产使用独立且稳定的 workflow UUID，不覆盖母模板或其他资产。
4. 查看输入预览再运行。记录任务 ID、实际耗时与页面显示的计算用量。生成 JSON / 图结构无错误不等于任务已完成。
5. 任务资产面板下载整组输出 ZIP，交给 `collect_cloud_archive.py`。它只接受该资产前缀下的母版、五张 PBR 和预览，并记录文件校验和；不同内容不能覆盖同一版原始下载。
6. Blender 先检查完整件，再确认切片 / 碎片配方。修改拓扑、绑定、轴心等属于本地后处理。
7. Unity 导入 `unity_asset.json`，检查材质、尺寸、碰撞与交互，再放入场景。重复生成应提高版本号；导入器拒绝覆盖已有版本。

```bash
python3 Tools/ComfyCloud/prepare_asset.py \
  ArtSource/KitchenAssets/Manifests/plate_v001.json \
  --template Tools/ComfyCloud/workflows/KitchenAssets_v1_Single_Pixal_Trellis.json \
  --model pixal3d --output ArtSource/KitchenAssets/plate/v001/KitchenAsset_plate_v001_Pixal3D.json

python3 Tools/ComfyCloud/collect_cloud_archive.py \
  ArtSource/KitchenAssets/Manifests/plate_v001.json /path/to/downloaded_job.zip

"/Applications/Blender.app/Contents/MacOS/Blender" --background --factory-startup \
  --python-exit-code 1 --python Tools/ComfyCloud/blender_postprocess.py -- \
  --manifest ArtSource/KitchenAssets/Manifests/plate_v001.json \
  --input ArtSource/KitchenAssets/plate/v001/download/master.glb \
  --maps ArtSource/KitchenAssets/plate/v001/TexturesSource \
  --output ArtSource/KitchenAssets/plate/v001/new_prepared
```

Windows 换成本机 `blender.exe` 路径。脚本以 Blender 5.2.2 LTS 验证；输出目录必须为空。加 `--inspect-only` 只处理完整件。普通物品走 `XR Apartment/Assets/Import Prepared Kitchen Asset`；角色、柜体的专门后处理结果走 `Import Authored Character or Module`。`Rebuild Authored Workbench` 重建制作工具管理的厨房 Prefab，会恢复其默认摆设。

## 输入图、节点与参数

单图：一件完整物体，1024 方图或更大，轮廓清晰、留边、光照均匀，无文字、标尺、强景深或其他物体。人物需完整中立姿态；被遮挡的手、背面、刀背和盘底必须在三维结果中检查。

多视角：四张独立的正面、物体左侧、背面、物体右侧图。姿态、世界尺度、画布、焦距、距离与光照相同；狭窄侧面保持狭窄，不能逐张按轮廓放大。共享裁切、1024 统一缩放、去背景、黑底合成与四路预览均在图中提供。默认共享裁切为 `[0,0,1024,1024]`，可在 manifest 的 `multiview.shared_crop` 修改。

节点坐标为 Z-up，镜头看原点：front `(0,-d,0)`、left `(+d,0,0)`、back `(0,+d,0)`、right `(-d,0,0)`；四个镜头 FOV 20°、距离相同。本轮四视角输入由同一已整理柜体渲染，属于可校准的流程对照，不冒充四张独立照片。AI 文生多视角仍需人工检查几何一致性。

实际账号可用节点包括 `LoadImage`、`RemoveBackground`、`Pixal3DMultiViewConditioning`、四阶段 `KSampler`、`RemeshMesh`、`DecimateMesh`、`UnwrapMesh`、`MeshToFile3D`、`Save3DAdvanced`、`SaveImageAdvanced`、`RenderMesh`。使用官方原生 Pixal3D / TRELLIS.2 INT8 convrot、shape/texture BF16 VAE、DINOv3 和 BiRefNet；未使用付费合作方 API 节点。具体模型文件名与固定模板来源见 `WORKFLOW_INDEX.json`、`upstream/PROVENANCE.json`。

主要控制在 manifest：`asset_name`、`asset_class`、`input_mode`、`quality`、`seed`。`parameters` 可覆写预算，例如 `{"master_faces":150000,"master_texture":2048,"game_triangles":3000,"game_texture":1024}`。四阶段种子为 seed 到 seed+3，全部 fixed；平台更新后不承诺逐位重现。单图 `--model trellis2` 切换对照模型；多视角只接受 Pixal3D。本轮实际生成用 Pixal3D，TRELLIS.2 开关与接线已测试，尚未另跑对照任务。

| 标准档 | 云端母版目标面数 | 颜色 / 金属 / 粗糙、法线、AO | 游戏三角形预算 | 游戏贴图 |
| --- | ---: | --- | ---: | ---: |
| 物品 | 100000 | 2048 / 2048 / 1024 | 4000 | 1024 |
| 人物 | 100000 | 2048 / 2048 / 1024 | 30000 | 2048 |
| 建筑或单个模块 | 100000 | 2048 / 2048 / 1024 | 8000 | 2048 |

模板曾在导入时出现 `RemeshMesh` 参数错位，现按固定官方基线校验位置参数与命名参数：768 / solid / 20 / false / 1 / 0 / false / 20 / 0.01 / 20000000。两份母模板清空示例图片，缺少自有参考图时不能误跑官方斧头。

## 后处理与分工

| 资产 / 步骤 | Cloud | Blender / 绑定工具 | Unity |
| --- | --- | --- | --- |
| 普通道具 | 完整外观、母版、PBR | 米制尺度、UV 接缝焊接、减面、FBX、简化碰撞 | Standard 材质、抓取、防穿模 |
| 可碎 / 可切物品 | 先生成完整件 | 从同一确认完整件派生，封断面、保外表面 UV、简化凸包 | 触发、完整件与碎块互斥、物理、粒子、声音、回收、重置 |
| 人物 | 中立姿态静态网格 | 拓扑检查、骨架、蒙皮、动作；生产用关节与衣物拓扑仍需精修 | Generic rig 的站立、走路、抬臂、屈膝样例；面部口型另验收 |
| 外景建筑 | 外观参考 | 明确尺度、LOD、碰撞、独立运动构件 | 外观、坐标和性能 |
| 可进入建筑 / 模块 | 外观参考 | 按布局制作墙地顶门窗及墙厚、接缝、轴心；不把生成实体盒当可信空间 | 通行、运动件、碰撞与登记尺寸 |
| 当前柜体 | 柜体外观代理 | 空心柜身、层板、独立刚性门板、把手与铰链；复用木台面 UV 外观 | 门绕独立铰链开合，柜身保持固定 |

`blender_asset_variants.py --mode character` 和 `--mode module` 是当前人物与 0.6×0.6×1m 柜体样例的本地制作脚本，并非任意人物自动绑定或任意建筑自动模块化服务。柜体生成结果曾出现倾斜和布料状门面，因此刚性柜身 / 门板由本地明确尺寸制作；保留生成代理供追溯，不用其尺寸作空间结论。名义柜体尺寸不含把手突出量。人物仅提供基础身体动作实验，衣物 / 肩部仍需人工精修；没有口型、表情、逐指动画或 NPC 逻辑。

GLB 导入后会因 UV 接缝重复顶点，脚本在保留 loop UV 的情况下按极小位置容差焊接；绑定前再次焊接，避免不同蒙皮权重打开接缝。减面保留 UV，不能称作生产级重拓扑。若手工改变 UV，需重新烘焙贴图。人物权重限制为 3 个影响骨骼，模板输出 20 根骨骼；身体与面部接受不同验收状态。

PBR 原始五图与游戏副本分别保留。底色 sRGB，金属 / 粗糙 / AO 为线性数据，法线按 Normal Map 导入。Standard 的金属图 alpha 为 smoothness，脚本写入 R=metallic、A=1−roughness；不把未知 ORM 打包图冒充独立五图。法线绿通道不盲目翻转，成品在 Unity 光照下目视复核。

盘子为 8 块平面 Voronoi；果蔬为 4 或 6 个预切片；鸡蛋由同一网格向内加 0.0008m 壳厚并横向分成两个半壳。只封新切出的断面，发现未知孔洞、空块或总体积误差超过 2% 即拒绝输出。各块与完整件共用底部中心原点。凸包超过预算时先简化并重新求凸包；盘子完整态使用碎块凸包组成复合碰撞，保留凹槽。

新断面采用独立基础材质；番茄籽、苹果核、柠檬瓣等内部细节尚未制作。刀只触发预切变体，未实现任意平面实时切割。鸡蛋是有限体积、重力平面与弹道流的近似，不是 SPH / FLIP、热传导或食品安全模拟。烹饪时间、质量、冲量与尺寸均为原型参数；空间参数登记在 `ASM_KitchenParameters`。

碗、煎锅、搅拌器、炉灶、粒子和确定性的短声音由本地工具制作，并非 Cloud 生成资产。声音由 `synthesize_kitchen_audio.py` 生成，属占位 Foley。所有可抓物复用 `KitchenGrabAuthoring.ConfigureProp`，不绕过已存在的整物防穿模逻辑。

## 实测、费用与批量门槛

每件 manifest 记录参考 SHA256、任务 ID、模型 / 工作流、种子、原始输出校验和、实际面数、尺度、处理耗时与验收。`pilot_runs.csv` 汇总实际结果，不以图结构验证冒充生成成功。部分任务用量页面显示计算小时，积分扣费未获得逐任务确认；账户菜单在首批 9 个任务前后均显示 6,631，不能据此声称免费或零成本。未知值保留为空。

本轮没有人类在 Blender 中拖拽修模，相关字段记录为 0 分钟；本地整理、脚本编写和多次返修由 agent 完成，总工时未单独计时，保留为空及修改说明。这不代表资产无需修整。人物衣物精修、食材内部细节、真实头显手感及帧率尚未验收，不能据这些样例直接确定整批资产的最终人力预算。

资产流水线阶段 Python 12/12、Blender 7/7、EditMode 138/138、PlayMode 42/42 通过；完整菜谱扩展后全项目 EditMode **145/145**、PlayMode **47/47** 通过。全量测试曾发现切片在多 XR 管理器环境下注册到默认管理器，现已改为继承完整物体的管理器，回归通过。四视角任务实际耗时 181.92 秒，真实 FBX 的 8000 三角形、米制尺度、Y-up 和 Standard 材质导入检查通过；其褶皱门面未通过美术检查，临时导入随后移除，不放入厨房。

资产流水线阶段独立光照烘焙后另跑场景检查 1/1 通过：隔离编辑器之前加载的厨房，验证食材和容器由本场景桌面支撑、实际 XRI 抓住鸡蛋后触发、碗接住并保留 50mL 蛋液，以及重置。测试在等待抓取注册时暂时关闭该鸡蛋的重力，避免慢帧让未抓住的鸡蛋先掉落；运行时代码未因此放宽接收条件。菜谱扩展又增加 5 条场景检查，全流程实测 50mL 蛋液及 14 块 / 110.22831g 番茄入锅并成菜，且继续加热会变焦，重置后食材回冰箱。19 个受保护的原评审场景 / 光照文件校验仍未变，最新预览在 `ArtSource/KitchenAssets/Validation/Recipe/`。

批量起点保留本轮实测的单图物品标准档（4000 三角形 / 1024 游戏贴图），逐件检查后入库。柜体采用明确尺寸的本地模块制作；人物与四视角硬表面结果暂不进入无人复核的批量生产。

最终验证摘要见 `ArtSource/KitchenAssets/Validation/validation_summary.json`。完整测试结果与诊断日志在 `Logs/KitchenAssetPipeline/`，不入库。测试包括工作流参数 / 接线、Blender 封闭性与体积、真实生成 FBX 导入、XRI 抓取 / 切分 / 重抓、蛋液守恒、搅拌 / 倒入 / 受热、场景支撑与重置、角色网格实际变形与接缝、柜体独立铰链。正式 Windows Player 构建和 Quest Link 尚未执行。

参考来源：固定于 `Comfy-Org/workflow_templates@3c2199b9362457b12b9c60c6cc303501526ebd62` 的官方模板；许可证和 SHA256 随 `upstream/` 保存。节点与相机约定来自官方 TRELLIS.2 / Pixal3D 文档与节点源码，模板许可证不替代模型及生成来源记录。
