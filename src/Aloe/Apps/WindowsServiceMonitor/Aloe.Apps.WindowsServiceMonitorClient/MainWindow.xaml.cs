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
        this.InitializeComponent();

        // 設定読み込み
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        this._options = configuration.GetSection("WindowsServiceMonitor").Get<WindowsServiceMonitorClientOptions>()
            ?? new WindowsServiceMonitorClientOptions();

        // トレイアイコン初期化
        this._trayIconManager = new TrayIconManager();
        this._trayIconManager.ShowWindowRequested += this.TrayIcon_ShowWindowRequested;
        this._trayIconManager.OpenServicesRequested += this.TrayIcon_OpenServicesRequested;
        this._trayIconManager.ExitRequested += this.TrayIcon_ExitRequested;

        // SignalR監視開始
        this._statusMonitor = new ServiceStatusMonitor(this._options);
        this._statusMonitor.ServiceStatusUpdated += this.StatusMonitor_ServiceStatusUpdated;
        this._statusMonitor.CriticalServicesStatusChanged += this.StatusMonitor_CriticalServicesStatusChanged;
        _ = this._statusMonitor.StartAsync();

        // ポーリングタイマー
        this._pollingTimer = new System.Threading.Timer(
            this.PollCriticalServices,
            null,
            TimeSpan.FromSeconds(this._options.TrayIconUpdateIntervalSeconds),
            TimeSpan.FromSeconds(this._options.TrayIconUpdateIntervalSeconds)
        );

        this.InitializeWebView2();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.Title = $"サービス監視 - {this._options.ServerUrl}";
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

            await this.webView.EnsureCoreWebView2Async(environment);
            this.webView.CoreWebView2.Navigate($"{this._options.ServerUrl}/");
            this.webView.NavigationCompleted += this.WebView_NavigationCompleted;
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
            using var httpClient = new WindowsServiceMonitorHttpClient(this._options);
            var services = await httpClient.GetServicesAsync();

            if (services != null)
            {
                lock (this._serviceStatuses)
                {
                    this._serviceStatuses.Clear();
                    foreach (var service in services)
                    {
                        this._serviceStatuses[service.ServiceName] = service;
                    }
                }

                this.UpdateCriticalServicesStatus();
            }
        }
        catch (Exception)
        {
            // ポーリングエラーは無視（サーバーオフライン時など）
        }
    }

    private void StatusMonitor_ServiceStatusUpdated(object? sender, WindowsServiceMonitorLib.Models.ServiceInfo service)
    {
        lock (this._serviceStatuses)
        {
            this._serviceStatuses[service.ServiceName] = service;
        }

        this.UpdateCriticalServicesStatus();
    }

    private void UpdateCriticalServicesStatus()
    {
        bool hasCriticalDown;
        lock (this._serviceStatuses)
        {
            hasCriticalDown = this._serviceStatuses.Values.Any(s =>
                s.IsCritical && s.Status != WindowsServiceMonitorLib.Models.ServiceStatus.Running);
        }

        this._statusMonitor.UpdateCriticalServicesStatus(hasCriticalDown);
    }

    private void StatusMonitor_CriticalServicesStatusChanged(object? sender, bool hasCriticalDown)
    {
        this.Dispatcher.Invoke(() =>
        {
            if (hasCriticalDown)
                this._trayIconManager.SetStatusRed();
            else
                this._trayIconManager.SetStatusGreen();
        });
    }

    private void TrayIcon_ShowWindowRequested(object? sender, EventArgs e)
    {
        this.Dispatcher.Invoke(() =>
        {
            if (this.IsVisible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();

                // ウィンドウを再表示したときに明示的にリフレッシュ
                this.webView?.CoreWebView2?.Reload();
            }
        });
    }

    private void TrayIcon_OpenServicesRequested(object? sender, EventArgs e)
    {
        this.OpenServicesManagement();
    }

    private void TrayIcon_ExitRequested(object? sender, EventArgs e)
    {
        this._isClosing = true;
        this.Close();
    }

    private void MenuOpenServices_Click(object sender, RoutedEventArgs e)
    {
        this.OpenServicesManagement();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        this._isClosing = true;
        this.Close();
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
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!this._isClosing)
        {
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
        }
        else
        {
            // WebView2のクリーンアップ（クッキーをディスクに書き込むために必要）
            this.webView?.Dispose();

            this._pollingTimer?.Dispose();
            this._trayIconManager.Dispose();
            this._statusMonitor.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
        }
    }
}
