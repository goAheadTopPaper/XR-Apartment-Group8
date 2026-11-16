# B-v6 原木开放柜与暖白备餐柜

> 本页保留B-v6历史证据；当前B-v7加长板、固定微波炉与工具同步见 [B_Extended_Shelf_And_Tools.md](B_Extended_Shelf_And_Tools.md)。

2026-11-16（Asia/Shanghai），feature/zh，Uncommitted。用户确认餐厅原木开放层格、厨房暖白细节柜体和一排悬空层板；本轮只改B专属资源/作者工具及必要测试。

## 当前接入

- 当前B external-fridge-m2-v6，A仍integrated-kitchen-m2-v5。新增PREF_B_StorageAndPrep，嵌在B世界根下。原SM_WindowTable保持禁用，复用其木纹图集的连续区域，使用B专属材质；不重新启用会与冰箱重叠的整件旧柜。
- 餐区WINDOW_STORAGE中心(-4.79,0,-1.255)m，Yaw90°，外尺寸1.35×1.00×0.60m；两列上下两层，共四格。顶板3cm、倒角3mm，顶面1.00m，窗台下沿1.1576m，底部后退木色踢脚。柜北端Z=-0.58m，保持与冰箱南端的独立缝隙，承托落在既有B补齐地面上。
- 厨房地柜替换原Released_Kitchen_PrepCounter，保留CABINET_LAYOUT身份；中心(-1.7519,0,1.1868064)m，Yaw-90°，宽0.97361285m、深0.60m、台面高1.00m。北沿1.67361285与原台面前沿1.67661285留3mm拼缝，填补原孤立小柜与台面之间缺口；原台面、墙、门洞不裁切、不缩放。
- 暖白门面采用20mm细边框、略内凹中心面与细哑光深灰拉手，完整侧封板、背板、内部隔板、10cm高后退式踢脚。新增CABINET_PREP_NORTH/SOUTH两扇门，外侧铰接，原KinematicSwingDoor真实手拉90°，完整面板/把手禁用探针继续走原扫掠；禁用探针不进入XRI可交互碰撞体列表。
- FLOATING_SHELF语义根位于(-1.5769,1.535,1.1868064)m，Yaw-90°，板宽0.95m、深0.25m、厚3cm，顶面1.55m、底面1.52m，台面上方净空0.52m。2mm倒角、隐藏式后支撑外观，具有实际承托BoxCollider，不新增灯具/热源。
- 两块倒角板均为本地96顶点/44三角网格，板/柜/门维持正尺度。五个B专属材质；沿用既有PBR/木纹来源，没有新增Cloud 3D任务。
- 作者入口XR Apartment → Review V2 → Apply B Storage And Prep Design。完整重建和旧B冰箱入口统一接入B-v6，不恢复旧盒子或降级版本；复用嵌套实例与稳定节点，重复应用已实测10文件字节一致。未改公共API或Goal语义；已有G01储物区仍属于冰箱。新增层格/层板不制造Goal完成证据。

## 实际验证与失败记录

| 批次 | 实际结果 | 覆盖/限制 |
|---|---|---|
| authoring-first.json | 首轮作者失败，已修复 | Editor GetComponent返回Unity伪空对象，??未添加MeshFilter；改为Unity对象判空的通用获取/添加。失败发生在保存新柜Prefab及B世界之前 |
| authoring-second.json / compile-ready.json | 作者成功，编译0 error | 当前77个身份、配置有效；冰箱布局仍B-v5批准值 |
| edit-result.json | 首批33/34，1失败 | 2mm倒角角面的有效小叉积被Vector3.Normalize的1e-5阈值归零；检验改为非零叉积与等价0.99方向余弦比较，不改几何或碰撞容差 |
| edit-final-result.json | EditMode34/34 Passed | 新柜格、90°门、禁用探针、正尺度、台面接缝、悬空层板/锚点、倒角绕序、B专属材质/无发光及原资产/Core |
| play-new-result.json | 新交互PlayMode5/5 Passed | 实际手拉90°/关闭、暂停门控、障碍停止/移除后开启；原杯子真实XRI抓放到四格和层板共五个位置并物理承托；Review/Reset/B-A-B及实际截图 |
| idempotence-first.json | 作者重复应用10文件字节一致 | 五个材质、两个倒角网格、嵌套柜Prefab、B世界及身份目录；不累积对象/坐标 |
| play-final-result.json | 兼容首批8/23，15失败，已定位 | 旧A夹具默认初始A，实际沿用用户未保存的B起始：6项A吸附找不到A组件、6项B夹具重复切B被拒、2项A方案夹具初始/回退假设失败；另1项地面射线命中新柜底板。新增5项与B单门手拉等仍通过 |
| play-fixed-result.json | 兼容PlayMode23/23 Passed | A专用夹具用真实命令显式切A并按实际初始历史计增量；B夹具仅在非B时切B。保留用户主场景/未保存起始设置，不放宽断言；地板直接Raycast原地板Collider，步行净空由胶囊真实路线验证 |

