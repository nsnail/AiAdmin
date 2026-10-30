# AiAdmin.Infrastructure

基础设施层：数据库上下文与种子数据、动态查询、权限与数据范围、令牌、对象存储、加密等。仅依赖 `AiAdmin.Core`，被 `AiAdmin.Api` 和测试项目引用。

## 目录

| 目录            | 内容                                                                                                     |
|-----------------|----------------------------------------------------------------------------------------------------------|
| `Data`          | `AppDbContext`（框架实体）与 `AppDbContext.Business`（业务实体）、数据库初始化与种子、SQL 审计与停机拦截器 |
| `Services`      | 动态查询、列表分组与筛选元数据、权限与数据范围缓存、令牌、对象存储、加密、IP 归属地、日表服务等           |

## 数据库

- **数据库上下文**：`AppDbContext` 同时承载框架实体和业务实体，业务实体的映射放在 `AppDbContext.Business.cs`，避免与框架文件产生合并冲突
- **提供方**：通过 `Database:Provider` 选择 MySQL、SQL Server、PostgreSQL 或 SQLite；所有提供方统一启用拆分查询
- **自动维护**：保存时新增实体写入 `CreatedAt`，修改时更新 `UpdatedAt`（`IUpdatedAt`）且禁止改写 `CreatedAt`，`IVersion` 实体的版本号随每次修改自增并作为并发令牌；为 `IOwner` 实体补齐当前操作者和部门；每次写入还会生成数据库审计日志
- **数据权限过滤**：对所有实现 `IOwner` 的实体统一追加全局查询过滤。未登录场景（后台任务等）和拥有全部数据权限的用户不过滤，其余用户只能看到所有者是自己或所有者部门在授权范围内的数据。业务实体无需自行声明过滤
- **拦截器**：`DatabaseCommandAuditInterceptor` 把每条 SQL 作为结构化日志输出，`DatabaseShutdownInterceptor` 把数据库调用登记为安全停机的在途活动
- **初始化**：`DatabaseInitializer` 分阶段初始化，系统数据（角色、菜单、字典、用户）和业务种子分开，便于只重建业务数据；菜单种子来自 `Data/menu-seed.json`，其 `component` 字段对应前端 `views` 下的目录。生产环境不会自动执行
- **API 请求日志**：按天分表，建表、批量写入和过期清理由 `ApiRequestLogTableService` 负责，使用与主库相同的提供方和连接串

## 动态查询

这是框架最核心的公共能力，业务代码不得绕开它自行解析筛选字段。

- `DynamicFilterExtensions.ApplyDynamicFilter`：把 `And / Or` 嵌套的 `DynamicFilter` 转换为表达式，统一验证字段、操作符和值类型，无效输入抛出 `DynamicFilterValidationException`（映射为明确的 400 错误），不会静默忽略条件
- `DynamicFilterExtensions.ApplyDynamicSort`：按 `sortField`、`sortOrder` 排序，支持别名映射，非法字段抛出异常，默认排序必须显式指定
- `ListFilterMetadataService.GetFields<T>()`：读取实体属性上的 `ListFilterAttribute`，生成 `filter-fields` 接口的元数据
- `ListFilterGroupingService`：生成分组筛选栏的数量统计
- 操作符定义必须与前端一致；新增操作符需同时更新前端操作符列表、动态查询编辑器、`DynamicFilterExtensions` 和测试

标准用法（先筛选，再统计总数，然后排序，最后分页）：

```csharp
var query = db.Xxx.AsNoTracking().ApplyDynamicFilter(request.DynamicFilter);
var total = await query.CountAsync();
var rows = await query
    .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(Xxx.CreatedAt), true)
    .Skip((request.Current - 1) * request.Size).Take(request.Size)
    .ToListAsync();
```

## 权限

- **接口权限**：`ApiEndpointSyncService` 启动时扫描控制器并同步接口表，`ApiPermissionCache` 缓存角色与接口的授权关系，供 `ApiPermissionMiddleware` 判定
- **数据范围**：`DataScopeContext` / `DataScopeCache` 缓存当前用户的数据范围（全部、本部门、本部门及下级、仅本人），由 `DataScopeMiddleware` 在请求入口填充
- **菜单权限**：`MenuAuthListFilter` 过滤菜单按钮权限

## 其他服务

| 服务                        | 说明                                                             |
|-----------------------------|------------------------------------------------------------------|
| `TokenService`              | JWT 签发与校验                                                   |
| `EncryptionService`、`CredentialProtectionService` | 加解密与凭据保护                          |
| `MinioStorageService`       | MinIO 对象存储，配置见 `MinioOptions`                            |
| `IpLocationService`         | IP 归属地查询                                                    |
| `ServerTime`                | 服务器时区与 UTC 时间转换，列表时间字段统一经它输出              |
| `*JsonConverter`            | `long` 序列化为字符串、`DateTime` / `DateTimeOffset` 统一为 UTC  |
| `ApiDocumentationService`   | 根据 `ApiDocumentedAttribute` 生成开放接口文档                   |

## 约定

- 缓存只使用 Redis，不使用 `IMemoryCache`
- 不得直接使用 `HttpClient` 对外发请求，对外请求统一经过 `Api` 层的 `ExternalHttpRequestService`
- 参与 EF 翻译的集合使用 `List<T>`；`await using var` 声明的上下文不再手动释放
- 新增或修改代码必须通过 `dotnet build`（0 警告 0 错误），并补充对应测试