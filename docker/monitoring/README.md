# monitoring

> 配置中的内网 IP（`172.16.5.x`）、库名和端口是示例，按实际环境替换后使用。

Prometheus、Grafana 与 MySQL exporter 的部署配置，独立于业务 Compose 项目，互不影响发布。部署机目录为 `/opt/monitoring`。

| 文件                                                    | 说明                                                                  |
|---------------------------------------------------------|-----------------------------------------------------------------------|
| `docker-compose.yml`                                    | `prometheus`、`grafana`、`mysqld-exporter` 三个服务                   |
| `prometheus.yml`                                        | 抓取 `api-a`、`api-b` 的 `/metrics`、`mysqld-exporter:9104` 和两台服务器的 `node_exporter:9100` |
| `grafana/provisioning/datasources/prometheus.yml`       | Prometheus 数据源                                                     |
| `grafana/provisioning/dashboards/`                      | 仪表盘加载配置、`mysql-overview.json` 和 `node-overview.json`         |
| `grafana/provisioning/alerting/mysql-alerts.yml`        | MySQL 告警规则                                                        |
| `grafana/provisioning/alerting/contact-points.yml`      | Telegram 联系点与默认通知路由                                         |
| `tools/gen_mysql_dashboard.py`                          | 生成 `mysql-overview.json` 的脚本                                     |
| `tools/gen_node_dashboard.py`                           | 生成 `node-overview.json` 的脚本                                      |

## 不在仓库里的文件

部署机上还需要两份不进仓库的文件，权限都应收紧：

- `.env`：`GRAFANA_ADMIN_PASSWORD` 和 `TG_BOT_TOKEN`（都是必填，缺失时 Compose 直接报错），以及可选的 `GRAFANA_PORT`；权限应为 `600`
- `mysqld-exporter.cnf`：exporter 连接 MySQL 的账号，权限 `600`，属主 `65534`（容器里的 `nobody`），内容如下

```ini
[client]
user=exporter
password=<密码>
host=<MySQL 地址>
port=3306
```

## MySQL 监控账号

exporter 使用一个只读账号，只能从应用服务器登录，限制最多 3 个连接，读不到任何业务表数据：

```sql
CREATE USER 'exporter'@'<应用服务器 IP>' IDENTIFIED BY '<密码>' WITH MAX_USER_CONNECTIONS 3;
GRANT PROCESS, REPLICATION CLIENT ON *.* TO 'exporter'@'<应用服务器 IP>';
GRANT SELECT ON performance_schema.* TO 'exporter'@'<应用服务器 IP>';
GRANT REFERENCES ON aiadmin.* TO 'exporter'@'<应用服务器 IP>';
```

`REFERENCES` 只用来让 `information_schema` 暴露业务表的大小和估算行数；缺少它时，仪表盘的“业务表容量”两个面板没有数据。

## 仪表盘

`MySQL 监控` 在 Grafana 的 `AiAdmin` 文件夹，分概览、连接、吞吐与慢查询、InnoDB 与锁、网络与资源、业务表容量六组。
仪表盘配置了 `allowUiUpdates: false`，不能在界面里直接保存；修改 `tools/gen_mysql_dashboard.py` 后重新生成：

```powershell
cd docker/monitoring/tools
python gen_mysql_dashboard.py
```

面板用到的指标名取自 `mysqld_exporter` v0.17.2，升级 exporter 后需要核对指标名是否变化。

## 服务器性能监控

Web 服务器（172.16.5.105，监控栈所在机器）和 DB 服务器（172.16.5.104）上都已通过 systemd 运行 `node_exporter`，监听 `:9100`，Prometheus 的 `node` 任务按内网地址抓取，
指标带 `role`（`web` 或 `db`）和 `instance` 标签。`node_exporter` 没有鉴权，只应在内网访问，两台机器的 9100 端口不要对公网开放。

`服务器性能监控` 仪表盘在 `AiAdmin` 文件夹，顶部的“服务器”变量可单选、多选或全选，分概览、CPU、内存、磁盘、网络、系统六组。
磁盘和网卡面板已排除 docker 网桥、虚拟网卡和临时文件系统。修改 `tools/gen_node_dashboard.py` 后重新生成：

```powershell
cd docker/monitoring/tools
python gen_node_dashboard.py
```

新增服务器时，先在新机器安装并启动 `node_exporter`，再在 `prometheus.yml` 的 `node` 任务里追加一个带 `role` 标签的目标，重启 Prometheus 即可，仪表盘不用改。

## 告警

`mysql-alerts.yml` 定义两条规则，每分钟评估一次，数据源 uid 写在文件里，数据源被重建后需要同步修改：

| 规则                 | 条件                                     | 持续时间 |
|----------------------|------------------------------------------|----------|
| MySQL 连接使用率过高 | 当前连接数 / `max_connections` 超过 80%  | 3 分钟   |
| MySQL 行锁持续等待   | `innodb_row_lock_current_waits` 大于 0   | 2 分钟   |

## 告警通知

告警通过 Telegram Bot 发送到运维群，配置在 `grafana/provisioning/alerting/contact-points.yml`：

- 联系点 `telegram-ops` 是默认通知策略的接收方，消息开头 `@` 值班人，触发和恢复都会发送
- 按规则名和实例分组，首次等待 30 秒，同一告警每 4 小时重复提醒一次
- Bot Token 通过环境变量 `TG_BOT_TOKEN` 注入，不进仓库；换 Token 时只改部署机的 `.env`，再执行 `docker compose up -d grafana`
- 群 Chat ID 不是密钥，直接写在 `contact-points.yml` 里，**必须是带引号的字符串**。不要改成环境变量引用：群 ID 是负数，经环境变量替换后会被识别成数字，
  Grafana 校验失败会导致服务无法启动，监控页面打不开
- 换值班人时修改消息模板开头的 `@用户名`；被 @ 的人需要在群里，且设置了 Telegram 用户名
- Bot 需要先被拉进群并收到过一条消息（如 `/start@<Bot 用户名>`），才能从 `getUpdates` 查到群的 Chat ID

没有数据或评估出错时规则也会告警（`noDataState` 和 `execErrState` 都是 `Alerting`），用来发现 exporter 或 MySQL 自身不可用。

联系点和路由由文件加载，不能在界面里修改；改完配置后重启 Grafana，并先检查日志里没有 `Failed to provision alerting`：

```bash
docker compose up -d grafana
docker logs --since 1m monitoring-grafana-1 2>&1 | grep -i "provision"
```

配置有误时 Grafana 会反复重启，改回正确配置前页面不可用；可以先把出错的文件移出 `provisioning/alerting/` 恢复服务。

## 部署

```bash
cd /opt/monitoring
docker compose config -q                      # 校验 Compose
docker compose up -d mysqld-exporter          # 先启动 exporter，确认 mysql_up 为 1
docker compose restart prometheus             # 新抓取任务生效，期间有几秒指标空窗
docker compose restart grafana                # 加载仪表盘和告警规则
```

Prometheus 配置可用 `promtool check config prometheus.yml` 校验。变更前先备份原配置。