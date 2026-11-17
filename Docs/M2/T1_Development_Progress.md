# T1 / M2 Development Progress

2026-11-17 任务书占位增量（用户批准，Verified — Independent）：现有 Onboarding 展示体验说明和 G01–G05 活动卡片，Experience 快捷栏及菜单均提供重开入口。文案核对 Unified Narrative v0.5 §3.3；沿用自由顺序/并行/零目标可 Review 的叙事。新增只读 `ReviewActivityBriefView`，通过原 Panel 的快照/Changed 展示 State 和 Available，G03–G05 未接入；不写 Goal、不新增会话阶段或 T3 业务。Prefab 原位补齐，无场景/Host/Core/方案世界/输入改动。Panel/Integration/Scheme/InitialScheme 同批 PlayMode **26/26通过**（job `3ae13c9f2f0a4fc4a16ea468606e3030`，约18.35秒），包括真实切菜更新、任务书开合/Review往返保留Visit、B新Visit状态、缺失与不可用Goal、16:9/4:3边界。引导页保留原重新居中/中止/关闭操作，紧凑标题预留失败提示空间；补回这些兼容行为后最终26/26通过，首批26/26记录独立保留。实际模拟器T扳机链路已操作任务书快捷入口、菜单入口和返回；两种比例渲染截图文字无溢出，模拟手部遮挡如实保留。日志 `Logs/ReviewV2/ActivityBrief/`；历史missing-script、StaticLightingSky、夹具多XRManager等警告仍存在，未据此宣称正式T2/T3、Player或Quest Link验收。使用及字段见 [占位UI使用说明](../ReviewV2_Placeholder_UI_Usage.md)。

2026-11-17 最简完整占位UI（用户明确“执行”，P1–P5 Verified — Independent / Ready for T3 Integration）：在原Prefab原位增加体验快捷栏、Mark表单、丢弃确认及Review列表/详情滚动区域；会话命令继续由唯一Host处理，新增`ReviewMarkDraftView`/`ReviewPlaceholderBoardView`管理视图，`ReviewPlaceholderUiAuthoring`负责可重复补齐资源。T3引用允许为空，捕捉/提交/筛选禁用并明确未接入；仅试填UI草稿，不创建Marker或存储。草稿在取消/丢弃或上下文变化后清理，Review往返保留Visit，零标记可结束且明确未保存。最终Panel/Integration/Scheme/InitialScheme四组PlayMode **24/24通过**（job `dbe33455ab9a4d16b3901e2498ab2e3a`，约17.55秒）。此前布局两批23/24失败均是Mark标题高度不足，实测字体行高后改为两行标题，几何单项复测通过；截图又发现筛选标签高度不足，修复后补溢出断言。既有missing-script/Static Lighting Sky及测试夹具多XRManager警告保留；实际界面检查控制台0错误。输入验证使用窗口鼠标定位、InputSystem T/F8事件→模拟器→UI射线链路，以及原生中英文备注输入；未用直接按钮回调代替实际输入。16:9和4:3页面截图及事件日志在`Logs/ReviewV2/PlaceholderUI/`。原Prefab 194个既有对象ID保留；无正式T3/Player/Quest Link验收，P6仍待交付。上手操作与接线见 [占位UI使用说明](../ReviewV2_Placeholder_UI_Usage.md)。

2026-11-17 占位UI执行计划（仅规划，Proposed）：负责人要求先写计划。新增 [最简完整占位UI执行计划](../ReviewV2_Placeholder_UI_Execution_Plan.md)，基于当前Host/临时菜单规划设置、引导、体验快捷入口、Mark编辑、Review看板、结束状态和T3留空行为；用户已解决点击问题，不重新改输入。P1–P5可独立实施，P6待T3实际交付；列明实际输入验收、空引用/草稿/Visit/保存边界及接入清单。本轮只写文档，不修改代码/Prefab/场景、不运行Unity测试；实施等待明确批准。

