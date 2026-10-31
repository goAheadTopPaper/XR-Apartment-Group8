# 玩家移动设置

## 使用

在厨房评审和独立烹饪场景中，按左手 **Y / Menu** 打开 `MOVEMENT` 面板，键盘为 **F8**。选择 `TELEPORT` 或 `CONTINUOUS`，再关闭面板。初次启动默认瞬移，之后记住本机选择。

| 模式 | 左摇杆 | 右摇杆 |
| --- | --- | --- |
| TELEPORT 瞬移 | 向前推以显示落点，松开确认 | 左右分段转向；向前推也可瞬移 |
| CONTINUOUS 平移 | 按视线的水平朝向前后 / 侧向走路 | 左右分段转向；保留瞬移快捷操作 |

平移默认 1.5m/s，分段转向 45°。它们是当前原型的配置，不代表完成了舒适度验收。瞬移保持面向，使用短暂的黑色眨眼过渡；没有开启连续旋转、飞行或抓拉移动。打开面板时临时阻止摇杆移动 / 转向 / 瞬移和键盘 FPS 走路，关闭后恢复所选模式，不暂停时间或重置房间。

键鼠仍保留先前的 WASD 碰撞漫游，供无头显时接近道具；模式选择主要切换 VR 摇杆输入。要模拟摇杆 / 瞬移，用 XRI 模拟器的设备操作模式（Y 查看提示）。移动设置不占用正式恢复菜单的右手 B / Escape。

## 碰撞和瞬移范围

连续平移复用现有 `DynamicMoveProvider → XRBodyTransformer → CharacterControllerBodyManipulator`，由角色胶囊阻挡柜体和墙面。键盘 FPS 走路继续使用此前修复的同一路径。

原有瞬移射线位于 XRI 的第 31 位 `Teleport` 层，而旧区域的交互掩码为 Default。本次将区域统一到既有 Teleport 层，并在实际 `SM_Ground` 上增加可瞬移标记；没有创建覆盖墙体的大矩形地板或改变原碰撞几何。普通抓取 / 菜谱交互仍使用原交互层。

`KitchenTeleportationProvider` 检查地面支撑、落点的角色胶囊空间及从当前位置到落点的直线胶囊扫掠；柜内、地板外或被墙 / 家具隔开的落点会被拒绝。确认时再次检查，防止门或物品进入路径；打开设置菜单或切换模式会取消待执行请求。查询使用固定容量的非分配缓冲，容量满时保守拒绝。

这是适合当前房间的保守直线路径规则，不是导航网格寻路。转角处需要分段选择落点，不能借瞬移穿过实体障碍。它不提供任意攀爬、跳跃、SAFE-01 边界淡出或 FR-06 设计冲突判断；真实头部追踪不受此胶囊扫掠钳制。

## 代码与制作入口

- `Runtime/Interaction/KitchenLocomotionSettings`：唯一的本机移动偏好状态，通过持久 UnityEvent 调用原 XRI 控制器管理器的公开设置方法；Runtime 不新增 Samples 程序集依赖，也不在每帧反射。
- `KitchenTeleportationProvider` / `KitchenTeleportationArea`：在 XRI 原请求、射线和交互生命周期上增加落点 / 路径约束。
- `Runtime/UI/LocomotionSettingsView`：追踪射线可操作的世界空间面板、独立快捷键和眨眼显示。靠近墙面打开时，面板交互不被墙体遮挡。
- `Editor/KitchenLocomotionAuthoring`：`XR Apartment → Movement → Configure Movement Modes`，只修改共享 Rig、地面和旧瞬移区 Prefab；不重写场景或烘焙。菜单为 Rig 内嵌的 `Prefabs/UI/PREF_UI_LocomotionSettings` 实例。

原有 `KitchenMenuController.ToggleSmoothLocomotion` 保留方法名，但转交同一偏好控制器，不再单独开关移动组件。玩家设置保存在 `PlayerPrefs` 的 `XRApartment.Locomotion.Mode`，不写入 Core、CSV 或任务进度。完整重建烹饪场景会继续引用共享 Rig 和地面 Prefab。

## 验证

`LocomotionModesPlayTests` 加载实际烹饪场景、Rig 和模拟器，检查真实 XRI 摇杆动作的启停、连续走路撞柜、真实射线选择 / 释放后的瞬移与面向保持、落点 / 墙体限制、取消请求、路径后来受阻、F8 打开菜单、追踪 UI 射线命中按钮，以及新 Rig 读取已保存偏好。

最终测试结果及源码 / 资产校验见 `ArtSource/KitchenAssets/Validation/Locomotion/`；完整诊断在忽略的 `Logs/LocomotionModes/`。Mac 编辑器的自动化和局部 CPU 采样不替代 Windows Player、Quest Link、双眼眨眼呈现、摇杆手感和晕动舒适度验收。

2026-10-31：EditMode **145/145**、整批 PlayMode **61/61**，包含上述移动检查 **5/5**。两个实际场景的移动菜单、相机、地面与瞬移 Provider 引用有效，missing script 为 0；四个制作 Prefab 连续配置后逐字节一致。617 个基线文件中仅 3 个预期共享 Prefab 改变，其余 614 个一致，场景、烘焙、Packages、Samples、XR 和工程设置不变。

经过预热的固定空闲落点 CPU 检查 256 次，均可接受，平均约 **0.00237ms**、最大 **0.0173ms**，采样循环分配 **0B**。这是单个落点查询的编辑器局部采样，不是完整射线更新、整帧或头显性能。完整菜谱本次通过，未据此宣称此前物理操作自动化的间歇波动已解决。
