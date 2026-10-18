# 本地 CSV 存储（FR-10）

适用：Unity 2022.3.62f3 / StandaloneWindows64，Windows 电脑连接 VR 头显。数据保存在运行程序的电脑上，不需要服务器或网络。本次交付存储能力，表单通过现有流程接口对接。

## 文件位置

默认：`Application.persistentDataPath/ReviewRecords/sessions.csv`。

按当前工程的 Company/Product 设置，Windows 路径为：

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\XR-Apartment-Group8\ReviewRecords\sessions.csv
```

首次成功保存时创建目录和文件；退出程序、重新启动后保留。Editor 和 Windows Player 在 Company/Product 设置相同时共用该目录。`LocalCsvStorageAdapter.CsvPath` 返回实际完整路径；组件右键菜单 `Log CSV Storage Path` 可将路径写到 Console。修改 Company/Product 会改变默认目录，旧数据需要自行保留。

## 接入现有流程

1. 场景集成人在流程对象上添加 `LocalCsvStorageAdapter`（Add Component → XR Apartment → Storage → Local CSV Storage Adapter）。
2. 把组件拖到 `JourneyRuntimeHost` 的 **Storage Adapter** 字段，替换 `MockStorageAdapter` 的引用。当前 `PREF_Core_PlayValidation` 的 Mock 引用保留，未改动共享 Scene/核心 Prefab。
3. 后续表单使用 `JourneyRuntimeHost.SubmitFeedback(FeedbackDraft)`。Core 已在逐方案提交、暂停、退出和最终确认等保存节点调用 `ISessionStore.Save`，表单不需要再直接保存一遍。
4. 完成状态继续依据 `SaveResult.Succeeded && SaveResult.ReadBackVerified`；失败由 Host/Core 保留数据并提供重试，不显示保存成功。

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
| `feedback_summary` | 各方案的任务结果、Decision/Category/Importance、说明和跳过原因，可读枚举名称 |
| `final_preference_combination_id` / `final_trade_off_summary` | 最终偏好和取舍说明 |
| `record_json` | 完整 `SessionRecord`，包括全部反馈字段、markers、冲突、提示/帮助/舒适事件；用于无损读回 |

嵌套列表放在同一 CSV 的 JSON 单元格里，没有额外 JSON 数据文件；`record_json` 的枚举按现有 Unity schema 保存为数值。CSV 使用 UTF-8 BOM、逗号分列和双引号转义，支持中文、逗号、引号和换行；一条记录可能占多个文本行，应使用 CSV 解析器或 Excel 导入，不能按文本行数统计用户。Excel 的区域设置如未自动识别逗号，可使用“数据 → 从文本/CSV”并选择 UTF-8 和逗号。

以公式起始字符开头的展示文本会增加单引号，避免 Excel 当成公式执行；原始答案仍完整保存在 `record_json`。不要把 Excel 手工修改后的 CSV 当作恢复数据源；摘要和完整记录不一致时读取会明确失败。

## 保存与失败行为

同目录 `.lock` 文件通过独占文件句柄串行化存储实例；锁文件本身保留，进程结束后系统释放句柄。写入先生成临时 CSV、刷盘、验证，再原子替换最终文件，并再次读回确认。新会话在逻辑上追加一行，为保证旧记录完整，物理上重写整份小型评审 CSV。

Excel 占用文件、目录不可写、磁盘满、CSV 损坏或校验不一致时，回调返回 `OperationFailed` 和可读说明；不会自动清空、覆盖损坏文件或另换路径假装成功。关闭占用文件的程序或修复问题后可重试，重试不会新增重复用户行。突然断电前未确认成功的数据不能视为已保存；遗留 `.tmp` 不作为正式记录读取。

自动化检查：`Assets/_Project/Tests/EditMode/CsvSessionStoreTests.cs`，在 Unity Test Runner 的 EditMode 中运行；使用临时测试目录，不把测试数据混入真实用户文件。测试覆盖多用户、检查点、重启读回、全字段/中文/换行、文件锁/重试、损坏文件、无效输入、旧回执、模型版本保护及回调异常。

2026-10-18：上述 15 个用例已在 Unity 2022.3.62f3 编辑器主线程执行（含每例 SetUp/TearDown 和 NUnit 断言），15 通过、0 失败。当前未做正式表单/共享场景接线、Windows Player 或 Quest Link 端到端验证。
