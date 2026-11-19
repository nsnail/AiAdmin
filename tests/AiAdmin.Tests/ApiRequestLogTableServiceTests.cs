using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证请求日志按天分表：按日期建表、同批跨天分别写入、重复建表幂等、删除过期分表
/// </summary>
public sealed class ApiRequestLogTableServiceTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAlive;
    private readonly ApiRequestLogTableService _service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ApiRequestLogTableServiceTests" /> class.
    ///     初始化共享缓存的内存数据库和分表服务
    /// </summary>
    public ApiRequestLogTableServiceTests() {
        _connectionString = $"DataSource=file:api-log-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        _service = new ApiRequestLogTableService("sqlite", _connectionString);
    }

    /// <summary>
    ///     验证会建索引的数据库都会建出"创建时间 + 路径 CRC32"联合索引，且创建时间在前：仪表盘按接口统计请求数时，
    ///     只有这个顺序的覆盖索引才能只扫描时间范围内的条目，否则优化器会全索引扫描单列路径索引并逐行回表，千万行要几分钟。
    ///     SQL Server 的建表脚本本来就不含建索引语句（既有行为，与本次无关），因此不在此列
    /// </summary>
    /// <param name="provider">数据库提供方</param>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public void BuildCreateStatements_CreatesCreatedAtPathCoveringIndex(string provider) {
        using var service = new ApiRequestLogTableService(provider, "Server=localhost;Database=x;User Id=u;Password=p;");

        // 只比较括号里的列清单：索引名本身也含有这两个词，不能参与顺序判断
        var columnLists = service
            .BuildCreateStatements("sys_api_request_log_20261027")
            .Where(x => x.Contains("CREATE INDEX", StringComparison.Ordinal))
            .Select(x => x[x.LastIndexOf('(')..])
            .Where(x => x.Contains("CreatedAt", StringComparison.Ordinal) && x.Contains("PathCrc32", StringComparison.Ordinal))
            .ToList();

        var columns = Assert.Single(columnLists);
        Assert.True(
            columns.IndexOf("CreatedAt", StringComparison.Ordinal) < columns.IndexOf("PathCrc32", StringComparison.Ordinal)
            , $"CreatedAt must come before PathCrc32: {columns}"
        );
    }

    /// <summary>
    ///     验证建表语句包含建表和各个索引，且表名为日分表名
    /// </summary>
    [Fact]
    public void BuildCreateStatements_CreatesTableAndIndexes() {
        var statements = _service.BuildCreateStatements("sys_api_request_log_20261027");

        Assert.Contains(
            statements
            , x => x.Contains("CREATE TABLE", StringComparison.Ordinal) && x.Contains("sys_api_request_log_20261027", StringComparison.Ordinal)
        );
        Assert.True(statements.Count(x => x.Contains("CREATE INDEX", StringComparison.Ordinal)) >= 4);
    }

    /// <summary>
    ///     验证千万级大表的列设计：不含任何变长字符串列，也不再包含路径文本、用户名、UserAgent，路径、IP 和方法以数值存储
    /// </summary>
    /// <param name="provider">数据库提供方</param>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public void BuildCreateStatements_UsesOnlyFixedWidthColumns(string provider) {
        using var service = new ApiRequestLogTableService(provider, "Server=localhost;Database=x;User Id=u;Password=p;");

        var create = Assert.Single(
            service.BuildCreateStatements("sys_api_request_log_20261027"), x => x.Contains("CREATE TABLE", StringComparison.Ordinal)
        );

        foreach (var removed in new[] { "UserName", "UserAgent", "Path\"", "Path`", "[Path]" }) {
            Assert.DoesNotContain(removed, create, StringComparison.Ordinal);
        }

        // sqlite 把日期存成 TEXT，那是它自己的类型而不是字符串列，其余三种库的建表语句不得出现任何字符串类型
        if (provider != "sqlite") {
            foreach (var variable in new[] { "varchar", "nvarchar", "text", "char(" }) {
                Assert.DoesNotContain(variable, create, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var column in new[] { "PathCrc32", "ClientIp", "ServerIp", "Method", "ElapsedMilliseconds" }) {
            Assert.Contains(column, create, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     验证四种数据库的建表语句都能生成，字符串列带长度而不是不限长的大文本类型，索引名不超过各库长度上限
    /// </summary>
    /// <param name="provider">数据库提供方</param>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public void BuildCreateStatements_WorksForEveryProvider(string provider) {
        using var service = new ApiRequestLogTableService(provider, "Server=localhost;Database=x;User Id=u;Password=p;");

        var statements = service.BuildCreateStatements("sys_api_request_log_20261027");

        var create = Assert.Single(statements, x => x.Contains("CREATE TABLE", StringComparison.Ordinal));
        Assert.Contains("sys_api_request_log_20261027", create, StringComparison.Ordinal);

        // sqlite 的文本列本来就没有长度概念，其余三种库的字符串列必须带长度，不能退化为不限长的大文本
        if (provider != "sqlite") {
            Assert.DoesNotContain("longtext", create, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("(max)", create, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" text ", create, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var index in statements.Where(x => x.Contains("CREATE INDEX", StringComparison.Ordinal))) {
            var name = index
                .Split(['"', '`', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
                .First(x => x.StartsWith("IX_", StringComparison.Ordinal));
            Assert.True(name.Length <= 63, $"Index name '{name}' is longer than 63 characters");
        }
    }

    /// <summary>
    ///     释放服务和数据库连接
    /// </summary>
    public void Dispose() {
        _service.Dispose();
        _keepAlive.Dispose();
    }

    /// <summary>
    ///     验证表名里下划线被 LIKE 当作通配符时命中的相似表不会被当成日分表，也不会被删除；
    ///     查询语句不使用 ESCAPE 子句，因为 MySQL 里反斜杠是字符串转义符，写 ESCAPE 子句会导致语法错误
    /// </summary>
    [Fact]
    public async Task DropTablesBefore_IgnoresLookalikeTablesAsync() {
        Execute("CREATE TABLE sysXapiXrequestXlogX20200101 (id INTEGER)");
        Execute("CREATE TABLE sys_api_request_log_backup (id INTEGER)");
        _ = await _service.InsertAsync([NewLog(Utc(2020, 1, 1), "/api/a", 200)], CancellationToken.None).ConfigureAwait(true);

        var dropped = await _service.DropTablesBeforeAsync(Utc(2026, 1, 1), CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(["sys_api_request_log_20200101"], dropped);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sysXapiXrequestXlogX20200101'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sys_api_request_log_backup'"));
    }

    /// <summary>
    ///     验证只删除早于截止日期的分表，截止当天及之后的分表和其他表保留
    /// </summary>
    [Fact]
    public async Task DropTablesBefore_RemovesOnlyOlderTablesAsync() {
        foreach (var day in new[] { Utc(2026, 9, 1), Utc(2026, 10, 1), Utc(2026, 10, 27) }) {
            _ = await _service.InsertAsync([NewLog(day, "/api/a", 200)], CancellationToken.None).ConfigureAwait(true);
        }

        Execute("CREATE TABLE other_table (id INTEGER)");
        var dropped = await _service.DropTablesBeforeAsync(Utc(2026, 10, 1), CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(["sys_api_request_log_20260901"], dropped);
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sys_api_request_log_20260901'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sys_api_request_log_20261001'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'sys_api_request_log_20261027'"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'other_table'"));
    }

    /// <summary>
    ///     验证批量超过单条语句参数上限时被拆分，数据完整写入
    /// </summary>
    [Fact]
    public async Task Insert_ChunksLargeBatchesAsync() {
        var day = new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);
        var logs = Enumerable.Range(0, 400).Select(i => NewLog(day, $"/api/{i}", 200)).ToList();

        var count = await _service.InsertAsync(logs, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(400, count);
        Assert.Equal(400L, Scalar("SELECT COUNT(*) FROM sys_api_request_log_20261027"));
    }

    /// <summary>
    ///     验证重复写入同一天不会重复建表，数据累加
    /// </summary>
    [Fact]
    public async Task Insert_ReusesExistingTableAsync() {
        var day = new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);

        _ = await _service.InsertAsync([NewLog(day, "/api/a", 200)], CancellationToken.None).ConfigureAwait(true);
        _ = await _service.InsertAsync([NewLog(day, "/api/b", 200)], CancellationToken.None).ConfigureAwait(true);
        using var second = new ApiRequestLogTableService("sqlite", _connectionString);
        _ = await second.InsertAsync([NewLog(day, "/api/c", 200)], CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3L, Scalar("SELECT COUNT(*) FROM sys_api_request_log_20261027"));
    }

    /// <summary>
    ///     验证同一批日志跨天时分别写入各自的日分表，并保留全部字段
    /// </summary>
    [Fact]
    public async Task Insert_SplitsLogsAcrossDailyTablesAsync() {
        var day1 = new DateTime(2026, 10, 26, 23, 59, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 10, 27, 0, 1, 0, DateTimeKind.Utc);
        var logs = new[] { NewLog(day1, "/api/a", 200), NewLog(day1, "/api/b", 500), NewLog(day2, "/api/c", 200, 42) };

        var count = await _service.InsertAsync(logs, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3, count);
        Assert.Equal(2L, Scalar("SELECT COUNT(*) FROM sys_api_request_log_20261026"));
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM sys_api_request_log_20261027"));
        Assert.Equal(42L, Scalar("SELECT UserId FROM sys_api_request_log_20261027"));
        Assert.Equal(DBNull.Value, Scalar("SELECT DepartmentId FROM sys_api_request_log_20261027"));
        Assert.Equal(
            500L, Scalar($"SELECT StatusCode FROM sys_api_request_log_20261026 WHERE PathCrc32 = {ApiEndpointKey.ComputePathCrc32("/api/b")}")
        );
        Assert.Equal(2L, Scalar("SELECT Method FROM sys_api_request_log_20261026 LIMIT 1"));
        Assert.Equal(0x7F000001L, Scalar("SELECT ClientIp FROM sys_api_request_log_20261026 LIMIT 1"));
    }

    /// <summary>
    ///     验证分表名称按日期生成，并能从表名反解出日期
    /// </summary>
    [Fact]
    public void TableName_RoundTripsDate() {
        var name = ApiRequestLogTableService.GetTableName(new DateTime(2026, 10, 27, 13, 0, 0, DateTimeKind.Utc));

        Assert.Equal("sys_api_request_log_20261027", name);
        Assert.True(ApiRequestLogTableService.TryParseTableDate(name, out var date));
        Assert.Equal(new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc), date.Date);
        Assert.False(ApiRequestLogTableService.TryParseTableDate("sys_api_request_log_notadate", out _));
        Assert.False(ApiRequestLogTableService.TryParseTableDate("tg_task", out _));
    }

    /// <summary>
    ///     创建测试用日志
    /// </summary>
    /// <param name="createdAt">创建时间</param>
    /// <param name="path">请求路径，转换为路径 CRC32 保存</param>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="userId">用户主键</param>
    /// <returns>请求日志</returns>
    private static ApiRequestLog NewLog(
        DateTime createdAt
        , string path
        , short statusCode
        , long? userId = null
    ) {
        return new ApiRequestLog
        {
            CreatedAt = createdAt
            , Method = ApiRequestMethod.Post
            , PathCrc32 = ApiEndpointKey.ComputePathCrc32(path)
            , StatusCode = statusCode
            , ApiResponseCode = 200
            , ElapsedMilliseconds = 12
            , UserId = userId
            , ClientIp = 0x7F000001
            , TraceId = 99
            , WorkerId = 1
        };
    }

    /// <summary>
    ///     创建指定日期零点的 UTC 时间
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="day">日</param>
    /// <returns>UTC 时间</returns>
    private static DateTime Utc(
        int year
        , int month
        , int day
    ) {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    ///     执行不返回结果的语句
    /// </summary>
    /// <param name="sql">语句</param>
    private void Execute(string sql) {
        using var command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>
    ///     执行查询并返回第一行第一列
    /// </summary>
    /// <param name="sql">查询语句</param>
    /// <returns>查询结果</returns>
    private object Scalar(string sql) {
        using var command = _keepAlive.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() ?? DBNull.Value;
    }
}