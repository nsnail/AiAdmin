using AiAdmin.Api.Models;

namespace AiAdmin.Api.Contracts;

/// <summary>
///     计划作业执行记录结果
/// </summary>
/// <param name="Id">执行记录编号</param>
/// <param name="StartedAt">开始时间</param>
/// <param name="FinishedAt">完成时间</param>
/// <param name="RequestUrl">请求地址</param>
/// <param name="RequestMethod">请求方法</param>
/// <param name="RequestHeaders">请求头</param>
/// <param name="RequestBody">请求体</param>
/// <param name="ResponseStatusCode">响应状态码</param>
/// <param name="ResponseHeaders">响应头</param>
/// <param name="ResponseBody">响应体</param>
/// <param name="Status">执行状态</param>
/// <param name="ErrorMessage">错误信息</param>
public sealed record ScheduledJobExecutionResult(
    long Id
    , DateTimeOffset StartedAt
    , DateTimeOffset? FinishedAt
    , string RequestUrl
    , string RequestMethod
    , string RequestHeaders
    , string RequestBody
    , int? ResponseStatusCode
    , string ResponseHeaders
    , string ResponseBody
    , ScheduledJobStatus Status
    , string ErrorMessage);