using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Services;

/// <summary>
///     列表里展示用户（分配的商家、回调所属商家等）的批量查询：一次取回用户名和头像，避免逐行查询
/// </summary>
public static class UserDisplayQueryExtensions
{
    /// <summary>
    ///     按用户主键批量查询用户名和头像
    /// </summary>
    /// <param name="users">用户集合</param>
    /// <param name="userIds">需要展示的用户主键，可含重复和空值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户主键到用户展示信息的字典，已被删除的用户不在其中；没有需要查询的主键时不访问数据库</returns>
    public static Task<Dictionary<long, UserDisplayInfo>> LoadDisplayInfoAsync(
        this IQueryable<User> users
        , IEnumerable<long?> userIds
        , CancellationToken cancellationToken = default
    ) {
        // 参与 Contains 翻译的内存集合使用 List<T>
        var ids = userIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        return ids.Count == 0
            ? Task.FromResult(new Dictionary<long, UserDisplayInfo>())
            : users
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, Info = new UserDisplayInfo(x.UserName, x.Avatar) })
                .ToDictionaryAsync(x => x.Id, x => x.Info, cancellationToken);
    }
}