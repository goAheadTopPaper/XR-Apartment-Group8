# M1 Integration Manifest

2026-11-14 的交接记录，当时 Uncommitted；当前提交与同步状态以 Git 为准。T1 G01/G02 已有独立验证保持有效；完整反馈闭环 **Integration Pending**。此前失败与批次范围见 Development Progress，本文件不把接口预留算作端到端验收。

## 接入清单

| 集成项 / Owner | 接口与交付格式 / 挂载位置 | 当前状态与缺口 | 接入验收 |
|---|---|---|---|
| 全局入口/任务书/教学 T2 | 独立 UI Prefab + 脚本路径、GUID、引用清单；挂 V2 专属 UI 根，绑定唯一 ReviewV2 Session 的 ReviewSessionHost | Integration Pending：正式 Prefab 未交付；M0 ReviewIntegrationPanel 仅为临时入口 | Setup 禁止 Cooking；教学可跳过；从 Host.Snapshot 读取各 Goal 的 State 与 Available，多项并行，未接入不显示可完成；无完成数量门槛 |
| Review/Continue/Reset/SchemeSwitch T2 | IReviewSessionCommands.Execute；读取 Changed、OperationCompleted；命令 Accepted 后以相同 RequestId 的异步结果定成功 | T1 命令 Ready for Integration；正式入口 Integration Pending；M1 Baseline 不支持真实方案切换 | 任意进度主动 Review/Continue；Reset 需显式确认；M2 切换前显示进度重置/标记保留，失败可见且可重试 |
| Marker 创建/定位/回顾 T3 | MarkerCapture/MarkerReview 独立 Prefab + 组件路径；V2 UI/反馈根；T2 统一可见入口 | Integration Pending：正式组件未交付 | G02 半途及零 Goal 均可标记；0 Marker 可 Review；返回保持原 Marker 身份 |
| 空间定位 T1→T3 | host.Resolve(stableId)；目录 ReviewV2_M1_Identities.tsv；命中对象用 AnchorId+局部坐标并附世界快照，无对象用明确场景位置后备 | Ready for Integration：原66 ID及袋/储物区；Resolve(null/未知)不可猜坐标 | FRIDGE/COOKING_STATION/GROCERY_BAG/ZONE_FRIDGE_STORAGE 解析准确；对象移动后回顾保留原位置与身份 |
| 身份/事实 T1→T3 | ISessionReadModel、IInteractionEventSource；捕捉时复制 SessionId/SchemeId/SchemeVisitId/ModelVersion；Observed 为不可变事实，毫秒是会话单调时间 | Verified — Independent；正式 EventBuffer Integration Pending | Before/After 仅收同 Visit；Reset/切换关闭旧窗口；空事件合法，评价只取用户输入 |
| Marker 即时保存/读回 T3 | 提供 Schema、样例JSON/CSV、写盘/读回与失败重试说明；创建后即时保存，After 异步补记 | Integration Pending：正式持久化缺失 | 实际文件读回身份/位置/用户备注一致；无后续事件也完成即时保存 |
| 会话保存 T3→T1 | 实现 ISessionSavePort 的 MonoBehaviour；Inspector 注入 host.sessionSavePort；Awake 前配置完成 | T1 注入 Ready for Integration；正式端口 Integration Pending | Save(requestId,snapshot,callback) 实际写盘+读回后回同 ID,true；失败 false+原因，停 Review 可重试；错/旧/重复回执无效 |
| 历史保留 T3 | 按创建时身份保存；Reset/Visit 变化不清 Marker；回顾按原方案/Visit 展示 | Integration Pending：正式历史存储未交付 | Reset 后 Goal 清零且 Visit 新建，旧反馈文件可读；Review 往返不重置世界或覆盖历史 |

## 初始化和联合验收

Host 在 Awake（执行顺序 -100）创建 Core。在场景保存前绑定 Host 的 station、recipe、roomReset、identities、groceries、modelVersion 与可选保存组件；消费者 OnEnable 订阅、先读当前 Snapshot，OnDisable 退订。Resolve 只定位当前模型，历史位置使用捕捉快照。回调必须完成真实工作后执行；测试 ReviewSavePortProbe 不写盘，仅验证路由。

| 联合路径 / Owner | 需交付 / 当前状态 | 通过证据 |
|---|---|---|
| Narrative A：两种活动顺序 T1/T2/T3/T4 | T1 顺序/同对象料理独立通过；反馈 Integration Pending | 活动不互锁、不重置世界；具体观察实际保存读回且位置/Visit正确 |
| Narrative B：半途标记→Review→返回 T1/T2/T3/T4 | T1 世界/Goal往返独立通过；Marker Integration Pending | 原食材、锅、活动证据、Marker身份保持；可继续料理 |
| Narrative C：零Goal/零或多Marker→保存结束 T2/T3/T4 | Core允许；正式存储 Integration Pending | 真实文件读回后 Finished；失败留Review，重试成功；无目标或标记数量门槛 |
| 保存失败与恢复/Reset历史 T3/T4 | Integration Pending | 实际写盘失败证据、可重试回执与读回、旧Visit隔离及历史保留 |

M1 依赖待联调不阻止 T1 独立 M2。正式交付到达后由 T1 在唯一 V2 主场景增量接线，保留旧 Cooking 和 Phase 2。
