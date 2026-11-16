# V2 活动上下文与反馈端口交接

2026-11-14 的交接记录，当时 Uncommitted；当前提交与同步状态以 Git 为准。T1 提供身份、事实与端口；正式 Marker/UI/存储业务仍由 T2/T3 负责。本文件不宣称已交付正式反馈闭环。

## 可立即并行的接入

场景内唯一 `ReviewSessionHost` 实现 `ISessionReadModel`、`IReviewSessionCommands`、`IInteractionEventSource`，提供 `Resolve(id)`。T3 无需等待 G01：COOKING_STATION、FRIDGE 与既有 OBJECT_nnn 均可用于对象锚点；现有快照、Grab/Release 与 G02 事实已可消费。Resolve 为 null 时不得猜测对象坐标或将不存在的 Anchor 当真实登记。

T3 创建 Marker 时保存当时 SessionId/SchemeId/SchemeVisitId/ModelVersion、锚点局部坐标及世界位置快照。异步补记只收同一 Visit；没有事件时可存空上下文，不阻塞 Marker 即时保存。所有评价文字来自用户，不由 Goal 状态生成。

ModelVersion由T1在Host场景配置中明确指定，Host创建Core时固定；T3始终读取Snapshot，不从Goal.Available反推版本。G01资源缺失/未配置不会把同一M1模型回退成M0；Reset只更新Visit。显式版本为空时会标为UNVERSIONED，不能冒充已登记版本。

## G01 增量目录与语义

V2 食材沿用配方原对象：OBJECT_030 番茄、OBJECT_020/021/022 鸡蛋，未生成外观相同却不能参与配方的另一套食品。新 GROCERY_BAG 是餐桌上敞开的简化购物袋原型，当前固定放置，不支持抓起整袋或开合。ZONE_FRIDGE_STORAGE 是冰箱内的登记区域。目录见 `Docs/ReviewV2_M1_Identities.tsv`，原 M0 目录保留为历史。

`IngredientStored` 为真实用户释放后通过几何/稳定条件的事实，ObjectId 是具体食材，ZoneId/AnchorId 都指登记储物区域。每件食材每 Visit 仅记一次，后续取出做菜不回退 Goal。阈值及稳定参数为原型假设，详见开发记录。`TomatoCut`/`EggReceivedInBowl` 目前只表示首次料理开始事实，ObjectId=COOKING_STATION，不能据此识别具体鸡蛋，也不是完整加工流水。

枚举仅在末尾追加，既有值/事件字段/订阅接口保留。消费者应容忍未使用的事件种类，不把新枚举视为主观意图。

## 正式保存端口接线

T3 可交付实现 `ISessionSavePort` 的 MonoBehaviour。T1 在 V2 Host 的 `sessionSavePort` Inspector 字段引用该组件，Host Awake 时注入 SessionController。错误类型会收到失败回执并停在 Review，不能忽略错误后显示保存成功。

Save 收到 requestId 与快照，只有实际持久化及读回验证完成才回调相同 requestId、success=true；失败 false 并给出可显示原因。Core 已拒绝迟到/重复回执，失败保持 Review 可重试。未接端口的 M0 NotConfigured Finish 继续存在，不属于 M1 的可靠保存验收。

`ReviewSavePortProbe` 仅为测试夹具：它证明路由与错误/成功状态处理，不写盘，不能作为 T3 存储证据。正式端口交付后仍须以真实文件、读回、写盘失败、零标记、Reset 历史保留和半途退出做集成验证。

## 集成交付清单

T3 需提供 Prefab/组件路径、所需 Host/锚点引用、存储 Schema、真实保存/读回与失败重试说明、前后事件窗口规则。T2 提供统一标记入口与回顾入口接线。T1 单独在 V2 场景集成，避免改旧 Cooking/Phase 2 UI；不得替 T3 实现正式业务来绕开依赖。

已知引导风险：继承的 Cooking 文案仍有“从冰箱取番茄/鸡蛋”等旧路径提示，不能当作新叙事的正式教学验收。T2 正式接入时需核对袋中起点与自由活动文案；T1 当前只验证料理机制与恢复，不代表非专业住户已能自主理解全部入口。
