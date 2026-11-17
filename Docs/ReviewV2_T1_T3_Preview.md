# T1 + T3 Editor 试用版

2026-11-17。T2 正式界面未接入，使用 T1 占位 UI。基础版本：T1 `feature/zh@d0302a8`，T3 `feature/mzh@7175372`。本地分支 `integration/t1-t3-preview`，独立工程 `D:/XR_virtual apartment/XR-Apartment-Group8-T1T3`。首次实施未提交或推送；2026-11-17 用户授权将此试用版提交并发布到远端新分支 `integration/t1-t3-preview`。不合并共享分支，不调整默认 Build Settings。

## 开始试用

1. 在 Unity **2022.3.62f3** 打开上述独立工程。原 `XR-Apartment-Group8` 保持原样；请核对窗口标题含 **T1T3**。
2. 使用 **XR Apartment → Review V2 → Open Phase 3 Review**，进入 Play，点击 Game 窗口让模拟器获得焦点。
3. 依次“确认设置”“开始体验”。WASD/鼠标和手柄选择沿用项目原有模拟器；右扳机 **T**、左扳机 **Shift+T**、菜单 **F8**。用鼠标定位射线目标后按扳机。
4. “记录 / Mark”打开表单，选择正面、担忧或待讨论（默认待讨论），点击“捕捉位置”。面板暂时隐藏，底部显示捕捉说明。
5. 指向厨房，先松开扳机，再按右扳机确认；完全松开后回到表单。未命中厨房实体时继续捕捉；F8 取消本次捕捉并返回表单。此时不创建正式记录。
6. 通过实体键盘填写可选中英文备注（最多 2000 字），点击“提交标记”。只有真实写盘并完整读回后才显示保存已验证。成功后可关闭表单；失败保留同一 MarkerId，按“重试保存”继续，避免重复标记。
7. “回顾 / Review”默认列出本次会话、当前方案的所有 Visit；“全部方案”查看本次会话的 A/B 记录。点击列表行查看详情，可拖动滚动条查看长备注。
8. “继续体验”保持原世界和 Visit；菜单提供 A/B 切换和重置确认。成功切换/重置创建新 Visit，旧记录保留。当前世界仅显示当前 Visit 的标记，以颜色和 + / ! / ? 区分类型；标记不参与碰撞、抓取或捕捉。
9. 在 Review 中“结束体验”保存会话 checkpoint。保存失败保持 Review 并允许重试；切换前保存失败保持旧方案。零 Goal、零 Marker 仍可 Review 和结束。

捕捉到稳定对象时保存对象局部坐标；无登记身份时明确显示“世界坐标”。不猜测 Zone/Anchor。备注保留原文，UI 不解析富文本。没有 VR 虚拟键盘；本轮为 Editor 键鼠试用，不能据此视为 Quest Link 验收。

## 保存与恢复边界

- 默认数据目录：`Application.persistentDataPath/ReviewV2/`。
- `feedback.json`：沿用 T3 `review-v2-t3-1` schema，保存标记的 Session、Scheme、Visit、ModelVersion、位置、类型、备注、前后事件 ID 和状态。事件窗口按 Session+Visit 隔离，暂停/Busy/重置/切换关闭旧窗口，不把后续 Visit 事件附到旧标记。
- `sessions/<SessionId>.json`：`review-v2-session-1` checkpoint，包含请求 ID、方案历史、当前身份、Goal 与调用保存时的会话阶段。Finish 前写入的阶段为 Review；这是保存请求时的快照，不是可恢复的终态会话文件。
- 临时文件替换后校验完整序列化内容。失败返回 false；不以文件存在或条数相同冒充成功。原文件损坏/不支持的 schema 不会被空记录覆盖，Review 显示失败并提供重试。
- 保存状态属于不同层次：单条标记显示自身保存结果；菜单的“上次会话保存已验证”不表示之后所有改动都已生成新 checkpoint。SafeExit 沿用即时中止，不承诺保存。
- 退出 Play 再进入会创建新 Session。旧标记仍从磁盘读入，但当前 UI 只列新 Session，不提供跨启动恢复或历史会话选择。
- 自动测试使用独立临时目录。实际输入验证使用忽略的 `Logs/ReviewV2/T1T3/actual-data`。测试时通过进程环境变量 `REVIEW_V2_FEEDBACK_DIRECTORY` 指定隔离目录，清除该变量后恢复默认路径。

## 接线与维护

会话场景中唯一 Host 上挂载记录器、`ReviewFeedbackSavePort`、`ReviewFeedbackCapture`、`ReviewFeedbackMarkers`。它们不随方案世界卸载。Host 的 `sessionSavePort` 在 Awake 前绑定真实保存适配器；记录器在 Host 前初始化，初始化读取失败可见且禁止提交。

原 UI Prefab 与既有对象 ID 保留。`ReviewMarkDraftView.t3Source` / `ReviewPlaceholderBoardView.t3Source` 绑定同一个记录器，Mark 绑定 capture，Board 绑定同一个 Host。公共 `ReviewContracts`、SessionController 和方案端口签名保持原样；T3 `AddMarker` 原签名保留，并增加将备注和位置一次保存的兼容重载。

**XR Apartment → Review V2 → Connect T3 Feedback** 是可重复的显式作者入口，仅在 Edit mode 使用：原位补齐 UI 控件与 Marker Prefab，并为 V2 主场景接线。不会重建厨房或切换构建目标。T2 后续替换 UI 时禁用旧 Panel，并将 Mark/Review 视图适配到同一记录器和 Host；不要创建第二个会话控制器或存储实例。

## 验证记录

最终源码定向 **EditMode 29/29**（job `818e277d434c4981b828ce76f16d743f`，约0.19秒）、**PlayMode 32/32**（job `1a8222042daa4df9b534b5249faa5a53`，约21.10秒）分批通过，不合称一次全量。涵盖存储5项、Core/上下文、主场景反馈6项和原Panel/Integration/Scheme/InitialScheme回归。Mark/Review的1280×720、960×720真实渲染已查看，文字无溢出。记录器绑定及标记跟随/无碰撞、当前会话隔离、真实写失败/恢复均有断言。当前控制台0 error。

作者重复配置UI/Marker Prefab和主场景3资源字节一致；原UI 493个和场景333个既有对象ID全部保留。详细 JSON、实际输入事件及磁盘样本在忽略的 `Logs/ReviewV2/T1T3/`。先前验证通过不替代最终源码重测。

- 初始存储测试 4/4 通过；首轮 PlayMode 28/31，三个失败涉及空 ID 序列化与旧存储状态断言。保留失败记录，按历史 M1 场景/当前 M2 场景分别修正断言。
- 第二轮 PlayMode 30/31，发现合成保存夹具的 Verified 断言被范围过宽的替换误改，已恢复；随后定向 31/31 通过。
- 实际输入发现类型按钮/标题裁切和捕捉确认松开后重入，分别调整类型行高度/字号和确认释放门控。最终类型标签完整可见。
- 实际模拟器链路完成 A 担忧标记（COUNTER_WORKTOP，中文+英文）、Review 往返、确认切 B、B 正面标记（世界坐标）、全部方案筛选、选择 A 历史详情和 Finish/Verified。磁盘样本中两条反馈保留各自 Scheme/Visit/ModelVersion。没有直接调用按钮回调替代该段输入验证。
- 现有 missing-script、StaticLightingSky、多 XRManager 夹具及模拟器 haptic 警告按实际日志保留，不宣称零警告。空间尺寸/位置继续为 prototype assumption；未做 Windows Player、Quest Link 或正式 T2 验收。
