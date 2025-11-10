using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using TcpServer.ViewModels;

namespace TcpServer.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		// 检查 DataContext 是否为 null 或者不是 MainWindowViewModel 类型
		DataContext ??= new MainWindowViewModel();

		// 检查 DataContext 是否为 MainWindowViewModel 类型
		if (DataContext is not MainWindowViewModel viewModel)
		{
			throw new InvalidOperationException("DataContext must be set to an instance of MainWindowViewModel.");
		}

		// 订阅 MessageUpdated 事件
		viewModel.MessageUpdated += OnMessageUpdated;
	}

	private void MsgBox_TextChanged(object sender, Avalonia.Controls.TextChangedEventArgs e)
	{
		// 滚动到最后一行
		MsgScrollViewer.ScrollToEnd();
	}

	private void OnMessageUpdated()
	{
		// 滚动到最后一行
		MsgScrollViewer.ScrollToEnd();
	}
}
