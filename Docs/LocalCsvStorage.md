# 本地 CSV 存储（FR-10）

适用：Unity 2022.3.62f3 / StandaloneWindows64，Windows 电脑连接 VR 头显。数据保存在运行程序的电脑上，不需要服务器或网络。现有 Feedback 表单已通过 Host 接入 CSV；每次有效提交即保存，不需要等整场评审结束。

## 文件位置

默认：`Application.persistentDataPath/ReviewRecords/sessions.csv`。

按当前工程的 Company/Product 设置，Windows 路径为：

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\XR-Apartment-Group8\ReviewRecords\sessions.csv
```

当前机器经 Unity API 确认的实际路径：`XR-Apartment-Group8\ReviewRecords\sessions.csv`。首次有效提交前文件可能尚未创建；本次测试使用独立临时目录，不向正式 CSV 填入演示用户。

首次成功保存时创建目录和文件；退出程序、重新启动后保留。Editor 和 Windows Player 在 Company/Product 设置相同时共用该目录。`LocalCsvStorageAdapter.CsvPath` 返回实际完整路径；组件右键菜单 `Log CSV Storage Path` 可将路径写到 Console。修改 Company/Product 会改变默认目录，旧数据需要自行保留。

## 接入现有流程

`PREF_Core_PlayValidation` 已添加 `LocalCsvStorageAdapter`，并把 `JourneyRuntimeHost.Storage Adapter` 从 Mock 改为该组件；厨房场景中的实例继承该绑定。使用 Unity Prefab API 修改，无需对场景 YAML 增加覆盖。

现有 `FeedbackFormView` 的 Submit → `JourneyRuntimeHost.SubmitFeedback` → Feedback 校验 → Core 保存请求 → CSV 写入/读回 → Core 推进。每个新会话新增一行；同一会话第二方案和最终比较更新原行。保存失败时表单保留答案并显示 `Retry save`，关闭 Excel 等占用文件的程序后点击重试；读回确认前不推进。

按 2026-10-18 用户确认，完成任务后的表单只要求 Decision、Category、Importance 三个选择，Experience/Everyday impact 为可选；不再要求文字说明。兼容字段 `shortExplanation` 保留为空，不把口述写成已采集文字。此规则相对 PRD Final FR-07 四项必填有明确偏差，见模块契约 §6。

如在其他流程 Prefab 中复用，在对象上添加 `LocalCsvStorageAdapter`（Add Component → XR Apartment → Storage → Local CSV Storage Adapter），并拖到 Host 的 Storage Adapter 字段即可；不需要在表单中重复保存一次。若在 Play 中更新了 Prefab，退出并重新进入 Play，使 Host 使用新的适配器。

也可以在尚未接入流程的独立表单脚本中调用组件：

```csharp
using System;
using XRApartment.Data;
using XRApartment.Runtime.Storage;

