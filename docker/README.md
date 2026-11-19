# docker

镜像构建、编排、网关与滚动发布相关文件。流水线见 [.gitea/workflows](../.gitea/workflows/README.md)。

| 文件                      | 说明                                                                                                                |
|---------------------------|---------------------------------------------------------------------------------------------------------------------|
| `api.Dockerfile`          | 后端镜像：SDK 镜像发布，运行时镜像以 `--enabled-hosted-services` 启动，监听 80 端口                                 |
| `web.Dockerfile`          | 前端镜像：`cnpm` 安装依赖、`vite build`，产物交给 Nginx 提供                                                        |
| `nginx.conf`              | 前端镜像内的 Nginx 配置，单页应用回退到 `index.html`                                                                |
| `docker-compose.test.yml` | 测试环境：单个 `api`、`web` 和 `redis`（带持久化数据卷），端口 5678 / 5679                                          |
| `docker-compose.prod.yml` | 生产环境：`api-a`、`api-b` 双实例加 `web`，端口 5678 / 5680 / 5679                                                  |
| `rolling-deploy.sh`       | 部署机上的滚动发布步骤：`pull`、`roll <api-a\|api-b>`、`settle <api-a\|api-b>`、`finish`                            |
| `gate-upstream.sh`        | 网关服务器上摘除或恢复 Nginx upstream 节点：`down\|up <端口>`                                                       |
| `monitoring/`             | Prometheus、Grafana、MySQL exporter 的监控栈配置、仪表盘和告警规则，见 [monitoring/README.md](monitoring/README.md) |

## 构建

镜像构建上下文是仓库根目录，不含 `.git`，版本号由流水线通过构建参数 `APP_VERSION` 传入（取标签名，可带 `v` 前缀），此时 MinVer 被关闭。

```powershell
docker build -f docker/api.Dockerfile --build-arg APP_VERSION=v0.0.0 -t aiadmin-api .
docker build -f docker/web.Dockerfile -t aiadmin-web .
```

前端 API 地址通过构建参数 `VITE_API_URL` 注入，默认 `/`，由网关把 `/api` 转发到后端。

## 环境与配置

- 测试环境使用 `ASPNETCORE_ENVIRONMENT=Test`，首次启动自动建表并写入种子数据；生产环境为 `Production`，不会自动初始化数据库
- 镜像地址通过环境变量 `API_IMAGE`、`WEB_IMAGE` 传入，不设置时 Compose 会直接报错；前端端口可用 `WEB_PORT` 覆盖
- 数据库、Redis、MinIO、`Jwt:Key` 等配置不写进镜像或 Compose 文件，使用环境变量（双下划线表示层级，如 `ConnectionStrings__MySQL`）或挂载的
  `appsettings.Local.json` 提供
- 每个后端实例的 `Snowflake__WorkerId` 必须唯一（0 到 31）：`api-a` 为 0，`api-b` 为 1，新增实例时顺延，否则会生成重复主键
- 两个实例共享同一数据库和 Redis；计划作业的执行通过 Redis 锁保证同一时刻只有一个实例触发

## 无中断滚动发布

生产环境由网关（Nginx）对 `api-a`、`api-b` 做 upstream 负载均衡，发布时逐个实例重建，流程由流水线编排：

1. 部署机 `rolling-deploy.sh pull` 拉取新镜像
2. 网关 `gate-upstream.sh down <端口>` 摘除一个节点，校验并平滑重载 Nginx
3. 部署机 `rolling-deploy.sh roll <服务>`：先调用 `POST /api/system/shutdown/prepare` 关闭安全停机闸门并等待在途工作归零，再强制重建容器，等待
   `/health` 返回 200，然后用 `/ready` 预热连接池（见下文）
4. 网关 `gate-upstream.sh up <端口>` 恢复节点
5. 部署机 `rolling-deploy.sh settle <服务>`：节点放回网关后观察它在真实流量下是否稳定，稳定后才继续
6. 另一个实例重复 2 到 5，最后 `rolling-deploy.sh finish` 启动 `web` 并清理无用镜像

要点：

