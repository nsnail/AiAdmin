using AiAdmin.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     列表导出公共流程：对已应用动态筛选的查询统计总数，按系统设置的导出上限截取，并转换为与列表一致的记录。
///     各控制器的 export 动作只负责构造筛选后的查询、声明排序和转换，不再各自重复总数、上限和截取逻辑
/// </summary>
public static class ListExport
{
    /// <summary>
    ///     执行导出，读取行后同步转换为导出记录
    /// </summary>
    /// <typeparam name="TEntity">查询实体类型</typeparam>
    /// <typeparam name="TRow">排序后读取的行类型，未投影时与实体类型相同</typeparam>
    /// <typeparam name="TItem">导出记录类型，与列表接口返回的记录一致</typeparam>
    /// <param name="filtered">已应用动态筛选的查询</param>
    /// <param name="limits">导出上限服务</param>
    /// <param name="sortAndSelect">应用与列表接口一致的排序和投影，不得包含分页</param>
    /// <param name="convert">把读取的行转换为导出记录</param>
    /// <returns>受上限约束的导出结果</returns>
    public static Task<DynamicExportResult<TItem>> RunAsync<TEntity, TRow, TItem>(
        IQueryable<TEntity> filtered
        , ExportLimitService limits
        , Func<IQueryable<TEntity>, IQueryable<TRow>> sortAndSelect
        , Func<List<TRow>, List<TItem>> convert
    ) {
        return RunCustomAsync(
            filtered, limits, (
                query
                , limit
            ) => sortAndSelect(query).Take(limit).ToListAsync(), rows => Task.FromResult(convert(rows))
        );
    }

    /// <summary>
    ///     执行导出，由调用方自行取数，用于无法用单个排序查询表达的列表（例如按内存计算值排序）
    /// </summary>
    /// <typeparam name="TEntity">查询实体类型</typeparam>
    /// <typeparam name="TRow">读取的行类型</typeparam>
    /// <typeparam name="TItem">导出记录类型，与列表接口返回的记录一致</typeparam>
    /// <param name="filtered">已应用动态筛选的查询</param>
    /// <param name="limits">导出上限服务</param>
    /// <param name="loadAsync">按导出上限读取行，参数为筛选后的查询和上限</param>
    /// <param name="convertAsync">把读取的行转换为导出记录</param>
    /// <returns>受上限约束的导出结果</returns>
    public static async Task<DynamicExportResult<TItem>> RunCustomAsync<TEntity, TRow, TItem>(
        IQueryable<TEntity> filtered
        , ExportLimitService limits
        , Func<IQueryable<TEntity>, int, Task<List<TRow>>> loadAsync
        , Func<List<TRow>, Task<List<TItem>>> convertAsync
    ) {
        var total = await filtered.CountAsync().ConfigureAwait(false);
        var limit = await limits.GetLimitAsync().ConfigureAwait(false);
        var rows = await loadAsync(filtered, limit).ConfigureAwait(false);
        var items = await convertAsync(rows).ConfigureAwait(false);
        return new DynamicExportResult<TItem>(items, limit, total);
    }

    /// <summary>
    ///     执行导出，读取行后需要批量查询关联数据（例如用户名、设备名称）再转换为导出记录
    /// </summary>
    /// <typeparam name="TEntity">查询实体类型</typeparam>
    /// <typeparam name="TRow">排序后读取的行类型，未投影时与实体类型相同</typeparam>
    /// <typeparam name="TItem">导出记录类型，与列表接口返回的记录一致</typeparam>
    /// <param name="filtered">已应用动态筛选的查询</param>
    /// <param name="limits">导出上限服务</param>
    /// <param name="sortAndSelect">应用与列表接口一致的排序和投影，不得包含分页</param>
    /// <param name="convertAsync">把读取的行转换为导出记录，可以批量查询关联数据</param>
    /// <returns>受上限约束的导出结果</returns>
    public static Task<DynamicExportResult<TItem>> RunWithLookupAsync<TEntity, TRow, TItem>(
        IQueryable<TEntity> filtered
        , ExportLimitService limits
        , Func<IQueryable<TEntity>, IQueryable<TRow>> sortAndSelect
        , Func<List<TRow>, Task<List<TItem>>> convertAsync
    ) {
        return RunCustomAsync(
            filtered, limits, (
                query
                , limit
            ) => sortAndSelect(query).Take(limit).ToListAsync(), convertAsync
        );
    }
}