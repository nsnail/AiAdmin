# .gitea/workflows

Gitea Actions 流水线，文件为 发布流水线文件。

## 触发条件

- 推送到 `main` 分支，或推送任意标签
- 构建与部署任务都带有 `startsWith(github.ref, 'refs/tags/')` 条件，**只有推送标签才会构建镜像并部署**；推送 `main` 本身不执行任何任务

版本标签由 `./build/new.version.tag.ps1` 或 `git-tag` 技能生成（`v` 前缀的语义化版本），同时更新 `package.json` 与 `CHANGELOG.md`。

## 任务

| 任务             | 说明                                                                                                          |
|------------------|---------------------------------------------------------------------------------------------------------------|
| `release-api`    | 构建后端镜像并推送到镜像仓库，标签为版本号、`latest` 和 `cache`，使用仓库缓存加速                              |
| `release-web`    | 构建前端镜像并推送，标签规则同上                                                                              |
| `release-deploy` | 部署生产环境：上传编排文件与脚本、拉取镜像，再按 `api-b`、`api-a` 的顺序摘除网关节点、滚动重建、恢复节点，最后 `finish` |

`release-deploy` 依赖前两个镜像任务，每个实例的摘除、重建、恢复是独立步骤，任一步失败后流水线停止，需要人工检查网关与实例状态。

## 密钥

在仓库的 Secrets 中配置，不要写进文件：

| 名称                                            | 用途                                          |
|-------------------------------------------------|-----------------------------------------------|
| `REGISTRY_USERNAME`、`REGISTRY_PASSWORD`        | 镜像仓库登录                                  |
| `DEPLOY_USERNAME`、`DEPLOY_PASSWORD`、`DEPLOY_SSH_KEY` | 部署机 SSH                              |
| `GATE_HOST`、`GATE_PORT`、`GATE_USERNAME`、`GATE_PASSWORD`、`GATE_SSH_KEY` | 网关服务器 SSH                |
| `SHUTDOWN_TOKEN`                                | 调用安全停机排空接口的令牌，未设置则跳过排空  |

镜像仓库地址和命名空间在文件顶部的 `env` 中定义。

## 约定

- 部署机上的编排文件与脚本上传到 `/opt/aiadmin`，网关上的脚本上传到 `/opt/aiadmin-gate`，路径写死在流水线中；端口与
  `docker/docker-compose.prod.yml` 一致（`api-a` 为 6678，`api-b` 为 6680）
- 修改部署流程时同步更新 [docker/README.md](../../docker/README.md)
