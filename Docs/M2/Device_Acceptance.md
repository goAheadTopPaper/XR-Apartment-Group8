# M2 Windows64、Quest Link 与 Pilot 验收准备

2026-11-16 B冰箱留缝增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v8。冰箱仅向水槽横墙靠近10cm，目标净距约7.7cm，105°及碰撞规则保留；其余v7布局保持。实测最小净距0.077018m；Edit35/35、Play24/24、B收纳/完整G02 2/2分批通过，10文件重复作者一致。Windows64构建0错误/0警告及启动冒烟通过，2811保护文件不变，用户未保存B设置保留。T1 Verified — Independent / Ready for Integration，Quest Link仍待验收。见 [B_Fridge_Wall_Gap.md](B_Fridge_Wall_Gap.md)。下文为此前历史。

2026-11-16 B加长板/微波炉/工具布局增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v7。悬空板1.56m、左段下方固定微波炉；B工具按A水平布局和朝向同步，因台面低1cm相应下降，B台面形状保留。Edit35/35、Play24/24、完整料理/收纳5/5分批通过，10文件重复作者字节一致；Windows64构建0错误/0警告、默认A启动冒烟通过，2792保护文件不变。B Player交互/Quest Link仍待验收；T1 Verified — Independent / Ready for Integration。见 [B_Extended_Shelf_And_Tools.md](B_Extended_Shelf_And_Tools.md)。下文为此前历史。

2026-11-16 B收纳设计增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v6。新增原木四格窗下开放柜、接续台面的暖白细框双门地柜和单层悬空层板；保留冰箱/横墙/门洞。Edit34/34、兼容Play23/23（含新交互5项）及10文件重复作者字节一致已通过；料理/收纳首批4/5，B完整G02相同源码单项复测1/1，保留切菜物理批量波动。Windows64构建0错误/0警告、启动冒烟通过；3726保护文件不变，用户Editor未保存B起始保留。T1 Verified — Independent / Ready for Integration，Quest Link及正式联合仍待验收。接入、参数、失败和实际截图见 [B_Storage_And_Prep.md](B_Storage_And_Prep.md)。下文为此前版本历史。

2026-11-16 B 北移增量：当前 A **integrated-kitchen-m2-v5**、B **external-fridge-m2-v5**。B冰箱目标(-4.65,0,-0.20)m，北端铰链靠水槽侧横墙、南端把手，105°目标；门/食材/储物/稳定身份接线保留，新增B专属派生门体网格和可重复作者配置。Edit31/31、Play18/18、完整料理/收纳5/5分批通过；横墙净距实测17.70cm、作者12文件重复字节一致。A-v5/B-v5 Windows64构建及启动冒烟通过（0错误，2条MCP断连警告），3703个受保护文件保持；T1 Verified — Independent / Ready for Integration，正式联合/Quest Link仍待验收。参数、截图与失败记录见 [B_North_Fridge.md](B_North_Fridge.md)。下文为此前版本历史。

2026-11-16 锅灶吸附：Editor 当前 A 为 **integrated-kitchen-m2-v5**，B 仍 external-fridge-m2-v4。v4 几何/摆位保持，A 锅放平靠近灶架松手后居中固定、抓取解除。设备验收需补查重复抓放、锅柄转向、外力防漂移、锅铲从上方入锅、倾锅及 Reset/切案；旧 Windows 构建不含此功能。详细证据见 [A_Pan_Stove_Snap.md](A_Pan_Stove_Snap.md)。

2026-11-16：Editor 当前 A 为 integrated-kitchen-m2-v4（斜边台面、左灶右切配、锅在熄火灶架上），B 保持 external-fridge-m2-v4。柜墙层次不改；当前专项证据见 A_Chamfer_Worktop.md。下文 A-v2 的 Windows 构建仍不含当前 v4。真机需检查新的操作范围、锅初始支撑、开门净空与手持转移。

2026-11-15：Editor 当前方案 A 已改为 integrated-kitchen-m2-v3（整合柜体），B 仍 v4。下文 A-v2 Windows 构建不含新柜体，不能用于柜体验收；本轮 Editor 证据与限制见 A_Integrated_Cabinet.md。Quest Link 需补验六门手拉、两扇冰箱门 105°、门体与持物接触、Reset 及新几何下可达性。

