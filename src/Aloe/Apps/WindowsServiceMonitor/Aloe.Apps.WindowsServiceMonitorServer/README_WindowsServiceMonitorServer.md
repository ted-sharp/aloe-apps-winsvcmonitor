# WindowsServiceMonitorServer

ブラウザからWindows サービスの監視と操作を行うための Blazor Server アプリケーション

## 主な機能

- Windows サービスのリアルタイム監視（稼働状況、プロセスID、メモリ使用量、稼働時間）
- サービスの操作（開始、停止、再起動）
- サービスの OS レベル登録・解除（sc create / sc delete）
- Cookie 認証によるアクセス制御
- SignalR によるリアルタイム更新

## 技術構成

- **.NET 10.0** Blazor Server (Interactive Server Rendering)
- **System.ServiceProcess.ServiceController** - サービス状態取得
- **Win32 API (advapi32.dll)** - プロセス詳細情報取得（PID、メモリ、稼働時間）
- **SignalR** - リアルタイム更新配信 (`/servicemonitorhub`)
- **Pico.css** - UI フレームワーク（classless）

## ビルドと実行

```bash
# ビルド
dotnet build "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorServer/Aloe.Apps.WindowsServiceMonitorServer.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorServer/Aloe.Apps.WindowsServiceMonitorServer.csproj"
```

起動後:
- HTTP: http://localhost:5298
- HTTPS: https://localhost:7147

## 設定ファイル

### appsettings.json
- 認証パスワード (`Authentication:Password`)
- ログ設定
- 監視対象サービスの定義（`WindowsServiceMonitor:MonitoredServices`）

### appsettings.services.json
- 監視対象サービスの詳細リスト
- サービス登録時のデフォルト設定（`serviceDefaults`）
- 各サービスの構成（`serviceName`, `displayName`, `description`, `binaryPath`, `critical`）

## 認証

Cookie 認証を使用。ログインエンドポイント: `POST /api/login`
- セッション有効期間: 60分（スライディング）
- HttpOnly, SameSite=Strict

## UI ページ

- `/` - ホーム（ログインページ）
- `/services` - サービス一覧（カスタムCSS使用）
- `/services2` - サービス一覧（Pure Pico.css）

ボタン配色は IEC 60073 に準拠:
- 緑 = 開始
- 赤 = 停止
- 黄 = 再起動

## アーキテクチャ

主要インターフェース:
- `IServiceManager` - サービス操作の統括
- `IServiceRegistrar` - OS レベルのサービス登録（sc.exe 実行）
- `IWin32ServiceApi` - Win32 API 呼び出し
- `IMonitoredServiceRepository` - JSON ファイル CRUD

バックグラウンド監視:
- `BackgroundWindowsServiceMonitor` - 定期的にサービス状態をポーリング
- `WindowsServiceMonitorHub` - SignalR 経由でクライアントに状態を配信

