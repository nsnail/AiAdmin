using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     告警监控列表项
/// </summary>
/// <param name="Id">监控主键</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="Name">监控名称</param>
/// <param name="Source">数据来源</param>
/// <param name="CategoryCode">字典目录编码</param>
/// <param name="Target">字典标签或 Redis 缓存键</param>
/// <param name="Metric">比较的指标，值本身或长度</param>
/// <param name="ValueType">比较类型</param>
/// <param name="Operator">比较操作符</param>
/// <param name="Threshold">阈值</param>
/// <param name="CaseSensitive">字符串比较是否区分大小写</param>
/// <param name="Mentions">报警时提醒的 Telegram 用户名</param>
/// <param name="Remark">备注</param>
/// <param name="IsEnabled">是否启用</param>
/// <param name="AlertActive">当前是否处于报警状态</param>
/// <param name="AlertedAt">最近一次进入报警状态的时间</param>
/// <param name="LastValue">最近一次扫描读取到的值</param>
/// <param name="LastMessage">最近一次扫描的状态说明</param>
/// <param name="LastCheckedAt">最近一次扫描时间</param>
/// <param name="UpdatedAt">最后更新时间</param>
public sealed record AlertMonitorListItem(
    long Id
    , DateTimeOffset CreatedAt
    , string Name
    , AlertMonitorSource Source
    , string CategoryCode
    , string Target
    , AlertMonitorMetric Metric
    , AlertMonitorValueType ValueType
    , AlertMonitorOperator Operator
    , string Threshold
    , bool CaseSensitive
    , string Mentions
    , string Remark
    , bool IsEnabled
    , bool AlertActive
    , DateTimeOffset? AlertedAt
    , string LastValue
    , string LastMessage
    , DateTimeOffset? LastCheckedAt
    , DateTimeOffset? UpdatedAt);