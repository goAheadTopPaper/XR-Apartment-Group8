# B-v7 加长悬空板、微波炉与工具布局

2026-11-16，feature/zh，Uncommitted。用户批准悬空板跨图示整面墙、两端留隙、左段下方台面放固定微波炉，并同步A台面工具初始布局；明确不改B台面形状。

## 接入与原型尺寸

- B版本external-fridge-m2-v7；A保持integrated-kitchen-m2-v5。沿用B_StorageAndPrep嵌套Prefab；窗下木柜、双门地柜及其门缝/铰链、原冰箱及食材/储物Zone均保持。
- FLOATING_SHELF中心(-1.5769,1.535,1.48)m，Yaw-90°；长度1.56m、深0.25m、厚0.03m，顶1.55m、底1.52m。端点Z0.70至2.26m，目标两端留隙；沿原墙MeshCollider三角面交点实测右/南端0.097468m、左/北端0.129223m（不以台面边缘冒充墙面）；后方隐藏支撑随板加长，原2mm倒角、材质和承托碰撞保留。更新原B网格内容，保留GUID，未负缩放。
- MICROWAVE_VISUAL中心(-1.7519,1.16,2.00)m，Yaw-90°，正面朝厨房操作区(-X)；主体0.52×0.30×0.38m，脚底1.00m，主体顶1.31m，与板底净空0.21m。复制当前A微波炉外观及材质引用，只在B创建固定实例；无Rigidbody、XRI交互、热源或G04接线，A资源未改。
- 从当前A资源复制16个工具/灶具相关Transform的水平位置和旋转，统一Y偏移-0.01m，适配A1.01m与B1.00m台面高差；不调用A台面/吸附作者工具，不复制A几何或锅吸附行为。WORKTOP锚点跟随B砧板新位置。初始位置在Awake前写入Prefab，由现有CookingStation/RoomReset缓存。
- 主要位置：灶(-3.95,1.005,1.95)、炒锅(-3.95,1.058,1.95)、砧板(-2.95,1.005,1.95)、刀(-2.95,1.035,1.95)、搅拌碗(-2.43,1.005,1.90)、番茄碗(-2.42,1.005,2.25)m。其余锅铲/打蛋器/汤勺/盘按A对应位置及高差同步。水槽台、餐桌、购物袋和食材不移动。
- 作者入口仍为XR Apartment → Review V2 → Apply B Storage And Prep Design。完整重建经B原作者入口接入，重复应用采用绝对位置，不累计偏移；新增身份只在当前B世界目录解析。

以上为Unity原型登记，不是施工/设备安装参数。菜单、射线、整体亮度、公共接口与Goal完成语义不在本轮修改范围。

## 实际验证与失败记录

| 批次 | 实际结果 | 说明 |
|---|---|---|
| compile-first / compile-tests | 编译0 error | 仅B作者工具与受影响测试变更，无Runtime改动 |
| edit-result | 首批34/35，1失败 | 旧A柜体测试把B不得有MICROWAVE_VISUAL作为历史前提；按本轮批准更新为B有且仅有1个微波炉，B仍无A窄柜，其他断言保持 |
| edit-final-result | Edit35/35 Passed | 尺寸、正尺度、实际锚点、工具与A高差对齐、微波炉固定无热源、既有资产/Core |
| idempotence-first | 10文件重复作者字节一致 | 五材质、两个倒角网格、柜Prefab、B世界、身份目录 |
| play-result | 运行器初始化超时，0项执行 | 恢复仅含Code-based tests runner的自动引导场景及运行器标志后重跑；无场景实现/包版本变化 |
| play-retry-result | 交互首批23/24，1失败 | 新增的人为锅中心2.5cm断言在长时间放杯后记录2.874cm偏移；B仍为原动态锅，本轮只同步初始摆位，不应额外引入吸附半径要求。改为原KitchenHeatSource真实加热判定，初始摆位仍由精确资产断言验证；运行时规则不变 |
| play-final-result | PlayMode24/24 Passed | 长板两端、四格和中心真实放杯；微波炉脚真实台面支撑；冷灶启动/原灶具真实加热；柜门90°、冰箱105°及持物路线、暂停/Review/Reset/B-A-B；A吸附回归通过 |
| shelf-wall-gaps | 墙面净距实测通过 | 当前墙MeshCollider三角面交点：右/南9.7468cm、左/北12.9223cm；保留约10cm量级留隙，没有移动/缩放墙体 |
| recipe-result | 再次运行器初始化超时，0项执行 | 保留失败日志；恢复仅含运行器的自动引导场景，测试期间开启场景重载、保留Domain以维持MCP回调，结束恢复开工false/3。无包/运行时变更 |
| recipe-retry-result | 料理/收纳5/5 Passed | 同一批次原Cooking完整流程、A/B两件食材G01、A/B完整G02和Reset通过；B实际50mL蛋液、16块/120g番茄入锅完成。测试脚本TomatoEggRecipePlayTests未改；本轮通过不代表此前切菜批量波动已永久解决 |
| build-summary / build-warnings | Windows64 Succeeded，0 error / 0 warning | Unity2022.3.62f3，A-v5/B-v7，560884194 bytes，22.02s；Builds/ReviewV2/BLayout-v7/ReviewV2.exe；保留主场景原始A起始和默认Build Settings |
| player-smoke-result | 启动冒烟通过 | 自有Player进程D3D11/batchmode存活并响应10s，引擎初始化正常，未发现托管/图形初始化失败，随后仅关闭该PID；启动默认A，不能冒称B Player交互已验收 |
| final-preservation-check / final-editor-result / final-console | 2792保护文件字节一致，Console0 error | A、共享Cooking、原台面/墙体、北铰链资源、主/历史场景、包与XR配置保留开工字节；Editor停止、initial A、dirty=False、false/3测试选项与字体1字形恢复；B配置有效，78身份 |

环境快照436行已保存；唯一预期差异为WORKTOP随砧板新位置移动，其余435行原环境Transform、碰撞体形状与网格身份一致（忽略Editor瞬时instanceID）。当前原SM_KitchenTable可见网格和MeshCollider仍引用原FBX，没有把A斜边台面带入B。完整保护核对已通过，实际截图已检查。

测试/构建产生的EditorSettings、PlayerSettings XR预加载、QualitySettings序列化及TMP动态缓存均已归档差异并恢复开工原字节；两份超时测试自动场景与本轮生成且无引用的空OpenXR Editor Settings已移入忽略的Logs备份，不留在产品Assets。主场景文件始终与开工字节一致，未修改其默认起始方案。

## 查看与交接

当前Editor停止在SCN_Kitchen_ReviewV2，保留开工A起始。查看B：停止Play，在Hierarchy选ReviewV2 Session → Review Scheme Port → Initial Scheme Id设SCHEME_B，再Play并完成设置进入Experience。正式菜单仍无A/B切换按钮，本轮未增加临时产品入口。

T1 Verified — Independent / Ready for Integration。作者工具、身份/参数目录、当前截图和失败/修复记录已更新；Windows只证明构建与默认A启动，B运行交互由Editor定向测试证明。正式T2/T3联合、Quest Link与舒适性仍待验收。

Quest Link与正式T2/T3联合另行验收；保留既有未提交工作，不自动commit或push。

## 当前实际截图

PlayMode真实HDRP帧，测试杯子为真实抓取放置，不是参考合成图。

![长板和左侧微波炉](Images/B_Layout_v7/02-prep-closed.png)

![同视角柜门打开](Images/B_Layout_v7/03-prep-open.png)

![工具同步后的B俯视图](Images/B_Layout_v7/04-top.png)
