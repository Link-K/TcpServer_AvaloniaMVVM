using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
	}

	private void MsgBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		// 滚动到最后一行
		MsgScrollViewer.ScrollToEnd();
	}

	private void AddConnect_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		// 创建新的按钮
		var newButton = new Button
		{
			Height = 40,
			Width = 40,
			Content = new PathIcon
			{
				Data = (StreamGeometry)this.FindResource("organization_regular")
			}
		};

		// 将新按钮添加到 PaneStackPanel
		PaneStackPanel.Children.Add(newButton);
	}
}
