# AiAdmin

.NET 10 + Vue 3 的后台管理框架，前端基于 Art Design Pro。

## 开发运行

```powershell
dotnet run --project src/AiAdmin.Api --launch-profile http
cd src/AiAdmin.Web
npm install
npm run dev -- --no-open
```

- 前端：http://localhost:3006
- 后端：http://localhost:5285
- 默认账号：`admin` / `123456`

开发环境默认使用 SQLite，首次启动自动创建 `src/AiAdmin.Api/aiadmin.db` 并写入角色和管理员种子数据。
生产环境可在 `appsettings.json` 中将 `Database:Provider` 设置为 `SqlServer`、`PostgreSQL` 或 `MySQL`，并配置同名连接字符串。首次连接空数据库时会自动建表。

部署前必须通过配置中心或环境变量覆盖 `Jwt:Key` 和数据库密码。
