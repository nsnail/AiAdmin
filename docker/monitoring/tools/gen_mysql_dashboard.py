"""
生成 Grafana 的 MySQL 监控仪表盘 JSON：在本目录执行 python gen_mysql_dashboard.py，输出到 ../grafana/provisioning/dashboards/mysql-overview.json。
仪表盘配置了 allowUiUpdates: false，不能在界面里直接改，需要修改本脚本后重新生成并提交。
"""
import json, io

DS = {"type": "prometheus", "uid": "${DS_PROMETHEUS}"}
panels = []
_id = [0]
_y = [0]


def target(expr, legend="", ref="A"):
    return {"datasource": DS, "expr": expr, "legendFormat": legend, "refId": ref}


def add(title, targets, kind="timeseries", x=0, w=8, h=7, unit="short", desc="", thresholds=None, decimals=None, minv=None, maxv=None):
    _id[0] += 1
    fc = {"defaults": {"unit": unit, "custom": {}}, "overrides": []}
    if decimals is not None:
        fc["defaults"]["decimals"] = decimals
    if minv is not None:
        fc["defaults"]["min"] = minv
    if maxv is not None:
        fc["defaults"]["max"] = maxv
    if thresholds:
        fc["defaults"]["thresholds"] = {"mode": "absolute", "steps": thresholds}
        fc["defaults"]["color"] = {"mode": "thresholds"}
    p = {"id": _id[0], "title": title, "type": kind, "datasource": DS, "description": desc,
         "gridPos": {"h": h, "w": w, "x": x, "y": _y[0]}, "targets": targets, "fieldConfig": fc, "options": {}}
    if kind == "timeseries":
        p["options"] = {"legend": {"displayMode": "list", "placement": "bottom"}, "tooltip": {"mode": "multi", "sort": "desc"}}
        fc["defaults"]["custom"] = {"lineWidth": 1, "fillOpacity": 10, "spanNulls": True}
    else:
        p["options"] = {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}, "colorMode": "value", "graphMode": "area"}
    panels.append(p)
    return p


def row(title):
    _id[0] += 1
    _y[0] += 1
    panels.append({"id": _id[0], "type": "row", "title": title, "collapsed": False, "gridPos": {"h": 1, "w": 24, "x": 0, "y": _y[0]}, "panels": []})
    _y[0] += 1


def nextrow(h):
    _y[0] += h


I = 'instance=~"$instance"'
GREEN_RED = [{"color": "green", "value": None}, {"color": "orange", "value": 70}, {"color": "red", "value": 85}]
RED_ONLY = [{"color": "green", "value": None}, {"color": "red", "value": 1}]

# ---------- 概览 ----------
row("概览")
add("MySQL 是否在线", [target(f"mysql_up{{{I}}}")], "stat", 0, 4, 4, "none", thresholds=[{"color": "red", "value": None}, {"color": "green", "value": 1}],
    desc="1 表示 exporter 能连上 MySQL")
add("运行时长", [target(f"mysql_global_status_uptime{{{I}}}")], "stat", 4, 4, 4, "s")
add("当前连接数", [target(f"mysql_global_status_threads_connected{{{I}}}")], "stat", 8, 4, 4, "short")
add("连接使用率", [target(f"mysql_global_status_threads_connected{{{I}}} / mysql_global_variables_max_connections{{{I}}} * 100")], "stat", 12, 4, 4, "percent",
    thresholds=GREEN_RED, decimals=1, desc="当前连接数 / max_connections，超过 80% 需要关注")
add("正在执行的线程", [target(f"mysql_global_status_threads_running{{{I}}}")], "stat", 16, 4, 4, "short", thresholds=[{"color": "green", "value": None}, {"color": "orange", "value": 20}, {"color": "red", "value": 50}])
add("历史最大连接数", [target(f"mysql_global_status_max_used_connections{{{I}}}")], "stat", 20, 4, 4, "short")
nextrow(4)

# ---------- 连接 ----------
row("连接")
add("连接数", [target(f"mysql_global_status_threads_connected{{{I}}}", "已连接"), target(f"mysql_global_status_threads_running{{{I}}}", "运行中", "B"),
               target(f"mysql_global_variables_max_connections{{{I}}}", "上限 max_connections", "C")], x=0, w=12, h=8)
add("连接建立与失败", [target(f"rate(mysql_global_status_connections{{{I}}}[$__rate_interval])", "新建连接/秒"),
                       target(f"rate(mysql_global_status_aborted_connects{{{I}}}[$__rate_interval])", "握手失败/秒", "B"),
                       target(f"rate(mysql_global_status_aborted_clients{{{I}}}[$__rate_interval])", "客户端异常断开/秒", "C"),
                       target(f"rate(mysql_global_status_connection_errors_total{{{I}}}[$__rate_interval])", "连接错误/秒 {{error}}", "D")], x=12, w=12, h=8,
    desc="握手失败持续增长通常意味着密码错误、网络问题或连接数已满")
nextrow(8)

# ---------- 吞吐 ----------
row("吞吐与慢查询")
add("QPS / TPS", [target(f"rate(mysql_global_status_questions{{{I}}}[$__rate_interval])", "QPS"),
                  target(f"rate(mysql_global_status_commands_total{{command=\"commit\",{I}}}[$__rate_interval])", "提交/秒", "B"),
                  target(f"rate(mysql_global_status_commands_total{{command=\"rollback\",{I}}}[$__rate_interval])", "回滚/秒", "C")], x=0, w=8, h=8)
