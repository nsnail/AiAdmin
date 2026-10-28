using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiAdmin.Api.Services;

/// <summary>
///     API 请求日志按天分表服务，负责按日期生成表名、建表、批量写入以及删除过期分表，支持 sqlite、sqlserver、postgresql、mysql
/// </summary>
/// <param name="provider">数据库提供方名称，已转为小写</param>
/// <param name="connectionString">数据库连接字符串</param>
public sealed class ApiRequestLogTableService(string provider, string connectionString) : IDisposable
{
    private const int _MAX_COLUMNS_PER_STATEMENT = 2000;

    /// <summary>
    ///     分表名称前缀，完整表名为前缀加 yyyyMMdd
    /// </summary>
    private const string _TABLE_PREFIX = "sys_api_request_log_";

    private static readonly string[] _columns =
    [
        nameof(ApiRequestLog.Id), nameof(ApiRequestLog.CreatedAt), nameof(ApiRequestLog.Method), nameof(ApiRequestLog.PathCrc32)
        , nameof(ApiRequestLog.StatusCode), nameof(ApiRequestLog.ApiResponseCode), nameof(ApiRequestLog.ElapsedMilliseconds)
        , nameof(ApiRequestLog.UserId), nameof(ApiRequestLog.DepartmentId), nameof(ApiRequestLog.ClientIp), nameof(ApiRequestLog.ServerIp)
        , nameof(ApiRequestLog.TraceId), nameof(ApiRequestLog.WorkerId)
    ];

    private static readonly Regex _identifier = new(
        "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)
    );

    private readonly SemaphoreSlim _createLock = new(1, 1);
    private readonly HashSet<string> _knownTables = [with(StringComparer.Ordinal)];

