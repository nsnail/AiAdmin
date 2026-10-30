# AiAdmin.Api

应用宿主：控制器、中间件、日志、计划作业、实时推送。引用 `AiAdmin.Core` 与 `AiAdmin.Infrastructure`，是唯一的可执行项目。

## 目录

| 目录          | 内容                                                                                                              |
|---------------|-------------------------------------------------------------------------------------------------------------------|
| `Controllers` | 框架控制器：认证、用户、角色、部门、菜单、字典、文件、计划作业、站内信、客服、日志、缓存、仪表盘、安全停机等    |
| `Middleware`  | 请求管道中间件与异常处理                                                                                          |
| `Services`    | 外部请求、计划作业、实时推送、字典快照、导出限制、系统信息等宿主服务                                              |
| `Logging`     | 控制台 / 文件 / Elasticsearch 日志提供程序，API 请求日志入库队列与后台服务                                        |
| `Properties`  | `launchSettings.json` 启动配置                                                                                    |

## 请求管道

`Program.cs` 中的中间件顺序（从外到内）：

1. `ShutdownGateMiddleware`：安全停机闸门关闭后直接返回 503，`/health` 不受影响
2. 异常处理（`DataAccessExceptionHandler`）：数据权限不足转 403，动态筛选校验失败转 400，并发冲突转明确提示
3. CORS、转发头、路由、WebSocket
4. 认证（JWT）、`RealtimeWebSocketMiddleware`（实时推送入口，浏览器 WebSocket 无法携带请求头，令牌通过查询参数 `access_token` 传递）
5. `ApiHttpLoggingMiddleware`：记录 HTTP 快照
6. `ResponseJsonCleanupMiddleware`：递归移除响应 JSON 中值为 null 或空字符串的属性
7. `DataScopeMiddleware`：根据用户角色构造数据范围
8. `ApiPermissionMiddleware`：接口权限判定，除显式 `AllowAnonymous` 的端点外全部检查，`R_SUPER` 放行
9. 授权与控制器；`/health` 匿名可访问且不产生请求日志

## 配置

| 文件                           | 说明                                                                                   |
|--------------------------------|----------------------------------------------------------------------------------------|
| `appsettings.json`             | 通用默认配置                                                                           |
| `appsettings.Development.json` | 开发环境                                                                               |
| `appsettings.Test.json`        | 测试环境                                                                               |
| `appsettings.Remote.json`      | 远程环境                                                                               |
| `appsettings.Local.json`       | 本地覆盖，**已排除 Git 跟踪**，存放真实连接串、密钥和 SMTP；格式见 `appsettings.Local.example.json` |

配置优先级：`appsettings.json` < 环境配置 < `appsettings.Local.json` < 环境变量（双下划线表示层级，如 `ConnectionStrings__MySQL`）。仓库中的配置文件只放占位或开发用值，**不得提交真实密码、密钥和令牌**，部署前必须覆盖 `Jwt:Key` 与各类密码。

主要配置节：`Database`、`ConnectionStrings`、`Snowflake`、`Jwt`、`Smtp`、`Minio`、`IpLocation`、`Elasticsearch`、`FileLogging`、`ApiLogStorage`、`Cors`。多实例部署时每个实例的 `Snowflake:WorkerId` 必须唯一。

## 启动参数与启动配置

| 参数                        | 作用                                                                       |
|-----------------------------|----------------------------------------------------------------------------|
| `--enabled-hosted-services` | 启用计划作业、Elasticsearch 日志和 API 请求日志入库后台服务（容器镜像默认带此参数） |
| `--clear-database`          | 仅 `Development` / `Test`：清空所有表后重新初始化（`--delete-all-tables` 同义） |
| `--replace-job-request-url` | 覆盖计划作业的请求地址，支持 `=` 和空格两种写法                            |

`launchSettings.json` 提供 `dev`、`dev --enabled-hosted-services`、`dev --clear-database` 等配置，端口 5678。`Development`、`Test` 环境启动时自动初始化数据库，`Production` 不会。

## 后台服务

- `ScheduledJobHostedService`：按 Cron 触发计划作业，通过 Redis 锁保证多实例下同一作业只触发一次，作业请求经 `ExternalHttpRequestService` 发出并记录执行结果
- `ScheduledJobReleaseHostedService`：每 5 秒扫描一次，释放运行时间超过作业超时设置的计划作业
- `ElasticsearchLogBackgroundService`、`ApiLogStorageBackgroundService`：从 Redis 队列批量消费日志，分别写入 Elasticsearch 和按天分表
- `RealtimeShutdownHostedService`：主机停止时关闭安全停机闸门并关闭所有实时连接（始终注册，不受 `--enabled-hosted-services` 影响）

计划作业与日志入库类后台服务只在 `--enabled-hosted-services` 时注册。

## 安全停机

`ShutdownGate`（在 Core 中）登记在途的 HTTP、计划作业（`Job`）、外部请求（`Outbound`）、后台批处理（`Background`）和数据库（`Database`）等活动。滚动发布时 `POST /api/system/shutdown/prepare` 关闭闸门并等待在途工作归零，`POST /api/system/shutdown/cancel` 重新打开。主机停机超时为 40 秒，必须小于容器的 `stop_grace_period`。

## 外部请求

所有对外 HTTP 调用必须经过 `ExternalHttpRequestService`，由它统一记录请求地址、请求体、响应、状态码和耗时。禁止直接使用 `HttpClient` 或 `IHttpClientFactory`。

## 新增接口的检查清单

1. 控制器和每个公开方法标注 `ApiDescriptionAttribute`，并补全中文翻译（前端 `server-message.ts` 或业务文件 `server-message.business.ts`），否则启动时接口同步会抛出异常
2. 只使用 `[HttpGet]` / `[HttpPost]`
3. 列表接口遵循动态列表规范：`POST {resource}/list`、`GET {resource}/filter-fields`，并补全动态筛选与排序测试
4. 需要出现在开放接口文档中的方法标注 `ApiDocumentedAttribute`
5. 新增菜单需同步 `menu-seed.json`、前端路由权限和 i18n
6. 运行 `dotnet build`（0 警告 0 错误）与 `dotnet test tests/AiAdmin.Tests`

具体规则见 [AGENTS.md](../../AGENTS.md) 第 3 节。