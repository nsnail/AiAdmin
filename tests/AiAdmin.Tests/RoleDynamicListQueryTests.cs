using System.Text.Json;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Models;
using AiAdmin.Api.Services;
using Xunit;

namespace AiAdmin.Tests;

/// <summary>
///     验证角色列表动态筛选和排序协议
/// </summary>
public sealed class RoleDynamicListQueryTests
{
    private static readonly string[] _januaryRange = ["2026-01-01T00:00:00", "2026-02-01T00:00:00"];

    /// <summary>
    ///     验证角色列表支持字符串、数值、布尔和日期范围筛选
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_FiltersSupportedRoleFieldTypes() {
        var filter = new DynamicFilter {
            Logic = "And", Filters = [
                Condition(nameof(Role.Name), "Contains", "Admin"), Condition(nameof(Role.Id), "GreaterThan", 1L),
                Condition(nameof(Role.IsEnabled), "Equal", true), Condition(nameof(Role.CreatedAt), "DateRange", _januaryRange)
            ]
        };
        Assert.Equal([2L], CreateRoles().ApplyDynamicFilter(filter).Select(x => x.Id).ToArray());
    }

    /// <summary>
    ///     验证日期范围使用左闭右开语义
    /// </summary>
    [Fact]
    public void ApplyDynamicFilter_UsesLeftClosedRightOpenDateRange() {
        var filter = Condition(nameof(Role.CreatedAt), "DateRange", _januaryRange);
        Assert.Equal([1L, 2L], CreateRoles().ApplyDynamicFilter(filter).Select(x => x.Id).ToArray());
    }

    private static DynamicFilter Condition(string field, string operation, object value) {
        return new DynamicFilter { Field = field, Operator = operation, Value = JsonSerializer.SerializeToElement(value) };
    }

    private static IQueryable<Role> CreateRoles() {
        return new[] {
            new Role { Id = 1L, Name = "Super Admin", Code = "R_SUPER", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), IsEnabled = true },
            new Role { Id = 2L, Name = "Department Admin", Code = "R_ADMIN", CreatedAt = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Local), IsEnabled = true },
            new Role { Id = 3L, Name = "User", Code = "R_USER", CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Local), IsEnabled = false }
        }.AsQueryable();
    }
}
