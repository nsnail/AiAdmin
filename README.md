# AiAdmin

AiAdmin 是基于 .NET 10 + Vue 3 的通用后台权限管理框架，前后端分离，可在其上构建具体业务项目。

- **框架部分**：登录与鉴权、用户 / 角色 / 部门 / 菜单 / 数据权限、字典、计划作业、文件、日志、站内信与客服会话、实时推送、统一的动态列表查询等，说明见本文档与各目录 `README.md`
- **业务部分**：基于本框架构建的具体业务，设计与说明写在 [docs](docs/README.md)

开发约定（后端、前端、统一列表页、导出、质量与提交流程）见 [AGENTS.md](AGENTS.md)。

## 技术栈

| 层   | 技术                                                                                                                  |
|------|-----------------------------------------------------------------------------------------------------------------------|
| 后端 | .NET 10、ASP.NET Core、EF Core（MySQL / SQL Server / PostgreSQL / SQLite）、Redis（StackExchange.Redis）、MinIO、JWT  |
| 前端 | Vue 3、Vite、TypeScript、Element Plus、Pinia、vue-router、vue-i18n、Tailwind CSS、ECharts、Ace / WangEditor           |
| 日志 | 控制台、文件、Elasticsearch，以及按天分表入库的 API 请求日志                                                          |
| 部署 | Docker、Docker Compose、Nginx 网关、Gitea Actions 滚动发布                                                            |

## 目录结构

| 目录                                                   | 说明                                                       |
|--------------------------------------------------------|------------------------------------------------------------|
| [src/AiAdmin.Core](src/AiAdmin.Core/README.md)         | 实体模型、契约、特性、雪花 ID 等共享类型，不依赖其他项目   |
| [src/AiAdmin.Infrastructure](src/AiAdmin.Infrastructure/README.md) | 数据库、动态查询、权限与数据范围、令牌、对象存储等基础设施 |
| [src/AiAdmin.Api](src/AiAdmin.Api/README.md)           | 应用宿主：控制器、中间件、日志、计划作业、实时推送         |
| [src/AiAdmin.Web](src/AiAdmin.Web/README.md)           | 前端应用                                                   |
| [tests](tests/README.md)                               | 后端 xUnit 测试                                            |
| [build](build/README.md)                               | 代码清理、质量规则、版本标签脚本                           |
| [docker](docker/README.md)                             | 镜像构建、编排、网关与滚动发布                             |
| [.gitea/workflows](.gitea/workflows/README.md)         | Gitea Actions 发布流水线                                   |
| [.github/workflows](.github/workflows/README.md)       | GitHub Actions（暂未使用）                                 |
| [docs](docs/README.md)                                 | 业务设计与设计稿                                           |
| [scripts](scripts/README.md)                           | 辅助脚本目录（暂无脚本）                                   |

## 快速开始

环境要求：.NET SDK 10（见 `global.json`）、Node.js 20.19 及以上、MySQL 8.4、Redis。

```powershell
# 后端
dotnet run --project src/AiAdmin.Api --launch-profile dev

# 前端
cd src/AiAdmin.Web
cnpm install
cnpm run dev -- --no-open
```

- 后端：http://localhost:5678
- 前端：http://localhost:5679（开发环境通过 Vite 代理把请求转发到后端，代理目标见 `src/AiAdmin.Web/.env.development`）
- 种子账号：`root`、`job`、`admin`、`user`，初始密码均为 `1234qwer`，首次登录后请立即修改

### 数据库与配置

- 默认使用 MySQL，通过 `Database:Provider` 切换，可选 `sqlserver`、`postgresql`、`sqlite`；开发环境需先创建 `utf8mb4` 字符集的数据库和用户
- 真实连接串、Redis、MinIO、SMTP 等敏感配置不要提交，写入 `src/AiAdmin.Api/appsettings.Local.json`（已排除 Git 跟踪，格式参考同目录的
  `appsettings.Local.example.json`）或环境变量（如 `ConnectionStrings__MySQL`）；系统设置类种子参数仅在对应字典项不存在时写入数据库
- 开发与测试环境（`Development`、`Test`）首次启动自动建表并写入角色、菜单和用户种子数据；生产环境（`Production`）不会自动初始化，数据库结构与种子数据需另行准备，见 [docker/README.md](docker/README.md)
- 部署前必须通过配置中心或环境变量覆盖 `Jwt:Key` 和各类密码，切换已有数据库不会自动迁移旧数据，应先备份并单独迁移

### 启动参数

