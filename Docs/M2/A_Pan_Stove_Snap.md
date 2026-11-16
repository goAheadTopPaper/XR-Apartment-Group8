# A 方案锅灶吸附

2026-11-16（Asia/Shanghai），feature/zh，未提交。用户批准：锅靠近灶架、基本放平并松手时自动居中固定；抓取解除；远处或倾斜不吸；初始/Reset/切回A保持冷灶吸附；加热、食材、搅拌和倾锅仍可用。

## 实际接入

- A 世界当前 `integrated-kitchen-m2-v5`，B 仍 `external-fridge-m2-v4`。台面、灶位、柜墙、水槽和物品摆位沿用已确认的 v4。
- 新组件 `Assets/_Project/Scripts/Runtime/ReviewV2/ReviewPanStoveSnap.cs` 只挂在 A 世界的 `PREF_Cooking_FryingPan` 实例，绑定本世界 CookingStation、`LOC_BurnerSurface`、原禁用 `COL_GrabSweepProbe` 和 `SET_HandInteraction`。不改共享锅 Prefab 或公共 Cooking/XRI 源码。
- 落位锁定位置和旋转并清速度，使用 Rigidbody FreezeAll；保持原动态刚体及重力设定，不创建 Socket 选择。自动吸附时 `grab.isSelected=false`，因此不会伪造手持事件；实际抓取时恢复原约束，仍由原 XRI/持物扫掠接管。
- XRI 在选中前缓存原 kinematic/gravity，吸附不修改这两个值，避免重复抓放后恢复成错误刚体模式。松手后等待XRI释放流程，重置期间解锁并暂停吸附，原重置完成后重新落位；组件停用时撤销自身约束。
- 自动落位仍调用现有 HeldPropSweep，包含锅和锅柄的完整包围盒。目标受阻、旋转受阻或查询饱和时不吸附，不放宽碰撞规则。
- 保留放下时的锅柄水平朝向，仅校正倾斜；初始/Reset/新A世界使用作者初始姿态。初始目标来自已登记灶架与锅底间距，不自动点火。

作者入口：`XR Apartment → Review V2 → Apply A Pan Stove Snap` / `ReviewV2PanSnapAuthoring.Apply()`，只增量接入 A 组件和版本。原台面/柜体制作链也接同一配置方法，避免将来重建丢失吸附。完整身份目录更新为A-v5，已有Object/Zone/Anchor ID不变。

## 参数假设

水平捕捉半径 0.065m；高度范围 ±0.06m；最大倾斜 20°；最大平移速度 0.6m/s、角速度 3.5rad/s，避免高速飞过时被吸住。受限扫掠允许不超过15mm的竖直支撑间隙修正；水平目标误差须≤2mm、角误差≤0.2°。这些是可调原型参数，不是磁力或真实灶具物理模拟，也不是空间合规/人机工学结论。详见 Layout_Assumptions.tsv。

## 验证与历史

| 批次 | 实际结果 | 覆盖与限制 |
|---|---|---|
| `Logs/ReviewV2/M2/PanSnap/compile-first.json` | 首轮2项编译错误 | Unity2022没有Rigidbody.SetPositionAndRotation，改用原项目已有position/rotation接口；未升级引擎 |
| `compile-second.json` | 第二轮4项编译错误 | XRI3.5旧SelectEnter/Exit重载被禁用，测试显式使用IXRSelectInteractor接口；未改XRI包。该阶段一次作者尝试因未加载新类型而失败，未形成交付 |
| `compile-ready.json` / `authoring-result.json` | 编译0错误、作者成功 | 实际A-v5组件与灶架目标读取确认 |
| `idempotence-result.json` | 作者重复应用通过 | 再次Apply后A Prefab及身份目录逐字节相同 |
| `edit-result.json` | EditMode **29/29 Passed** | A独有吸附绑定、原禁用探针、Prefab原始动态刚体/约束、A/B版本及原资源/Core测试 |
| `play-first-result.json` | PlayMode **19/19 Passed** | 初始吸附、外力/扭矩后不漂移、真实开火加热而无手持选择；真实XRI抓起/倾锅/放回居中/锅柄朝向；远处释放保持自由物理；障碍阻挡与移除后再吸；持锅Reset、Review往返、A-B-A冷灶吸附；停用恢复约束；原柜门/台面/切换/回退测试 |
| `recipe-result.json` | 完整料理/收纳首批 **2/3 Passed** | 原 Cooking 完整流程与 A 实际 G01 通过；A G02 在蛋块翻炒失败，距离0 |
| `recipe-a-diag-result.json` | A 诊断复现失败 | 铲头0/160步在锅口内，局部坐标约(0.1498,0.0195,0.0540)m；锅持续受热、50mL蛋液且混合1.0，排除加热/配方条件，确认铲头被锅沿挡住 |
| `recipe-final-result.json` | 修改测试进锅路径后批次 **2/3 Passed** | 原 Cooking 完整流程与 A 实际 G01 再次通过；A 在更早的 Piece_02 切块处被邻近 dice 碰撞体挡刀，保留旧物理测试波动，不视作完成G02 |
| `recipe-a-top-result.json` | A 完整 G02 单独复测 **1/1 Passed** | 实际抓放/刀切/搅蛋/倾倒/锅铲翻炒、G02完成、烧焦与Reset均通过；`recipe-a-scenario.json`：50mL蛋液、16块/120g番茄，累计有效炒蛋2.667m、后续翻炒1.741m |
| `smoke-result.json` / `smoke-next-result.json` | 主场景 Play 启动检查通过 | Setup阶段A-v5，frame2→1913仍冷灶吸附、无手持选择、锅位置(-3.95,1.068,1.95)不变，退出Play后控制台0 error |
| `final-preservation-check.json` | 359个受保护既有文件校验一致 | B/原Cooking/柜墙Prefab/主与历史场景/共享源码/设置/包未改；测试更改的EditorSettings按开工精确字节恢复；当前仍feature/zh |

自动测试的锅铲原路径从侧面直入锅内，对自由锅体可能造成位移；吸附后锅保持原位，原碰撞扫掠正确阻挡该路径。夹具改为先抬高18cm、经锅口上方下降后翻炒；没有修改公共锅/锅铲运行时、碰撞规则、0.45m翻炒阈值或Goal判定。失败诊断保留在断言消息中。

本轮源码/文档范围的 `git diff --check` 通过；全工作区检查仍报告既有字体及材质YAML尾空格，按开工哈希确认它们不属本轮修改，未替用户清理。

各批次独立，不合称一次全量。真实控制器/Quest Link操作尚未验收；测试使用tracked-hand夹具实际触发XRI选择/释放和运动，不能代替真机手感。未重建旧Windows Player，未自动commit/push。

## 使用检查

退出当前 Play 后重新启动 V2 主场景并进入方案 A；初始锅固定在熄火灶架上。抓锅柄可正常拿起/倾斜，灶架附近约6.5cm内基本放平并松手后自动居中，锅柄保留本次水平朝向；远处、倾斜或目标被挡时保持自由物理。开火仍需使用原灶开关。锅铲从锅口上方放入；Reset 和 A→B→A 回到冷灶吸附状态。
