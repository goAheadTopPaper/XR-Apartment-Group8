# Review V2 占位 UI 使用与 T3 接线

2026-11-17。当前为 T1 独立联调 UI，正式 T2 界面和 T3 数据接入仍待交付。

## 如何试用

1. 打开 `Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity`，进入 Play。
2. 完成“初始设置”，引导页直接显示“任务书”：阅读体验说明和五项活动卡片，再选择“开始体验”。
3. 视野下方显示 **任务书 / Brief、记录 / Mark、回顾 / Review、菜单 / F8** 四个快捷入口。体验菜单中也可打开“任务书 / Activities”。这些入口不要求先完成料理或 Goal。
4. Mark 打开备注编辑页。可以用键盘试填中英文，最多 2000 字；当前不捕捉位置、不创建标记、不提交或保存。
5. 非空草稿按 F8 或“取消/返回”会询问是否丢弃。“继续填写”保留试填内容；“丢弃并返回”清理草稿，返回体验。空草稿直接返回。
6. Review 进入真实的 Review 会话阶段，显示标记列表/详情的占位看板。当前明确显示 **T3 未接入，暂不能读取标记**；筛选按钮可见但禁用。
7. “继续体验”保留世界和 Visit；“结束体验”调用真实 Finish。无存储端口时仍可结束，但明确显示数据未保存。
8. 菜单保留暂停、校准、A/B 切换确认、重置确认和中止。F8 / 左手 Y 继续使用原有输入路径。

Editor 模拟手柄沿用当前项目绑定：右手扳机为 T，左手为 Shift+T，输入需先让 Game 窗口获得焦点。本轮没有修改 Rig、模拟器或输入绑定。备注试填用实体键盘，未增加 VR 虚拟键盘。

## 任务书与活动卡片

文案取自 `XR_Group8_Five_Task_Briefs_and_Unified_Narrative_v0.5.docx` §3.3，并按当前契约表达可用性。任务书属于现有引导/体验页面，不增加会话阶段或任务选择状态。

| 活动 | 当前显示依据 |
|---|---|
| G01 将今天买回来的食材放好 | Host 当前快照的收纳进度 |
| G02 做好一份西红柿炒蛋 | Host 当前快照的真实加工/成菜进度 |
| G03 将做好的菜端上餐桌 | 当前未接入 |
| G04 加热一份剩菜 | 当前未接入；微波炉外观不代表加热已实现 |
| G05 准备一杯热水 | 当前未接入 |

已接入活动显示“尚未开始 / 进行中 / 已完成”。`Available=false` 或缺失的 Goal 显示“未接入”，不计作用户失败。卡片通过同一 Host 的初始快照和 Changed 更新；页面没有手动完成按钮，也不从打开任务书、抓工具或切换活动推断进度。

活动顺序自由，允许并行和中途离开。任务书的“返回体验”、F8 开合及 Review 往返保留当前物品和 Visit；可以在零 Goal/零 Marker 时进入 Review 和结束。确认切换 A/B 或重置后，卡片读取新 Visit 的状态。

## 当前禁用项的含义

| 显示/行为 | 含义 |
|---|---|
| 位置待接入、提交待接入 | T3 捕捉和提交尚未适配，按钮有意禁用 |
| 界面草稿，未提交、未保存 | 仅保留在当前表单中的临时文字；不是正式 Marker，也不是草稿持久化 |
| T3 未接入 | 没有读取真实反馈；不等价于已读取且零条标记 |
| T3 组件已配置，待接口适配 | 引用已挂载，但尚未连接真实调用；不会因为引用非空就启用操作 |
| 存储未接入，数据未保存 | Host 没有真实保存端口，不承诺结束/切换已写盘 |

暂停、Busy、离开 Experience 或会话身份/Visit/模型变化时，表单会清理失效草稿并提示，避免把 A 的试填内容带入 B。禁用 UI 组件也会清理其表单草稿。

为保证文字可读，主面板或快捷栏可见时临时避开相机 FXAA；所有占位 UI 隐藏或控制器禁用时恢复原 FXAA。其他 AA 模式不改，未修改全局画质资产。

