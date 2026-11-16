# M2 Integration Manifest

2026-11-16 B冰箱留缝增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v8。冰箱仅向水槽横墙靠近10cm，目标净距约7.7cm，105°及碰撞规则保留；其余v7布局保持。实测最小净距0.077018m；Edit35/35、Play24/24、B收纳/完整G02 2/2分批通过，10文件重复作者一致。Windows64构建0错误/0警告及启动冒烟通过，2811保护文件不变，用户未保存B设置保留。T1 Verified — Independent / Ready for Integration，Quest Link仍待验收。见 [B_Fridge_Wall_Gap.md](B_Fridge_Wall_Gap.md)。下文为此前历史。

2026-11-16 B加长板/微波炉/工具布局增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v7。悬空板1.56m、左段下方固定微波炉；B工具按A水平布局和朝向同步，因台面低1cm相应下降，B台面形状保留。Edit35/35、Play24/24、完整料理/收纳5/5分批通过，10文件重复作者字节一致；Windows64构建0错误/0警告、默认A启动冒烟通过，2792保护文件不变。B Player交互/Quest Link仍待验收；T1 Verified — Independent / Ready for Integration。见 [B_Extended_Shelf_And_Tools.md](B_Extended_Shelf_And_Tools.md)。下文为此前历史。

2026-11-16 B收纳设计增量：当前A integrated-kitchen-m2-v5、B external-fridge-m2-v6。新增原木四格窗下开放柜、接续台面的暖白细框双门地柜和单层悬空层板；保留冰箱/横墙/门洞。Edit34/34、兼容Play23/23（含新交互5项）及10文件重复作者字节一致已通过；料理/收纳首批4/5，B完整G02相同源码单项复测1/1，保留切菜物理批量波动。Windows64构建0错误/0警告、启动冒烟通过；3726保护文件不变，用户Editor未保存B起始保留。T1 Verified — Independent / Ready for Integration，Quest Link及正式联合仍待验收。接入、参数、失败和实际截图见 [B_Storage_And_Prep.md](B_Storage_And_Prep.md)。下文为此前版本历史。

2026-11-16 B 北移增量：当前 A **integrated-kitchen-m2-v5**、B **external-fridge-m2-v5**。B冰箱目标(-4.65,0,-0.20)m，北端铰链靠水槽侧横墙、南端把手，105°目标；门/食材/储物/稳定身份接线保留，新增B专属派生门体网格和可重复作者配置。Edit31/31、Play18/18、完整料理/收纳5/5分批通过；横墙净距实测17.70cm、作者12文件重复字节一致。A-v5/B-v5 Windows64构建及启动冒烟通过（0错误，2条MCP断连警告），3703个受保护文件保持；T1 Verified — Independent / Ready for Integration，正式联合/Quest Link仍待验收。参数、截图与失败记录见 [B_North_Fridge.md](B_North_Fridge.md)。下文为此前版本历史。

2026-11-16 锅灶吸附增量：A 当前 **integrated-kitchen-m2-v5**，B 仍 **external-fridge-m2-v4**。A/PREF_Cooking_FryingPan实例新增ReviewPanStoveSnap，绑定本世界station/灶架/原禁用扫掠探针/现有碰撞profile；无新Socket选择、无公共端口或Goal语义变化。自动落位不发布手持事实；抓取解除固定，原Reset/世界卸载生命周期复用。Edit29/29、Play19/19独立通过，原Cooking完整料理/A收纳批次2/3、A完整G02单独复测1/1通过；保留锅铲侧入失败修正及旧切菜批量波动，未合称一次全量。主场景冷灶吸附启动检查通过，359个受保护文件一致；详见 [A_Pan_Stove_Snap.md](A_Pan_Stove_Snap.md)。v4台面、布局、稳定ID保持，正式T2/T3/设备缺口不变。

