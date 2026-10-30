# tests

后端 xUnit 测试，项目为 `AiAdmin.Tests`，引用 `AiAdmin.Core`、`AiAdmin.Infrastructure` 和 `AiAdmin.Api`。前端的工具函数测试在 `src/AiAdmin.Web/tests`。

## 运行

```powershell
dotnet test tests/AiAdmin.Tests
dotnet test tests/AiAdmin.Tests --filter "FullyQualifiedName~DeviceDynamicListQueryTests"
```

- `AiAdmin.Api` 进程运行时会锁住输出文件，运行前先停止
- 提交前必须全部通过，同时 `dotnet build` 要求 0 警告 0 错误（测试项目同样受 `build/code.quality.props` 约束）

## 测试分类

| 类别               | 代表文件                                                                                                           | 说明                                                       |
|--------------------|--------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------|
| 动态列表查询       | `*DynamicListQueryTests`（设备、角色、注册配置、系统消息，以及业务回调 / 消息 / 任务）                             | 每个列表接口必须覆盖的动态筛选与排序                       |
| 数据权限           | `OwnerQueryFilterTests`                                                                                            | 所有者与数据范围的查询过滤                                 |
| 实时推送与客服     | `RealtimeWebSocketTests`、`CrossInstanceRealtimeTests`、`SupportChatTests`                                         | WebSocket 推送、多实例经 Redis 总线转发、客服会话          |
| 安全停机           | `ShutdownGateTests`、`ShutdownGateArchitectureTests`、`DatabaseShutdownInterceptorTests`                           | 闸门状态、在途活动登记与释放；架构测试扫描源码，强制脱离请求生命周期的后台工作都登记到闸门 |
| 日志               | `ApiLogStorageFlowTests`、`ApiRequestLogTableServiceTests`、`ApiRequestLogsControllerTests`、`ApiRequestLogMapperTests` | 请求日志队列、分表入库、查询与映射                         |
| 计划作业与清理     | `OrphanMappingCleanupTests`                                                                                        | 种子作业、失效中间表映射清理                               |
| 接口与文档         | `BasicApiKeysTests`、`ApiDocumentationEnumTests`、`MenuAuthListFilterTests`                                        | 默认授予角色的接口键与真实控制器一致、文档枚举取值、菜单按钮权限 |
| 仪表盘与诊断       | `DashboardRequestStatsTests`、`ProcessDiagnosticsSamplerTests`                                                     | 操作日志统计、进程诊断采样                                 |
| 业务逻辑           | `AccountArchiveReaderTests`、`ProtocolTypeTests`、`CheckNumberServiceTests`、`PhoneNumberNormalizerTests`、`TaskConcurrencyTests`、`TgCallbackFlowTests` | 业务规则，不属于框架                                       |

## 测试替身

| 文件                          | 说明                                                       |
|-------------------------------|------------------------------------------------------------|
| `FakeSocket`                  | 内存 WebSocket，记录发送内容并支持等待收到指定条数的消息   |
| `InMemoryRealtimeBus`         | 内存版实时总线，用来模拟多实例之间的转发                   |
| `StubHttpMessageHandler`      | 替换 HTTP 处理器，避免真实外部请求                         |
| `ShutdownGateCollection`      | xUnit 集合定义（`ShutdownGate`），禁用并行，让依赖进程级闸门的测试串行执行 |

测试通常使用 SQLite 内存库和 `DispatchProxy` 生成的 Redis 假对象，不依赖真实的 MySQL 与 Redis。

## 编写约定

- 每个列表接口必须覆盖动态筛选与排序：字符串、数值、布尔、日期范围、集合、嵌套 And/Or、别名字段、非法操作符、非法排序字段和分页稳定性
- 系统种子会内置多条计划作业和字典项，读取种子表时不要使用不带条件的 `SingleAsync()`，按名称等业务键筛选
- 返回 `Task` 的辅助方法同样以 `Async` 结尾
- `await using var` 声明的对象不要再手动释放；需要在断言前释放时使用 `await using (var x = ...) { }`
- `Task.Run` 的 lambda 不要直接捕获 `using var` 变量，通过带参数的辅助方法传入
- 依赖进程级静态状态（如 `ShutdownGate`）的测试加上 `[Collection("ShutdownGate")]`，该集合禁用并行，避免相互干扰
- 方法与类使用中文 XML 文档注释，断言字符串使用英文