add("各类语句每秒数", [target(f"sum by (command) (rate(mysql_global_status_commands_total{{command=~\"select|insert|update|delete|replace\",{I}}}[$__rate_interval]))", "{{command}}")],
    x=8, w=8, h=8)
add("慢查询", [target(f"rate(mysql_global_status_slow_queries{{{I}}}[$__rate_interval])", "慢查询/秒")], x=16, w=8, h=8,
    desc="超过 long_query_time 的语句数；若始终为 0，请确认 MySQL 已开启慢查询日志")
nextrow(8)

# ---------- InnoDB ----------
row("InnoDB 与锁")
add("行锁等待", [target(f"mysql_global_status_innodb_row_lock_current_waits{{{I}}}", "当前等待行锁的事务"),
                 target(f"rate(mysql_global_status_innodb_row_lock_waits{{{I}}}[$__rate_interval])", "行锁等待/秒", "B")], x=0, w=8, h=8,
    desc="持续大于 0 说明有事务在互相等锁，这类问题正是之前连接池耗尽的原因之一")
add("行锁等待耗时", [target(f"rate(mysql_global_status_innodb_row_lock_time{{{I}}}[$__rate_interval])", "等待毫秒/秒"),
                     target(f"mysql_global_status_innodb_row_lock_time_max{{{I}}}", "单次最长等待(ms)", "B")], x=8, w=8, h=8, unit="ms")
add("缓冲池命中率", [target(f"(1 - rate(mysql_global_status_innodb_buffer_pool_reads{{{I}}}[$__rate_interval]) / rate(mysql_global_status_innodb_buffer_pool_read_requests{{{I}}}[$__rate_interval])) * 100", "命中率")],
    x=16, w=8, h=8, unit="percent", minv=0, maxv=100, desc="低于 99% 说明缓冲池偏小，大量读要落到磁盘")
nextrow(8)
add("缓冲池页面", [target(f"mysql_global_status_buffer_pool_pages{{{I}}}", "{{state}}"), target(f"mysql_global_status_buffer_pool_dirty_pages{{{I}}}", "脏页", "B")],
    x=0, w=8, h=8, desc="state=data 为已用的数据页，free 为空闲页，misc 为其他；空闲页接近 0 说明缓冲池已用满")
add("InnoDB 行操作", [target(f"sum by (operation) (rate(mysql_global_status_innodb_row_ops_total{{{I}}}[$__rate_interval]))", "{{operation}}")], x=8, w=8, h=8)
add("临时表与全表扫描", [target(f"rate(mysql_global_status_created_tmp_disk_tables{{{I}}}[$__rate_interval])", "磁盘临时表/秒"),
                          target(f"rate(mysql_global_status_select_full_join{{{I}}}[$__rate_interval])", "无索引联表/秒", "B"),
                          target(f"rate(mysql_global_status_select_scan{{{I}}}[$__rate_interval])", "全表扫描/秒", "C")], x=16, w=8, h=8)
nextrow(8)

# ---------- 网络与资源 ----------
row("网络与资源")
add("网络流量", [target(f"rate(mysql_global_status_bytes_received{{{I}}}[$__rate_interval])", "接收"), target(f"rate(mysql_global_status_bytes_sent{{{I}}}[$__rate_interval])", "发送", "B")],
    x=0, w=8, h=8, unit="Bps")
add("打开的表与文件", [target(f"mysql_global_status_open_tables{{{I}}}", "打开的表"), target(f"mysql_global_status_open_files{{{I}}}", "打开的文件", "B")], x=8, w=8, h=8)
add("表缓存未命中", [target(f"rate(mysql_global_status_opened_tables{{{I}}}[$__rate_interval])", "新打开的表/秒")], x=16, w=8, h=8)
nextrow(8)

# ---------- 业务表 ----------
row("业务表容量")
add("业务表大小", [target('topk(10, mysql_info_schema_table_size{schema="aiadmin",component="data_length"})', "{{table}} 数据"),
                    target('topk(10, mysql_info_schema_table_size{schema="aiadmin",component="index_length"})', "{{table}} 索引", "B")],
    x=0, w=12, h=9, unit="bytes", desc="按表统计的数据与索引大小，取前 10；表统计采集每 15 秒一次，取自 information_schema")
add("业务表估算行数", [target('topk(10, mysql_info_schema_table_rows{schema="aiadmin"})', "{{table}}")], x=12, w=12, h=9,
    desc="来自 information_schema 的估算值，InnoDB 不会实时刷新，只看量级与趋势")
nextrow(9)

dash = {
    "uid": "mysql-overview", "title": "MySQL 监控", "tags": ["mysql", "aiadmin"], "timezone": "browser", "schemaVersion": 39, "version": 1,
    "refresh": "30s", "time": {"from": "now-6h", "to": "now"}, "editable": True,
    "templating": {"list": [
        {"name": "DS_PROMETHEUS", "label": "数据源", "type": "datasource", "query": "prometheus", "current": {}, "hide": 0},
        {"name": "instance", "label": "实例", "type": "query", "datasource": DS, "query": {"query": "label_values(mysql_up, instance)", "refId": "q"},
         "includeAll": True, "multi": True, "current": {"text": "All", "value": "$__all"}, "refresh": 2, "sort": 1},
    ]},
    "panels": panels,
}
io.open("../grafana/provisioning/dashboards/mysql-overview.json", "w", encoding="utf-8", newline="\n").write(json.dumps(dash, ensure_ascii=False, indent=1))
print("panels:", sum(1 for p in panels if p["type"] != "row"), "rows:", sum(1 for p in panels if p["type"] == "row"))