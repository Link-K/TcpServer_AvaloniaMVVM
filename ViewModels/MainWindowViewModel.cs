using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TcpServer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	[ObservableProperty]
	private bool _isListening;
	[ObservableProperty]
	private string _message;

	private readonly ObservableCollection<string> _messages = [];
	private const int MaxMessageCount = 100;
	public event Action? MessageUpdated;

	public MainWindowViewModel()
	{
		// 初始化属性
		IsListening = false;

		Message = "Server stop listening";
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
		MessageUpdated?.Invoke();
	}

	partial void OnIsListeningChanged(bool value)
	{
		AddMessage(value ? "Listening" : "Stop listening");
	}
}
