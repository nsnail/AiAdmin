"""
生成 Grafana 的服务器性能仪表盘 JSON：在本目录执行 python gen_node_dashboard.py，输出到 ../grafana/provisioning/dashboards/node-overview.json。
数据来自两台服务器（web、db）上的 node_exporter，由 prometheus.yml 的 node 任务抓取，用 $instance 变量在两台机器之间切换或叠加。
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
        p["options"] = {"reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}, "colorMode": "value", "graphMode": "area",
                        "textMode": "value_and_name"}
    panels.append(p)
    return p


def row(title):
    _id[0] += 1
    _y[0] += 1
    panels.append({"id": _id[0], "type": "row", "title": title, "collapsed": False, "gridPos": {"h": 1, "w": 24, "x": 0, "y": _y[0]}, "panels": []})
    _y[0] += 1


def nextrow(h):
    _y[0] += h


# 所有查询都限定在 node 任务和所选实例内；磁盘与网卡排除容器虚拟设备和临时文件系统，避免曲线被 docker 网桥淹没
I = 'job="node",instance=~"$instance"'
FS = f'{I},fstype!~"tmpfs|overlay|squashfs|vfat|fuse.*",mountpoint!~"/var/lib/docker/.*|/run/.*"'
NIC = f'{I},device!~"lo|docker.*|br-.*|veth.*"'
DISK = f'{I},device=~"nvme.*|sd.*|vd.*|xvd.*"'
LEGEND = "{{role}} {{instance}}"
GREEN_RED = [{"color": "green", "value": None}, {"color": "orange", "value": 70}, {"color": "red", "value": 85}]
GREEN_RED_LOW = [{"color": "green", "value": None}, {"color": "orange", "value": 80}, {"color": "red", "value": 90}]

CPU_BUSY = f'(1 - avg by (instance, role) (rate(node_cpu_seconds_total{{{I},mode="idle"}}[$__rate_interval]))) * 100'
MEM_USED = f'(1 - node_memory_MemAvailable_bytes{{{I}}} / node_memory_MemTotal_bytes{{{I}}}) * 100'
DISK_USED = f'(1 - node_filesystem_avail_bytes{{{FS}}} / node_filesystem_size_bytes{{{FS}}}) * 100'

# ---------- 概览 ----------
row("概览")
add("CPU 使用率", [target(CPU_BUSY, LEGEND)], "stat", 0, 4, 4, "percent", thresholds=GREEN_RED, decimals=1, minv=0, maxv=100,
    desc="除空闲外的 CPU 时间占比，所有核心平均")
add("内存使用率", [target(MEM_USED, LEGEND)], "stat", 4, 4, 4, "percent", thresholds=GREEN_RED, decimals=1, minv=0, maxv=100,
    desc="按可用内存（MemAvailable）计算，已把可回收的页缓存算作可用，比 free 命令的 used 更接近真实压力")
add("根分区使用率", [target(DISK_USED, LEGEND)], "stat", 8, 4, 4, "percent", thresholds=GREEN_RED_LOW, decimals=1, minv=0, maxv=100)
add("1 分钟负载", [target(f"node_load1{{{I}}}", LEGEND)], "stat", 12, 4, 4, "short", decimals=2,
    desc="可运行加不可中断进程的平均数，长期高于 CPU 核数说明排队")
add("运行时长", [target(f"time() - node_boot_time_seconds{{{I}}}", LEGEND)], "stat", 16, 4, 4, "s")
add("CPU 核数", [target(f'count by (instance, role) (node_cpu_seconds_total{{{I},mode="idle"}})', LEGEND)], "stat", 20, 4, 4, "short")
nextrow(4)

# ---------- CPU ----------
row("CPU")
add("CPU 使用率", [target(CPU_BUSY, LEGEND)], x=0, w=8, h=8, unit="percent", minv=0, maxv=100)
add("CPU 时间分布", [target(f'avg by (mode, instance, role) (rate(node_cpu_seconds_total{{{I},mode!="idle"}}[$__rate_interval])) * 100', "{{role}} {{mode}}")],
    x=8, w=8, h=8, unit="percent",
    desc="iowait 偏高说明在等磁盘，steal 偏高说明云主机被宿主机抢占，system 偏高多为网络或系统调用开销")
add("系统负载", [target(f"node_load1{{{I}}}", "{{role}} 1 分钟"), target(f"node_load5{{{I}}}", "{{role}} 5 分钟", "B"),
                 target(f"node_load15{{{I}}}", "{{role}} 15 分钟", "C")], x=16, w=8, h=8, decimals=2)
nextrow(8)

# ---------- 内存 ----------
row("内存")
add("内存使用率", [target(MEM_USED, LEGEND)], x=0, w=8, h=8, unit="percent", minv=0, maxv=100)
add("内存构成", [target(f"node_memory_MemTotal_bytes{{{I}}} - node_memory_MemAvailable_bytes{{{I}}}", "{{role}} 已用"),
                 target(f"node_memory_Cached_bytes{{{I}}} + node_memory_Buffers_bytes{{{I}}}", "{{role}} 缓存", "B"),
                 target(f"node_memory_MemAvailable_bytes{{{I}}}", "{{role}} 可用", "C")], x=8, w=8, h=8, unit="bytes",
    desc="已用 = 总量 - 可用；缓存可被内核回收，已包含在可用里")
add("Swap 使用与换页", [target(f"node_memory_SwapTotal_bytes{{{I}}} - node_memory_SwapFree_bytes{{{I}}}", "{{role}} Swap 已用", "A"),
                        target(f"rate(node_vmstat_pswpin{{{I}}}[$__rate_interval]) * 4096", "{{role}} 换入/秒", "B"),
                        target(f"rate(node_vmstat_pswpout{{{I}}}[$__rate_interval]) * 4096", "{{role}} 换出/秒", "C")], x=16, w=8, h=8, unit="bytes",
    desc="换入换出持续不为 0 说明物理内存不足，性能会明显下降")
nextrow(8)

# ---------- 磁盘 ----------
row("磁盘")
add("磁盘空间使用率", [target(DISK_USED, "{{role}} {{mountpoint}}")], x=0, w=8, h=8, unit="percent", minv=0, maxv=100)
add("磁盘读写吞吐", [target(f"rate(node_disk_read_bytes_total{{{DISK}}}[$__rate_interval])", "{{role}} 读"),
                     target(f"rate(node_disk_written_bytes_total{{{DISK}}}[$__rate_interval])", "{{role}} 写", "B")], x=8, w=8, h=8, unit="Bps")
add("磁盘 IOPS", [target(f"rate(node_disk_reads_completed_total{{{DISK}}}[$__rate_interval])", "{{role}} 读"),
                  target(f"rate(node_disk_writes_completed_total{{{DISK}}}[$__rate_interval])", "{{role}} 写", "B")], x=16, w=8, h=8, unit="iops")
nextrow(8)
add("磁盘繁忙度", [target(f"rate(node_disk_io_time_seconds_total{{{DISK}}}[$__rate_interval]) * 100", LEGEND)], x=0, w=8, h=8, unit="percent", minv=0, maxv=100,
    desc="磁盘有 IO 在处理的时间占比，持续接近 100% 说明磁盘已饱和")
add("磁盘平均延迟", [target(f"rate(node_disk_read_time_seconds_total{{{DISK}}}[$__rate_interval]) / rate(node_disk_reads_completed_total{{{DISK}}}[$__rate_interval])",
                            "{{role}} 读"),
                     target(f"rate(node_disk_write_time_seconds_total{{{DISK}}}[$__rate_interval]) / rate(node_disk_writes_completed_total{{{DISK}}}[$__rate_interval])",
                            "{{role}} 写", "B")], x=8, w=8, h=8, unit="s")
add("inode 使用率", [target(f"(1 - node_filesystem_files_free{{{FS}}} / node_filesystem_files{{{FS}}}) * 100", "{{role}} {{mountpoint}}")], x=16, w=8, h=8,
    unit="percent", minv=0, maxv=100)
nextrow(8)

# ---------- 网络 ----------
row("网络")
add("网络流量", [target(f"sum by (instance, role) (rate(node_network_receive_bytes_total{{{NIC}}}[$__rate_interval]))", "{{role}} 接收"),
                 target(f"sum by (instance, role) (rate(node_network_transmit_bytes_total{{{NIC}}}[$__rate_interval]))", "{{role}} 发送", "B")],
    x=0, w=8, h=8, unit="Bps", desc="只统计物理网卡，不含回环和 docker 虚拟网桥")
add("TCP 连接", [target(f"node_netstat_Tcp_CurrEstab{{{I}}}", "{{role}} 已建立"), target(f"node_sockstat_TCP_tw{{{I}}}", "{{role}} TIME_WAIT", "B")],
    x=8, w=8, h=8)
add("网络丢包与错误", [target(f"sum by (instance, role) (rate(node_network_receive_drop_total{{{NIC}}}[$__rate_interval]))", "{{role}} 接收丢包/秒"),
                       target(f"sum by (instance, role) (rate(node_network_receive_errs_total{{{NIC}}}[$__rate_interval]))", "{{role}} 接收错误/秒", "B"),
                       target(f"rate(node_netstat_Tcp_RetransSegs{{{I}}}[$__rate_interval])", "{{role}} TCP 重传/秒", "C")], x=16, w=8, h=8)
nextrow(8)

# ---------- 系统 ----------
row("系统")
add("文件句柄", [target(f"node_filefd_allocated{{{I}}} / node_filefd_maximum{{{I}}} * 100", LEGEND)], x=0, w=8, h=7, unit="percent", minv=0, maxv=100,
    desc="已分配句柄占系统上限的比例，接近 100% 时进程会报 too many open files")
add("上下文切换与中断", [target(f"rate(node_context_switches_total{{{I}}}[$__rate_interval])", "{{role}} 上下文切换/秒"),
                         target(f"rate(node_intr_total{{{I}}}[$__rate_interval])", "{{role}} 中断/秒", "B")], x=8, w=8, h=7)
add("进程数", [target(f"node_procs_running{{{I}}}", "{{role}} 运行中"), target(f"node_procs_blocked{{{I}}}", "{{role}} 阻塞", "B")], x=16, w=8, h=7)
nextrow(7)

dash = {
    "uid": "node-overview", "title": "服务器性能监控", "tags": ["node", "aiadmin"], "timezone": "browser", "schemaVersion": 39, "version": 1,
    "refresh": "30s", "time": {"from": "now-6h", "to": "now"}, "editable": True,
    "templating": {"list": [
        {"name": "DS_PROMETHEUS", "label": "数据源", "type": "datasource", "query": "prometheus", "current": {}, "hide": 0},
        {"name": "instance", "label": "服务器", "type": "query", "datasource": DS,
         "query": {"query": 'label_values(node_uname_info{job="node"}, instance)', "refId": "q"},
         "includeAll": True, "multi": True, "current": {"text": "All", "value": "$__all"}, "refresh": 2, "sort": 1},
    ]},
    "panels": panels,
}
io.open("../grafana/provisioning/dashboards/node-overview.json", "w", encoding="utf-8", newline="\n").write(json.dumps(dash, ensure_ascii=False, indent=1))
print("panels:", sum(1 for p in panels if p["type"] != "row"), "rows:", sum(1 for p in panels if p["type"] == "row"))