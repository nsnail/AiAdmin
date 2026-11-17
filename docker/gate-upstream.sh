#!/usr/bin/env bash
# 在网关服务器上摘除或恢复 ai_admin_api 的某个节点：通过给 server 行增加或移除 down 标记实现，随后校验并平滑重载 nginx。
# 用法：gate-upstream.sh down|up <port>
# 保护：摘除后若没有剩余可用节点则拒绝；nginx -t 失败则还原文件；无变化时不重载。
set -euo pipefail

FILE="${UPSTREAM_FILE:-/etc/nginx/upstream/ai_admin_api.conf}"
LOCK_FILE="${LOCK_FILE:-/var/lock/ai-admin-gate.lock}"
action="${1:?usage: gate-upstream.sh down|up <port>}"
port="${2:?usage: gate-upstream.sh down|up <port>}"

# 串行化，避免并发修改同一个文件
if command -v flock >/dev/null 2>&1; then
  exec 9>"$LOCK_FILE"
  flock -w 60 9
fi

[ -f "$FILE" ] || { echo "ERROR: ${FILE} not found" >&2; exit 1; }
grep -qE "^[[:space:]]*server [^ ]+:${port}[ ;]" "$FILE" || { echo "ERROR: no node with port ${port} in ${FILE}" >&2; exit 1; }

backup="$(mktemp)"
trap 'rm -f "$backup"' EXIT
cp -p "$FILE" "$backup"

case "$action" in
  down)
    sed -i -E "/^[[:space:]]*server [^ ]+:${port}[ ;]/{/ down/!s/;[[:space:]]*\$/ down;/}" "$FILE"
    alive="$(grep -E '^[[:space:]]*server ' "$FILE" | grep -vc ' down' || true)"
    if [ "${alive:-0}" -lt 1 ]; then
      cp -p "$backup" "$FILE"
      echo "ERROR: refuse to take down port ${port}: no remaining node would be available" >&2
      exit 1
    fi
    ;;
  up)
    sed -i -E "/^[[:space:]]*server [^ ]+:${port}[ ;]/s/ down;/;/" "$FILE"
    ;;
  *)
    echo "ERROR: unknown action ${action}" >&2
    exit 1
    ;;
esac

if cmp -s "$backup" "$FILE"; then
  echo "Node ${port} already ${action}, nothing to do"
  exit 0
fi

if ! nginx -t; then
  cp -p "$backup" "$FILE"
  echo "ERROR: nginx -t failed, ${FILE} restored" >&2
  exit 1
fi

if ! nginx -s reload; then
  cp -p "$backup" "$FILE"
  echo "ERROR: nginx reload failed, ${FILE} restored; check that /run/nginx.pid matches the running nginx master process" >&2
  exit 1
fi
echo "Node ${port} is now ${action}:"
grep -E '^[[:space:]]*server ' "$FILE"