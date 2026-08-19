using System.Reflection;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Data;
using AiAdmin.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

// 通过 MVC 动作描述器反射系统接口，并将变化同步到接口权限表。
namespace AiAdmin.Api.Services;

/// <summary>
///     反射并同步系统 Web API 到接口权限表
/// </summary>
public sealed class ApiEndpointSyncService(
    AppDbContext db
    , IActionDescriptorCollectionProvider actionDescriptorProvider
    , ApiPermissionCache permissionCache)
{
    /// <summary>
    ///     执行接口新增、更新和删除同步
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步统计结果</returns>
    public async Task<ApiSyncResult> SyncAsync(CancellationToken cancellationToken = default) {
        var reflected = ReflectEndpoints();
        var existing = await db.ApiEndpoints.Include(x => x.RoleApis).ToListAsync(cancellationToken).ConfigureAwait(false);
        var existingByKey = existing.ToDictionary(x => ApiEndpointKey.Create(x.Method, x.Path), StringComparer.Ordinal);
        var reflectedKeys = reflected.Keys.ToHashSet(StringComparer.Ordinal);
        var added = 0;
        var updated = 0;

        foreach (var (key, item) in reflected) {
            if (existingByKey.TryGetValue(key, out var endpoint)) {
                if (endpoint.Name != item.Name
                    || endpoint.Controller != item.Controller
                    || endpoint.ControllerName != item.ControllerName
                    || endpoint.Action != item.Action
                    || endpoint.AllowAnonymous != item.AllowAnonymous) {
                    updated++;
                }

                endpoint.Name = item.Name;
                endpoint.Method = item.Method;
                endpoint.Path = item.Path;
                endpoint.Controller = item.Controller;
                endpoint.ControllerName = item.ControllerName;
                endpoint.Action = item.Action;
                endpoint.AllowAnonymous = item.AllowAnonymous;
                continue;
            }

            endpoint = item;
            _ = await db.ApiEndpoints.AddAsync(endpoint, cancellationToken).ConfigureAwait(false);
            existingByKey.Add(key, endpoint);
            added++;
        }

        var stale = existing.Where(x => !reflectedKeys.Contains(ApiEndpointKey.Create(x.Method, x.Path))).ToList();
        db.ApiEndpoints.RemoveRange(stale);
        _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var activeEndpoints = await db.ApiEndpoints.ToListAsync(cancellationToken).ConfigureAwait(false);
        _ = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        permissionCache.Invalidate();
        return new ApiSyncResult(added, updated, stale.Count, activeEndpoints.Count);
    }

    private Dictionary<string, ApiEndpoint> ReflectEndpoints() {
        // 读取控制器和操作上的描述、HTTP 方法及匿名特性。
        var result = new Dictionary<string, ApiEndpoint>(StringComparer.Ordinal);
        foreach (var action in actionDescriptorProvider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()) {
            var template = action.AttributeRouteInfo?.Template;
            if (string.IsNullOrWhiteSpace(template)) {
                continue;
            }

            var description = action.MethodInfo.GetCustomAttribute<ApiDescriptionAttribute>()?.Description
                              ?? throw new InvalidOperationException(
                                  $"Action {action.ControllerName}.{action.ActionName} is missing ApiDescriptionAttribute"
                              );
            var controllerDescription = action.ControllerTypeInfo.GetCustomAttribute<ApiDescriptionAttribute>()?.Description
                                        ?? throw new InvalidOperationException(
                                            $"Controller {action.ControllerName} is missing ApiDescriptionAttribute"
                                        );
            var methods = action
                              .ActionConstraints?.OfType<HttpMethodActionConstraint>()
                              .SelectMany(x => x.HttpMethods)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToArray()
                          ?? [];
            foreach (var method in methods) {
                var normalizedPath = ApiEndpointKey.NormalizePath(template);
                var endpoint = new ApiEndpoint
                {
                    Name = description
                    , AllowAnonymous = action.EndpointMetadata?.OfType<IAllowAnonymous>().Any() == true
                    , Method = method.ToUpperInvariant()
                    , Path = normalizedPath
                    , Controller = action.ControllerName
                    , ControllerName = controllerDescription
                    , Action = action.MethodInfo.Name
                };
                result[ApiEndpointKey.Create(endpoint.Method, endpoint.Path)] = endpoint;
            }
        }

        return result;
    }
}