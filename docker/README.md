# docker

镜像构建、编排、网关与滚动发布相关文件。流水线见 [.gitea/workflows](../.gitea/workflows/README.md)。

| 文件                          | 说明                                                                                                   |
|-------------------------------|--------------------------------------------------------------------------------------------------------|
| `api.Dockerfile`              | 后端镜像：SDK 镜像发布，运行时镜像以 `--enabled-hosted-services` 启动，监听 80 端口                    |
| `web.Dockerfile`              | 前端镜像：`cnpm` 安装依赖、`vite build`，产物交给 Nginx 提供                                           |
| `nginx.conf`                  | 前端镜像内的 Nginx 配置，单页应用回退到 `index.html`                                                   |
| `docker-compose.test.yml`     | 测试环境：单个 `api`、`web` 和 `redis`（带持久化数据卷），端口 5678 / 5679                             |
| `docker-compose.prod.yml`     | 生产环境：`api-a`、`api-b` 双实例加 `web`，端口 6678 / 6680 / 6679                                     |
| `rolling-deploy.sh`           | 部署机上的滚动发布步骤：`pull`、`roll <api-a\|api-b>`、`finish`                                        |
| `gate-upstream.sh`            | 网关服务器上摘除或恢复 Nginx upstream 节点：`down\|up <端口>`                                          |

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
   `/health` 返回 200
4. 网关 `gate-upstream.sh up <端口>` 恢复节点
5. 另一个实例重复 2 到 4，最后 `rolling-deploy.sh finish` 启动 `web` 并清理无用镜像

要点：

- `stop_grace_period`（50 秒）必须大于应用的 `HostOptions.ShutdownTimeout`（40 秒），让主机先完成优雅停止再被 SIGKILL
- 排空需要环境变量 `SHUTDOWN_TOKEN`（具备权限的账号令牌）；未设置时跳过排空，仅依赖优雅停止
- 排空会关闭进程内闸门，所以重建必须使用 `--force-recreate`，否则镜像未变化时容器不会重启，闸门将一直关闭
- `gate-upstream.sh` 通过给 `server` 行加 `down` 标记实现摘除，摘除后若没有剩余可用节点则拒绝；`nginx -t` 失败或 `nginx -s reload` 失败时会还原配置文件；
  `reload` 依赖 `/run/nginx.pid` 与运行中的 Nginx 主进程一致，若 Nginx 不是由 systemd 启动，PID 文件为空会导致 `reload` 失败
- 脚本会在 `api-a` 重建前移除单实例时期遗留的 `api` 容器，它占用 6678 端口

## 首次部署生产环境

生产环境不会自动建库建表。首次部署前需准备好数据库结构和种子数据（可先在测试环境初始化后迁移），再启动 Compose。切换已有数据库不会自动迁移旧数据，应先备份并单独迁移。