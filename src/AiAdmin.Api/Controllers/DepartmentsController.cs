using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// 提供部门树查询、部门维护以及删除约束校验接口。
namespace AiAdmin.Api.Controllers;

/// <summary>
///     部门管理控制器
/// </summary>
[ApiController]
[ApiDescription("Department management")]
[Authorize]
[Route("api/department")]
public sealed class DepartmentsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    ///     创建部门
    /// </summary>
    /// <param name="request">部门保存请求</param>
    /// <returns>创建后的部门节点</returns>
    [HttpPost]
    [ApiDescription("Create department")]
    public async Task<ActionResult<ApiResponse<DepartmentTreeItem>>> CreateAsync(SaveDepartmentRequest request) {
        var code = request.Code.Trim();
        if (await db.Departments.AnyAsync(x => x.Code == code).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Department code already exists", null));
        }

        if (!await ParentExistsAsync(request.ParentId).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Parent department not found", null));
        }

        var department = FromRequest(request);
        _ = await db.Departments.AddAsync(department).ConfigureAwait(false);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DepartmentTreeItem>.Ok(ToTreeItem(department), "Department created"));
    }

    /// <summary>
    ///     删除部门
    /// </summary>
    /// <param name="id">部门主键</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id:long}")]
    [ApiDescription("Delete department")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAsync(long id) {
        var department = await db.Departments.FindAsync(id).ConfigureAwait(false);
        if (department is null) {
            return NotFound(new ApiResponse<object>(404, "Department not found", null));
        }

        if (await db.Departments.AnyAsync(x => x.ParentId == id).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Delete child departments first", null));
        }

        if (await db.UserDepartments.AnyAsync(x => x.DepartmentId == id).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "The department has assigned users", null));
        }

        _ = db.Departments.Remove(department);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { }, "Department deleted"));
    }

    /// <summary>
    ///     查询部门列表筛选字段元数据
    /// </summary>
    /// <returns>部门筛选字段定义</returns>
    [HttpGet("filter-fields")]
    [ApiDescription("Query department filter fields")]
    public ActionResult<ApiResponse<IReadOnlyList<ListFilterFieldResult>>> FilterFields() {
        return Ok(ApiResponse<IReadOnlyList<ListFilterFieldResult>>.Ok(ListFilterMetadataService.GetFields<Department>()));
    }

    /// <summary>
    ///     查询全部部门树
    /// </summary>
    /// <returns>部门树节点集合</returns>
    [HttpGet("tree")]
    [ApiDescription("Query department tree")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DepartmentTreeItem>>>> TreeAsync() {
        var departments = await db.Departments.AsNoTracking().OrderBy(x => x.Sort).ThenBy(x => x.Id).ToListAsync().ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<DepartmentTreeItem>>.Ok(BuildTree(departments)));
    }

    /// <summary>
    ///     更新部门
    /// </summary>
    /// <param name="id">部门主键</param>
    /// <param name="request">部门保存请求</param>
    /// <returns>更新后的部门节点</returns>
    [HttpPut("{id:long}")]
    [ApiDescription("Update department")]
    public async Task<ActionResult<ApiResponse<DepartmentTreeItem>>> UpdateAsync(
        long id
        , SaveDepartmentRequest request
    ) {
        var department = await db.Departments.FindAsync(id).ConfigureAwait(false);
        if (department is null) {
            return NotFound(new ApiResponse<object>(404, "Department not found", null));
        }

        var code = request.Code.Trim();
        if (await db.Departments.AnyAsync(x => x.Code == code && x.Id != id).ConfigureAwait(false)) {
            return Conflict(new ApiResponse<object>(409, "Department code already exists", null));
        }

        if (!await ParentExistsAsync(request.ParentId).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Parent department not found", null));
        }

        if (await CreatesCycleAsync(id, request.ParentId).ConfigureAwait(false)) {
            return BadRequest(new ApiResponse<object>(400, "Parent department cannot be itself or its descendant", null));
        }

        ApplyRequest(department, request);
        _ = await db.SaveChangesAsync().ConfigureAwait(false);
        return Ok(ApiResponse<DepartmentTreeItem>.Ok(ToTreeItem(department), "Department updated"));
    }

    /// <summary>
    ///     将请求字段应用到部门实体
    /// </summary>
    /// <param name="department">部门实体</param>
    /// <param name="request">部门保存请求</param>
    private static void ApplyRequest(
        Department department
        , SaveDepartmentRequest request
    ) {
        department.Name = request.Name.Trim();
        department.Code = request.Code.Trim();
        department.ParentId = request.ParentId;
        department.Sort = request.Sort;
        department.Leader = request.Leader.Trim();
        department.Phone = request.Phone.Trim();
        department.Email = request.Email?.Trim() ?? string.Empty;
        department.IsEnabled = request.IsEnabled;
    }

    /// <summary>
    ///     将平面部门集合构建为树
    /// </summary>
    /// <param name="departments">平面部门集合</param>
    /// <returns>部门树节点集合</returns>
    private static IReadOnlyList<DepartmentTreeItem> BuildTree(IReadOnlyList<Department> departments) {
        return BuildChildren(null);

        IReadOnlyList<DepartmentTreeItem> BuildChildren(long? parentId) {
            return
            [
                .. departments
                    .Where(x => x.ParentId == parentId)
                    .OrderBy(x => x.Sort)
                    .ThenBy(x => x.Id)
                    .Select(x => ToTreeItem(x, BuildChildren(x.Id)))
            ];
        }
    }

    /// <summary>
    ///     根据请求创建部门实体
    /// </summary>
    /// <param name="request">部门保存请求</param>
    /// <returns>部门实体</returns>
    private static Department FromRequest(SaveDepartmentRequest request) {
        var department = new Department { Name = request.Name.Trim(), Code = request.Code.Trim() };
        ApplyRequest(department, request);
        return department;
    }

    /// <summary>
    ///     将部门实体转换为树节点
    /// </summary>
    /// <param name="department">部门实体</param>
    /// <param name="children">子部门节点集合</param>
    /// <returns>部门树节点</returns>
    private static DepartmentTreeItem ToTreeItem(
        Department department
        , IReadOnlyList<DepartmentTreeItem>? children = null
    ) {
        return new DepartmentTreeItem
        {
            Id = department.Id
            , Name = department.Name
            , Code = department.Code
            , ParentId = department.ParentId
            , Sort = department.Sort
            , Leader = department.Leader
            , Phone = department.Phone
            , Email = department.Email
            , IsEnabled = department.IsEnabled
            , CreatedAt = department.CreatedAt
            , Children = children ?? []
        };
    }

    /// <summary>
    ///     判断目标父部门是否会形成循环引用
    /// </summary>
    /// <param name="id">当前部门主键</param>
    /// <param name="parentId">目标父部门主键</param>
    /// <returns>会形成循环引用时返回 true</returns>
    private async Task<bool> CreatesCycleAsync(
        long id
        , long? parentId
    ) {
        var currentParentId = parentId;
        while (currentParentId.HasValue) {
            if (currentParentId.Value == id) {
                return true;
            }

            currentParentId = await db
                .Departments.Where(x => x.Id == currentParentId.Value)
                .Select(x => x.ParentId)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    ///     判断父部门是否存在
    /// </summary>
    /// <param name="parentId">父部门主键</param>
    /// <returns>父部门为空或存在时返回 true</returns>
    private async Task<bool> ParentExistsAsync(long? parentId) {
        return !parentId.HasValue || await db.Departments.AnyAsync(x => x.Id == parentId.Value).ConfigureAwait(false);
    }
}