// storage 是 Inspector 绑定的 LocalCsvStorageAdapter。
// 每位新用户创建一个 SessionRecord；同一用户的会话中持续使用相同 sessionId。
var record = new SessionRecord
{
    sessionId = Guid.NewGuid().ToString("N"),
    modelVersion = actualModelVersion // 使用当前模型/参数实际版本
};
// 表单完成后填入 record.benchmarkFeedback 等字段；正式校验仍由 Feedback 模块负责。
storage.Save(1, record, result =>
{
    if (result.Succeeded && result.ReadBackVerified)
        UnityEngine.Debug.Log("CSV 已保存：" + result.Location);
    else
        ShowSaveError(result.Message); // 由表单实现失败提示与重试
});
```

代码片段中的 `actualModelVersion`、`storage`、`ShowSaveError` 由调用方提供。正式流程应优先使用 Host，避免重复保存。请求 ID 在同一存储实例/会话内递增；重试沿用 sessionId，使用新的 requestId。组件及其回调同步执行，须在 Unity 主线程调用。

## 一行对应什么

- 每位评审者每次新会话使用新的匿名 `sessionId`，在 CSV 中新增一行。
- 同一会话的 B1/B2 反馈、暂停、退出、最终结果或失败重试更新这一行，保留其他用户的记录。这是最新快照表，不是每次点击都新增一行的事件日志。
- 可保存尚未完成或中止的记录：`None / Completed / Incomplete / Aborted` 按 Core 实际结果保留，存储层不冒充表单校验器。
- 相同 sessionId 不允许换模型版本；旧 requestId 不得覆盖同一存储实例已收到的新请求。重新启动可通过 `TryRead(sessionId, callback)` 取回记录；这里提供数据读取，不提供尚未设计的跨启动流程恢复 UI。

## CSV 内容

| 列 | 内容 |
| --- | --- |
| `schema_version` | 当前固定 `1`；未知格式返回失败，避免覆盖 |
| `session_id` | 匿名会话 ID |
| `saved_at_utc` | 最近一次保存的 UTC 时间，ISO 8601 |
| `model_version` | 本次模型/参数版本 |
| `session_outcome` / `total_seconds` | 会话结果与会话累计秒数 |
| `benchmark_order` | 实际方案顺序，如 `B1 > B2` |
| `feedback_summary` | 各方案的任务结果、Decision/Category/Importance、Experience/Everyday impact/Discovery mode、兼容说明和跳过原因，可读枚举名称；同时兼容旧版不含可选项的摘要 |
| `final_preference_combination_id` / `final_trade_off_summary` | 最终偏好和取舍说明 |
| `record_json` | 完整 `SessionRecord`，包括全部反馈字段、markers、冲突、提示/帮助/舒适事件；用于无损读回 |

嵌套列表放在同一 CSV 的 JSON 单元格里，没有额外 JSON 数据文件；`record_json` 的枚举按现有 Unity schema 保存为数值。CSV 使用 UTF-8 BOM、逗号分列和双引号转义，支持中文、逗号、引号和换行；一条记录可能占多个文本行，应使用 CSV 解析器或 Excel 导入，不能按文本行数统计用户。Excel 的区域设置如未自动识别逗号，可使用“数据 → 从文本/CSV”并选择 UTF-8 和逗号。

以公式起始字符开头的展示文本会增加单引号，避免 Excel 当成公式执行；原始答案仍完整保存在 `record_json`。不要把 Excel 手工修改后的 CSV 当作恢复数据源；摘要和完整记录不一致时读取会明确失败。

## 保存与失败行为

同目录 `.lock` 文件通过独占文件句柄串行化存储实例；锁文件本身保留，进程结束后系统释放句柄。写入先生成临时 CSV、刷盘、验证，再原子替换最终文件，并再次读回确认。新会话在逻辑上追加一行，为保证旧记录完整，物理上重写整份小型评审 CSV。

Excel 占用文件、目录不可写、磁盘满、CSV 损坏或校验不一致时，回调返回 `OperationFailed` 和可读说明；不会自动清空、覆盖损坏文件或另换路径假装成功。关闭占用文件的程序或修复问题后可重试，重试不会新增重复用户行。突然断电前未确认成功的数据不能视为已保存；遗留 `.tmp` 不作为正式记录读取。

自动化检查：`Assets/_Project/Tests/EditMode/CsvSessionStoreTests.cs`，在 Unity Test Runner 的 EditMode 中运行；使用临时测试目录，不把测试数据混入真实用户文件。测试覆盖多用户、检查点、重启读回、全字段/中文/换行、文件锁/重试、损坏文件、无效输入、旧回执、模型版本保护及回调异常。

2026-10-18 首轮存储测试：15 通过、0 失败。此次接入共执行 58 项检查，58 通过、0 失败，包含 Prefab 绑定、选择项无文字说明的校验、完整 B1/B2 动作链后的真实 CSV 写入/读回、文件占用失败后 Core 重试、最终比较更新和旧 CSV 摘要兼容；测试在 Unity 2022.3.62f3 编辑器主线程执行（含每例 SetUp/TearDown 和 NUnit 断言）。Windows Player 与 Quest Link 端到端验收仍待实际设备完成。