| 参数                       | 作用                                                                         |
|----------------------------|------------------------------------------------------------------------------|
| `--enabled-hosted-services` | 启用计划作业、Elasticsearch 日志和 API 请求日志入库等后台服务               |
| `--clear-database`          | 仅开发 / 测试环境：清空所有表后重新初始化（`--delete-all-tables` 为同义参数） |
| `--replace-job-request-url` | 覆盖计划作业的请求地址                                                       |

`launchSettings.json` 已提供对应的启动配置。多实例部署时只由部分实例启用后台服务，作业执行通过 Redis 锁避免重复触发。

## 架构要点

### 后端分层

三个项目单向引用：`Api` → `Infrastructure` → `Core`，形成增量编译边界，只修改 API 层不会重新编译 Core 和 Infrastructure。

- **主键**：系统数字主键使用 52 位 Snowflake ID（十进制约 15 位，起算 2025-01-01，可用约 17.4 年，单实例每毫秒最多 256 个）。多实例部署必须在每个实例设置不同的
  `Snowflake:WorkerId`（0 到 31，最多 32 个实例）
- **缓存**：一律使用 Redis（`IDistributedCache` / `IConnectionMultiplexer`），键前缀 `aiadmin:`，不使用 `IMemoryCache`
- **接口约定**：只使用 GET / POST；每个控制器和公开方法必须标注 `ApiDescriptionAttribute`，启动时据此同步接口表，作为接口权限的依据
- **权限**：菜单与按钮权限（`R_SUPER`、`R_ADMIN`、`R_USER` 等角色）、接口权限（`ApiPermissionMiddleware`）、数据范围（全部 / 本部门 / 本部门及下级 / 仅本人，
  由 `DataScopeMiddleware` 构造查询过滤）三层叠加
- **外部请求**：所有对外 HTTP 调用必须经过 `ExternalHttpRequestService`，统一记录地址、请求体、响应、状态码和耗时
- **日志**：控制台、文件、Elasticsearch 三路输出；API 请求日志经 Redis 队列批量写入按天分表，由后台服务消费
- **安全停机**：`ShutdownGate` 记录在途的 HTTP、作业、数据库、外部请求等活动，滚动发布时先排空再停止，配合 `/health` 与网关摘除节点实现无中断发布
- **实时推送**：WebSocket 连接由 `RealtimeConnectionManager` 管理，多实例间通过 Redis 总线转发

### 动态列表查询

所有表格列表共用同一套协议，是框架最核心的约定：

- 后端：`POST {resource}/list`，请求体 `DynamicQueryRequest`（`current`、`size`、`dynamicFilter`、`sortField`、`sortOrder`），返回 `PagedResponse<T>`；
  `GET {resource}/filter-fields` 返回由实体属性上 `ListFilterAttribute` 驱动的筛选元数据；`ApplyDynamicFilter` / `ApplyDynamicSort` 统一处理 `And / Or`
  嵌套筛选和排序
- 前端：`ArtSearchBar + ArtTable + useTable`，基础筛选、高级查询、已保存查询、右键筛选统一汇总为一棵 `dynamicFilter` JSON 树
- 用户管理页是标准模板，详细规则见 [AGENTS.md](AGENTS.md) 第 5 节

### 前端结构

```
src/AiAdmin.Web/src
├─ api/            接口函数与类型（框架 system-manage、业务 business-manage 分文件）
├─ components/     core（框架通用组件）、business（业务组件）
├─ hooks/core/     组合式函数，useTable 是列表页核心
├─ locales/        框架语言包 langs，业务语言包 business
├─ router/         静态路由、动态路由注册、守卫
├─ store/          Pinia 状态
├─ utils/          http、i18n、table、json（动态查询）、realtime、socket 等
└─ views/          页面，system 为框架页面，business 为业务页面
```

菜单由后端菜单种子（`menu-seed.json`）下发，`component` 字段对应 `views` 下的目录；新增页面需同步菜单种子、路由权限与 i18n。

## 构建、测试与发布

```powershell
dotnet build                                   # 要求 0 警告 0 错误
dotnet test tests/AiAdmin.Tests                # 后端测试
cd src/AiAdmin.Web; npx vite build             # 前端构建验证
./build/code.clean.full.ps1                    # 提交前必须运行
```

- 版本：使用 `standard-version` 生成 `CHANGELOG.md` 并打 `v*` 标签，`./build/new.version.tag.ps1` 提供交互式升级
- 发布：推送标签（版本脚本生成 `v*` 标签）触发 Gitea 流水线，构建 API 与 Web 镜像并滚动部署，见 [.gitea/workflows/README.md](.gitea/workflows/README.md) 与
  [docker/README.md](docker/README.md)