2026-11-17 临时A/B切换菜单（用户批准，Verified — Independent）：在已有占位UI中新增“切换到方案B/A”和独立确认/取消，复用现有Core/ReviewSchemePort，不改方案世界、公共接口或默认Build Settings。Experience开放，Setup/Onboarding/Review/暂停不开放；取消保持世界与Visit，切换成功更新实际方案、版本和新Visit，失败显示原因并可重试；未接T3时继续明确数据未保存。同步/异步结果按requestId关联，避免Accepted误报成功。最终Panel/Scheme/InitialScheme三组PlayMode **10/10通过**（job `7dba933aa0364ab994fcda00d7c25336`，约3.43秒），包含真实按钮A→B→A、取消、家具阻挡回滚/重试和合成保存等待/错回执/失败重试。首批5项中4项通过，合成保存夹具因同时启用两套世界灯光触发HDRP单方向光阴影限制；已改为先停用夹具中的旧世界，不屏蔽错误、不改产品灯光。既有missing-script/Static Lighting Sky和合成夹具多XRManager警告保留，不宣称零警告。实际主场景Play中从按钮确认切换至B-v8，菜单/确认/结果页检查均无文本溢出、可见按钮在视野内，控制台0错误，截图 `Logs/ReviewV2/switch-{menu,confirm,result}.png` 不入库；未做Windows Player构建或Quest Link验收。

2026-11-17 占位菜单可读性修复（用户批准）：保留 `ReviewIntegrationPanel` 及原 Prefab 对象/GUID/Host 接线，在原资源上增量更新；正文/按钮从19号改为30/32号，标题40号，面板宽960、按可见行自适应高度，打开时位于当前视线前0.9m。按Setup、Onboarding、Experience、Pause、Review、Reset确认及终态显示相关操作，明确“初始设置”“操作引导”和未保存提示；暂停页保留移动方式/校准。原运行时检查复现17按钮同屏/19号字，修复后Setup仅8按钮，无溢出且均在视野内。相机FXAA开/关截图对比确认细字毛边，菜单打开期间仅临时停用FXAA，关闭/禁用恢复，其他抗锯齿模式和全局画质不变。验证结果与范围见本轮Integration Manifest增量；正式T2/T3、Windows Player和Quest Link验收不据此提前通过。

2026-11-17 接口交接：负责人批准先核对实现并整理接口清单，随后要求补充如何通过接口实现产品逻辑。新增 [T1 接口清单与产品逻辑接入手册](../ReviewV2_T1_Interface_Reference.md)，核对 `ReviewContracts`、`SessionController`、Host、方案端口、事实桥及 A-v5/B-v8 身份目录，补齐 T2 引导/确认/回顾/结束与 T3 标记/上下文/保存/历史的调用顺序、边界和联合清单；README、Contract、Manifest 增加入口。本次只改文档，保留既有工作区修改，无 commit/push。文档链接、DTO 属性和命令覆盖检查通过，A 81/B 78 个稳定身份唯一且版本与 Prefab 一致；文档范围空白检查通过。全工作树 diff 检查仍报告本轮开始前已有材质/字体资产的尾随空格，未修改这些文件。未重跑 Unity 或宣称正式 T2/T3/设备通过。

2026-11-16 B冰箱留缝增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v8。冰箱仅向水槽横墙靠近10cm，目标净距约7.7cm，105°及碰撞规则保留；其余v7布局保持。实测最小净距0.077018m；Edit35/35、Play24/24、B收纳/完整G02 2/2分批通过，10文件重复作者一致。Windows64构建0错误/0警告及启动冒烟通过，2811保护文件不变，用户未保存B设置保留。T1 Verified — Independent / Ready for Integration，Quest Link仍待验收。见 [B_Fridge_Wall_Gap.md](B_Fridge_Wall_Gap.md)。下文为此前历史。

