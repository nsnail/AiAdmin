# AiAdmin.Core

框架的共享类型层：实体模型、请求与结果契约、特性、雪花 ID 等。**不依赖任何其他项目**，被 `AiAdmin.Infrastructure`、`AiAdmin.Api` 和测试项目引用。

## 目录

| 目录          | 内容                                                                                                              |
|---------------|-------------------------------------------------------------------------------------------------------------------|
| `Models`      | 框架实体：用户、角色、部门、菜单、字典、计划作业、站内信、客服会话、日志等，以及实体基类与接口                    |
| `Contracts`   | 框架的请求、结果和响应契约（`ApiResponse<T>`、`DynamicQueryRequest`、`PagedResponse<T>` 等）                      |
| `Attributes`  | 公共特性：`ApiDescriptionAttribute`、`ApiDocumentedAttribute`、`ListFilterAttribute`                              |
| `Data`        | `SnowflakeIdGenerator`（雪花 ID）、`Crc32`                                                                        |
| `Services`    | 无外部依赖的公共服务：`ShutdownGate` 安全停机闸门及其活动类型、`JsonParsing`                                      |
| `Caching`     | `RedisKeyPrefix`，所有 Redis 键统一使用 `aiadmin:` 前缀                                                           |

## 约定

- **命名空间**：沿用 `AiAdmin.Api.*`（如 `AiAdmin.Api.Models`），不随项目名变化；业务代码使用 `AiAdmin.Api.{业务}.{域}`，与目录一致
- **实体基类**：实体继承 `EntityBase`，基类只提供 `CreatedAt`，并在其上声明列表筛选的默认第一项日期范围；主键由各实体自己声明
- **常用接口**：
    - `IUpdatedAt`：带更新时间的实体，保存时自动维护
    - `IVersion`：带乐观并发版本号的实体，修改时版本号自增，更新前需回填客户端持有的原版本号才能触发并发冲突检测
    - `IOwner`：带所有者（`OwnerId`）和所有者部门（`OwnerDepartmentId`）的实体，基础设施会据此自动追加数据权限过滤，并在新增时补齐当前操作者
    - `IAssignedOwner`：所有者表示分配关系的实体，新增时不自动补当前用户，未分配时保持为空
- **主键**：实体声明 `public long Id { get; init; } = SnowflakeIdGenerator.Next();`，数据库不生成主键；`long` 序列化为 JSON 时以字符串输出，避免前端精度丢失
- **列表筛选**：实体的可筛选属性使用 `ListFilterAttribute` 声明控件类型、标签、顺序、栅格宽度、占位符和选项，标签使用 i18n 键；前端的基础筛选控件全部由它驱动
- **接口描述**：控制器和公开方法必须标注 `ApiDescriptionAttribute`；需要出现在开放接口文档中的方法再标注 `ApiDocumentedAttribute`
- **契约**：每个类或 record 单独一个文件；响应 record 的位置参数由序列化读取，不要为消除 IDE 提示改成可写属性
- **不要引入依赖**：本项目只依赖 .NET 基础库，涉及数据库、缓存、HTTP 的代码放到 `AiAdmin.Infrastructure` 或 `AiAdmin.Api`

## 新增内容时

- 新增框架实体：放入 `Models`，在 `Infrastructure/Data/AppDbContext.cs` 中注册 `DbSet`，并按需补充种子数据与菜单
- 新增业务实体：放入业务目录（如 `{业务}/{域}`），在 `AppDbContext.Business.cs` 中注册
- 新增请求或结果：放入 `Contracts`（框架）或业务目录，补全 `<summary>`、`<param>` 等 XML 文档注释