## T3 到位后接哪里

UI 挂在会话场景的 `PREF_UI_ReviewV2Integration` 实例上，保留原 Prefab GUID。新增控件由 `ReviewPlaceholderUiAuthoring.Ensure` 和 `ReviewV2Authoring.RefreshPanelPresentation` 原位创建/补齐。

| 组件/字段 | 当前职责 | 收到 T3 后的工作 |
|---|---|---|
| `ReviewIntegrationPanel` | 唯一菜单输入与 T1 命令入口；管理页面和快捷栏 | 保持绑定同一 Host，不另建 SessionController |
| `ReviewActivityBriefView` | 只读展示五项活动；不写 Goal、不发事实 | T2 可复用现有快照语义替换布局，无 T3 依赖 |
| `ReviewMarkDraftView.t3Source` | 允许为空的接入占位；本地表单生命周期 | 根据实际 T3 交付适配位置捕捉、提交/取消及逐条保存结果 |
| `ReviewPlaceholderBoardView.t3Source` | 允许为空的接入占位；列表/详情显示结构 | 连接真实仓储、查询、筛选、详情和失败重试 |
| `ReviewSessionHost.sessionSavePort` | 当前为空；Core 在 Awake 时读取 | 在 Awake 前接入真正实现 `ISessionSavePort` 的 MonoBehaviour，真实写盘并读回后才回成功 |

两个 `t3Source` 字段只表示待适配位置，不定义 T3 公共方法、Marker DTO 或磁盘 Schema。`ReviewBoardDisplayState` 和 `Present` 仅用于 UI 呈现，不是读取数据的 API。T3 交付可能自带捕捉/编辑界面；届时按实际交付做最小适配，不要求对方迁就这些临时控件。

UI 不订阅/缓存事实来替代 T3 上下文业务，不生成 Marker ID，不另建存储，不将旧 Phase 2 CSV 组件当作 V2 T3 成果。

## 已验证范围

- 任务书增量：Panel / Integration / Scheme / InitialScheme 四组 PlayMode **26/26 通过**（job `3ae13c9f2f0a4fc4a16ea468606e3030`，约 18.35 秒）；包含引导首屏、真实切菜更新卡片、开合及 Review 往返保留 Visit、切 B 后刷新、缺失/未接入状态与 16:9/4:3 视野检查。运行结果保存在 `Logs/ReviewV2/ActivityBrief/playmode-results.json`。
- 新任务书的快捷入口、菜单入口和返回体验已通过原生鼠标定位及模拟器 T 扳机链路操作；画面已检查 16:9、4:3，文字无溢出。截图保留模拟手部，当前手部姿态会遮挡部分页面。相关记录在 `Logs/ReviewV2/ActivityBrief/`，不入库。以下 24/24 为此前占位 UI 批次，不能与本批叠加计数。
- 最终 Panel / Integration / Scheme / InitialScheme 四组 PlayMode **24/24 通过**；涵盖 T3 留空、草稿丢弃与失效、Busy/暂停/终态权限、看板状态与长文本滚动、视野布局及既有会话/方案功能。
- 输入检查通过窗口鼠标定位、Input System 的 T/F8 按下/释放事件驱动实际模拟器与 UI 射线链路；相关检查未直接调用按钮回调。备注经过原生键盘输入验证，中英文文字均能进入表单。
- Mark、Review、保留/丢弃、空草稿取消、快捷菜单和 Review 返回体验已实际操作。对应输入事件和状态记录位于 `Logs/ReviewV2/PlaceholderUI/actual-input.jsonl`，不入库。
- 已检查 16:9、4:3 渲染截图，以及俯视打开页面的按钮视野范围。长文本滚动使用隔离的呈现测试字符串，不是演示 Marker 或真实记录。
- 未构建本轮 Windows Player，未执行 Quest Link 验收；T3 标记、历史和真实持久化闭环仍待交付后单独验证。

计划及验收编号见 [执行计划](ReviewV2_Placeholder_UI_Execution_Plan.md)，接口语义见 [T1 接入手册](ReviewV2_T1_Interface_Reference.md)。