2026-11-16 增量：当前 A **integrated-kitchen-m2-v4**（连续斜边台面、长台左灶右切配、锅初始在熄火灶架上），B 仍 **external-fridge-m2-v4**。新增 A 世界根 `A_ChamferWorktop` / `COUNTER_WORKTOP`；原 `WORKTOP` ID 跟随右侧砧板，不改变捕捉完整 Scheme/Visit/Model 身份的契约。柜墙 Prefab、公共运行时/接口、B、主/历史场景保持。Edit **28/28**、Play **14/14**、食材/完整料理 **3/3** 分批通过，T1 Ready for Integration，正式T2/T3/设备不视作已验收。详见 [A_Chamfer_Worktop.md](A_Chamfer_Worktop.md)。下文 11-15 的 v3 证据保留为历史，不能替代当前 v4 验收。

2026-11-15 增量：当前 A 为 **integrated-kitchen-m2-v3**，B 为 **external-fridge-m2-v4**。A 世界下新增嵌套 `PREF_A_IntegratedCabinet.prefab`（与工作台同级，沿用 RoomReset/交互门控）；六扇柜门及 `MICROWAVE_VISUAL` 身份已登记，`CABINET_LAYOUT` 保留。微波炉无热源/交互，不开放 G04。T2/T3 仍从 Host.Resolve 捕捉当前模型版本及稳定身份；不把该外观身份当作可加热设备。接线、尺寸及验证记录见 [A_Integrated_Cabinet.md](A_Integrated_Cabinet.md)。本轮柜体/切换/冰箱11/11、食材与完整料理3/3、资源/Core27/27分批通过，T1 Verified — Independent / Ready for Integration；不等于正式联合/设备验收。旧 Windows 构建只包含 A-v2；下列 11-14 构建/验证是历史证据，不能冒充 v3 验收。

2026-11-14，Uncommitted。真实A/B已接唯一V2主场景，主场景定向PlayMode21/21通过。正式T2/T3联合链路仍Integration Pending，Quest Link/Pilot为Device Pending。独立能力不等于完整M2验收。

| 项 / Owner | 接口、挂载和初始化 | 状态 / 缺口 | 接入验收 |
|---|---|---|---|
| A/B世界 T1 | V2专属方案资源；唯一Host绑定当前station/recipe/reset/groceries/identities，Rig/UI归会话 | Ready for Integration：实际主场景两案料理/搬运/开门/几何通过，当前版本Windows64构建与启动冒烟完成 | 两案可加载且真实引用一致，无混合碰撞世界；原Cooking不变；完整设备操作仍待验收 |
| 方案端口 T1 | ISchemePort.Supports/Change(requestId,schemeId,callback)；可选ISchemeModelCatalog.ModelVersionFor；Host Awake前注入 | Ready for Integration：双向切换、占用失败/重试、持物失败、异步保存等待/失败保留抓取、Reset均独立通过 | 回调成功前世界已完全就绪；失败旧世界保持；旧/重复回执不更新身份；模型版本请求前固定 |
| 切换说明/确认 T2 | 正式UI Prefab交付到V2 UI根；读Snapshot/Changed，Execute(ChangeScheme,target)，监听OperationCompleted | Integration Pending：T2正式入口未交付 | 仅Experience；确认前明示进度重置、标记保留；Accepted不是加载成功，忙碌/失败原因可见 |
| 空间身份 T1→T3 | 当前host.Resolve；每案稳定Object/Zone/Anchor目录与ModelVersion，捕捉完整Session/Scheme/Visit/Model | Ready for Integration：A/B目录已交付，Core/资源独立验证通过 | 每案唯一可解析；历史位置用捕捉快照；切换/Reset后旧Visit事件不能更新Goal |
| Marker/历史/保存 T3 | 独立MarkerCapture/Review Prefab；sessionSavePort MonoBehaviour；Save后真实写盘+读回再回同ID成功 | Integration Pending：正式组件/Schema未交付 | A/B各标记身份不串写；切换关闭After窗口；Reset不清历史；零Marker结束及失败重试 |
| 联调/设备 T4/T5 | 交付构建版本、设备、实际路径、失败与复测证据；约20分钟Pilot允许只体验单案 | Integration Pending / Device Pending | 叙事A-E真实闭环；Quest Link平台与实际A/B注明；用户自述与客观事实分开 |

