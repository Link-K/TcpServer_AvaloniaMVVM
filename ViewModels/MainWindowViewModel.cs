using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TcpServer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isListening;

    [ObservableProperty]
    private string message;

    public MainWindowViewModel()
    {
        // 初始化属性
        IsListening = false;
        Message = "";
    }

    partial void OnIsListeningChanged(bool value)
    {
        // 在这里添加你的逻辑
        Console.WriteLine($"State changed: {value}");
    }

    partial void OnMessageChanged(string value)
    {
        // 在这里添加你的逻辑
        Console.WriteLine($"Message changed: {value}");
    }
}
