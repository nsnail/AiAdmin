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
///     字典管理控制器
/// </summary>
[ApiController]
[Authorize]
[ApiDescription("Dictionary management")]
[Route("api/dictionary")]
public sealed class DictionariesController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     查询字典目录树
    /// </summary>
    /// <returns>字典目录树</returns>
    [HttpGet("categories")]
    [ApiDescription("Query dictionary categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DictionaryCategoryResult>>>> CategoriesAsync() {
        var rows = await db.DictionaryCategories.AsNoTracking().OrderBy(x => x.Sort).ThenBy(x => x.Id).ToListAsync().ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<DictionaryCategoryResult>>.Ok(BuildTree(rows)));
    }

    /// <summary>
    ///     新增字典目录
    /// </summary>
    /// <param name="request">目录保存请求</param>
    /// <returns>新增目录</returns>
    [HttpPost("categories")]
    [ApiDescription("Create dictionary category")]
    public async Task<ActionResult<ApiResponse<DictionaryCategoryResult>>> CreateCategoryAsync(SaveDictionaryCategoryRequest request) {
        var code = request.Code.Trim();
        if (await db.DictionaryCategories.AnyAsync(x => x.Code == code).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Dictionary code already exists", null));
        }

        if (request.ParentId.HasValue && !await db.DictionaryCategories.AnyAsync(x => x.Id == request.ParentId).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Parent category not found", null));
        }

        var category = new DictionaryCategory
        {
            Code = code
            , Name = request.Name.Trim()
            , ParentId = request.ParentId
            , Sort = request.Sort
            , IsEnabled = request.IsEnabled
        };
        _ = await db.DictionaryCategories.AddAsync(category).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DictionaryCategoryResult>.Ok(ToResult(category), "Dictionary category created"));
    }

    /// <summary>
    ///     新增字典内容
    /// </summary>
    /// <param name="categoryId">目录主键</param>
    /// <param name="request">内容保存请求</param>
    /// <returns>新增内容</returns>
    [HttpPost("categories/{categoryId:long}/items")]
    [ApiDescription("Create dictionary item")]
    public async Task<ActionResult<ApiResponse<DictionaryItemResult>>> CreateItemAsync(
        long categoryId
        , SaveDictionaryItemRequest request
    ) {
        if (!await db.DictionaryCategories.AnyAsync(x => x.Id == categoryId).ConfigureAwait(false)) {
            return NotFound(new ApiResponse<object>(404, "Dictionary category not found", null));
        }

        var value = request.Value.Trim();
        var label = request.Label.Trim();
        if (await db.DictionaryItems.AnyAsync(x => x.CategoryId == categoryId && x.Label == label).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Dictionary label already exists", null));
        }

        var item = new DictionaryItem
        {
            CategoryId = categoryId
            , Value = value
            , Label = label
            , Sort = request.Sort
            , IsEnabled = request.IsEnabled
            , Remark = request.Remark.Trim()
        };
        _ = await db.DictionaryItems.AddAsync(item).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DictionaryItemResult>.Ok(ToItem(item), "Dictionary item created"));
    }

    /// <summary>
    ///     删除字典目录
    /// </summary>
    /// <param name="id">目录主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("categories/{id:long}")]
    [ApiDescription("Delete dictionary category")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCategoryAsync(long id) {
        var category = await db.DictionaryCategories.FindAsync(id).ConfigureAwait(false);
        if (category is null) {
            return NotFound(new ApiResponse<object>(404, "Dictionary category not found", null));
        }

        var hasChildren = await db.DictionaryCategories.AnyAsync(x => x.ParentId == id).ConfigureAwait(false);
        var hasItems = await db.DictionaryItems.AnyAsync(x => x.CategoryId == id).ConfigureAwait(false);
        if (hasChildren || hasItems) {
            return BadRequest(new ApiResponse<object>(400, "Delete child categories and items first", null));
        }

        _ = db.DictionaryCategories.Remove(category);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Dictionary category deleted"));
    }

    /// <summary>
    ///     删除字典内容
    /// </summary>
    /// <param name="id">内容主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("items/{id:long}")]
    [ApiDescription("Delete dictionary item")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteItemAsync(long id) {
        var item = await db.DictionaryItems.FindAsync(id).ConfigureAwait(false);
        if (item is null) {
            return NotFound(new ApiResponse<object>(404, "Dictionary item not found", null));
        }

        _ = db.DictionaryItems.Remove(item);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Dictionary item deleted"));
    }

    /// <summary>
    ///     查询指定目录的字典内容
    /// </summary>
    /// <param name="categoryId">目录主键</param>
    /// <returns>字典内容列表</returns>
    [HttpGet("categories/{categoryId:long}/items")]
    [ApiDescription("Query dictionary items")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DictionaryItemResult>>>> ItemsAsync(long categoryId) {
        var rows = await db
            .DictionaryItems.AsNoTracking()
            .Where(x => x.CategoryId == categoryId)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync()
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<DictionaryItemResult>>.Ok(rows.ConvertAll(ToItem)));
    }

    /// <summary>
    ///     修改字典目录
    /// </summary>
    /// <param name="id">目录主键</param>
    /// <param name="request">目录保存请求</param>
    /// <returns>修改后的目录</returns>
    [HttpPut("categories/{id:long}")]
    [ApiDescription("Update dictionary category")]
    public async Task<ActionResult<ApiResponse<DictionaryCategoryResult>>> UpdateCategoryAsync(
        long id
        , SaveDictionaryCategoryRequest request
    ) {
        var category = await db.DictionaryCategories.FindAsync(id).ConfigureAwait(false);
        if (category is null) {
            return NotFound(new ApiResponse<object>(404, "Dictionary category not found", null));
        }

        if (request.ParentId == id || (request.ParentId.HasValue && await IsDescendantAsync(id, request.ParentId.Value).ConfigureAwait(false))) {
            return BadRequest(new ApiResponse<object>(400, "Invalid parent category", null));
        }

        var code = request.Code.Trim();
        if (await db.DictionaryCategories.AnyAsync(x => x.Code == code && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Dictionary code already exists", null));
        }

        category.Code = code;
        category.Name = request.Name.Trim();
        category.ParentId = request.ParentId;
        category.Sort = request.Sort;
        category.IsEnabled = request.IsEnabled;
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DictionaryCategoryResult>.Ok(ToResult(category), "Dictionary category updated"));
    }

    /// <summary>
    ///     修改字典内容
    /// </summary>
    /// <param name="id">内容主键</param>
    /// <param name="request">内容保存请求</param>
    /// <returns>修改后的内容</returns>
    [HttpPut("items/{id:long}")]
    [ApiDescription("Update dictionary item")]
    public async Task<ActionResult<ApiResponse<DictionaryItemResult>>> UpdateItemAsync(
        long id
        , SaveDictionaryItemRequest request
    ) {
        var item = await db.DictionaryItems.FindAsync(id).ConfigureAwait(false);
        if (item is null) {
            return NotFound(new ApiResponse<object>(404, "Dictionary item not found", null));
        }

        var value = request.Value.Trim();
        var label = request.Label.Trim();
        if (await db.DictionaryItems.AnyAsync(x => x.CategoryId == item.CategoryId && x.Label == label && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Dictionary label already exists", null));
        }

        item.Value = value;
        item.Label = label;
        item.Sort = request.Sort;
        item.IsEnabled = request.IsEnabled;
        item.Remark = request.Remark.Trim();
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DictionaryItemResult>.Ok(ToItem(item), "Dictionary item updated"));
    }

    /// <summary>
    ///     将字典目录列表构建为树
    /// </summary>
    /// <param name="rows">字典目录列表</param>
    /// <returns>字典目录树</returns>
    private static IReadOnlyList<DictionaryCategoryResult> BuildTree(IReadOnlyList<DictionaryCategory> rows) {
        return Build(null);

        IReadOnlyList<DictionaryCategoryResult> Build(long? parentId) {
            return
            [
                .. rows
                    .Where(x => x.ParentId == parentId)
                    .OrderBy(x => x.Sort)
                    .ThenBy(x => x.Id)
                    .Select(x => ToResult(x) with { Children = Build(x.Id) })
            ];
        }
    }

    /// <summary>
    ///     将字典内容实体转换为响应模型
    /// </summary>
    /// <param name="item">字典内容实体</param>
    /// <returns>字典内容响应模型</returns>
    private static DictionaryItemResult ToItem(DictionaryItem item) {
        return new DictionaryItemResult(
            item.Id, ServerTime.ToOffset(item.CreatedAt), item.CategoryId, item.Value, item.Label, item.Sort, item.IsEnabled, item.Remark
        );
    }

    /// <summary>
    ///     将字典目录实体转换为响应模型
    /// </summary>
    /// <param name="category">字典目录实体</param>
    /// <returns>字典目录响应模型</returns>
    private static DictionaryCategoryResult ToResult(DictionaryCategory category) {
        return new DictionaryCategoryResult(
            category.Id, ServerTime.ToOffset(category.CreatedAt), category.Code, category.Name, category.ParentId, category.Sort, category.IsEnabled
            , []
        );
    }

    /// <summary>
    ///     判断候选父目录是否为当前目录的后代
    /// </summary>
    /// <param name="id">当前目录主键</param>
    /// <param name="candidate">候选父目录主键</param>
    /// <returns>候选目录位于当前目录子树时返回 true</returns>
    private async Task<bool> IsDescendantAsync(
        long id
        , long candidate
    ) {
        var parent = await db.DictionaryCategories.FindAsync(candidate).ConfigureAwait(false);
        while (parent?.ParentId is { } parentId) {
            if (parentId == id) {
                return true;
            }

            parent = await db.DictionaryCategories.FindAsync(parentId).ConfigureAwait(false);
        }

        return false;
    }
}