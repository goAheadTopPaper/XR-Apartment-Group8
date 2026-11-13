# Phase 3 场景与文件所有权

**正式决定：`Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity` 是 Group 8 Phase 3 唯一的产品集成与验收主场景。** `SCN_Kitchen_Cooking.unity` 保留烹饪功能回归用途；`SCN_Kitchen_Review.unity` 保留 Phase 2 历史参考用途。它们不是 Phase 3 产品验收的替代入口，也不因本决定而删除或修改。

## 当前依赖审查

V2 从 Cooking 复制而来。两个场景分别持有同一批 **15 个共享 Prefab 的实例**：`PREF_Cooking_Workbench`（含冰箱、食材和厨具）、`PREF_Cooking_WaterStation`、`PREF_Cooking_RoomReset`、水壶/水杯、`PREF_Furniture_DiningSet`、`PREF_Kitchen_Environment`、`PREF_Apartment_Dressing`、`PREF_XR_KitchenRig`、`PREF_Lighting_KitchenDaylight`、`PREF_Lighting_CookingFill`、Teleport 和旧 UI/Layout Prefab。Prefab 源资产变化会传给两边，除非相应属性被场景实例覆盖。V2 另实例化 `PREF_UI_ReviewV2Integration`，并有场景内的 `ReviewV2 Session`、EventSystem、XR Interaction Manager 和 SceneIDMap。

V2 工作台实例上叠加稳定身份与抓取桥，并覆盖菜谱/重置接线；旧活动菜单父对象在 V2 关闭。V2 和 Cooking 各自的摆位、实例属性、增删对象及 SceneIDMap 是独立场景数据。Prefab 源更新不等于场景覆盖自动同步。变更共享 Prefab 时同时在 V2 和 Cooking 检查引用、覆盖和回归；不得用整场景复制替代逐项接入。

**当前烘焙限制：** V2 的 `m_LightingDataAsset` 与 Cooking 指向同一个 `Assets/_Project/Scenes/SCN_Kitchen_Cooking/LightingData.asset`，Lighting Settings 同样指向 `Assets/_Project/Art/Cooking/LGT_KitchenCooking.lighting`。灯光 Prefab 虽共享，烘焙数据不是会随着场景布局自动更新的 Prefab。静态几何、家具、灯光或方案布局变化时，由 T1 为 V2 另存并重烘焙场景专用数据，核对反射探针和资源 GUID，再做 V2 视觉与构建验收。在此之前，不能将 Cooking 的烘焙结果宣称为未来 A/B 布局的正确光照。

## 文件所有权与交付路径

| 负责人 | 主要开发文件 | 接入 V2 的方式 |
|---|---|---|
| T1 | `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`、`Assets/_Project/Editor/ReviewV2Authoring.cs`、Data/Simulation/Runtime 下的 `ReviewV2` 契约与 Host、稳定身份目录 | 唯一负责主场景接线和审核场景差异；维护方案、访问身份与共享资源同步 |
| T2 | 独立的 `Assets/_Project/Prefabs/UI/` UI Prefab，以及 `Assets/_Project/Scripts/Runtime/ReviewV2/` 中的 UI 组件（或明确命名的子目录） | 通过 Host 快照/命令接口交付 Prefab 和接线说明；T1 在 V2 实例化并替换 M0 临时面板，避免修改共享 Cooking UI Prefab |
| T3 | 独立的 Marker/反馈 Prefab、`Assets/_Project/Scripts/Runtime/ReviewV2/` 适配组件及存储实现 | 使用事实事件与完整 Session/Scheme/Visit 身份；交付序列化引用清单、失败/读回验证说明，由 T1 接入场景 |
| T4 | `Assets/_Project/Tests/EditMode/`、`Assets/_Project/Tests/PlayMode/` 的 V2 测试与验收记录 | 以 V2 为产品集成测试场景；另跑 Cooking 功能回归、旧 Review 历史兼容检查，记录桌面与 Quest Link 证据差别 |
| T5 | `Docs/` 下任务脚本、研究与验收记录，不直接编辑主场景 | 在通过所需设备验收的 V2 构建上验证叙事与任务；把发现交给 T1–T4 处理 |

各组员可提交自己负责的 Prefab、组件、测试和接口文档。需要场景引用、锚点或测试物件时，交付 Prefab 路径、GUID/稳定 ID、放置位置、所需引用和验收步骤。T1 审查后单独修改 V2 主场景；避免多人同时编辑同一 `.unity` 文件。共享 Cooking Prefab 的修改须同时说明对 Cooking 回归的影响。

## 厨房资源更新流程

1. 优先修改既有共享 Prefab 或独立资源；记录影响的组件、实例覆盖与稳定 ID。
2. T1 查看 Cooking 与 V2 的实例差异，只将需要的新增对象或覆盖逐项接入 V2。核对 XR Rig、EventSystem、交互管理器、V2 Host 各只有一个，旧 Journey 不被启用。
3. 如果变化影响静态布局或灯光，生成 V2 专用烘焙与反射数据并检查材质、光照和资源引用。方案 A/B 的可替换部分应由独立 Prefab/适配接口承载。
4. T4 运行受影响的 V2 定向测试和 Cooking 功能回归；对涉及方案/设备的变化补 Windows64 构建及 Quest Link 验收。

`ReviewV2Authoring.RebaseOnCooking` 是**整场景替换工具**，不作日常同步。它现在要求交互式确认，或批处理显式参数 `-reviewV2AllowDestructiveRebase`，并先备份旧 V2 场景及身份目录到忽略的 `Logs/ReviewV2/`。备份不能自动合并 T2/T3 的场景接线；执行前仍须审查并在执行后逐项恢复、验证。

## 开发入口与正式切换条件

Unity 2022.3.62f3 中用 `XR Apartment / Review V2 / Open Phase 3 Review` 打开 V2，仅为当前编辑器会话设置 Play 入口。`ReviewV2Authoring.BuildValidation` 以显式 V2 场景列表做 Windows64 验证；`ReviewV2IntegrationTests` 显式加载 V2。默认 Build Settings 仍保持 Cooking 为第 0 场景、旧 Review 为第 1 场景。

正式启动切换需另行评审：真实 A/B 整厨方案、T2 正式 UI/引导、T3 Marker 与可靠存储、目标检测、场景独立烘焙、V2/Cooking 回归、Windows64 构建和 Quest Link 端到端验收均达到团队约定的门槛。切换时由 T1 单独更新 Build Settings，并复核 Editor Play 入口和打包场景列表；M0 测试结果不能代替上述验收。