    /// <summary>
    ///     根据日期生成分表名称
    /// </summary>
    /// <param name="date">日志的 UTC 日期</param>
    /// <returns>分表名称</returns>
    public static string GetTableName(DateTime date) {
        return _TABLE_PREFIX + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     尝试从分表名称解析日期
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="date">解析出的日期</param>
    /// <returns>是请求日志分表且日期有效时返回 true</returns>
    public static bool TryParseTableDate(
        string tableName
        , out DateTime date
    ) {
        date = default;
        return tableName.StartsWith(_TABLE_PREFIX, StringComparison.Ordinal)
               && DateTime.TryParseExact(
                   tableName[_TABLE_PREFIX.Length..], "yyyyMMdd", CultureInfo.InvariantCulture
                   , DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date
               );
    }

    /// <summary>
    ///     使用 EF 迁移引擎生成日分表的建表语句，保证与各数据库的类型映射一致
    /// </summary>
    /// <param name="tableName">日分表名称</param>
    /// <returns>建表语句和建索引语句</returns>
    public IReadOnlyList<string> BuildCreateStatements(string tableName) {
        using var context = CreateContext(tableName);
        var script = context.GetService<IRelationalDatabaseCreator>().GenerateCreateScript();

        // 脚本里建表和建索引语句以分号结尾并用空行分隔，拆成独立语句分别执行
        return
        [
            .. script
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase))
        ];
    }

    /// <summary>
    ///     统计时间范围内各接口（路径 CRC32）的请求数：每张日表在数据库内分组，再合并相加
    /// </summary>
    /// <param name="startUtc">起始时刻，含</param>
    /// <param name="endUtc">结束时刻，不含</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>以路径 CRC32 为键的请求数</returns>
    public async Task<Dictionary<int, int>> CountByApiAsync(
        DateTime startUtc
        , DateTime endUtc
        , CancellationToken cancellationToken
    ) {
        var result = new Dictionary<int, int>();
        foreach (var name in await ListTablesInRangeAsync(startUtc.Date, endUtc.AddTicks(-1).Date, cancellationToken).ConfigureAwait(false)) {
            await using var context = OpenQueryContext(name);
            var rows = await context
                .Logs.AsNoTracking()
                .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
                .GroupBy(x => x.PathCrc32)
                .Select(g => new { Crc = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in rows) {
                result[row.Crc] = result.GetValueOrDefault(row.Crc) + row.Count;
            }
        }

        return result;
    }

    /// <summary>
    ///     统计时间范围内每个整点小时的请求数：每张日表在数据库内按小时分组，再合并相加，不把明细读入内存
    /// </summary>
    /// <param name="startUtc">起始时刻，含</param>
    /// <param name="endUtc">结束时刻，不含</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>以 UTC 整点小时为键的请求数</returns>
    public async Task<Dictionary<DateTime, int>> CountByHourAsync(
        DateTime startUtc
        , DateTime endUtc
        , CancellationToken cancellationToken
    ) {
        var result = new Dictionary<DateTime, int>();
        foreach (var name in await ListTablesInRangeAsync(startUtc.Date, endUtc.AddTicks(-1).Date, cancellationToken).ConfigureAwait(false)) {
            await using var context = OpenQueryContext(name);
            var rows = await context
                .Logs.AsNoTracking()
                .Where(x => x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
                .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month, x.CreatedAt.Day, x.CreatedAt.Hour })
                .Select(g => new
                    {
                        g.Key.Year
                        , g.Key.Month
                        , g.Key.Day
                        , g.Key.Hour
                        , Count = g.Count()
                    }
                )
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in rows) {
                var hour = new DateTime(row.Year, row.Month, row.Day, row.Hour, 0, 0, DateTimeKind.Utc);
                result[hour] = result.GetValueOrDefault(hour) + row.Count;
            }
        }

        return result;
    }

    /// <summary>
    ///     释放建表互斥锁
    /// </summary>
    public void Dispose() {
        _createLock.Dispose();
    }

    /// <summary>
    ///     删除早于截止日期的日分表
    /// </summary>
    /// <param name="cutoffDate">截止日期，早于该日期的分表被删除，该日当天的分表保留</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>被删除的分表名称</returns>
    public async Task<IReadOnlyList<string>> DropTablesBeforeAsync(
        DateTime cutoffDate
        , CancellationToken cancellationToken
    ) {
        var dropped = new List<string>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tables = await ListTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var table in tables.Where(x => TryParseTableDate(x, out var date) && date < cutoffDate.Date)) {
            await using var command = connection.CreateCommand();
            #pragma warning disable S2077 // 表名无法参数化，Quote 已用正则校验标识符
            command.CommandText = $"DROP TABLE {Quote(table)}";
            #pragma warning restore S2077
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _ = _knownTables.Remove(table);
            dropped.Add(table);
        }

        return dropped;
    }

    /// <summary>
    ///     按日志的 UTC 日期分组，确保各日分表存在后批量写入
    /// </summary>
    /// <param name="logs">待写入的日志</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>写入的日志数量</returns>
    public async Task<int> InsertAsync(
        IReadOnlyCollection<ApiRequestLog> logs
        , CancellationToken cancellationToken
    ) {
        var count = 0;
        foreach (var group in logs.GroupBy(x => x.CreatedAt.Date)) {
            var tableName = GetTableName(group.Key);
            await EnsureTableAsync(tableName, cancellationToken).ConfigureAwait(false);
            count += await InsertIntoTableAsync(tableName, [.. group], cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    /// <summary>
    ///     列出日期范围内实际存在的日分表，按日期倒序，范围按 UTC 日期闭区间计算
    /// </summary>
    /// <param name="from">起始日期，含当天</param>
    /// <param name="to">结束日期，含当天</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>存在的日分表名称</returns>
    public async Task<IReadOnlyList<string>> ListTablesInRangeAsync(
        DateTime from
        , DateTime to
        , CancellationToken cancellationToken
    ) {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tables = await ListTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        return
        [
            .. tables
                .Select(table => (Table: table, Valid: TryParseTableDate(table, out var date), Date: date))
                .Where(item => item.Valid && item.Date >= from.Date && item.Date <= to.Date)
                .OrderByDescending(item => item.Date)
                .Select(item => item.Table)
        ];
    }

    /// <summary>
    ///     创建绑定指定日分表的只读查询上下文，调用方负责释放，查询结果可直接复用动态筛选与排序
    /// </summary>
    /// <param name="tableName">日分表名称，必须是请求日志日分表</param>
    /// <returns>日分表查询上下文</returns>
    /// <exception cref="ArgumentException">表名不是请求日志日分表时引发</exception>
    public ApiRequestLogContext OpenQueryContext(string tableName) {
        return TryParseTableDate(tableName, out _)
            ? CreateContext(tableName)
            : throw new ArgumentException("Invalid request log table name.", nameof(tableName));
    }

    /// <summary>
    ///     读取日志指定列的值
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="column">列名</param>
    /// <returns>列值，为空时返回 null</returns>
    private static object? GetValue(
        ApiRequestLog log
        , string column
    ) {
        var value = typeof(ApiRequestLog).GetProperty(column)!.GetValue(log);
        return value is ApiRequestMethod method ? (byte)method : value;
    }

    /// <summary>
    ///     按提供方创建原生数据库连接
    /// </summary>
    /// <returns>未打开的数据库连接</returns>
    private DbConnection CreateConnection() {
        using var context = CreateContext(_TABLE_PREFIX + "probe");
        var connection = context.Database.GetDbConnection();
        return (DbConnection)Activator.CreateInstance(connection.GetType(), connectionString)!;
    }

    /// <summary>
    ///     创建绑定指定表名的分表上下文
    /// </summary>
    /// <param name="tableName">日分表名称</param>
    /// <returns>分表上下文</returns>
    /// <exception cref="InvalidOperationException">数据库提供方不受支持时引发</exception>
    private ApiRequestLogContext CreateContext(string tableName) {
        var builder = new DbContextOptionsBuilder<ApiRequestLogContext>().ReplaceService<IModelCacheKeyFactory, ApiRequestLogModelCacheKeyFactory>();
        _ = provider switch
        {
            "sqlite" => builder.UseSqlite(connectionString)
            , "sqlserver" => builder.UseSqlServer(connectionString)
            , "postgresql" or "postgres" => builder.UseNpgsql(connectionString)
            , "mysql" => builder.UseMySQL(connectionString)
            , _ => throw new InvalidOperationException($"Unsupported database provider '{provider}'.")
        };
        return new ApiRequestLogContext(builder.Options, tableName);
    }

    /// <summary>
    ///     确保日分表存在，已确认存在的表缓存在内存中，之后不再查询数据库
    /// </summary>
    /// <param name="tableName">日分表名称</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步任务</returns>
    private async Task EnsureTableAsync(
        string tableName
        , CancellationToken cancellationToken
    ) {
        if (_knownTables.Contains(tableName)) {
            return;
        }

        await _createLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_knownTables.Contains(tableName)) {
                return;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var existing = await ListTablesAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!existing.Contains(tableName, StringComparer.Ordinal)) {
                foreach (var statement in BuildCreateStatements(tableName)) {
                    await using var command = connection.CreateCommand();
                    command.CommandText = statement;
                    try {
                        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (DbException) {
                        // 多个实例同时建表时后到者会收到已存在错误，表已就绪则忽略，索引语句由先到者负责，其他错误继续抛出
                        if (!await TableExistsAsync(connection, tableName, cancellationToken).ConfigureAwait(false)) {
                            throw;
                        }

                        break;
                    }
                }
            }

            _ = _knownTables.Add(tableName);
        }
        finally {
            _ = _createLock.Release();
        }
    }

    /// <summary>
    ///     把同一天的日志批量写入日分表，按参数上限拆分语句
    /// </summary>
    /// <param name="tableName">日分表名称</param>
    /// <param name="logs">同一天的日志</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>写入的日志数量</returns>
    private async Task<int> InsertIntoTableAsync(
        string tableName
        , IReadOnlyList<ApiRequestLog> logs
        , CancellationToken cancellationToken
    ) {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var rowsPerStatement = Math.Max(1, _MAX_COLUMNS_PER_STATEMENT / _columns.Length);
        var total = 0;
        foreach (var chunk in logs.Chunk(rowsPerStatement)) {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var rows = new List<string>(chunk.Length);
            for (var row = 0; row < chunk.Length; row++) {
                var names = new List<string>(_columns.Length);
                for (var column = 0; column < _columns.Length; column++) {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"@p{row}_{column}";
                    parameter.Value = GetValue(chunk[row], _columns[column]) ?? DBNull.Value;
                    _ = command.Parameters.Add(parameter);
                    names.Add(parameter.ParameterName);
                }

                rows.Add("(" + string.Join(", ", names) + ")");
            }

            #pragma warning disable S2077 // 表名和列名无法参数化，Quote 已用正则校验标识符，值全部为参数
            command.CommandText = $"INSERT INTO {Quote(tableName)} ({string.Join(", ", _columns.Select(Quote))}) VALUES {string.Join(", ", rows)}";
            #pragma warning restore S2077
            total += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    /// <summary>
    ///     列出数据库中全部以请求日志前缀开头的表
    /// </summary>
    /// <param name="connection">已打开的连接</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>表名集合</returns>
    private async Task<List<string>> ListTablesAsync(
        DbConnection connection
        , CancellationToken cancellationToken
    ) {
        var sql = provider switch
        {
            "sqlite" => "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE @prefix"
            , "sqlserver" => "SELECT name FROM sys.tables WHERE name LIKE @prefix"
            , "postgresql" or "postgres" => "SELECT tablename FROM pg_tables WHERE schemaname = current_schema() AND tablename LIKE @prefix"
            , _ => "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE @prefix"
        };
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@prefix";
        parameter.Value = _TABLE_PREFIX + "%";
        _ = command.Parameters.Add(parameter);
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            // LIKE 中的下划线是单字符通配符，各数据库的转义写法不一致（MySQL 的反斜杠还是字符串转义符），这里只做粗筛，再按前缀和日期后缀精确过滤
            var name = reader.GetString(0);
            if (TryParseTableDate(name, out _)) {
                tables.Add(name);
            }
        }

        return tables;
    }

    /// <summary>
    ///     用数据库标识符引号包裹名称
    /// </summary>
    /// <param name="name">表名或列名</param>
    /// <returns>带引号的标识符</returns>
    /// <exception cref="ArgumentException">名称包含非法字符时引发</exception>
    private string Quote(string name) {
        return !_identifier.IsMatch(name)
            ? throw new ArgumentException("Invalid identifier.", nameof(name))
            : provider switch
            {
                "sqlserver" => $"[{name}]"
                , "mysql" => $"`{name}`"
                , _ => $"\"{name}\""
            };
    }

    /// <summary>
    ///     判断指定表是否已存在
    /// </summary>
    /// <param name="connection">已打开的连接</param>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>表存在时返回 true</returns>
    private async Task<bool> TableExistsAsync(
        DbConnection connection
        , string tableName
        , CancellationToken cancellationToken
    ) {
        return (await ListTablesAsync(connection, cancellationToken).ConfigureAwait(false)).Contains(tableName, StringComparer.Ordinal);
    }
}