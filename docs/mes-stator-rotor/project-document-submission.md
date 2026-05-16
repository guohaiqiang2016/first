# 项目文档提交清单

本文档用于汇总汽车零部件定转子 MES 项目的全部相关交付文档、代码实现资料、数据初始化资料与验证资料，作为项目评审、归档和交付移交时的提交清单。

## 1. PMP 阶段文档清单

| PMP 阶段 | 文档 | 路径 | 提交状态 | 说明 |
| --- | --- | --- | --- | --- |
| 启动 | 项目章程与范围说明 | `docs/mes-stator-rotor/07-project-management/project-charter-and-scope.md` | 已提交 | 明确项目目标、范围、干系人、里程碑、风险和验收原则。 |
| 规划 | 需求规格说明书 | `docs/mes-stator-rotor/01-requirements/requirements-specification.md` | 已提交 | 覆盖用户角色、功能需求、非功能需求、数据需求、接口需求和需求追溯矩阵。 |
| 规划 | 业务蓝图 | `docs/mes-stator-rotor/02-business-blueprint/business-blueprint.md` | 已提交 | 描述计划到完工、定子制造、转子制造、质量闭环、主数据、业务规则和 KPI。 |
| 规划 | 系统设计说明书 | `docs/mes-stator-rotor/03-design/system-design.md` | 已提交 | 定义总体架构、模块设计、数据模型、状态机、接口、安全、部署和可观测性。 |
| 规划 | UI 原型与交互说明 | `docs/mes-stator-rotor/04-ui/ui-prototype.md` | 已提交 | 提供首页、排产、工位、检验、追溯、不合格处理等低保真原型。 |
| 执行 | 开发实施说明书 | `docs/mes-stator-rotor/05-development/development-guide.md` | 已提交 | 规范技术架构、工程模块、开发流程、API 示例、采集、数据迁移和发布回滚。 |
| 监控 | 测试计划与用例 | `docs/mes-stator-rotor/06-testing/test-plan-and-cases.md` | 已提交 | 覆盖测试范围、环境、准入准出、缺陷等级、核心用例、性能指标和 UAT 场景。 |
| 收尾 | 上线切换与运维交接 | `docs/mes-stator-rotor/07-project-management/go-live-and-handover.md` | 已提交 | 明确上线策略、检查清单、切换步骤、回退预案、培训和运维交接。 |
| 全阶段 | 文档集总览 | `docs/mes-stator-rotor/README.md` | 已提交 | 提供文档目录、建设目标和适用范围。 |

## 2. 实现与交付资料清单

| 类别 | 资料 | 路径 | 提交状态 | 说明 |
| --- | --- | --- | --- | --- |
| 解决方案 | C# 解决方案 | `Mes.StatorRotor.sln` | 已提交 | 包含领域层、API 层和测试项目。 |
| 领域实现 | MES 领域模型与服务 | `src/Mes.Domain` | 已提交 | 覆盖生产执行、质量、设备、追溯和系统交互消息。 |
| API 实现 | MES HTTP API | `src/Mes.Api` | 已提交 | 覆盖健康检查、驾驶舱、工单、单件、工序、检验、设备、追溯和系统交互接口。 |
| 自动化测试 | xUnit 测试 | `tests/Mes.Domain.Tests` | 已提交 | 覆盖缺料、参数超限、检验、不合格、追溯、设备故障和集成消息。 |
| 离线校验 | 结构与覆盖校验脚本 | `scripts/validate_csharp_project.py` | 已提交 | 用于无 .NET SDK 环境的文件、端点、规则、数据库脚本和测试结构验证。 |
| 数据存储 | 数据库表结构脚本 | `database/schema.sql` | 已提交 | PostgreSQL 兼容的 MES 业务表、质量表、设备表和集成消息表。 |
| 初始化数据 | 初始化数据脚本 | `database/seed-data.sql` | 已提交 | 初始化定子/转子产品、工艺路线、工序、参数、设备、演示工单和 WMS 出站消息。 |
| 数据库说明 | 数据库执行说明 | `database/README.md` | 已提交 | 说明 schema 与 seed 脚本用途和执行顺序。 |
| 根说明 | 仓库 README | `README.md` | 已提交 | 说明项目结构、运行方式、业务能力和数据库初始化。 |

## 3. 文档与实现追溯

| 文档需求域 | 实现/资料支撑 | 覆盖说明 |
| --- | --- | --- |
| 计划与工单 | `MesExecutionService.CreateOrder`、`ReleaseSerial`、`/api/orders`、`production_orders` | 支撑 ERP 计划转工单、工单发布、单件释放和状态管理。 |
| 工艺路线与参数 | `ProcessRoute`、`OperationDefinition`、`ParameterSpec`、`process_routes`、`operation_parameter_specs` | 支撑定子/转子工艺路线、关键参数上下限和阻断规则。 |
| 生产执行与防错 | `StartOperation`、`BindMaterial`、`RecordParameter`、`CompleteOperation` | 支撑工序顺序、物料匹配、设备状态和参数超限防错。 |
| 质量管理 | `InspectionTask`、`RecordInspection`、`Nonconformance`、`DisposeNonconformance` | 支撑检验任务、不合格生成、评审处置、返工/报废/让步。 |
| 设备采集 | `Equipment`、`EquipmentSample`、`EquipmentAlarm`、设备 API、`equipment_samples` | 支撑设备状态、参数采样、报警和设备故障开工拦截。 |
| 追溯管理 | `Trace`、`TraceByMaterialLot`、追溯 API、物料/参数/检验/不合格记录表 | 支撑单件追溯和物料批次反向影响分析。 |
| 系统交互 | `MesIntegrationService`、集成 API、`integration_messages` | 支撑 ERP、WMS、QMS、EAM 的入站/出站消息与送达确认。 |
| 报表看板 | `ProductionDashboard`、`/api/dashboard` | 支撑计划达成、良品、报废、不合格和设备报警概览。 |
| 测试验证 | `MesExecutionServiceTests`、`validate_csharp_project.py` | 支撑核心业务规则和交付物完整性验证。 |

## 4. 提交确认

1. 项目管理文档、业务文档、设计文档、UI 文档、开发文档、测试文档、上线移交文档均已纳入 `docs/mes-stator-rotor`。
2. C# 系统代码、API、测试、数据库脚本和系统交互资料均已纳入仓库根目录、`src`、`tests`、`database` 和 `scripts`。
3. 后续如新增高级排产、真实数据库仓储、身份认证、前端页面或设备网关，应同步补充需求、设计、开发、测试和上线文档，并更新本文档清单。
