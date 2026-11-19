using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace AiAdmin.Tests;

/// <summary>
///     内存中的 WebSocket 测试替身：记录服务端发送的文本消息，并可模拟客户端关闭连接
/// </summary>
public sealed class FakeSocket : WebSocket
{
    private readonly Channel<bool> _closeSignal = Channel.CreateUnbounded<bool>();
    private readonly Lock _lock = new();
    private readonly List<JsonElement> _sent = [];
    private WebSocketState _state = WebSocketState.Open;

    /// <inheritdoc />
    public override WebSocketCloseStatus? CloseStatus => null;

    /// <inheritdoc />
    public override string? CloseStatusDescription => null;

    /// <summary>
    ///     服务端已发送的 JSON 消息快照，ping 回复等非 JSON 文本被忽略
    /// </summary>
    public IReadOnlyList<JsonElement> Sent
    {
        get
        {
            lock (_lock) {
                return [.. _sent];
            }
        }
    }

    /// <inheritdoc />
    public override WebSocketState State => _state;

    /// <inheritdoc />
    public override string? SubProtocol => null;

    /// <inheritdoc />
    public override void Abort() {
        _state = WebSocketState.Aborted;
    }

    /// <summary>
    ///     模拟客户端发送关闭帧，使服务端的接收循环结束
    /// </summary>
    public void Close() {
        _ = _closeSignal.Writer.TryWrite(true);
    }

    /// <inheritdoc />
    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus
        , string? statusDescription
        , CancellationToken cancellationToken
    ) {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus
        , string? statusDescription
        , CancellationToken cancellationToken
    ) {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose() {
        _state = WebSocketState.Closed;
    }

    /// <inheritdoc />
    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer
        , CancellationToken cancellationToken
    ) {
        _ = await _closeSignal.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, string.Empty);
    }

    /// <inheritdoc />
    public override Task SendAsync(
        ArraySegment<byte> buffer
        , WebSocketMessageType messageType
        , bool endOfMessage
        , CancellationToken cancellationToken
    ) {
        var text = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        try {
            using var document = JsonDocument.Parse(text);
            lock (_lock) {
                _sent.Add(document.RootElement.Clone());
            }
        }
        catch (JsonException) {
            // 心跳回复 pong 不是 JSON，测试不关心
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     等待服务端累计发送了指定数量的 JSON 消息，超时视为失败
    /// </summary>
    /// <param name="count">期望的消息数量</param>
    /// <returns>满足数量时完成的任务</returns>
    public async Task WaitForCountAsync(int count) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Sent.Count < count) {
            await Task.Delay(20, timeout.Token).ConfigureAwait(false);
        }
    }
}