2026-11-16 B加长板/微波炉/工具布局增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v7。悬空板1.56m、左段下方固定微波炉；B工具按A水平布局和朝向同步，因台面低1cm相应下降，B台面形状保留。Edit35/35、Play24/24、完整料理/收纳5/5分批通过，10文件重复作者字节一致；Windows64构建0错误/0警告、默认A启动冒烟通过，2792保护文件不变。B Player交互/Quest Link仍待验收；T1 Verified — Independent / Ready for Integration。见 [B_Extended_Shelf_And_Tools.md](B_Extended_Shelf_And_Tools.md)。下文为此前历史。

2026-11-16 B收纳设计增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v6。新增原木四格窗下开放柜、接续台面的暖白细框双门地柜和单层悬空层板；保留冰箱/横墙/门洞。Edit34/34、兼容Play23/23（含新交互5项）及10文件重复作者字节一致已通过；料理/收纳首批4/5，B完整G02相同源码单项复测1/1，保留切菜物理批量波动。Windows64构建0错误/0警告、启动冒烟通过；3726保护文件不变，用户Editor未保存B起始保留。T1 Verified — Independent / Ready for Integration，Quest Link及正式联合仍待验收。接入、参数、失败和实际截图见 [B_Storage_And_Prep.md](B_Storage_And_Prep.md)。下文为此前版本历史。

2026-11-16 B 北移增量：当前 A **integrated-kitchen-m2-v5**、B **external-fridge-m2-v5**。B冰箱目标(-4.65,0,-0.20)m，北端铰链靠水槽侧横墙、南端把手，105°目标；门/食材/储物/稳定身份接线保留，新增B专属派生门体网格和可重复作者配置。Edit31/31、Play18/18、完整料理/收纳5/5分批通过；横墙净距实测17.70cm、作者12文件重复字节一致。A-v5/B-v5 Windows64构建及启动冒烟通过（0错误，2条MCP断连警告），3703个受保护文件保持；T1 Verified — Independent / Ready for Integration，正式联合/Quest Link仍待验收。参数、截图与失败记录见 [B_North_Fridge.md](B_North_Fridge.md)。下文为此前版本历史。

2026-11-16 锅灶吸附增量：用户批准 A 炒锅放下吸附、抓取解除，A 当前 **integrated-kitchen-m2-v5**，B 保持 **external-fridge-m2-v4**。新增A实例专属ReviewPanStoveSnap，复用原XRI和完整锅体扫掠，不改公共Cooking逻辑、共享锅、布局或Goal判定。Edit29/29、Play19/19独立通过；原Cooking完整料理和A收纳批次2/3，A完整G02单独复测1/1通过。锅铲夹具改为从上方入锅，保留侧入被锅沿挡住的失败及旧切菜批量波动；不合称一次全量。主场景冷灶吸附启动检查通过，359个受保护既有文件一致，仍待设备验收。参数、接入与编译失败修复记录见 [A_Pan_Stove_Snap.md](A_Pan_Stove_Snap.md)。既有v4台面与摆位保持，下文是此前历史。

2026-11-16 增量：用户批准 A 台面直角缺口改斜边、长台左灶右切配、锅初始放熄火灶架上；确认保留原水槽台，不处理柜墙凹凸。A 当前 **integrated-kitchen-m2-v4**，B 保持 **external-fridge-m2-v4**。EditMode **28/28**、台面/锅/热源/柜门/切换PlayMode **14/14**、A收纳/A完整G02/原Cooking完整料理 **3/3** 分批通过，T1 Verified — Independent。首批料理2/3失败、邻片碰撞诊断及A夹具前侧空位修正均保留。参数、接线和证据见 [A_Chamfer_Worktop.md](A_Chamfer_Worktop.md)。本轮只改 A 专属资源与作者/测试，保留既有未提交修改；柜墙Prefab、B、共享源码、主/历史场景保持，正式联合/设备仍待验收。下文是11-15及之前历史。

