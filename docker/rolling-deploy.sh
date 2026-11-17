#!/usr/bin/env bash
# 部署机上的滚动发布步骤，由工作流按实例编排：网关摘除节点（gate-upstream.sh down）-> roll -> 网关恢复节点（gate-upstream.sh up）。
# 子命令：
#   pull           拉取镜像
#   roll <service> 排空并重建单个实例（api-a / api-b），等待 /health 通过；api-a 重建前会移除单实例时期遗留的 api 容器
#   finish         启动 web 并清理无用镜像
# 依赖环境变量：API_IMAGE、WEB_IMAGE（compose 使用）；可选 SHUTDOWN_TOKEN（未设置则跳过排空，仅依赖 stop_grace_period）
set -euo pipefail

cd "$(dirname "$0")"
COMPOSE="docker compose -f docker-compose.prod.yml"
PROJECT="$(basename "$PWD")"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-120}"
PREPARE_TIMEOUT="${PREPARE_TIMEOUT:-30}"

declare -A PORTS=([api-a]=5678 [api-b]=5680)

# 等待实例的 /health 返回 200
wait_healthy() {
  local service="$1" port="$2" waited=0
  until [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://127.0.0.1:${port}/health" || true)" = "200" ]; do
    waited=$((waited + 2))
    if [ "$waited" -ge "$HEALTH_TIMEOUT" ]; then
      echo "ERROR: ${service} did not become healthy within ${HEALTH_TIMEOUT}s" >&2
      $COMPOSE logs --tail 80 "$service" >&2 || true
      return 1
    fi
    sleep 2
  done
  echo "${service} is healthy"
}

# 关闸并等待在途工作归零且保持稳定；此时网关已摘除该节点，排空通常很快。失败或超时只告警，仍继续重建，由 stop_grace_period 兜底
drain() {
  local service="$1" port="$2" status
  if [ -z "${SHUTDOWN_TOKEN:-}" ]; then
    echo "SHUTDOWN_TOKEN is not set, skip draining ${service}"
    return 0
  fi
  if ! $COMPOSE ps --status running --services | grep -qx "$service"; then
    echo "${service} is not running, skip draining"
    return 0
  fi
  status=$(curl -sS -o /tmp/shutdown-prepare.json -w '%{http_code}' --max-time $((PREPARE_TIMEOUT + 10)) -X POST \
    "http://127.0.0.1:${port}/api/system/shutdown/prepare" \
    -H "Authorization: Bearer ${SHUTDOWN_TOKEN}" -H 'Content-Type: application/json' \
    -d "{\"stableSeconds\":2,\"timeoutSeconds\":${PREPARE_TIMEOUT}}" || true)
  echo "Drain ${service}: HTTP ${status}"
  cat /tmp/shutdown-prepare.json 2>/dev/null || true
  echo
}

# 移除单实例时期遗留的 api 容器；它占用 5678，必须在 api-a 启动前停掉，调用时网关应已摘除 5678
remove_legacy() {
  local legacy
  legacy=$(docker ps -aq --filter "label=com.docker.compose.project=${PROJECT}" --filter "label=com.docker.compose.service=api")
  if [ -n "$legacy" ]; then
    echo "==> Legacy single-instance api container found, removing"
    docker stop --time 50 $legacy
    docker rm $legacy
  fi
}

roll() {
  local service="${1:?usage: rolling-deploy.sh roll <api-a|api-b>}"
  local port="${PORTS[$service]:-}"
  [ -n "$port" ] || { echo "ERROR: unknown service ${service}" >&2; exit 1; }
  echo "==> Rolling ${service}"
  drain "$service" "$port"
  if [ "$service" = "api-a" ]; then
    remove_legacy
  fi
  # 强制重建：排空会关闭进程内闸门，镜像未变化时普通 up 不会重启容器，闸门将一直保持关闭
  $COMPOSE up -d --no-deps --force-recreate "$service"
  wait_healthy "$service" "$port"
}

case "${1:-}" in
  pull)
    $COMPOSE pull
    ;;
  roll)
    roll "${2:-}"
    ;;
  finish)
    $COMPOSE up -d web
    docker image prune --force
    ;;
  *)
    echo "usage: rolling-deploy.sh pull | roll <api-a|api-b> | finish" >&2
    exit 1
    ;;
esac