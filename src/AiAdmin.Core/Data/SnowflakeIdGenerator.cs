namespace AiAdmin.Api.Data;

/// <summary>
///     生成按时间递增且跨实例唯一的 Snowflake 主键
/// </summary>
public static class SnowflakeIdGenerator
{
    private const long _CUSTOM_EPOCH_MILLISECONDS = 1735689600000L;
    private const int _SEQUENCE_BITS = 8;
    private const long _SEQUENCE_MASK = (1L << _SEQUENCE_BITS) - 1;
    private const int _TIMESTAMP_BITS = 39;
    private const long _TIMESTAMP_MASK = (1L << _TIMESTAMP_BITS) - 1;
    private const int _WORKER_BITS = 5;
    private const long _WORKER_MASK = (1L << _WORKER_BITS) - 1;
    private static readonly Lock _sync = new();
    private static long _lastTimestamp = -1;
    private static long _sequence;
    private static long _workerId;

    /// <summary>
    ///     设置当前实例的 Snowflake WorkerId
    /// </summary>
    /// <param name="workerId">实例编号，范围为 0 到 31</param>
    /// <exception cref="ArgumentOutOfRangeException">实例编号超出有效范围时抛出</exception>
    public static void Configure(long workerId) {
        if (workerId is < 0 or > _WORKER_MASK) {
            throw new ArgumentOutOfRangeException(nameof(workerId), workerId, "Snowflake WorkerId must be between 0 and 31.");
        }

        lock (_sync) {
            _workerId = workerId;
        }
    }

    /// <summary>
    ///     生成下一个 Snowflake 主键
    /// </summary>
    /// <returns>正数 Snowflake 主键</returns>
    /// <exception cref="InvalidOperationException">当前时间超出 Snowflake 时间戳有效范围时抛出</exception>
    public static long Next() {
        lock (_sync) {
            var timestamp = CurrentTimestamp();
            if (timestamp is < 0 or > _TIMESTAMP_MASK) {
                throw new InvalidOperationException("Current time is outside the Snowflake timestamp range.");
            }

            if (timestamp < _lastTimestamp) {
                timestamp = WaitUntil(_lastTimestamp);
            }

            if (timestamp == _lastTimestamp) {
                _sequence = (_sequence + 1) & _SEQUENCE_MASK;
                if (_sequence == 0) {
                    timestamp = WaitUntil(_lastTimestamp + 1);
                }
            }
            else {
                _sequence = 0;
            }

            if (timestamp > _TIMESTAMP_MASK) {
                throw new InvalidOperationException("Current time is outside the Snowflake timestamp range.");
            }

            _lastTimestamp = timestamp;
            return (timestamp << (_WORKER_BITS + _SEQUENCE_BITS)) | (_workerId << _SEQUENCE_BITS) | _sequence;
        }
    }

    /// <summary>
    ///     获取相对于自定义纪元的当前毫秒数
    /// </summary>
    /// <returns>相对毫秒时间戳</returns>
    private static long CurrentTimestamp() {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _CUSTOM_EPOCH_MILLISECONDS;
    }

    /// <summary>
    ///     等待系统时钟达到指定相对毫秒时间戳
    /// </summary>
    /// <param name="timestamp">目标相对毫秒时间戳</param>
    /// <returns>等待后的相对毫秒时间戳</returns>
    private static long WaitUntil(long timestamp) {
        var current = CurrentTimestamp();
        while (current < timestamp) {
            Thread.SpinWait(32);
            current = CurrentTimestamp();
        }

        return current;
    }
}