更新：2026-11-15（Asia/Shanghai）。本轮用户批准方案 A 整合柜体：A 已升为 **integrated-kitchen-m2-v3**，B 仍 **external-fridge-m2-v4**。新增 A 专属柜体 Prefab、六扇沿用既有逻辑的可拉柜门、微波炉固定外观和 A 专属转角派生网格；主场景、原 Cooking、共享运行时及 B 不改。最终柜体/切换/冰箱 PlayMode **11/11**、A 两件食材 G01/A 完整 G02/原 Cooking 完整料理 **3/3**、资源与 Core EditMode **27/27** 分批通过（不是一次全量）。截图、稳定身份、尺寸、失败/修复和证据见 [A_Integrated_Cabinet.md](A_Integrated_Cabinet.md)。左冰箱门仍受原厨房门框限位，新柜体不降低基线；旧 A-v2 Windows 构建不含柜体改造。菜单可读性、射线、整体亮度及旧提示牌位置仍未解决；正式联合/设备验收仍未完成。下文 11-14 的批次和失败保留为历史。

更新：2026-11-14（Asia/Shanghai），Uncommitted，feature/zh。当前 Narrative Goal：M2-G04构建与验收准备，G01–G03独立能力已实现并分批验证。**M2联合及设备验收未完成**。

## 最新状态

| 项目 | 事实 |
|---|---|
| Part 1 | 工作流已落地Docs/ReviewV2_Development_Workflow.md，AGENTS增加Phase3入口，保留Phase2历史 |
| Part 2 | M1 Manifest已建立，原G01/G02独立证据保留；反馈闭环Integration Pending，不阻止M2 |
| M2审查 | 已检查真实代码、共享资源、旧Layout Lab边界；Coverage Matrix与依赖图在Narrative_Goal_Plan.md |
| 当前实现 | A/B完整世界端口已接唯一V2主场景；请求前固定版本，仅成功更新Scheme/Visit/Goal；失败/保存等待保留原世界与抓取；Core/资产及真实场景定向验证已通过 |
| 布局 | A=integrated-kitchen-m2-v2、B=external-fridge-m2-v4；两案真实料理/开门/搬运和G01完成已分批通过；几何、地面、HDRP旧烘焙隔离及两角度图像检查通过；不证明设备舒适性 |
| 当前构建 | Windows64开发构建成功，当前A-v2/B-v4，约560MB，BuildReport 0 errors/0 warnings；Player启动进程存活且未见托管异常，已结束自启测试；未接头显 |
| 外部依赖 | T2菜单/确认、T3Marker/持久化Integration Pending；Quest Link及Pilot Device Pending |
| 下一步 | 接T2正式菜单/引导/确认与T3Marker/真实存储，按Manifest验证叙事A–E、失败重试及历史保留；T4/T5执行Quest Link和Pilot |

**当前交付：** SCN_Kitchen_ReviewV2仍是唯一产品主场景，初始A，可在Awake前配置仅B起始；M1原对象原位保留停用，M1Regression仅供历史测试。综合主场景21/21、持物修复17/17、两件实际食材完成G01专项2/2、最终HDRP修订后EditMode26/26及PlayMode6/6均分批通过，不能合称一次全量。最新构建摘要在Logs/ReviewV2/M2-build-summary.txt，当前源码已构建。正式T2/T3和设备保持待联调，M2不标Verified — End-to-End。

## 决策记录

下列M2-xx仅为本开发日志索引，不是新增产品需求或Phase 2的正式D编号；Codex的技术取舍保留Proposed，不追认为负责人Approved。