兼容性：现有ISchemePort签名不变；旧端口不实现模型目录时延用原模型版本（仅维持旧调用行为，不证明生产A/B有效）。M2生产端口必须提供每案非空版本。新增Core初始方案构造重载，旧签名和默认Baseline保留。当前任何替身只证明T1身份契约，不证明正式反馈保存。

## 当前实际交付路径（实施中）

- A/B资源：`Assets/_Project/Prefabs/ReviewV2/Schemes/PREF_SCHEME_A.prefab`、`PREF_SCHEME_B.prefab`，根ReviewSchemeWorld保存inactive。当前模型版本分别integrated-kitchen-m2-v5、external-fridge-m2-v8；目录为本目录SCHEME_A_Identities.tsv、SCHEME_B_Identities.tsv。语义ID在方案内稳定，完整身份由Scheme+Visit+Model区分。语义锚点包括WORKTOP、DINING_TABLE、SINK、SCENE_ANCHOR；A的COUNTER_WORKTOP代表实际台面，WORKTOP跟随右侧砧板；MICROWAVE_VISUAL仅为固定外观，不能作为加热目标。A-v5锅灶吸附不改变上述语义ID。B-v6保留CABINET_LAYOUT，新增WINDOW_STORAGE、FLOATING_SHELF、CABINET_PREP_NORTH、CABINET_PREP_SOUTH；B-v7再新增固定MICROWAVE_VISUAL，WORKTOP随新砧板位置移动，工具按A摆位并适配B台面高度；开放柜/层板无G01储物完成语义，仍使用冰箱Zone。新版本保持既有HDRP实时反射配置，旧Cooking源烘焙不改。
- 产品入口：`Assets/_Project/Scenes/SCN_Kitchen_ReviewV2.unity` 已增量接线，原M1对象原位保留停用。`SCN_Kitchen_ReviewV2_M1Regression.unity`仅保留历史测试；`SCN_Kitchen_ReviewV2_M2.unity`保留制作阶段的隔离夹具，不是第二个产品入口。当前M2测试显式加载唯一主场景；默认Build Settings不变。
- 唯一会话根上的ReviewSchemePort执行顺序-200，Awake先在独立Scene实例化A并Bind，Host在-100创建Core，随后UI/门控初始化；每次切换按同样完整世界绑定。Host.schemePort与initialSchemeId为兼容新增序列化字段，旧M1默认Baseline。
- T2将正式UI挂在会话场景，使用同一Host，不挂进可销毁的方案世界。T3持久化组件也应归会话场景，sessionSavePort在Awake前注入；Marker捕捉时复制完整身份与位置，不能持有已卸载方案Transform作为唯一历史数据。
- T1世界绑定包含station、recipe、roomReset、groceries、identities及全部CookingGrabBridge；交互门控在重绑定时刷新当前Scene对象。方案世界Scene不含Rig/会话Host/UI，以免重复Owner；RoomReset仅扫描自身Scene。
- 已配置保存端口时，ChangeScheme先保存旧方案/Visit的Snapshot，真实验证成功后才调用方案端口；失败留旧世界可重试，迟到/重复保存回执不加载方案。该Core路由已通过合成回执定向测试；无端口的主场景目前只能验证T1世界切换，正式保存能力仍待T3实际写盘/读回。
- 目标几何预检在同一次同步调用内完成、期间不推进物理；旧世界在预检成功前保持启用，保存Pending及失败不松开已有物体，新的抓取被门控。实际成功后才停用旧世界并清旧选择。T2只在匹配的OperationCompleted成功时显示切换成功。
- 仅B起始：在Host/Port Awake前设置ReviewSchemePort.initialSchemeId=SCHEME_B；Core从该真实世界创建，不先记录A访问。默认场景配置为A；运行后切换仍仅允许Experience。此初始化契约已独立测试，正式选择入口由T2交付。
- SafeExit保留既有Core的即时终止语义，当前不保证其本身产生Verified保存。正式T3即时草稿、失败恢复和T2退出提示必须联合验证；不得把SafeExit、NotConfigured Finish或测试回执描述为反馈可靠落盘。
