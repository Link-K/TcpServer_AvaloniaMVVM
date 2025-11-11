using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using TcpServer.Models;
using System.Threading.Tasks;

namespace TcpServer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	[ObservableProperty]
	private bool _isListening;
	[ObservableProperty]
	private string _message;
	[ObservableProperty]
	private string _ip;
	[ObservableProperty]
	private string _port;

	private readonly ObservableCollection<string> _messages = [];
	private const int MaxMessageCount = 100;
	private readonly TCPServerControl _serverControl = new();

	public MainWindowViewModel()
	{
		// 初始化属性
		IsListening = false;

		Message = "Server stop listening";
		Ip = "192.168.1.212";
		Port = "1212";

		// Subscribe to server events
		_serverControl.OnMessageReceived += msg => AddMessage(msg);
		_serverControl.OnClientConnected += msg => AddMessage(msg);
		_serverControl.OnClientDisconnected += msg => AddMessage(msg);
	}

	private void AddMessage(string msg)
	{
		var content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {msg}";
		_messages.Add(content);

		if (_messages.Count > MaxMessageCount)
			_messages.RemoveAt(0);

		UpdateMessage();
	}

	private void UpdateMessage()
	{
		Message = string.Join(Environment.NewLine, _messages);
		OnPropertyChanged(nameof(Message));
	}

	partial void OnIsListeningChanged(bool value)
	{
		if (value)
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await _serverControl.StartAsync(Ip, int.Parse(Port));
				}
				catch (Exception ex)
				{
					AddMessage($"Failed to start server: {ex.Message}");
					IsListening = false;
				}
			});
		}
		else
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await _serverControl.StopAsync();
				}
				catch (Exception ex)
				{
					AddMessage($"Failed to stop server: {ex.Message}");
				}
			});
		}
	}
}