| 编号 | 方案与理由 / 影响 | 状态 |
|---|---|---|
| M2-01 | 用独立里程碑状态取代整体等待T2/T3；复用M1既有接入点和证据。只延期相应联合测试 | Approved：本轮负责人完整指令 |
| M2-02 | 现ISchemePort回执无版本；采用可选ISchemeModelCatalog兼容增量，保留原签名。版本在请求前捕捉，缺失拒绝，成功后才更新。替代方案是改原回执签名，会破坏消费者 | Proposed：授权内可逆T1选择，实施中 |
| M2-03 | Core新增初始Scheme重载；旧调用仍Baseline/Mock默认值。生产布局必须真实绑定后才能指定A/B，避免仅重命名 | Proposed：授权内兼容增量 |

## 实施与验证记录

2026-11-14：读取本轮完整指令、当前Git状态及M1既有记录；未丢弃原修改。Part1/2文档完成，M1顶部按分层状态追加说明，历史失败/Blocked记录保留。

新增ReviewV2SchemeContextTests覆盖A→B→A、请求期版本固定、失败/错误/重复回执、旧Visit隔离、零进度初始化和Review/Pause禁止切换。新增只读ReviewV2SchemeInspection导出场景根和静态碰撞体，不保存场景。

Unity2022.3.62f3只读审查执行成功（M2-inspection.log），报告位于Logs/ReviewV2/M2/scene-inspection.txt。现有冰箱右侧墙位置(-1.81,0,1.055)，餐桌(-2.5,0,-1.25)，餐厅窗沿x约-5.125、z约-1.98至0.26；这些仅为模型内部坐标，不能代替碰撞和视觉验证。Core定向测试 **23/23 Passed**（M2-context-editmode.xml），仅包含ReviewV2SessionTests与ReviewV2SchemeContextTests，不包括全部EditMode或真实布局。

实现中新增ReviewSchemeWorld/ReviewSchemePort及Host可选注入、当前世界重绑定。独立运行时Scene承载完整世界，避免KitchenRoomReset跨世界扫描；Rig/UI/会话保持独立。目标Prefab必须以inactive保存；目标引用检查后再激活，检查头部与新家具接触，失败回退旧世界，成功才回执Core。门控刷新当前世界交互过滤器。**资源尚未制作，运行时端口待编译/测试，不能记为生产能力已验证。**

决策M2-04（Proposed，授权内可逆）：采用完整世界Prefab+独立运行时Scene，而非在同一Scene并排两套工作台或移动单冰箱。理由：现有RoomReset按Scene扫描，双世界同Scene会互相重置；整体资源能让对象引用随方案一起加载。代价：增加资源体积、需检查首次初始化与Scene卸载；保持共享Cooking源码行为。

M2-05（Proposed）：KitchenFridge兼容新增可选secondaryDoor，默认null维持原单门行为；A用两扇独立可拉门，完整储物区要求两扇均打开，避免从关闭的半边隔门取物。Reset关闭两门、灯按任一门开启；这是一处局部适配，须重跑原Cooking回归，不视为大幅共享重构。

M2-06（Proposed）：新布局暂用实时灯光/反射，清除Prefab渲染器的旧lightmap索引，M2隔离场景不引用Cooking烘焙；待视觉验证及性能检查再冻结照明。旧Cooking/V2烘焙不覆盖。B冰箱中心(-4.65,0,-1.3)、朝+X；A保留右墙位置，改双门并增上部柜，B在释放区增备餐台。均为代表性原型参数，未经空间体验/工程合规验收。

验证续记：Host/门控增量的M1定向PlayMode **10/10 Passed**（M2-host-gate-playmode.xml），本批未包括后续双门适配。作者首轮失败（M2-author.log，退出1）：SceneIDMap的SceneObjectIDMapSceneAsset不能存入Prefab；排除这个编辑器场景映射对象、增加保存失败立即抛错后，作者复跑成功（M2-author-retest.log）。原ReviewSceneIdentity目录完整保留；原M1源场景未保存。新资源见Schemes目录及SCHEME_A/B_Identities.tsv。实际方案PlayMode结果待XML。

