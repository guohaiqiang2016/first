# 开发实施说明书

## 1. 开发目标

开发实施说明书用于规范定转子 MES 系统的工程结构、开发流程、编码规范、接口实现、数据迁移、配置管理、发布部署和运维支持，确保需求到代码、代码到测试、测试到上线可追溯。

## 2. 推荐技术架构

| 层级 | 推荐选型 | 说明 |
| --- | --- | --- |
| 前端 | Vue/React + TypeScript | 管理端、工位端和看板端可复用组件库。 |
| 后端 | Java Spring Boot/.NET/Node.js | 以团队既有技术栈为准，要求支持 REST、消息和任务调度。 |
| 数据库 | PostgreSQL/MySQL/SQL Server | 存储业务主数据、工单、质量、追溯和日志索引。 |
| 时序数据 | TimescaleDB/InfluxDB | 存储设备高频参数和状态。 |
| 消息队列 | RabbitMQ/Kafka/RocketMQ | 解耦接口、采集、报工和报表聚合。 |
| 缓存 | Redis | 工位会话、热点主数据、编码流水和看板缓存。 |
| 文件 | MinIO/NAS/对象存储 | 存储 SOP、图纸、检验附件和追溯报告。 |
| 边缘采集 | 工业网关 + OPC UA/MQTT | 现场设备协议适配和断点续传。 |

## 3. 工程模块划分

```text
mes-platform/
  apps/
    web-admin/          # 管理端
    web-workstation/    # 工位端
    web-dashboard/      # 大屏看板
  services/
    mes-plan/           # 计划与工单
    mes-execution/      # 生产执行
    mes-quality/        # 质量管理
    mes-equipment/      # 设备采集
    mes-material/       # 物料与库存协同
    mes-trace/          # 追溯服务
    mes-report/         # 报表服务
    mes-admin/          # 系统管理
  integrations/
    erp-adapter/
    plm-adapter/
    wms-adapter/
    qms-adapter/
    eam-adapter/
  edge/
    gateway-agent/
  docs/
  deploy/
```

## 4. 开发流程

1. 需求确认：开发前必须引用需求编号、原型页面和验收标准。
2. 方案设计：复杂功能需完成接口、表结构、状态机和异常处理设计评审。
3. 编码实现：按模块分支开发，提交信息包含需求编号或缺陷编号。
4. 单元测试：核心规则、状态流转、接口适配和计算口径必须覆盖单元测试。
5. 联调测试：ERP、WMS、QMS、设备网关等接口按联调清单逐项验证。
6. 代码评审：必须检查安全、性能、幂等、日志、异常和可维护性。
7. 发布部署：按环境流水线发布，保留版本包、配置和数据库脚本。

## 5. 编码规范

| 类别 | 规范 |
| --- | --- |
| 命名 | 业务对象使用统一英文命名，例如 production_order、operation_record、inspection_task。 |
| API | REST 路径使用复数名词，写操作需支持幂等键或业务唯一约束。 |
| 状态 | 状态字段使用枚举字典，不允许前端硬编码中文状态。 |
| 时间 | 统一使用服务器时间和 ISO 8601 格式，数据库保存 UTC 或统一时区策略。 |
| 日志 | 关键业务日志必须包含 trace_id、order_no、serial_no、user_id。 |
| 异常 | 对用户展示业务可理解提示，对日志记录技术异常栈和上下文。 |
| 审计 | 主数据发布、质量处置、参数修改、报废审批必须记录变更前后值。 |
| 安全 | 后端必须校验权限和数据范围，不能仅依赖前端隐藏按钮。 |

## 6. API 设计示例

### 6.1 工单发布

```http
POST /api/production-orders/{orderNo}/release
Content-Type: application/json
Idempotency-Key: 20260516-WO001-release

{
  "lineCode": "L-STATOR-01",
  "shiftCode": "DAY",
  "releasedBy": "planner01"
}
```

成功响应：

```json
{
  "success": true,
  "orderNo": "WO20260516001",
  "status": "RELEASED",
  "message": "工单已发布"
}
```

### 6.2 工位过站

```http
POST /api/workstations/{stationCode}/complete-operation
Content-Type: application/json
Idempotency-Key: STATION-WND01-202605160001

{
  "orderNo": "WO20260516001",
  "serialNo": "STATOR202605160001",
  "operationCode": "WINDING",
  "operator": "op1001",
  "equipmentCode": "WND-01",
  "materialLots": [
    {"materialCode": "CU-WIRE-001", "lotNo": "CU20260501", "qty": 0.45}
  ],
  "parameters": [
    {"tagCode": "TENSION", "value": 12.1, "unit": "N"},
    {"tagCode": "TURNS", "value": 48, "unit": "turn"}
  ]
}
```

失败响应示例：

```json
{
  "success": false,
  "errorCode": "PARAM_OUT_OF_SPEC",
  "message": "绕线张力超过上限，请检查设备程序或联系班组长",
  "blocking": true
}
```

## 7. 数据库脚本规范

1. 数据库变更脚本按版本目录保存，例如 `V1.3.0__add_trace_relation.sql`。
2. 脚本必须可重复审查，禁止直接在生产环境手工改表后不留脚本。
3. 大表变更需评估锁表时间，采用在线变更或灰度字段策略。
4. 字典初始化数据需包含编码、中文名、英文名、排序、启用状态和版本说明。

## 8. 设备采集实现规范

1. 每台设备建立采集点清单，字段包括 tag_code、地址、数据类型、采集频率、单位、上下限和业务含义。
2. 边缘网关负责协议转换、本地缓存、质量码、时间戳校正和补传。
3. MES 接收采集数据后按工单上下文绑定 order_no、serial_no、operation_code。
4. 高频曲线数据保存原始文件或时序点，关键特征值写入产品履历。
5. 采集异常需区分设备离线、网关离线、数据超限、数据缺失和格式错误。

## 9. 数据迁移与初始化

| 数据 | 迁移方式 | 校验规则 |
| --- | --- | --- |
| 组织与人员 | 模板导入或接口同步 | 人员工号唯一，组织层级完整。 |
| 产品与 BOM | ERP/PLM 接口或 Excel 初始化 | 产品、BOM、版本、生效日期一致。 |
| 工艺路线 | PLM 接口或 MES 手工维护 | 工序顺序连续，关键工序参数完整。 |
| 设备与工位 | 模板导入 | 设备编码唯一，工位与产线关系正确。 |
| 物料批次库存 | WMS/ERP 同步 | 库位、批次、数量与账实一致。 |
| 在制品 | 上线盘点导入 | 单件码、工序状态、物料绑定经业务确认。 |

## 10. 发布与回滚

1. 发布包必须包含应用版本、数据库脚本、配置变更、接口版本、前端资源和回滚说明。
2. 灰度策略优先按产线或班次启用新功能，避免全车间同时切换。
3. 回滚前需确认是否存在不可逆数据结构变更；必要时提供数据修复脚本。
4. 每次发布后执行冒烟测试：登录、工单查询、工位扫码、检验提交、追溯查询和接口健康检查。
