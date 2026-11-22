# B-v8 冰箱与水槽横墙留缝收小

2026-11-16，feature/zh，Uncommitted。用户确认缩小冰箱与水槽旁横墙间的缝隙；只移动B冰箱，保留墙体、机身尺寸、北端铰链与105°目标开启角。

## 当前实现

- B external-fridge-m2-v8；A integrated-kitchen-m2-v5保持。冰箱原点从(-4.65,0,-0.20)改为(-4.65,0,-0.10)m，向横墙(+Z)靠近10cm，Yaw90°、正尺度、原门体/探针/扫掠规则均保持。
- 九点射线实测最小净距从17.7018cm缩至7.7018cm；不同高度/机身深度采样记录在BNorthRegression/sink-wall-clearance.txt。本轮用户调整的是布局原型留缝，替代先前≥15cm的布局假设；没有放宽实体碰撞深度、门角或手持扫掠规则，也不是施工/设备安装要求。
- 抽出既有冰箱/库存迁移逻辑为Editor ConfigurePosition：原B冰箱作者和B柜体增量作者共用它，避免完整重建或只更新柜体时恢复旧位置；绝对定位且已就位时不再次计算食材坐标。所属库存随移，购物袋留餐桌，稳定ID/Zone/配方引用保留。
- 实际B Prefab差异已保存world-position.diff：仅模型版本、冰箱根及所属库存Z坐标变化；长板、微波炉、双门柜、窗下木柜、工具与原台面/墙体不变。没有新增Runtime代码或公共接口。
- 开工Editor为未保存的SCHEME_B，已另存user-working-before.unity副本，主场景磁盘基线另行备份；测试保存的工作场景与开工用户工作副本规范化比较完全一致。最终保留Editor未保存B设置，恢复测试/构建产生的文件副作用，不覆盖用户原有修改。

## 实际验证

| 批次 | 结果 | 范围 |
|---|---|---|
| compile-first / compile-final | 0 error | B专属作者/受影响版本断言 |
| edit-result | EditMode35/35 Passed | 门轴/105°/禁用探针、B版本位置、保持已有柜体/工具/公共Core |
| idempotence-result | 10文件重复作者字节一致 | B世界、柜体Prefab、两网格、五材质、身份目录 |
| play-result | PlayMode24/24 Passed | 105°手拉/关闭，每步实体穿透检查、障碍停开与移除恢复；库存隔门限制及真实取材，原胶囊/持物关门开门通道，Review/Reset/B-A-B；A吸附、B长板/微波炉/工具回归 |
| recipe-result | B收纳/完整料理2/2 Passed | 两件食材G01、完整G02及Reset；实际50mL蛋液、16块/120g番茄；原菜谱测试脚本未改 |
| build-summary / build-warnings | Windows64 Succeeded，0 error / 0 warning | A-v5/B-v8，560884194 bytes，27.12s；Builds/ReviewV2/BFridgeGap-v8/ReviewV2.exe；沿用当时用户B起始，不改变默认构建列表/平台 |
| player-smoke-result | 启动冒烟通过 | 自有Player D3D11/batchmode存活并响应10s，引擎初始化正常，未见检查的托管/图形失败；只结束该次PID，不证明设备/Player完整交互 |
| final-preservation-check / final-editor-result / final-console | 2811保护文件字节一致，Console0 error | A、B-v7柜体Prefab/网格/材质、共享Cooking、墙/台面、原门体派生网格、主/历史场景和包/XR设置保持；Editor仍SCHEME_B、dirty=True、停止Play，测试false/3与字体1字形恢复 |

当前同视角关门/105°开门截图、九点最小净距与最终文件保护检查均完成。T1 Verified — Independent / Ready for Integration。

测试运行器保存了用户原先未保存的B起始。已将工作场景副本/测试保存场景与开工磁盘原文件分别归档；结束恢复主场景开工磁盘字节，同时保留Editor里的用户B设置为未保存。构建注入XR预加载、设置序列化与TMP动态字形清理已按开工字节恢复；本轮生成且无引用的空OpenXR Editor Settings已归档，不进入产品Assets。原v7截图与其他未提交成果保留。Quest Link与正式联合不在本轮Editor验收内；不自动commit/push。

## 实际截图

同一视角、当前B-v8真实HDRP帧：

![关门留缝](Images/B_FridgeGap_v8/01-closed.png)

![105度打开](Images/B_FridgeGap_v8/02-open.png)
