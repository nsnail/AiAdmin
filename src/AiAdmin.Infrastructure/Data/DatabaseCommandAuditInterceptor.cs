using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiAdmin.Api.Data;

/// <summary>
///     记录数据库读取和写入命令审计信息
/// </summary>
[SuppressMessage(
    "StyleCop.CSharp.DocumentationRules", "SA1625:Element documentation should not be copied and pasted"
    , Justification = "Interceptor overloads intentionally document the same database command parameters."
)]
public sealed class DatabaseCommandAuditInterceptor(ILogger<DatabaseCommandAuditInterceptor> logger) : DbCommandInterceptor
{
    private static readonly Action<ILogger, string, string, string, long, Exception?> _logCommand = LoggerMessage.Define<string, string, string, long>(
        LogLevel.Information
        , new EventId(2001, "DatabaseCommandAudit")
        , "SQL command executed; LogType={LogType}; Source={Source}; Sql={Sql}; ElapsedMilliseconds={ElapsedMilliseconds}"
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
    )
    {
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
    )
    {
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
    )
    {
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
    )
    {
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
    )
    {
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
    )
    {
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     记录同步非查询命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>命令执行结果</returns>
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        WriteAudit(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    /// <summary>
    ///     记录异步非查询命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步命令执行结果</returns>
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command
        , CommandExecutedEventData eventData
        , int result
        , CancellationToken cancellationToken = default
    )
    {
        WriteAudit(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     记录同步读取命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>读取结果</returns>
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        WriteAudit(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    /// <summary>
    ///     记录异步读取命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步读取结果</returns>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command
        , CommandExecutedEventData eventData
        , DbDataReader result
        , CancellationToken cancellationToken = default
    )
    {
        WriteAudit(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     记录同步标量命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <returns>标量结果</returns>
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        WriteAudit(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    /// <summary>
    ///     记录异步标量命令完成信息
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    /// <param name="result">命令执行结果</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>异步标量结果</returns>
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command
        , CommandExecutedEventData eventData
        , object? result
        , CancellationToken cancellationToken = default
    )
    {
        WriteAudit(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    ///     输出数据库操作审计日志
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="eventData">命令事件数据</param>
    private void WriteAudit(DbCommand command, CommandExecutedEventData eventData)
    {
        _logCommand(logger, "Sql", "SQL", command.CommandText, (long)eventData.Duration.TotalMilliseconds, null);
    }
}