实际方案首轮 **0/2 Failed**（M2-scheme-playmode.xml）；诊断复跑仍 **0/2 Failed**（M2-scheme-diagnostic.xml）：正常头部(-3.15,1.361,1.12)被SM_Wall_01的ClosestPoint返回自身误判。改为实际Sphere/Mesh ComputePenetration，保留半径0.18m原型假设，未删除目的地阻挡检查。正在复测真实A→B→A、占用失败恢复，并首次运行A/B完整料理和原Cooking完整料理。此轮尚未有最终XML，不能提前记成功。

后续源码增量（待下一批验证）：失败回退不清Host原料理边沿缓存；配置ISessionSavePort时，切换先保存旧快照并验证回执，失败留原世界可重试，未接端口仍明确NotConfigured。新增纯逻辑保存前置测试；新增资源局部引用/身份检查；将实际搬运夹具按当前冰箱坐标扩展到A/B，夹具不直接设置食材到Zone。完整料理夹具适配嵌套工作台，保留原Cooking入口。以上测试代码尚未全部执行，后续必须定向验证。

实际方案/料理批次 **4/5 Passed，1 Failed**（M2-scheme-recipe.xml，约280秒）：A→B→A与Reset通过；A完整真实料理、B完整真实料理及原Cooking完整料理均通过。占用目的地未拒绝，为未解决失败，不能记失败恢复Verified。新增定位消息并启动占用专项与A/B实际搬运测试，待结果。本批不包含之后保存前置和失败回退缓存修复，不与后续批次合成全量证据。

后续验证（保留上述历史失败）：

| 批次 | 实际结果 | 覆盖与限制 |
|---|---|---|
| M2-transport-diagnostic.xml | 2/3 Passed，1 Failed | A/B各自真实XRI袋→层板搬运通过；占用拒绝仍失败。诊断头部确实到达目标，OverlapSphere命中冰箱/窗体，ComputePenetration漏检；未放宽目的地安全规则 |
| M2-switch-retest.xml | 12/12 Passed | 改为目标Scene限定的OverlapSphere，真实占用拒绝/原世界保持/重试、A→B→A/Reset，以及M1 10项回归通过 |
| M2-anchors.log | 作者工具成功 | 对两个专属Prefab增量追加WORKTOP、DINING_TABLE、SINK、SCENE_ANCHOR；原ID保留，目录刷新；未改变主场景 |
| M2-assets-context.xml | 26/26 Passed | 当前Core保存前置、模型版本、回执/Visit、旧会话兼容及A/B资产局部引用/食材身份；合成保存回执仅证明Core，不证明真实持久化 |
| M2-door-visual.xml | 3/4 Passed，1 Failed | 当前真实双向切换/占用失败恢复、A双门真实手柄拉动通过；ScreenCapture批处理未产图，视觉未验收 |
| M2-visual-rt.xml | 执行中 | 改用HDRP Camera目标RenderTexture正常帧渲染并读回PNG；产图后仍须实际检查，不能仅以测试文件存在证明视觉质量 |

最新下一步：完成视觉与空间检查，再将已验证的完整方案增量接入唯一V2产品主场景，保留M1原始世界/回归证据；随后检查实际主场景引用与构建准备。正式T2/T3和Quest Link/Pilot仍分别Integration Pending / Device Pending，不能据独立Editor通过标完整M2验收。

视觉核查续记：M2-visual-rt.xml **1/1 Passed**，正常帧Camera→RenderTexture读回实际产出A/B PNG并已人工查看。图像不是视觉验收成功：B窗边原木搁架与新冰箱区域重叠，且此前占用诊断OverlapSphere同时命中SM_WindowTable及冰箱，构成真实空间缺陷。保留原图SCHEME_B-v1-overlap.png，A外部视角图也单独保留。新增B专属资源修订：仅在B副本禁用原窗边搁架，把该区域明确分配给冰箱；版本升为external-fridge-m2-v2，目录同步。新增封闭冰箱包络与固定家具交叉检查、B真实手拉开门，以及新的内部A视角；修订后验证待结果，不追认v1为空间通过。

