using System.Diagnostics;
using AiAdmin.Api.Attributes;
using AiAdmin.Api.Contracts;
using AiAdmin.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAdmin.Api.Controllers;

/// <summary>
///     系统安全停机控制器，用于发版前排空流量
/// </summary>
/// <param name="realtime">实时连接管理器，关闸时用于主动关闭长连接</param>
[ApiController]
[ApiDescription("System shutdown management")]
[Route("api/system/shutdown")]
public sealed class SystemShutdownController(RealtimeConnectionManager realtime) : ControllerBase
{
    /// <summary>
    ///     取消安全停机，重新打开闸门恢复接收新流量
    /// </summary>
    /// <returns>取消结果</returns>
    [HttpPost("cancel")]
    [ApiDescription("Cancel safe shutdown")]
    public ActionResult<ApiResponse<object>> Cancel() {
        ShutdownGate.Open();
        return Ok(ApiResponse<object>.Ok(new { }, "Shutdown cancelled"));
    }

    /// <summary>
    ///     关闭流量闸门（只出不进）并主动关闭实时长连接，等待在途工作归零并保持稳定，返回即表示系统已空闲、可以终止进程
    /// </summary>
    /// <param name="request">安全停机请求</param>
    /// <param name="cancellationToken">取消操作令牌</param>
    /// <returns>停机准备结果</returns>
    [HttpPost("prepare")]
    [ApiDescription("Prepare safe shutdown")]
    public async Task<ActionResult<ApiResponse<SafeShutdownResult>>> PrepareAsync(
        [FromBody] SafeShutdownRequest request
        , CancellationToken cancellationToken
    ) {
        var stopwatch = Stopwatch.StartNew();
        var sampler = new ProcessDiagnosticsSampler();
        ShutdownGate.Close();
        await realtime.CloseAllAsync().ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
        try {
            if (!await ShutdownGate.WaitForIdleAsync(TimeSpan.FromSeconds(request.StableSeconds), timeout.Token).ConfigureAwait(false)) {
                return Conflict(new ApiResponse<object>(409, "Shutdown was cancelled", null));
            }
        }
        catch (OperationCanceledException) {
            // 超时或调用方断开时保持闸门关闭，由调用方决定重试或调用 cancel
            var pending = new SafeShutdownResult(stopwatch.ElapsedMilliseconds, ShutdownGate.Snapshot(), sampler.Sample());
            return StatusCode(
                cancellationToken.IsCancellationRequested ? 499 : StatusCodes.Status408RequestTimeout
                , new ApiResponse<SafeShutdownResult>(cancellationToken.IsCancellationRequested ? 499 : 408, "Not idle yet", pending)
            );
        }

        return Ok(
            ApiResponse<SafeShutdownResult>.Ok(new SafeShutdownResult(stopwatch.ElapsedMilliseconds, ShutdownGate.Snapshot(), sampler.Sample()))
        );
    }
}