- `stop_grace_period`（50 秒）必须大于应用的 `HostOptions.ShutdownTimeout`（40 秒），让主机先完成优雅停止再被 SIGKILL
- 排空需要环境变量 `SHUTDOWN_TOKEN`（具备权限的账号令牌）；未设置时跳过排空，仅依赖优雅停止
- 排空会关闭进程内闸门，所以重建必须使用 `--force-recreate`，否则镜像未变化时容器不会重启，闸门将一直关闭
- `gate-upstream.sh` 通过给 `server` 行加 `down` 标记实现摘除，摘除后若没有剩余可用节点则拒绝；`nginx -t` 失败或 `nginx -s reload` 失败时会还原配置文件；
  `reload` 依赖 `/run/nginx.pid` 与运行中的 Nginx 主进程一致，若 Nginx 不是由 systemd 启动，PID 文件为空会导致 `reload` 失败
- 脚本会在 `api-a` 重建前移除单实例时期遗留的 `api` 容器，它占用 5678 端口

### 预热与稳定观察

`/health` 只说明进程活着。新进程刚启动时数据库连接池几乎是空的（`MinimumPoolSize=2`），节点一放回网关就要承接全部流量，连接要现建，请求排队后会把连接池迅速占满，
这是 2026-11-18 17:31 发版时连接池耗尽的直接原因（当时 api-b 启动 2 秒就被放回网关，0.4 秒后又摘除了 api-a）。为此增加两道检查，都基于 `GET /ready`：

- `/ready` 会真正访问一次数据库和 Redis，返回两者的往返耗时；任一依赖不可用或超过 3 秒无应答时返回 503
- **预热**（`roll` 内，节点还没放回网关，失败不影响线上流量）：连续 5 次 `/ready` 返回 200 且总耗时不超过 500ms，最多等 60 秒
- **稳定观察**（`settle`，节点已放回网关）：每 2 秒探测一次，连续 15 次都在 500ms 内才算稳定，任一次变慢或失败就重新计数，最多等 120 秒

稳定观察超时默认**只告警并继续发布**，避免一次偶发的慢查询卡住整个发版；需要严格中断时设置 `SETTLE_STRICT=1`，此时失败会中断流水线，被摘除的节点保持
`down`，另一个节点继续对外服务，由人工介入。阈值和次数可通过环境变量调整：`WARMUP_PASSES`、`WARMUP_TIMEOUT`、`SETTLE_PASSES`、`SETTLE_TIMEOUT`、
`READY_MAX_MS`、
`PROBE_INTERVAL`。预热和稳定观察会让每个实例多花约 30 到 60 秒，发布期间另一个实例需要独自承担全部流量更久，所以发布窗口里不要叠加重查询类的计划作业。

## 前端版本就绪检查

滚动发布的顺序是先后端、后前端（`finish` 才启动 `web`），后端上线后会通过实时连接向浏览器推送新版本号。如果浏览器立刻提示升级，此时 `web`
容器还是旧版本，刷新后拿到的仍是旧前端。为此：

- `web.Dockerfile` 接收构建参数 `APP_VERSION`（流水线传入 tag 名），`vite build` 结束后在产物根目录写入 `version.json`（内容如 `{"version":"1.8.1"}`，去掉
  `v` 前缀）
- `nginx.conf` 对 `/version.json` 返回 `Cache-Control: no-store`，保证每次回源
- 浏览器收到后端新版本后，不会立即提示，而是每 5 秒读取一次 `/version.json`，等前端版本与后端新版本一致才弹出升级提示
- 读不到 `version.json`（本地构建没有 `APP_VERSION`、旧镜像、读取失败）或等待超过 10 分钟，直接提示，避免前端发布失败时永远不提示
- 本地用 `APP_VERSION=v1.2.3 npx vite build` 可以验证产物里的 `dist/version.json`

## 首次部署生产环境

生产环境不会自动建库建表。首次部署前需准备好数据库结构和种子数据（可先在测试环境初始化后迁移），再启动 Compose。切换已有数据库不会自动迁移旧数据，应先备份并单独迁移。