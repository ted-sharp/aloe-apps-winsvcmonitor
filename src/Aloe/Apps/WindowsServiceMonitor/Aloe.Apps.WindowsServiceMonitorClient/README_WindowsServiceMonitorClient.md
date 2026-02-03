# WindowsServiceMonitorClient

タスクトレイに常駐し、Windows サービスの監視状態を通知する WPF デスクトップアプリケーション

## 主な機能

- WebView2 による WindowsServiceMonitorServer の埋め込み表示
- タスクトレイへの常駐とアイコンによるステータス表示
- 必須サービス停止時の視覚的通知（トレイアイコン赤色表示）
- SignalR によるリアルタイム監視とポーリングのハイブリッド方式
- Cookie 認証の永続化（自動ログイン維持）
- Windows サービス管理ツール (services.msc) のクイック起動

## 技術構成

- **.NET 10.0-windows** WPF アプリケーション
- **WebView2** - サーバー UI の埋め込み表示
- **SignalR Client** - リアルタイム更新受信 (`/servicemonitorhub`)
- **System.Drawing** - タスクトレイアイコン管理
- **HttpClient** - サーバー API へのポーリング（SignalR フォールバック）

## ビルドと実行

```bash
# ビルド
dotnet build "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorClient/Aloe.Apps.WindowsServiceMonitorClient.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorClient/Aloe.Apps.WindowsServiceMonitorClient.csproj"
```

## 設定ファイル

### appsettings.json

```json
{
  "WindowsServiceMonitor": {
    "ServerUrl": "https://localhost:7147",
    "PollingIntervalSeconds": 30,
    "TrayIconUpdateIntervalSeconds": 30
  }
}
```

- `ServerUrl` - 接続先の WindowsServiceMonitorServer URL
- `PollingIntervalSeconds` - サービス状態のポーリング間隔（秒）
- `TrayIconUpdateIntervalSeconds` - トレイアイコン更新間隔（秒）

## アーキテクチャ

主要コンポーネント:

- **MainWindow** - WebView2 ホスト、ウィンドウ管理
- **TrayIconManager** - タスクトレイアイコン、コンテキストメニュー
- **ServiceStatusMonitor** - SignalR 接続、リアルタイム更新受信
- **WindowsServiceMonitorHttpClient** - HTTP ポーリング（SignalR 切断時のフォールバック）

トレイアイコン:
- 緑 = すべての必須サービスが正常稼働
- 赤 = 1つ以上の必須サービスが停止

ウィンドウ動作:
- 最小化時にタスクトレイに格納
- 閉じるボタン (X) で最小化（終了しない）
- コンテキストメニューから明示的に終了可能