后续v2批次 **8/8 Passed**（M2-B-v2-geometry.xml），包括A/B实际开门与搬运、闭合包络无固定家具交叠、切换和截图。但图像检查发现移走搁架后暴露窗下开口及地面边缘；保留SCHEME_B-v2-lower-void.png。B专属补窗下围护和连续地面，版本升external-fridge-m2-v3；新增射线支撑检查。v3批次 **6/6 Passed**（M2-B-v3-geometry.xml）：两向切换/失败恢复、闭合包络和地面、B实际手拉开门/搬运及截图。已查看v3图像确认下部开口封闭；这不证明VR舒适性或建筑合规。

主场景集成工具成功执行（M2-main-integration.log），未删除原M1对象，旧M1测试明确加载M1Regression，新M2测试改为加载唯一主场景。新增Device_Acceptance.md准备Windows64、Quest Link与约20分钟自选探索Pilot；确认本机有Windows支持模块，实际构建尚未运行。当前未commit/push。

负责人临时要求A/B平面图：依据实际方案坐标制作会话内平面示意，标示双门/单门冰箱、柜体/新增备餐面、餐桌与可选路线；墙体简化、非施工图。该沟通图不代替Unity实际场景验收，原开发目标继续。

## 收尾审查与后续修复

实际主场景综合定向 **21/21 Passed**（M2-main-current-playmode.xml）：历史M1十项、实际主场景A/B往返/占用失败/几何、真实双门/单门手拉、两案搬运、A/B完整料理和原Cooking完整料理、截图。不能将此较早批次当作后续全部修复的一次全量结果。

持物失败边界复现 **1/2 Passed，1 Failed**（M2-held-rollback-before.xml）：仅B起始、零Goal Review通过；占用失败会提前取消用户抓取。根因为Busy门控主动松手，以及几何预检前先停用旧世界。M2-07决策（Proposed，授权内可逆）：等待/失败阶段只阻止新选择，允许原选择继续；同步激活目标做几何预检，无帧/物理推进，预检成功后才停用旧世界。复测 **17/17 Passed**（M2-held-rollback-retest.xml），包括异步保存Pending/Failed保持选择、实际目的地失败持物保留、双向切换/Reset、仅B起始、M1回归和两案搬运。新增测试回执不含实际磁盘IO。

补足实际G01完成：两案各搬入两件不同的真实鸡蛋，受原碰撞约束移动/释放后得到两个IngredientStored并完成G01，Phase与Visit不变，**2/2 Passed**（M2-two-grocery-completion.xml）。当前Core/资产最终批次 **26/26 Passed**（M2-final-editmode.xml，照明专项修订前）。

Windows64首次构建成功（M2-windows64-build.log），持物修复后刷新构建成功（M2-windows64-final-build.log；BuildReport摘要M2-build-summary.txt，当时A-v1/B-v3，约560MB、0 errors/0 warnings）。这是开发构建，不是Player完整交互或Quest Link验收。

照明隔离专项发现新缺口：旧作者逻辑仅设置Unity ReflectionProbe，HDRP自有探针仍Baked并引用Cooking旧反射。新增资产检查 **0/2 Failed**（M2-hdrp-probes-before.xml）。M2-08决策（Proposed）：两案HDAdditionalReflectionData明确Realtime/OnEnable，清旧baked/custom纹理，保留旧Cooking源资源；照明影响模型上下文，版本升A=integrated-kitchen-m2-v2、B=external-fridge-m2-v4，目录和主场景配置同步。修订后Core/资产 **26/26 Passed**（M2-hdrp-final-editmode.xml），包含HDRP模式与旧纹理清空。当前正复查PlayMode与渲染图像（M2-hdrp-final-playmode），之后需刷新构建，不能交付旧版本冒充新版本。

