using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServer.Models;

public class TCPServerControl
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new();
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
            _listener = new TcpListener(IPAddress.Parse(ip), port);
            _listener.Start();
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnMessageReceived?.Invoke($"Server started on {ip}:{port}");

            // Start accepting clients
            _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            OnMessageReceived?.Invoke($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _cancellationTokenSource?.Cancel();
        _listener?.Stop();

        // Disconnect all clients
        foreach (var client in _connectedClients.Values)
        {
            try
            {
                client.Close();
            }
            catch { }
        }
        _connectedClients.Clear();

        _isRunning = false;
        OnMessageReceived?.Invoke("Server stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                var clientId = Guid.NewGuid().ToString();
                _connectedClients[clientId] = client;

                OnClientConnected?.Invoke($"Client {clientId} connected from {((IPEndPoint)client.Client.RemoteEndPoint!).Address}");

                // Handle client in background
                _ = Task.Run(() => HandleClientAsync(clientId, client, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                OnMessageReceived?.Invoke($"Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(string clientId, TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[1024];

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                OnMessageReceived?.Invoke($"Received from {clientId}: {message}");

                // Broadcast message to all other clients
                await BroadcastMessageAsync(clientId, message, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OnMessageReceived?.Invoke($"Error handling client {clientId}: {ex.Message}");
        }
        finally
        {
            _connectedClients.TryRemove(clientId, out _);
            client.Close();
            OnClientDisconnected?.Invoke($"Client {clientId} disconnected");
        }
    }

    private async Task BroadcastMessageAsync(string senderId, string message, CancellationToken cancellationToken)
    {
        var data = Encoding.UTF8.GetBytes($"[{senderId}]: {message}");

        foreach (var kvp in _connectedClients)
        {
            if (kvp.Key == senderId) continue; // Don't send back to sender

            try
            {
                var stream = kvp.Value.GetStream();
                await stream.WriteAsync(data, cancellationToken);
            }
            catch (Exception ex)
            {
                OnMessageReceived?.Invoke($"Error broadcasting to {kvp.Key}: {ex.Message}");
                // Remove disconnected client
                _connectedClients.TryRemove(kvp.Key, out _);
            }
        }
    }

    public IEnumerable<string> GetConnectedClients()
    {
        return _connectedClients.Keys;
    }

    public int GetConnectedClientCount()
    {
        return _connectedClients.Count;
    }
}
