using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace TcpServer.Models;

public class TCPServerControl
{
    private static readonly AttributeKey<string> ClientIdAttributeKey = AttributeKey<string>.ValueOf("ClientId");

    private readonly ConcurrentDictionary<string, IChannel> _connectedClients = new();

    private IEventLoopGroup? _bossGroup;
    private IEventLoopGroup? _workerGroup;
    private IChannel? _boundChannel;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    public event Action<string>? OnMessageReceived;
    public event Action<string>? OnClientConnected;
    public event Action<string>? OnClientDisconnected;

    public bool IsRunning => _isRunning;

    public async Task StartAsync(string ip, int port)
    {
        if (_isRunning) return;

        try
        {
            _bossGroup = new MultithreadEventLoopGroup(1);
            _workerGroup = new MultithreadEventLoopGroup();
            _cancellationTokenSource = new CancellationTokenSource();

            var bootstrap = new ServerBootstrap()
                .Group(_bossGroup, _workerGroup)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 100)
                .ChildOption(ChannelOption.TcpNodelay, true)
                .ChildOption(ChannelOption.SoKeepalive, true)
                .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    var pipeline = channel.Pipeline;
                    pipeline.AddLast("encoder", new StringEncoder(Encoding.UTF8));
                    pipeline.AddLast("handler", new ServerChannelHandler(this));
                }));

            _boundChannel = await bootstrap.BindAsync(IPAddress.Parse(ip), port);
            _isRunning = true;
            OnMessageReceived?.Invoke($"Server started on {ip}:{port}");
        }
        catch (Exception ex)
        {
            await StopAsync();
            OnMessageReceived?.Invoke($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning && _boundChannel is null && _bossGroup is null && _workerGroup is null) return;

        _cancellationTokenSource?.Cancel();

        foreach (var (_, channel) in _connectedClients)
        {
            try
            {
                await channel.CloseAsync();
            }
            catch
            {
                // ignored
            }
        }

        _connectedClients.Clear();

        if (_boundChannel is not null)
        {
            try
            {
                await _boundChannel.CloseAsync();
            }
            catch
            {
                // ignored
            }

            _boundChannel = null;
        }

        if (_workerGroup is not null)
        {
            await _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            _workerGroup = null;
        }

        if (_bossGroup is not null)
        {
            await _bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            _bossGroup = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _isRunning = false;
        OnMessageReceived?.Invoke("Server stopped");
    }

    private void HandleClientConnected(IChannel channel)
    {
        var clientId = Guid.NewGuid().ToString("N");
        channel.GetAttribute(ClientIdAttributeKey).Set(clientId);
        _connectedClients[clientId] = channel;

        var address = (channel.RemoteAddress as IPEndPoint)?.Address.ToString() ?? "Unknown";
        OnClientConnected?.Invoke($"Client {clientId} connected from {address}");
    }

    private void HandleClientDisconnected(IChannel channel)
    {
        var clientId = channel.GetAttribute(ClientIdAttributeKey).Get();
        if (string.IsNullOrWhiteSpace(clientId)) return;

        _connectedClients.TryRemove(clientId, out _);
        OnClientDisconnected?.Invoke($"Client {clientId} disconnected");
    }

    private async Task HandleClientMessageAsync(IChannel senderChannel, string message)
    {
        var senderId = senderChannel.GetAttribute(ClientIdAttributeKey).Get();
        if (string.IsNullOrWhiteSpace(senderId)) return;

        OnMessageReceived?.Invoke($"Received from {senderId}: {message}");
        await BroadcastMessageAsync(senderId, message);
    }

    private async Task BroadcastMessageAsync(string senderId, string message)
    {
        var payload = $"[{senderId}]: {message}";

        foreach (var (clientId, channel) in _connectedClients)
        {
            if (clientId == senderId) continue;

            try
            {
                if (!channel.Active)
                {
                    _connectedClients.TryRemove(clientId, out _);
                    continue;
                }

                await channel.WriteAndFlushAsync(payload);
            }
            catch (Exception ex)
            {
                OnMessageReceived?.Invoke($"Error broadcasting to {clientId}: {ex.Message}");
                _connectedClients.TryRemove(clientId, out _);
                try
                {
                    await channel.CloseAsync();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    public IEnumerable<string> GetConnectedClients()
    {
        return _connectedClients.Keys.ToArray();
    }

    public int GetConnectedClientCount()
    {
        return _connectedClients.Count;
    }

    private sealed class ServerChannelHandler(TCPServerControl owner) : SimpleChannelInboundHandler<IByteBuffer>
    {
        private readonly TCPServerControl _owner = owner;

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _owner.HandleClientConnected(context.Channel);
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            _owner.HandleClientDisconnected(context.Channel);
            base.ChannelInactive(context);
        }

        protected override void ChannelRead0(IChannelHandlerContext context, IByteBuffer messageBuffer)
        {
            var message = messageBuffer.ToString(Encoding.UTF8).TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(message)) return;

            _ = _owner.HandleClientMessageAsync(context.Channel, message);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            var clientId = context.Channel.GetAttribute(ClientIdAttributeKey).Get() ?? "Unknown";
            _owner.OnMessageReceived?.Invoke($"Error handling client {clientId}: {exception.Message}");
            _ = context.CloseAsync();
        }
    }
}
