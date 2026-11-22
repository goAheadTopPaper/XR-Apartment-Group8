# Review V2 — T1 开发工作流

2026-11-14。负责人授权：Narrative-Driven, Integration-Ready Goal Development。适用于 Phase 3 T1；Phase 2 历史基线不回写。

## 每轮工作

1. 检查实际分支、工作树、当前契约和里程碑记录，以代码和真实测试为技术事实，保留已有个人修改。
2. 从住户叙事定义 Goal：生活体验、允许的不同路径、系统状态和真实交互、对建筑师—住户沟通的价值，以及可观察、可重复的验收条件。叙事是验收依据，不是强制操作脚本。
3. 审查仓库能力，维护 Narrative Coverage Matrix 和依赖图。区分已有能力、待实施能力及外部交付；每项验收关联证据与范围。
4. 复用 Contract、Host、事件和端口。必要时增加最小兼容边界；在 Integration Manifest 明确 Prefab 路径/挂载位置、脚本依赖、对象/Zone/Anchor 身份、初始化顺序、回调语义、Owner 和接入验收。T2/T3 正式 UI、Marker、存储由各自 Owner 交付。
5. 在授权范围内实施、运行可执行定向测试并修复。外部模块未交付时继续独立 T1 工作；替身明确标识，只证明其覆盖的契约。
6. 每次关键实现、决策、失败/修复、范围变化后刷新 Development Progress 与 Integration Manifest；文档顶部是最新状态，历史失败和 Proposed 决策保留。会话结束汇报 Goal、能力、证据、关键决策和下一步。

## 状态与完成判断

| 状态 | 所需证据 |
|---|---|
| Implemented | 当前源码/资源已存在，尚未通过相应执行验证 |
| Verified — Independent | T1 定向测试实际通过，注明版本、路径和覆盖范围 |
| Ready for Integration | 接入点、配置与验收说明齐备，并完成自身可执行验证 |
| Integration Pending | 明确的 T2/T3 交付或联合验证尚缺，列 Owner 与缺口 |
| Verified — End-to-End | 真实完整链路实际通过，注明方案、设备、持久化读回及测试范围 |
| Blocked | 具体技术/外部条件阻止该项继续，记录已尝试方案及解除条件 |

T1 修改必须完成可执行定向测试；仅依赖外部交付的联合测试允许延期。设备尚不可用时另注 Device Pending。Implemented、接口预留、Mock 成功及 Editor 通过均不能证明正式保存、完整产品闭环或 Quest Link 验收。不同版本/批次测试不能拼成一次全量。

## 风险分级与执行边界

低风险、可逆且在本轮授权及 T1 所有权内的代码、V2 场景和专属资源修改自主实施。重要但可逆的设计取舍记录备选方案、理由和影响后继续，不逐项申请批准。

以下情况暂停受影响的部分并等待负责人批准：改变核心叙事/验收目标；不兼容公共接口修改；大幅重构共享 Cooking 或破坏其他角色接口；删除/覆盖重要资源；修改其他角色正式业务；未经授权 commit、push、共享分支合并或构建配置修改。无关工作继续。

## 当前文档入口

- 技术契约：`ReviewV2_Contract_v0.4_M0.md`、`ReviewV2_M0_Integration.md`、`ReviewV2_Scene_Ownership.md`，可编译签名以 ReviewContracts.cs 为准。
- M1：`M1/T1_Development_Progress.md`、`M1/Integration_Manifest.md`、`ReviewV2_M1_Narrative_Plan.md`。
- M2：`M2/T1_Development_Progress.md`、`M2/Integration_Manifest.md`、`M2/Narrative_Goal_Plan.md`。
