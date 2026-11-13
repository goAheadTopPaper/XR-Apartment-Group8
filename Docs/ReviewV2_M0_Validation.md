# Review V2 M0 验证记录

日期：2026-11-13（香港时间）。本地基线 `test@bac8771`，工作分支 `codex/review-v2-m0`。未提交、未推送。

## 实际执行结果

| 项目 | 结果与证据 |
|---|---|
| Unity 版本 | 2022.3.62f3；未升级引擎或切换 Android |
| 场景作者生成与结构验证 | 成功。唯一活动 XR Rig、EventSystem、XRInteractionManager、V2 Host/面板，无旧 Journey Host、无 missing script、身份 ID 无重复 |
| 全量 EditMode | **164/164 通过**，其中新增 V2 13 项；`Logs/ReviewV2/editmode-final.xml` |
| 全量 PlayMode，图形模式 | **77/78 通过**：原有 76 项全部通过；新增一项通过，一项因测试未打开冰箱而被真实储物过滤器拒绝抓取 |
| 修正后的 V2 PlayMode | **2/2 通过**；测试先打开真实冰箱、明确绑定交互管理器，未禁用生产过滤器；`Logs/ReviewV2/playmode-v2.xml` |
| Windows64 Development 构建 | **成功**；`Logs/ReviewV2/build.log` 包含 `Build Finished, Result: Success.`；输出 `Builds/ReviewV2/ReviewV2.exe` |
| 面板渲染 | 已生成并人工检查 `Logs/ReviewV2/panel.png`，状态、命令、未保存提示可读；这是桌面相机渲染，不是头显验收 |

没有把两次 PlayMode 的结果描述成一次最终全量 78/78。全量结果保存在 `playmode-full.xml`；失败夹具修复后定向复测通过，未为同一证据重复运行整套物理回归。

最后一次 EditMode 后仅修改了共享菜谱提示中的条件文本；该改动随后通过 Player 编译与构建。原 Cooking 场景绑定 recipeButton，返回文本与以前完全相同；V2 不绑定，隐藏不存在的 RECIPE 操作提示。未修改菜谱状态机、物理、加热、水体或抓取约束。

## 已覆盖行为

- 零目标完成即可 Review、返回和 Finish；未配置存储保持 NotConfigured，不显示保存成功。
- 多目标独立进行、目标单调更新、旧快照不变；无明确证据/未接入目标不推进。
- Cooking 完成仅更新 G02，不进入 Review 或 Finished；Review 期间仍可接收结果事实。
- 暂停、非法命令、终态保护、忙碌状态、重复及迟到回执、旧 Visit 证据拒绝。
- 真正写入验证回执与失败/重试分离；Mock 不能产生 Verified；安全退出使旧保存回执无效。
- 实际加载 V2 场景，通过 XRInteractionManager 选择并释放真实 Cooking 鸡蛋，事件身份与次数正确，重复启停 Bridge 不重复订阅。
- 显式整屋重置完成后更新 Visit，程序释放不伪装成用户事件；Review 往返不改物体位置、菜谱步骤、水量或 Visit。
- 原场景、共享 Prefab、Build Settings 与原始 GUID 未产生产品改动。新场景沿用已有光照和资源依赖。

## 验证过程中处理的问题

1. 初次编译遇到旧 Simulation.SaveStatus 同名类型遮蔽，已用独立 V2 类型别名修复。
2. 用户打开编辑器时，第二个 Unity 批处理实例因工程锁拒绝运行。经用户选择关闭编辑器后继续，没有强制关闭用户进程。
3. `-nographics` 全量 PlayMode 在旧 Cooking 截图测试的 Camera.Render/阴影渲染原生调用中崩溃，没有完整结果。改为 `-batchmode -force-d3d11` 后完成回归；原日志保留在 `playmode-nographics-crash.log`。
4. Unity 导入/测试/构建产生的测试场景、XR Editor 设置、共享 Rig 空白序列化、构建预载设置及额外字体缓存已清理。保留任务开始前的工作区修改；V2 使用单独的字体资产。

## 文件级变更摘要

- 新增独立 V2 Scene、临时世界空间 UI Prefab、字体资产及 `.meta`。
- 新增 Data/ReviewV2 契约；Simulation/ReviewV2 会话、目标、Mock；Runtime/ReviewV2 Host、抓取桥、身份、面板、消费者示例。
- 新增 Editor/ReviewV2Authoring，以及 EditMode/ReviewV2SessionTests、PlayMode/ReviewV2IntegrationTests。
- 唯一既有业务脚本改动：KitchenRecipeController 的一行菜谱切换提示条件；旧入口有绑定时文本完全兼容。
- 新增本记录、`ReviewV2_M0_Integration.md` 和 `ReviewV2_M0_Identities.tsv`。两份原 v0.1 草案及用户预先修改不计作本次实现。

## 未验证或刻意延期

- Quest Link、真实控制器按键/射线手感、真实头显 Recenter/地面校准、舒适性和帧率。测试日志中的无设备 OpenXR 初始化失败/模拟控制器触觉警告不代表这些能力已验证。
- 构建产物已生成，未执行完整 Windows Player + 头显端到端会话。
- 真实 A/B 整厨加载、模型版本切换、跨方案物理世界恢复和持久化反馈保留。
- T3 Marker 创建/回顾/真实保存与读回，T2 正式引导和最终 UI。
- G01/G03/G04/G05 的功能/检测、G02 InProgress 判定、完整 Zone/语义 Anchor 目录。

T2/T3 可立即对接已编译通过的接口与 Mock。下一里程碑应先补全方案/锚点身份目录、T3 存储回执和方案切换前未保存反馈的协调，再接最终模型与用户界面。
