# Aloe Apps ServiceMonitor

Windows サービスの監視と管理を行うための .NET 10.0 アプリケーション群

## 概要

ServiceMonitor は、Windows サービスのリアルタイム監視、操作、管理を Web UI とデスクトップクライアントの両方から行えるシステムです。

### 構成プロジェクト

| プロジェクト | 種類 | 説明 |
|---|---|---|
| **ServiceMonitorServer** | Blazor Server | Web UI によるサービス監視・操作 |
| **ServiceMonitorClient** | WPF | タスクトレイ常駐型デスクトップクライアント |
| **ServiceMonitorLib** | Class Library | サービス管理コアロジック |
| **DummyService** | Windows Service | テスト用ダミーサービス |

## 主な機能

### ServiceMonitorServer (Web)
- ブラウザからのサービス監視（プロセスID、メモリ使用量、稼働時間）
- サービス操作（開始、停止、再起動）
- OS レベルのサービス登録・解除（sc create / sc delete）
- Cookie 認証によるアクセス制御
- SignalR リアルタイム更新

### ServiceMonitorClient (デスクトップ)
- タスクトレイ常駐
- 必須サービス停止時の視覚的通知（アイコン赤色表示）
- WebView2 による Server UI の埋め込み表示
- Cookie 認証の永続化
- Windows サービス管理ツール (services.msc) のクイック起動

### ServiceMonitorLib (コアライブラリ)
- `System.ServiceProcess.ServiceController` による状態取得
- Win32 API (advapi32.dll) によるプロセス詳細情報取得
- JSON ファイルによる監視対象サービス設定管理
- スレッドセーフな設定ファイル操作

## ビルドと実行

### 前提条件
- .NET 10.0 SDK
- Windows OS（サービス管理機能のため）
- Microsoft Edge WebView2 Runtime（クライアントのみ）

### サーバーの起動

```bash
# ビルド
dotnet build "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitorServer/Aloe.Apps.ServiceMonitorServer.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitorServer/Aloe.Apps.ServiceMonitorServer.csproj"
```

起動後:
- HTTP: http://localhost:5298
- HTTPS: https://localhost:7147

### クライアントの起動

```bash
# ビルド
dotnet build "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitor/Aloe.Apps.ServiceMonitorClient/Aloe.Apps.ServiceMonitorClient.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitor/Aloe.Apps.ServiceMonitorClient/Aloe.Apps.ServiceMonitorClient.csproj"
```

クライアント設定（`appsettings.json`）で接続先サーバー URL を指定します。

### テスト用ダミーサービスの登録

```bash
# ビルド（Native AOT）
dotnet publish "src/Aloe/Apps/ServiceMonitor/Aloe.Apps.DummyService/Aloe.Apps.DummyService.csproj" -c Release

# サービス登録（管理者権限必要）
sc create DummyService binPath="C:\path\to\DummyService.exe"
sc start DummyService
```

## アーキテクチャ

### データフロー

```
[appsettings.services.json] ─┐
                              ├─> ServiceManager ─> ServiceController (Win32)
[appsettings.json]           ─┘                   └> Win32ServiceApi (P/Invoke)

```

### 主要インターフェース

- `IServiceManager` - サービス操作の統括（開始/停止/再起動/削除）
- `IServiceRegistrar` - OS レベルのサービス登録（sc.exe 実行）
- `IWin32ServiceApi` - Win32 API 呼び出し（PID、メモリ、稼働時間取得）
- `IMonitoredServiceRepository` - JSON ファイル CRUD（セマフォによるスレッドセーフ制御）

### リアルタイム更新

- **Server**: `BackgroundServiceMonitor` が定期ポーリング → `ServiceMonitorHub` (SignalR) で配信
- **Client**: SignalR 受信 + HTTP ポーリング（フォールバック）

## 設定ファイル

### appsettings.json (Server)
- 認証パスワード (`Authentication:Password`)
- 監視対象サービス一覧（`ServiceMonitor:MonitoredServices`）

### appsettings.services.json (Server)
- 監視対象サービスの詳細設定
- サービス登録時のデフォルト設定（`serviceDefaults`）

### appsettings.json (Client)
- 接続先サーバー URL (`ServiceMonitor:ServerUrl`)
- ポーリング間隔設定

## 技術スタック

- **.NET 10.0** - すべてのプロジェクト
- **Blazor Server** - Interactive Server Rendering
- **WPF** - デスクトップクライアント
- **SignalR** - リアルタイム通信
- **WebView2** - Web UI 埋め込み
- **Pico.css** - Web UI フレームワーク（classless）
- **System.ServiceProcess** - Windows サービス操作
- **Win32 API (advapi32.dll)** - プロセス詳細情報

## 詳細ドキュメント

各プロジェクトの詳細については、以下のドキュメントを参照してください。

- [ServiceMonitorServer](src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitorServer/README_ServiceMonitorServer.md)
- [ServiceMonitorClient](src/Aloe/Apps/ServiceMonitor/Aloe.Apps.ServiceMonitor/Aloe.Apps.ServiceMonitorClient/README_ServiceMonitorClient.md)

## ライセンス

このプロジェクトは個人利用目的で開発されています。