| recipe-result.json | 料理/收纳首批4/5 Passed | 原Cooking完整流程、A完整G02、A/B两件食材G01通过；B完整G02在Piece_02放回砧板时掉落（中心Y0.398、持物中心0.989、板面1.02）失败，保留原日志 |
| recipe-b-result.json | 相同源码B完整G02单项1/1 Passed | 无运行时代码、路径或容差更改；真实冰箱取材、切菜、打蛋、点火、倒蛋/翻炒、50mL蛋液+16块/120g番茄、G02及Reset通过。不能合称首批5/5或声称切菜批量稳定性已解决 |
| build-summary.txt / build-warnings.txt | Windows64 Succeeded，0 error / 0 warning | Unity2022.3.62f3，A-v5/B-v6；Development Player位于Builds/ReviewV2/BStorage-v6/ReviewV2.exe，560879618 bytes，构建24.33s；不改默认构建列表/平台 |
| player-smoke-result.json | 启动冒烟通过 | 独立启动该exe，D3D11/batchmode存活且响应10s；引擎初始化正常，未发现托管异常/图形初始化失败；仅关闭该次PID，不等于Player交互/Quest Link验收 |
| final-preservation-check.json / final-editor-result.json / final-console.json | 3726保护文件字节一致，Console0 error | A、原Cooking、共享/历史资源、主场景磁盘、工程/包/XR设置保持开工状态；Editor仍为SCHEME_B、dirty=True、停止Play；字体原先已有的Unicode8722动态字形恢复 |

测试运行器将用户未保存的B起始及Editor自动序列化状态写入主场景文件；开工文件原字节已备份，测试期间差异保存在test-runner-saved-scene.unity。结束时已恢复开工文件字节，保留当前Editor中的B起始为未保存状态；未把它作为本轮主场景接线变更。Windows构建来自测试期间的B起始场景。构建清理动态TMP字形、注入XR预加载、序列化设置，均已归档差异并恢复开工字节；本轮生成且无引用的空OpenXR Editor Settings归档在忽略的Logs内，现有XR设置未更改。

冰箱北端到水槽侧横墙的9点检查最小净距仍为0.177018m；餐桌→冰箱→厨房的关门/开门角色胶囊及持物路径、105°手拉沿用现有碰撞规则通过。刀切物理批量波动仍为已知限制，不属于本轮柜体碰撞修复。

## 打开当前B方案

当前Editor已停止Play，保留用户未保存的SCHEME_B起始，直接Play后完成校准/设置进入Experience即可。若重新打开磁盘场景：停止Play，Hierarchy选ReviewV2 Session，在Review Scheme Port的Initial Scheme Id设SCHEME_B再Play。当前正式菜单没有A/B切换按钮，不能按不存在的按钮验收；跨方案生命周期通过真实端口命令测试，不新增临时产品UI。

## 交付与验收边界

T1 Verified — Independent / Ready for Integration。四张实际截图、原型尺寸登记、稳定ID目录和失败/复测记录已更新。Windows64仅启动冒烟，Quest Link、正式T2/T3联合与用户设备操作仍待验收。料理首批与B单项复测分开记录，不宣称一次全量通过。

尺寸均为Unity原型登记值，不是施工/设备安装参数。当前未自动commit或push；Quest Link、设备舒适性和正式T2/T3联合另行验收。菜单/射线/整体亮度仍不在本轮修复范围。

## 实际场景截图

截图来自当前B-v6 PlayMode实际HDRP帧，不是效果参考图。杯子在测试中真实抓取并放置，Reset后恢复原物品位置。

餐区窗下四格柜与原冰箱：

![窗下原木开放柜](Images/B_Storage_v6/01-window-storage.png)

厨房备餐柜关闭、连续台面与悬空层板：

![备餐柜关闭](Images/B_Storage_v6/02-prep-closed.png)

同一视角柜门90°打开：

![备餐柜打开](Images/B_Storage_v6/03-prep-open.png)

俯视布局：

![B-v6俯视](Images/B_Storage_v6/04-top.png)
