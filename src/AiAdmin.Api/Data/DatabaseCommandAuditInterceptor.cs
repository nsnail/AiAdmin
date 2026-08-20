// 记录所有 EF Core 数据库命令的读取和写入审计日志

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiAdmin.Api.Data;

/// <summary>
///     记录数据库读取和写入命令审计信息
/// </summary>
public sealed class DatabaseCommandAuditInterceptor(ILogger<DatabaseCommandAuditInterceptor> logger) : DbCommandInterceptor
{
    private static readonly Action<ILogger, string, long?, string?, Exception?> _logCommand = LoggerMessage.Define<string, long?, string?>(
        LogLevel.Information, new EventId(2001, "DatabaseCommandAudit"), "Database {Operation} command executed by user {UserId} for {ContextType}"
    );

    /// <summary>
    ///     记录同步写入命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>允许继续执行写入命令的拦截结果</returns>
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<int> result
    ) {
        WriteAudit("write", eventData);
        return base.NonQueryExecuting(command, eventData, result);
    }

    /// <summary>
    ///     记录异步写入命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步命令执行结果</returns>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<int> result
        , CancellationToken cancellationToken = default
    ) {
        WriteAudit("write", eventData);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     记录同步读取命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>允许继续执行读取命令的拦截结果</returns>
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<DbDataReader> result
    ) {
        WriteAudit("read", eventData);
        return base.ReaderExecuting(command, eventData, result);
    }

    /// <summary>
    ///     记录异步读取命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步命令执行结果</returns>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<DbDataReader> result
        , CancellationToken cancellationToken = default
    ) {
        WriteAudit("read", eventData);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     记录同步标量读取命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>允许继续执行标量读取命令的拦截结果</returns>
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<object> result
    ) {
        WriteAudit("read", eventData);
        return base.ScalarExecuting(command, eventData, result);
    }

    /// <summary>
    ///     记录异步标量读取命令
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步命令执行结果</returns>
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command
        , CommandEventData eventData
        , InterceptionResult<object> result
        , CancellationToken cancellationToken = default
    ) {
        WriteAudit("read", eventData);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     输出不包含 SQL 参数的数据库操作审计日志
    /// </summary>
    /// <param name="operation">数据库操作类型</param>
    /// <param name="eventData">命令事件数据</param>
    private void WriteAudit(
        string operation
        , CommandEventData eventData
    ) {
        var context = eventData.Context as AppDbContext;
        var userId = context?.CurrentAuditActorUserId;
        _logCommand(logger, operation, userId, eventData.Context?.GetType().Name, null);
    }
}