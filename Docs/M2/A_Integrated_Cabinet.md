# 方案 A 冰箱整合柜体

2026-11-16 后续增量：A 当前 integrated-kitchen-m2-v5，v4台面及本文柜墙形态保持，只增加锅灶吸附；接入与验证见 [A_Pan_Stove_Snap.md](A_Pan_Stove_Snap.md)。下文各版本制作记录作为历史保留。

2026-11-16 补充：本文主体保留11-15柜体v3的制作/失败记录。当前 A 已在柜墙形态不变的前提下更新到 integrated-kitchen-m2-v4，调整连续斜边台面及左灶右切配摆位，锅初始在灶架上；最新证据见 [A_Chamfer_Worktop.md](A_Chamfer_Worktop.md)。本轮未重建或修改本文柜墙Prefab。

2026-11-15，feature/zh，未提交。用户明确批准参考图构成、保留双门冰箱、微波炉仅外观、六扇柜门交互，以及必要作者工具和测试修改。图片比例不作为施工尺寸。

## 实际接入

- 产品仍由 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity` 在运行时加载方案世界；本轮不重建或保存主场景。
- A 世界：`Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab`，版本 `integrated-kitchen-m2-v3`。
- 新嵌套资源：`Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_A_IntegratedCabinet.prefab`，实例名 `A_IntegratedCabinet`，与 CookingStation 同级、挂在方案世界根下；位置、朝向取未移动的冰箱。
- 专属材质和转角派生网格在 `Assets/_Project/Art/ReviewV2/`。暖白柜体、内衬、深色微波炉面板与五金为本地硬表面制作，不是生成式模型或真实产品规格。
- 原 `CABINET_LAYOUT` ID 保留；新增 `CABINET_LEFT_UPPER_L/R`、`CABINET_FRIDGE_UPPER_L/R`、`CABINET_NARROW_LOWER`、`CABINET_NARROW_UPPER`、`MICROWAVE_VISUAL`。完整目录见 `SCHEME_A_Identities.tsv`。L/R 按正面观看区分，冰箱局部 +X 为画面左方。
- 六扇门复用 `KinematicSwingDoor` 和 `SET_HandInteraction`，门板/拉手各有禁用查询探针；XRI 只登记实际启用的门板和拉手碰撞体。门初始关闭、目标 90°，使用既有手拉交互，没有新增运行时门类。
- 原 RoomReset 扫描本世界 Scene、工作台外的门，负责复位；原门控管理 Setup/Pause；Review/Continue 不重建世界，A→B→A 新建起始世界。
- 微波炉无 Rigidbody、Interactable 或热源，不发布加热事件，也不使 G04 Available。原冰箱、食材、储物区及配方引用保留。

## 制作与空间处理

`XR Apartment → Review V2 → Apply A Integrated Cabinet` 在 Edit Mode 重新制作本轮专属资源，仅重接 A。对应 `ReviewV2IntegratedCabinetAuthoring.Apply()`；完整方案作者工具的 A 分支也使用同一 Attach。重复制作更新同一资源路径/GUID，不重新初始化工程。

尺寸与原型假设详见 `Layout_Assumptions.tsv`。局部 +Z 朝房间。原定门铰链平面 z=0.414m；实拉回归发现窄柜上门阻挡冰箱扫掠，因此左上柜和窄柜门平面后退 94mm 至 z=0.32m、其箱体前沿为 z=0.29m，保留整台冰箱及原完整扫掠。冰箱上柜仍 z=0.414m。柜顶 2.66m，微波炉开放格净空 0.60×0.44×0.50m。窄柜宽 0.29m。上柜靠墙留 55mm 收口，两组上柜之间留 50mm 铰链退让，使拉手也能通过完整扫掠。左上门下缘最终为 1.52m；冰箱上柜门下缘 1.865m，箱体下缘 1.845m。

原台面转角占用下窄柜的开启区。`ReviewV2CabinetCounterMesh` 每次从原 FBX 读取，按冰箱局部 x<0.755 且 z<0.75 的矩形作三角形裁切，保留其余表面、法线、UV、UV2、切线，并以独立封板收口。只替换 A 实例上的可见/碰撞网格；原 FBX、B 和 Cooking 不改。封板退让覆盖门板和拉手，而非只检查关闭位置。冰箱侧封板向后收，不阻碍原 105° 双门目标；踢脚保持在既有冰箱净空探针下方。

## 验证记录

资源与 Core 定向 EditMode 首批 **27/27 Passed**，覆盖六门组件/身份、A/B 配置、会话与版本上下文。该批在转角及铰链最终修订之前，不能代替最终验证。

失败记录保留在 `Logs/ReviewV2/M2/Cabinet/`：

| 批次 | 实际结果及定位 |
|---|---|
| play-first-7.json | 7 项中 6 项失败：外侧上门被 SM_Wall_02 卡住；下窄门穿原台面；踢脚进入冰箱净空；双门受侧封板阻挡；物理食材位置精确相等断言不适合沉降 |
| door-diagnostic.json | 单项复现外侧上门 0°，明确 SM_Wall_02 阻挡 |
| play-second-7.json | 修正台面、封板、踢脚后原方案三项通过；新柜门仍被靠墙收口卡在 2.8° |
| play-third-4.json | 靠墙收口后退，外侧拉手在约 85.7° 碰墙，需扩大侧向余量 |
| play-fourth-4.json | 单扇上门可达 90°；全部同时打开暴露相邻上门拉手冲突，下门被转角封板挡在约 44.85° |
| play-fifth-4.json | 铰链分隔及完整下门扫掠退让后，三项通过；重置仅余约 0.13° 的数值误差 |
| reset-diagnostic.json | 重置没有 Rotation/Recovery blocker；Unity 实测 Quaternion.Angle(Euler(0,0.1329394,0),identity)=0。关闭验收要求关闭目标、进度归零、角度<0.2°；不修改共享运行时或碰撞规则 |
| composite-interrupted.json | 脚本补写触发域重载，中断综合运行；途中还报告原 Cooking 切片落板、冰箱实拉和第二件食材收纳失败。整批不作为验收成功，保留原输出并稳定重跑 |
| play-stable-first-10.json / fridge-diagnostic.json | 稳定十项中柜体/生命周期/切换九项通过；冰箱右门实拉卡在 50.3125°，精确阻挡 CABINET_NARROW_UPPER/DoorPanel。按计划调整左侧柜组前缘，不缩放冰箱、不缩小探针；冰箱验收加强为两门均达到至少 104° |
| play-setback-first-10.json | 左侧柜组后退后，右冰箱门仍被窄柜踢脚限制约 93°；窄柜上门与开放格顶板净距不足，另留上沿/铰链退让 |
| play-stale-authoring-10.json | 资源制作调用早于新脚本导入，仍跑旧几何；随后增加强制刷新并实读资产铰链和踢脚坐标确认已使用新版本，不以这批证明修复无效或成功 |
| play-handle-first-10.json | 修正踢脚后，冰箱右门约 96° 接触窄柜竖拉手；将窄门拉手设在叶片中央，保持柜体宽度和门体扫掠 |
| play-doorframe-10.json | 右门实拉已超过 104°；左门在约 98.4° 接触既有 SM_KitchenDoor。这是与“两个 105° 目标”不同的实际可达角度，继续比较停用/启用新柜体的基线，不改旧厨房门框或降低碰撞约束 |

最终窄柜叶片在开放格侧留 20mm 铰链退让，上叶上缘 1.47m；竖拉手位于各叶中央。左门受既有厨房门框限制，不能将目标 105° 写成两门均实际全开 105°。新增基线测试以同一 Scene、同一门体扫掠，对比停用新柜体与启用新柜体的可达角度；原 105° 配置另由资源测试检查。

最终柜体/生命周期/冰箱定向 PlayMode **11/11 Passed**（`play-final-11.json`）：六门逐一真实 XRI 选择、手拉 90°、关闭、穿透检查；全部柜门同时打开；新增障碍阻挡与移除后继续；Setup 门控；Review/Continue 保留；Reset 关闭；A→B→A 起始状态；原方案切换/占用失败/回退；仅 B 起始及保存 pending/失败保持选择；双门真实手拉。不是全部项目 PlayMode。

`fridge-baseline.txt` 实测：右门停用/启用新柜体均 **105°**；左门即时扫掠分别 **96.81146° / 96.81665°**，差值约 0.005°；真实手拉左门约 **98.4°**。不同运动采样路径会有少量数值差异，但均由既有 SM_KitchenDoor 限制，新柜体不降低基线。两门目标仍 105°。不将这个目标误写成左门能越过既有门框。

最终食材/料理 PlayMode **3/3 Passed**（`recipe-final-3.json`，约205秒）：A 实际携带两件不同食材至冰箱层板完成 G01、A 完整真实料理完成 G02、原 Cooking 完整料理及 Reset。最终资源/Core EditMode **27/27 Passed**（`edit-final-27.json`），包含六门/双探针/XRI列表、原冰箱105°目标、微波炉无热源/交互、A专属转角网格、B原网格、版本/身份/会话验证。各批次独立，不合称一次全量。

本次截图来自运行中 HDRP 场景同一相机，不是参考图或示意渲染：`01-closed.png`、`02-fridge-open.png`、`03-cabinet-open.png`，均在上述 Logs 目录。资源加载冒烟读取主场景实际运行：SCHEME_A / integrated-kitchen-m2-v3 / 六扇柜门 / Setup，帧从1167继续至5930；不构成手动设备验收。

测试清理曾报告 Unity Test Framework 1.1.33 `PlaymodeLauncher.BackgroundWatcher` 退出回调空引用（包内113行，`test-runner-cleanup-exception.json`）。检查发现中断批次遗留的非持久化 DontSave `Code-based tests runner`；在 Edit Mode 仅移除此临时运行器，未改包代码或产品对象。8份新增 InitTestScene 及其meta移到忽略的 Logs/TestRunnerScratch 备份；EnterPlayModeOptionsEnabled 与动态 FNT_KitchenChinese 缓存均以开工 SHA256 验证恢复原始字节。其他已有未提交修改保留。

清理后重新进入/退出主场景 Play：实际读取 A-v3、六扇柜门、1674帧，退出后 Console **0 error entries**（`editor-final-smoke.json`）。最终受保护文件 SHA256 比对 **0 差异**：共享源码、主场景/历史场景、B、工程/输入/构建配置保持开工字节；清单见 `final-preservation-check.json`。本轮源码/文档差异检查通过；没有覆盖预存 Unity 自动材质/字体修改，也未为了全仓 whitespace 检查去清理它们。

## 保留限制

自动化 tracked-hand 夹具真实进入 XRI 选择/释放并移动手位，不等于用户键鼠可用性或 Quest Link 实测。原菜单字小、射线、整体亮度及旧料理提示牌的位置均不属于本轮修改；提示牌仍可能与新柜体视觉重叠，待相应 UI 工作处理。正式 T2/T3、持久化读回、Quest Link/Pilot 仍待联调。旧 Windows 构建是 A-v2/B-v4，不包含本轮 v3，不能用于本轮柜体验收。没有自动 commit 或 push。
