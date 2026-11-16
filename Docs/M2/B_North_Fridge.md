# B 方案冰箱北移与北端铰链

2026-11-16（Asia/Shanghai），feature/zh，Uncommitted。用户批准：冰箱靠窗北端、厨房入口外侧；与水槽下方横墙留隙；北端铰链靠该墙、南端把手；正面朝餐区，105°开门。参考图不是尺寸依据。

## 当前实现

- B 当前 external-fridge-m2-v5，A 保持 integrated-kitchen-m2-v5。B冰箱从(-4.65,0,-1.30)m移至(-4.65,0,-0.20)m，向北1.10m；Yaw90°、整机尺寸保持。墙、水槽、餐桌、既有备餐台和窗下围护/地面不改。
- 门轴局部(-0.345,0.04,0.37)m，swingSign=-1，目标105°。门轴位于世界+Z/北端，真实PullHandle位于-Z/南端。门对象、KinematicSwingDoor、XRSimpleInteractable和稳定ID保留，复用原抓门/扫掠。
- 新作者 ReviewV2BFridgeAuthoring 总是从不变的Cooking工作台源派生B门体，正尺度镜像节点/实体碰撞体/探针；10个近远LOD网格独立保存于Art/ReviewV2/B_NorthHinge。网格顶点、法线、切线方向与手性、三角绕序同步处理，原UV/材质和原网格保留。原禁用扫掠探针不列入XRI交互碰撞体。
- 非冰箱子级的原存放食材按刚体布局变换同步迁移，冰箱子级储物Zone/层板随根移动，购物袋内Goal食材留餐桌。原FRIDGE、门体、Zone、食材/配方引用与身份ID保留。
- 菜单 XR Apartment → Review V2 → Apply B North Fridge And Hinge 只增量保存B；完整重建、B窗边修订和HDRP作者入口统一接入同一B配置和版本，避免未来恢复旧位置/门轴或降级为v4。A作者链保持。
- 未新增公共接口、运行时共享源码或Goal语义。料理测试ToggleDoor现在读取每扇门的配置方向/角度，不继续假设所有单门为+105°。

## 参数与验收

横墙净距基准≥0.15m，当前9条采样射线的最小净距为 **0.177018m（17.70cm）**，采样覆盖机身深度方向三个位置及0.3/1.0/1.6m高度；采样原点、命中墙体及距离在Logs/ReviewV2/M2/BNorth/sink-wall-clearance.txt登记。它是模型内部原型参数，不是施工/安装/人体工学结论。真实控制器、Quest Link和设备舒适性仍待验收。

| 批次 | 实际结果 | 说明 |
|---|---|---|
| compile-console.json / authoring-result.json | 编译0错误、作者应用成功 | 只保存B根与派生网格 |
| edit-result.json | 首批30/31，1失败 | 新测试用默认GetComponentInParent漏掉停用Prefab父级；只读确认Zone父级正确，修正为包含inactive的查询 |
| edit-final-result.json | EditMode31/31 Passed | B位置/铰链/把手/探针/正尺度、全部派生LOD顶点/法线/切线/绕序，原资产/Core/方案检查 |
| play-result.json | 首批16/17，1失败 | 旧地面采样(-4.8,0.2,-0.4)因冰箱北移命中底板；采样改到腾出的原冰箱地面(-4.8,0.2,-1.4)，保持原支撑断言 |
| play-final-result.json | PlayMode17/17 Passed | B手拉105°、每步无实体穿透、障碍停止/移除后可开、17.70cm横墙净距；原Rig胶囊参数与真实XRI持物路线在关门/开门两态通过；Review/Reset/切换、A吸附和原方案回归 |
| play-stock-result.json | PlayMode18/18 Passed | 上述覆盖外，追加B关门阻止原库存选择、开门实际选取/抬起/取出并关闭的回归 |
| idempotence-first-result.json / idempotence-second-result.json | 重复作者检查失败，已修复 | 首次观察派生网格序列化重写及食材坐标变动；第二次只剩食材10^-8量级浮点累积。根姿态已匹配时不再往返旋转食材坐标，不改变网格/碰撞几何 |
| idempotence-final-result.json | 重复作者12个文件字节一致 | 10个派生网格、B Prefab及稳定身份目录，无累积位移 |
| recipe-result.json | 完整料理/收纳 **5/5 Passed** | 原Cooking完整料理、A两件食材G01/A完整G02、B两件食材G01/B完整G02全部通过；B实际50mL蛋液、16块/120g番茄，成菜/烧焦/Reset通过，详见recipe-b-scenario.json |
| build-summary.txt / build-report-confirmed.json | Windows64构建 Succeeded，**0 errors / 2 warnings** | Unity2022.3.62f3、唯一V2主场景、A-v5/B-v5、560824138 bytes；2条均为MCP WebSocket断连警告。原execute返回空结果，已从实际BuildReport和产物确认成功，未将工具响应误作构建失败 |
| player-smoke-result.json | Windows启动冒烟通过 | 独立新产物Builds/ReviewV2/BNorth-v5/ReviewV2.exe；batchmode/D3D11启动10秒存活且响应、引擎初始化正常，未见所检查的托管/图形启动异常；只结束本次自启进程，不证明完整Player交互或Quest Link |
| final-preservation-check.json | 3703个受保护既有文件一致 | A/共享资源/运行时/主与历史场景/包/原设置保留；构建自动注入的XR preloadedAssets、Settings序列化及清空动态Fallback字库已按开工字节恢复，字体重新导入；未重置用户修改；构建新生成且无引用的空OpenXR Editor Settings已归档到忽略的日志目录 |
| console-final.json | Editor 0 error | 本次失败和构建警告另存，不通过清除历史来冒充无失败 |

各批次独立记录，不拼成一次全量。当前没有commit或push。T1 Verified — Independent / Ready for Integration；正式T2/T3联合和Quest Link仍待验收。

## 实际场景截图

以下为本轮Editor/HDRP真实场景渲染，不是参考图生成结果。正交俯视为低于顶面的水平截面，吊灯仍在视野中；不据此宣称VR空间舒适性。

关门与105°开门使用同一相机：

![B北移关门](Images/B_North_v5/01-closed.png)

![B北移开门105度](Images/B_North_v5/02-open.png)

![B北移正交俯视](Images/B_North_v5/03-top.png)

门体派生自既有Cooking硬表面冰箱网格，沿用原材质与来源，不计新增Cloud 3D任务。

## 打开与交接

Editor继续使用唯一主场景 `SCN_Kitchen_ReviewV2`，重新进入Play并切换B即可。初始A保持原v5锅灶吸附。新版Windows入口为 `Builds/ReviewV2/BNorth-v5/ReviewV2.exe`，A/B均v5；旧目录产物保留为历史，不用于本轮B验收。启动冒烟没有替用户进行Player操作或约20分钟Pilot。

本轮源码/文档检查与自动验证分批记录；早前料理批量物理波动仍是历史限制，本轮5/5不能证明其永久消除。菜单可读性、射线、整体亮度以及既有交互提示牌位置不在本轮修复范围。