2026-11-14。当前为验收路径；**Device Pending**，不证明空间舒适性或正式设备可用。产品场景仅SCN_Kitchen_ReviewV2；M2开发场景不能替代正式主场景验收。

当前实际证据：A=integrated-kitchen-m2-v2、B=external-fridge-m2-v4的Windows64开发构建成功，560119034 bytes，BuildReport 0 errors/0 warnings。Player以batchmode/D3D11启动后进程响应，未见托管加载异常，已结束该自启进程；OpenXR报告FORM_FACTOR_UNAVAILABLE。启动冒烟不满足下述完整Player/Quest Link验收。构建和启动日志分别为Logs/ReviewV2/M2-hdrp-final-windows64-build.log、M2-player-startup.log；构建元数据为M2-build-summary.txt。

## Windows64

本机Unity 2022.3.62f3包含windowsstandalonesupport模块。主场景接线与独立回归完成后，使用既有 `ReviewV2Authoring.BuildValidation`：显式场景列表仅包含V2，Development Windows64输出到忽略的Builds/ReviewV2/ReviewV2.exe。不改变默认Build Settings、不切平台。记录源码工作树状态、A/B模型版本、Unity日志、BuildReport结果与实际产物；构建失败保留日志再修复。

在Windows Player逐项检查：唯一Rig/事件系统/交互管理器/Host；A加载；真实双门与B单门；食材从袋到冰箱和工作台；两案料理；显式切换/失败重试/Reset；Review往返保留世界；独立实时照明、材质和反射无旧烘焙残影。关闭应用再次启动应按已声明初始方案开始；跨启动反馈恢复由T3实际持久化能力单独验证。

## Quest Link 联合回归（T1/T2/T3/T4）

先确认Quest 3经Quest Link连接、活动OpenXR运行时、目标控制器profile与实际追踪；关闭Editor自动模拟器后测试。记录设备/运行时/构建/方案版本/失败和复测。VD调试、键鼠或Editor驱动均不能替代本门槛。

| 路径 | 验证与证据 |
|---|---|
| 首次进入任一方案 | T2安全说明与基础Teleport/抓取教学可跳过和重看；玩家站位无家具重叠；菜单在头显可发现、可读、可操作 |
| 任意生活路径 | 先收纳、先料理均可；活动中断不重置物品；两门/单门开合和冰箱取材门控准确；记录控制器困难，不推断为布局问题 |
| A→B→A | T2切换确认明确世界/Goal重置、Marker保留；T3先真实保存读回；仅成功更新Scheme/Visit/Model/History；返回A起始世界；目的地被占用则原世界保持且可重试 |
| 半途Marker→Review→返回 | T3即时持久化，After不跨Visit；食材、锅、水、Goal与Marker身份保持；用户解释可空；历史位置用捕捉快照 |
| 零Goal/零或多Marker结束 | 不强制完成或比较；真实写盘读回后Finished；失败留Review提示并重试；Reset保留历史 |
| 暂停、安全退出、舒适性 | 暂停恢复到原阶段；退出不被记录为设计失败；追踪/传送/转向/帧时间按实际设备测量；用户可随时停止 |

## 约20分钟Pilot（T5主持，T4记录技术问题）

建议预算：0–3分钟背景和知情说明，3–6分钟基础操作，6–15分钟自由体验，15–20分钟回顾与讨论。时间是主持预算，用户可提早结束、延长上手或只体验一案。生活提示不构成操作顺序，不要求完成目标、创建标记或选出胜者。

观察用户实际接触的Scheme/Visit/活动；询问其发现的空间影响与原因，允许正面、负面、无特别感觉和不确定。区分布局感受、模型简化、控制器困难和入口发现问题。访谈解释与客观事件分别记录，用marker_id回链，不由Goal状态生成意见。每个发现关联具体版本、位置、用户原意、行动与复测，不能将人数少的选择性探索表述为受控A/B优越性实验。

## 当前联合依赖

T2：正式入口/教学/切换确认/UI Prefab与引用。T3：MarkerCapture/Review、Schema、ISessionSavePort、实际写盘读回/失败重试/跨Visit保留。T4：Quest Link实机与Windows Player证据。T5：Pilot招募、同意、观察和访谈。缺交付项保留Integration Pending或Device Pending；T1独立能力继续推进，不能由替身回执抵消这些缺口。
