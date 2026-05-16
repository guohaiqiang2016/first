# MES 数据库存储脚本

本目录提供定转子 MES 参考实现的关系型数据存储初始化脚本，面向 PostgreSQL 兼容数据库。

## 脚本

1. `schema.sql`：创建产品、工艺路线、工序、参数规格、设备、工单、单件、物料绑定、过站记录、参数记录、检验、不合格、设备采样/报警和系统集成消息表。
2. `seed-data.sql`：初始化定子 A、转子 B 的产品、路线、关键参数、设备、演示工单和 WMS 备料出站消息。

## 执行顺序

```bash
psql "$MES_DATABASE" -f database/schema.sql
psql "$MES_DATABASE" -f database/seed-data.sql
```

这些脚本与当前内存仓储模型保持字段对齐，可作为后续替换为数据库仓储实现的持久化基线。
