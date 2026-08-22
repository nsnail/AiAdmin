using System.Globalization;
using System.Security.Claims;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     当前用户钱包控制器
/// </summary>
[ApiController]
[ApiDescription("Wallet management")]
[Authorize]
[Route("api/wallet")]
public sealed class WalletController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     查询钱包列表筛选字段元数据
    /// </summary>
    /// <returns>钱包筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query wallet filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(
            ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(
                ListFilterMetadataService.GetFields<Wallet>().Where(x => x.Field == nameof(Wallet.CreatedAt)).ToArray()
            )
        );
    }

    /// <summary>
    ///     分页查询钱包列表
    /// </summary>
    /// <param name="request">包含动态筛选、排序和分页信息的请求体</param>
    /// <returns>钱包分页结果</returns>
    [HttpPost("list")]
    [ApiDescription("Query wallet list")]
    public async Task<ActionResult<ApiResponse<PagedResponse<WalletListItem>>>> ListAsync([FromBody] DynamicQueryRequest request) {
        var query = db.Wallets.AsNoTracking().Include(x => x.User).ApplyDynamicFilter(request.DynamicFilter);
        var total = await query.CountAsync().ConfigureAwait(false);
        var rows = await query
            .ApplyDynamicSort(request.SortField, request.SortOrder, nameof(Wallet.UserId), true)
            .Skip((request.Current - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(
            ApiResponse<PagedResponse<WalletListItem>>.Ok(
                new PagedResponse<WalletListItem>(rows.ConvertAll(ToListItem), request.Current, request.Size, total)
            )
        );
    }

    /// <summary>
    ///     查询当前用户钱包
    /// </summary>
    /// <returns>当前用户钱包信息</returns>
    [HttpGet("me")]
    [ApiDescription("Query current user wallet")]
    public async Task<ActionResult<ApiResponse<WalletResult>>> GetCurrentAsync() {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId).ConfigureAwait(false);
        return wallet is null
            ? NotFound(new ApiResponse<object>(404, "Wallet not found", null))
            : Ok(ApiResponse<WalletResult>.Ok(ToResult(wallet)));
    }

    /// <summary>
    ///     转换钱包实体为接口结果
    /// </summary>
    /// <param name="wallet">钱包实体</param>
    /// <returns>钱包接口结果</returns>
    private static WalletResult ToResult(AiAdmin.Api.Models.Wallet wallet) {
        return new WalletResult(
            wallet.UserId
            , "USD"
            , wallet.AvailableBalance
            , wallet.FrozenBalance
            , wallet.TotalIncome
            , wallet.TotalExpense
            , wallet.LastTransactionAt.HasValue ? new DateTimeOffset(wallet.LastTransactionAt.Value) : null
            , wallet.Version);
    }

    /// <summary>
    ///     转换钱包实体为列表项
    /// </summary>
    /// <param name="wallet">钱包实体</param>
    /// <returns>钱包列表项</returns>
    private static WalletListItem ToListItem(Wallet wallet) {
        return new WalletListItem(
            wallet.UserId
            , "USD"
            , wallet.AvailableBalance
            , wallet.FrozenBalance
            , wallet.TotalIncome
            , wallet.TotalExpense
            , wallet.LastTransactionAt.HasValue ? new DateTimeOffset(wallet.LastTransactionAt.Value) : null
            , wallet.Version
            , wallet.UserId
            , new DateTimeOffset(wallet.CreatedAt)
            , wallet.User.UserName
            , wallet.User.Email
            , wallet.User.Avatar);
    }
}
