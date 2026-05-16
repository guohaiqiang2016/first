# Stator/Rotor MES Reference Implementation

本仓库包含汽车零部件定转子 MES 的设计文档和 C# 参考实现。文档位于 `docs/mes-stator-rotor`，系统代码位于 `src`，自动化测试位于 `tests`。

## 代码结构

| 路径 | 说明 |
| --- | --- |
| `src/Mes.Domain` | MES 核心领域模型、仓储接口、内存仓储和生产执行服务。 |
| `src/Mes.Api` | ASP.NET Core Minimal API，提供工单、单件、过站、物料、参数、检验和追溯接口。 |
| `tests/Mes.Domain.Tests` | xUnit 领域规则测试，覆盖缺料拦截、参数超限拦截和追溯履历。 |
| `scripts/validate_csharp_project.py` | 无 .NET SDK 环境下可运行的离线结构校验脚本。 |
| `database` | PostgreSQL 兼容 schema 与初始化数据脚本。 |

## 本地运行

需要安装 .NET 8 SDK：

```bash
dotnet restore Mes.StatorRotor.sln
dotnet test Mes.StatorRotor.sln
dotnet run --project src/Mes.Api/Mes.Api.csproj
```

启动后可访问 `GET /health` 检查服务状态，并通过 `/api/orders`、`/api/serials/{serialNo}/operations/{operationCode}/complete`、`/api/trace/{serialNo}` 等接口执行 MES 主流程。

## 业务能力

当前参考实现聚焦定转子 MES 的核心闭环：

1. 创建工单并按工艺路线释放单件。
2. 工位开工、物料批次绑定、设备参数记录和完工过站。
3. 校验工序顺序、必需物料、阻断型参数上下限和检验要求。
4. 自动生成检验任务，记录检验结果并生成单件追溯报告。
5. 支持不合格评审处置、设备状态/报警/采样、按物料批次反向追溯和生产驾驶舱指标。
6. 支持 ERP/WMS/QMS/EAM 等系统交互消息，提供入站计划接收、出站备料、完工回传、质量通知和设备报警通知队列。

## 数据存储初始化

初始化 PostgreSQL 兼容数据库时，按顺序执行：

```bash
psql "$MES_DATABASE" -f database/schema.sql
psql "$MES_DATABASE" -f database/seed-data.sql
```
