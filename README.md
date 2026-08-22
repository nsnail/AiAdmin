# AiAdmin

.NET 10 + Vue 3 的后台管理框架，前端基于 Art Design Pro。

## 开发运行

```powershell
dotnet run --project src/AiAdmin.Api --launch-profile http
cd src/AiAdmin.Web
cnpm install
cnpm run dev -- --no-open
```

- 前端：http://localhost:3006
- 后端：http://localhost:5285
- 默认账号：`admin` / `123456`

开发环境默认使用 SQLite，首次启动自动创建 `src/AiAdmin.Api/aiadmin.db` 并写入角色和管理员种子数据。
生产环境可在 `appsettings.json` 中将 `Database:Provider` 设置为 `SqlServer`、`PostgreSQL` 或 `MySQL`，并配置同名连接字符串。首次连接空数据库时会自动建表。

部署前必须通过配置中心或环境变量覆盖 `Jwt:Key` 和数据库密码。

## 后端分层

- `AiAdmin.Core`：实体模型、接口契约和共享特性，不依赖其他业务项目
- `AiAdmin.Infrastructure`：数据库、缓存、令牌和对象存储等基础设施实现，仅依赖 `AiAdmin.Core`
- `AiAdmin.Api`：应用宿主、控制器和中间件，依赖 `AiAdmin.Core` 与 `AiAdmin.Infrastructure`

项目使用单向引用形成增量编译边界。仅修改 API 层时不会重新编译 Core 和 Infrastructure；修改 Infrastructure 时不会重新编译 Core。

本地系统设置种子参数写入 `src/AiAdmin.Api/appsettings.Local.json`，包括登录滑块验证、用户注册、邮箱验证和 SMTP 配置，文件格式参考同目录的 `appsettings.Local.example.json`。本地配置文件已排除 Git 跟踪，配置仅在对应字典项不存在时写入数据库。

系统数字主键使用 52 位 Snowflake ID，当前十进制长度约 15 位。时间范围从 2025-01-01 起约 17.4 年，单实例每毫秒最多生成 256 个 ID。单实例使用默认 `Snowflake:WorkerId` 即可，多实例部署时必须在每个实例的本地配置中设置不同的 `WorkerId`，取值范围为 `0` 到 `31`，最多支持 32 个实例。