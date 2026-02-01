using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Aloe.Apps.WindowsServiceMonitorClient.Models;
using Aloe.Apps.WindowsServiceMonitorClient.Services;
using System.IO;

namespace Aloe.Apps.WindowsServiceMonitorClient;

public partial class MainWindow : Window
{
    private readonly WindowsServiceMonitorClientOptions _options;
    private readonly TrayIconManager _trayIconManager;
    private readonly ServiceStatusMonitor _statusMonitor;
    private System.Threading.Timer? _pollingTimer;
    private bool _isClosing;
    private readonly Dictionary<string, WindowsServiceMonitorLib.Models.ServiceInfo> _serviceStatuses = new();

    public MainWindow()
    {
        InitializeComponent();

        // 設定読み込み
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _options = configuration.GetSection("WindowsServiceMonitor").Get<WindowsServiceMonitorClientOptions>()
            ?? new WindowsServiceMonitorClientOptions();

        // トレイアイコン初期化
        _trayIconManager = new TrayIconManager();
        _trayIconManager.ShowWindowRequested += TrayIcon_ShowWindowRequested;
        _trayIconManager.OpenServicesRequested += TrayIcon_OpenServicesRequested;
        _trayIconManager.ExitRequested += TrayIcon_ExitRequested;

        // SignalR監視開始
        _statusMonitor = new ServiceStatusMonitor(_options);
        _statusMonitor.ServiceStatusUpdated += StatusMonitor_ServiceStatusUpdated;
        _statusMonitor.CriticalServicesStatusChanged += StatusMonitor_CriticalServicesStatusChanged;
        _ = _statusMonitor.StartAsync();

        // ポーリングタイマー
        _pollingTimer = new System.Threading.Timer(
            PollCriticalServices,
            null,
            TimeSpan.FromSeconds(_options.TrayIconUpdateIntervalSeconds),
            TimeSpan.FromSeconds(_options.TrayIconUpdateIntervalSeconds)
        );

        InitializeWebView2();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Title = $"サービス監視 - {_options.ServerUrl}";
    }

    private async void InitializeWebView2()
    {
        try
        {
            // クッキーを永続化するためのUserDataFolderを設定
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsServiceMonitorClient");

            var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder);

            await webView.EnsureCoreWebView2Async(environment);
            webView.CoreWebView2.Navigate($"{_options.ServerUrl}/");
            webView.NavigationCompleted += WebView_NavigationCompleted;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"WebView2の初期化に失敗しました: {ex.Message}\n\nMicrosoft Edge WebView2 Runtimeがインストールされているか確認してください。",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void WebView_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        // パスワードはクライアント側に保存しないため、手動ログインを行う
    }

    private async void PollCriticalServices(object? state)
    {
        try
        {
            using var httpClient = new WindowsServiceMonitorHttpClient(_options);
            var services = await httpClient.GetServicesAsync();

            if (services != null)
            {
                lock (_serviceStatuses)
                {
                    _serviceStatuses.Clear();
                    foreach (var service in services)
                    {
                        _serviceStatuses[service.ServiceName] = service;
                    }
                }

                UpdateCriticalServicesStatus();
            }
        }
        catch (Exception)
        {
            // ポーリングエラーは無視（サーバーオフライン時など）
        }
    }

    private void StatusMonitor_ServiceStatusUpdated(object? sender, WindowsServiceMonitorLib.Models.ServiceInfo service)
    {
        lock (_serviceStatuses)
        {
            _serviceStatuses[service.ServiceName] = service;
        }

        UpdateCriticalServicesStatus();
    }

    private void UpdateCriticalServicesStatus()
    {
        bool hasCriticalDown;
        lock (_serviceStatuses)
        {
            hasCriticalDown = _serviceStatuses.Values.Any(s =>
                s.IsCritical && s.Status != WindowsServiceMonitorLib.Models.ServiceStatus.Running);
        }

        _statusMonitor.UpdateCriticalServicesStatus(hasCriticalDown);
    }

    private void StatusMonitor_CriticalServicesStatusChanged(object? sender, bool hasCriticalDown)
    {
        Dispatcher.Invoke(() =>
        {
            if (hasCriticalDown)
                _trayIconManager.SetStatusRed();
            else
                _trayIconManager.SetStatusGreen();
        });
    }

    private void TrayIcon_ShowWindowRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();

                // ウィンドウを再表示したときに明示的にリフレッシュ
                webView?.CoreWebView2?.Reload();
            }
        });
    }

    private void TrayIcon_OpenServicesRequested(object? sender, EventArgs e)
    {
        OpenServicesManagement();
    }

    private void TrayIcon_ExitRequested(object? sender, EventArgs e)
    {
        _isClosing = true;
        Close();
    }

    private void MenuOpenServices_Click(object sender, RoutedEventArgs e)
    {
        OpenServicesManagement();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        _isClosing = true;
        Close();
    }

    private void OpenServicesManagement()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "services.msc",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"サービス管理ツールを開けませんでした: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isClosing)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }
        else
        {
            // WebView2のクリーンアップ（クッキーをディスクに書き込むために必要）
            webView?.Dispose();

            _pollingTimer?.Dispose();
            _trayIconManager.Dispose();
            _ = _statusMonitor.DisposeAsync();
        }
    }
}
