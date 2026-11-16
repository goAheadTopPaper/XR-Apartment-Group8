# A 方案斜边台面与料理摆位

后续版本：2026-11-16 A 已增量升至 **integrated-kitchen-m2-v5**，只增加锅灶吸附；本文 v4 台面/摆位与测试保留为几何历史。当前行为与验证见 [A_Pan_Stove_Snap.md](A_Pan_Stove_Snap.md)，B 仍 v4。

2026-11-16（Asia/Shanghai），feature/zh，未提交。用户确认：只处理台面缺口，柜墙前后凹凸不处理；矩形直角改斜边过渡，右侧作为切配工作区，锅初始在熄火的单眼灶上。用户进一步明确“左侧”指**长台面左半边，保留原水槽台**。

## 实际资源与布局

- `PREF_SCHEME_A.prefab` 模型版本更新为 `integrated-kitchen-m2-v4`；B 仍 `external-fridge-m2-v4`，未改 B 资源。版本必须结合方案 ID/Visit 解释。
- 新增 `A_ChamferWorktop` 世界根子物体及稳定身份 `COUNTER_WORKTOP`。`WORKTOP` 保持原 ID，跟随砧板移到右侧；其余已有食材、灶、冰箱、柜门身份保留。
- 新可见/碰撞网格为 `Assets/_Project/Art/ReviewV2/MESH_A_ChamferWorktop.asset`，材质沿用 A 原台面材质，未改变共享材质或整体光照。
- 台面上表面高 1.01m、厚 20mm。它在原下柜退让区上方补回连续支撑面，底面 0.99m，较下窄门上缘 0.985m 留 5mm 原型间隙。原柜体及其前后层次、下柜门所需的下方退让保持。
- 俯视前缘使用一段斜线：世界坐标 `(x,z)=(-2.56,1.67661285)` → `(-1.99,1.81)`，之后连续延至原右端 x=-1.45194459；不是只有颜色覆盖或禁用碰撞。相较旧矩形缺口，该三角形补回面积约 0.038m²，仅为模型参数，不是施工尺寸。
- 左灶位：`(-3.95,1.015,1.95)`；砧板：`(-2.95,1.015,1.95)`。灶具可见模型、旋钮、指示灯、支架、加热面、热区、火焰一起平移，现有配方/热源引用保留。
- 锅初始中心对准灶架；根据实际启用的实体碰撞体底面计算高度，作者初始锅底高于支架 2mm，物理运行后落在支架上。默认不点火。
- 刀具随砧板移动，搅拌碗、备料碗、打蛋器、锅铲、汤勺和盘子集中在右侧准备区。原左侧水槽台、水锅和杯子保留。用户确认方向以面对长台面时的左右为准。
- 物品姿态在世界激活/Awake 前写入 Prefab，由原 CookingStation/RoomReset 缓存；Reset 和 A→B→A 使用新起始姿态，Review/Continue 保持当前状态。

作者入口：`XR Apartment → Review V2 → Apply A Chamfer Worktop and Cooking Layout`，对应 `ReviewV2CounterLayoutAuthoring.Apply()`。既有 A 柜体 Attach 也调用同一摆位方法，避免以后完整制作时恢复旧灶位/矩形缺口。**本轮没有执行柜墙重建入口或改动柜墙 Prefab**，也没有改共享 Cooking 运行时、历史场景、主场景、输入、包或构建配置。

## 本轮验证

| 批次 | 实际结果 | 覆盖与限制 |
|---|---|---|
| `Logs/ReviewV2/M2/Counter/edit-result.json` | EditMode **28/28 Passed** | A/B 资源/模型版本、六柜门、原冰箱目标角、真实台面网格、灶具与热区绑定、锅居中、左右工具分区、稳定身份与 Core 会话；不是全部 EditMode |
| `Logs/ReviewV2/M2/Counter/play-first-result.json` | PlayMode **14/14 Passed** | 原柜门/冰箱/切换/占用回退/保存待定测试；新增斜边区域19个实际支撑射线；锅初始支撑、真实 XRI 点火后加热、Reset 熄火归位、Review/Continue、A→B→A；真实场景截图 |
| `recipe-current-result.json` | **2/3 Passed，1 Failed** | A两件食材G01与原Cooking完整料理通过；A完整G02在Piece_02切配失败，刀具实际高度1.10m而请求向下；未降低验收要求，继续诊断 |
| `recipe-diagnostic-result.json` | **0/1 Failed** | 单跑A流程复现；刀具扫掠接触COL_Dice_03_02/03，说明阻挡来自食材碰撞体，进一步定位是目标还是邻近切片；完整G02尚不能记通过 |
| `recipe-final-result.json` | 最终 **3/3 Passed**（约206秒） | A实际携带两件不同食材完成G01、A完整G02、原Cooking完整料理与Reset；最终源码夹具使用下述前侧空位，未改生产料理逻辑 |

诊断补充：只读查询确认 `COL_Dice_03_02(Clone)` 属于尚未处理的 `Piece_03`，而失败目标为 `Piece_02`。刀具被邻近切片挡住，不是新台面、碗具或高度错误。A 的自动操作夹具需把待切片放在砧板前侧空位，避免在其它完整切片旁下刀；保持真实手持移动、碰撞和切割成功断言，不修改共享运行时或放宽判定。

`recipe-second-diagnostic-result.json` 单跑 **1/1 Passed**（约93秒），未改资源即通过，证明旧摆放路线受切片堆叠影响而不稳定。随后仅调整 A 夹具的逐片切配位置：相对砧板前移的 z 偏移从 -0.025m 改为 -0.105m，仍在实际砧板边界内；其它方案/原 Cooking 保留原路线。最终三项完整回归 **3/3 Passed**。28/28、14/14、3/3分别是独立批次，不合称一次全量，也不覆盖真机。

截图：`Logs/ReviewV2/M2/Counter/01-worktop.png`（面对长台面的实景）、`02-top-view.png`（实际模型俯视）。俯视截图包含原水槽台、冰箱与上柜，未为截图删除家具。图片来自实际 HDRP 场景。

测试结束后恢复 Editor EnterPlayModeOptionsEnabled 和动态 FNT_KitchenChinese 缓存到开工字节；未覆盖已有修改。最后实际进入主场景 Play，读取 A-v4、左侧新灶位和锅、熄火 Setup 状态，帧从11813继续到12008（`smoke-first.json`、`smoke-final.json`）；退出后 Console 0 errors（`final-console.json`）。这只是 Editor 启动与实际加载检查，不是键鼠/Quest Link 完整人工验收。已退出 Play，主场景与 Play 入口仍为 SCN_Kitchen_ReviewV2。

最终保护比对见 `final-preservation-check.json`：共享源码、主/历史场景、B世界、柜墙Prefab及工程/输入/包/构建配置保持开工 SHA256；旧未提交修改保留。源码与文档差异检查通过。不自动 commit 或 push。

## 边界

本轮保留 11-15 柜门/冰箱碰撞规则与柜墙前后层次；左冰箱门仍受既有厨房门框限位，原 105° 目标不等于保证实际达到该角度。菜单、射线、料理提示牌、整体照明和正式 T2/T3 业务不调整。旧 Windows 构建不包含本轮 A-v4；Quest Link/Pilot 未完成。自动化实际 tracked-hand/XRI 与物理支撑通过不代替用户视觉和真机验收。不自动 commit 或 push。
