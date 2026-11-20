#!/usr/bin/env bash
# 部署机上的滚动发布步骤，由工作流按实例编排：网关摘除节点（gate-upstream.sh down）-> roll -> 网关恢复节点（gate-upstream.sh up）。
# 子命令：
#   pull             拉取镜像
#   roll <service>   排空并重建单个实例（api-a / api-b），等待 /health 通过，再用 /ready 预热连接池；api-a 重建前会移除单实例时期遗留的 api 容器
#   settle <service> 实例放回网关后观察它在真实流量下是否稳定，稳定才返回成功；工作流在“放回网关”之后、摘除另一个实例之前调用
#   finish           启动 web 并清理旧镜像（API_IMAGE、WEB_IMAGE 同仓库下除当前版本外的旧标签，以及悬空镜像）
# 依赖环境变量：API_IMAGE、WEB_IMAGE（compose 使用）；可选 SHUTDOWN_TOKEN（未设置则跳过排空，仅依赖 stop_grace_period）
# 可选调参：HEALTH_TIMEOUT、PREPARE_TIMEOUT，以及预热和稳定观察的 WARMUP_*、SETTLE_*（见下方默认值）；SETTLE_STRICT=1 时稳定观察超时会中断发布
set -euo pipefail

cd "$(dirname "$0")"
COMPOSE="docker compose -f docker-compose.prod.yml"
PROJECT="$(basename "$PWD")"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-120}"
PREPARE_TIMEOUT="${PREPARE_TIMEOUT:-30}"

# 预热：/ready 会真正访问数据库和 Redis；要求连续 WARMUP_PASSES 次都在 READY_MAX_MS 毫秒内返回 200，用来把新进程的连接池拉起来，期间实例还没放回网关
WARMUP_PASSES="${WARMUP_PASSES:-5}"
WARMUP_TIMEOUT="${WARMUP_TIMEOUT:-60}"
# 稳定观察：实例放回网关后每 2 秒探测一次，要求连续 SETTLE_PASSES 次都快于 READY_MAX_MS 才算稳定；任一次变慢或失败则重新计数
SETTLE_PASSES="${SETTLE_PASSES:-15}"
SETTLE_TIMEOUT="${SETTLE_TIMEOUT:-120}"
# 稳定观察超时后的处理：默认只告警并继续发布（避免一次偶发的慢查询卡住整个发版）；设为 1 则失败并中断，被摘除的实例保持 down，另一个实例继续对外服务，由人工介入
SETTLE_STRICT="${SETTLE_STRICT:-0}"
READY_MAX_MS="${READY_MAX_MS:-500}"
PROBE_INTERVAL="${PROBE_INTERVAL:-2}"

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

# 探测一次 /ready：返回 200 且总耗时不超过 READY_MAX_MS 毫秒才算通过（同时校验往返耗时，避免“能返回但已很慢”被当成正常）
probe_ready() {
  local port="$1" out code ms
  out=$(curl -s -o /dev/null -w '%{http_code} %{time_total}' --max-time 5 "http://127.0.0.1:${port}/ready" || echo "000 9.999")
  code="${out%% *}"
  ms=$(awk -v t="${out##* }" 'BEGIN { printf "%d", t * 1000 }')
  PROBE_CODE="$code"
  PROBE_MS="$ms"
  [ "$code" = "200" ] && [ "$ms" -le "$READY_MAX_MS" ]
}

# 连续 passes 次探测通过才返回成功；任一次未通过就从头计数；超过 timeout 秒仍未达成则失败
# 超时按真实时钟（SECONDS）计算，不依赖探测间隔，避免间隔被误设为 0 或探测本身很慢时超时保护失效
wait_stable() {
  local label="$1" port="$2" passes="$3" timeout="$4" ok=0 started=$SECONDS
  while [ "$ok" -lt "$passes" ]; do
    if probe_ready "$port"; then
      ok=$((ok + 1))
    else
      echo "${label}: not ready (HTTP ${PROBE_CODE}, ${PROBE_MS}ms), restart counting"
      ok=0
    fi
    if [ "$ok" -lt "$passes" ]; then
      if [ $((SECONDS - started)) -ge "$timeout" ]; then
        echo "ERROR: ${label} did not stay under ${READY_MAX_MS}ms for ${passes} consecutive probes within ${timeout}s" >&2
        return 1
      fi
      sleep "$PROBE_INTERVAL"
    fi
  done
  echo "${label}: ${passes} consecutive probes under ${READY_MAX_MS}ms (last ${PROBE_MS}ms)"
}

# 实例 /health 通过后预热连接池，此时还没放回网关，失败也不影响线上流量
warmup() {
  local service="$1" port="$2"
  echo "==> Warming up ${service}"
  wait_stable "${service} warmup" "$port" "$WARMUP_PASSES" "$WARMUP_TIMEOUT"
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
  warmup "$service" "$port"
}

# 实例放回网关后观察稳定性：由工作流在“放回网关”步骤之后调用，失败即中断发布，另一个实例继续保持在线
settle() {
  local service="${1:?usage: rolling-deploy.sh settle <api-a|api-b>}"
  local port="${PORTS[$service]:-}"
  [ -n "$port" ] || { echo "ERROR: unknown service ${service}" >&2; exit 1; }
  echo "==> Settling ${service} under real traffic"
  if wait_stable "${service} settle" "$port" "$SETTLE_PASSES" "$SETTLE_TIMEOUT"; then
    return 0
  fi
  if [ "$SETTLE_STRICT" = "1" ]; then
    echo "ERROR: ${service} is not stable, abort before taking the other instance out (SETTLE_STRICT=1)" >&2
    return 1
  fi
  # 非严格模式：只告警并继续。此时另一个实例即将被摘除，${service} 将独自承担全部流量，请在发版后检查连接池和慢日志
  echo "WARNING: ${service} did not settle within ${SETTLE_TIMEOUT}s, continuing the rollout anyway; check the MySQL slow log (/var/log/mysql/mysql-slow.log) and the connection pool after the release" >&2
  return 0
}

# 清理旧镜像：删除 API_IMAGE、WEB_IMAGE 同仓库下除当前版本外的所有旧版本标签，再清理悬空镜像
# 不使用 --force：仍被容器使用的镜像会删除失败并被跳过，清理失败不影响发布结果
cleanup_images() {
  local current repo ref
  for current in "${API_IMAGE:-}" "${WEB_IMAGE:-}"; do
    [ -n "$current" ] || continue
    # 仅当最后一个 / 之后含 : 时才去掉标签部分（避免把 registry:port 误当标签）
    repo="$current"
    case "${current##*/}" in
      *:*) repo="${current%:*}" ;;
    esac
    while read -r ref; do
      [ -n "$ref" ] && [ "$ref" != "$current" ] || continue
      echo "Removing old image ${ref}"
      docker rmi "$ref" || echo "WARNING: failed to remove ${ref}, skipped" >&2
    done < <(docker images --format '{{.Repository}}:{{.Tag}}' "$repo" | grep -v ':<none>$' || true)
  done
  docker image prune --force || true
}

case "${1:-}" in
  pull)
    $COMPOSE pull
    ;;
  roll)
    roll "${2:-}"
    ;;
  settle)
    settle "${2:-}"
    ;;
  finish)
    $COMPOSE up -d web
    cleanup_images
    ;;
  *)
    echo "usage: rolling-deploy.sh pull | roll <api-a|api-b> | settle <api-a|api-b> | finish" >&2
    exit 1
    ;;
esac