最终照明修订复查：**6/6 Passed**（M2-hdrp-final-playmode.xml），包括初始仅B/零Goal Review、异步保存Pending/Failed持物保持、真实双向切换/占用失败、闭合冰箱包络和地面支撑以及图像产出；已再次查看A/B PNG，未见此前搁架交叠和窗下开口。当前Windows64构建再次成功（M2-hdrp-final-windows64-build.log）：A-v2/B-v4，560119034 bytes，0 errors/0 warnings，约21.39秒增量构建。未宣称Player完整操作或Quest Link验收。

## 授权范围完成审查

| 指令项 | 当前证据与边界 |
|---|---|
| Part1：叙事、集成边界、六层状态、风险分级、动态记录 | ReviewV2_Development_Workflow.md及AGENTS入口已落地；M1/M2各有Progress和Manifest；Proposed选择、历史失败均保留，Phase2历史未覆盖 |
| Part2：保留M1、明确T2/T3交付与联测 | 原G01/G02源码/证据/文档及v0.5 DOCX保留；M1 Manifest列Owner、接口、挂载、初始化、状态、缺口、验收；正式反馈Integration Pending，未用替身冒充保存 |
| M2开始前只读审查与依赖图 | Narrative_Goal_Plan.md保留七项审查、Coverage Matrix和Mermaid依赖图；实际Unity检查报告在Logs/ReviewV2/M2 |
| M2-G01：两案真实世界与活动 | 独立Prefab、真实双门/单门、柜体/备餐面/餐厅差异、目录/参数；两案完整Cooking、原Cooking回归、受碰撞约束搬运和两物品完成G01均有实际批次证据 |
| M2-G02：安全切换与恢复 | Core请求/回执/阶段/重复和旧Visit隔离；实际双向、重新初始化、失败持物保持、Reset、Review往返通过；正式确认UI待T2 |
| M2-G03：空间上下文 | 当前Host.Resolve绑定各案本地身份，版本与Goal可用性独立，捕捉对象不可变；Marker即时保存/回顾/跨Visit历史待T3，T1未实现他人业务 |
| M2-G04：可执行验证和设备准备 | 上述实际Unity测试、当前版本Windows64构建、Device_Acceptance.md含Quest Link/约20分钟Pilot；设备和正式联合测试按指令允许延期，不能据Mock或Editor宣称通过 |
| 权限与工作区 | feature/zh，未commit/push/合并/切平台；默认Build Settings不变；恢复本轮已知初始干净文件的Unity导入漂移；最终检查记录另见下文 |

本轮T1独立交付达到可接入状态；M2完整产品叙事的正式UI、Marker/真实落盘及Quest Link/Pilot仍须由对应Owner交付并联合验收。

最终工作区检查：git diff --check通过；ProjectSettings、Packages、Samples、原Cooking/Phase2 Review场景和共享Rig与原HEAD一致（这些路径本轮开始时均干净），新Assets资源未发现缺少.meta。仅恢复本轮自动产生的漂移：字体动态缓存、两材质浮点/序列化、Rig空白、OpenXR包配置、PlayerSettings构建回调的preloadedAssets；另移除本轮生成且无引用的OpenXR Editor Settings空编辑器资产。M1最初已有的源码/资源/文档改动未恢复或丢弃。Unity编辑器/测试进程已结束，仅Hub/授权服务保留；未commit/push。

当前A-v2/B-v4 Windows Player以batchmode/D3D11实际启动，观察到进程响应和场景加载后的UnloadTime，日志未出现NullReference/MissingReference/InvalidOperation/Unable-to-load托管错误；随后结束该自启测试进程。日志保留M2-player-startup.log，包含XR_ERROR_FORM_FACTOR_UNAVAILABLE，因此仅为启动冒烟，不是XR、完整交互或Quest Link通过。日志及Builds均由gitignore排除。
