using System.Globalization;
using AiAdmin.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     读取并限制列表数据的单次导出条数
/// </summary>
/// <param name="db">应用数据库上下文</param>
/// <param name="dictionarySnapshotService">字典快照服务</param>
public sealed class ExportLimitService(AppDbContext db, DictionarySnapshotService dictionarySnapshotService)
{
    private const int _DEFAULT_EXPORT_LIMIT = 10000;
    private const string _MAXIMUM_EXPORT_ROWS_LABEL = "Maximum export rows";
    private const int _MAX_EXPORT_LIMIT = 100000;

    /// <summary>
    ///     获取系统配置的单次导出上限
    /// </summary>
    /// <returns>经过安全范围限制的导出条数</returns>
    public async Task<int> GetLimitAsync() {
        var settings = await dictionarySnapshotService.GetItemsAsync(DictionarySnapshotService.SYSTEM_SETTINGS_CODE).ConfigureAwait(false);
        var configuredValue = settings.FirstOrDefault(x => x.IsEnabled && x.Label == _MAXIMUM_EXPORT_ROWS_LABEL)?.Value;
        configuredValue ??= await db
            .DictionaryItems.AsNoTracking()
            .Where(x => x.IsEnabled && x.Label == _MAXIMUM_EXPORT_ROWS_LABEL && x.Category.Code == DictionarySnapshotService.SYSTEM_SETTINGS_CODE)
            .Select(x => x.Value)
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        return int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            ? Math.Clamp(limit, 1, _MAX_EXPORT_LIMIT)
            : _DEFAULT_EXPORT_LIMIT;
    }
}