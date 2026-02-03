# Aloe Apps WindowsServiceMonitor

Windows サービスの監視と管理を行うための .NET 10.0 アプリケーション群

## 概要

WindowsServiceMonitor は、Windows サービスのリアルタイム監視、操作、管理を Web UI とデスクトップクライアントの両方から行えるシステムです。

### 構成プロジェクト

| プロジェクト | 種類 | 説明 |
|---|---|---|
| **WindowsServiceMonitorServer** | Blazor Server | Web UI によるサービス監視・操作 |
| **WindowsServiceMonitorClient** | WPF | タスクトレイ常駐型デスクトップクライアント |
| **WindowsServiceMonitorLib** | Class Library | サービス管理コアロジック |
| **DummyService** | Windows Service | テスト用ダミーサービス |

## 主な機能

### WindowsServiceMonitorServer (Web)

#### サービス監視・操作（/services）
- リアルタイムサービス監視（プロセスID、メモリ使用量、稼働時間、依存サービス数）
- サービス操作（開始、停止、再起動）
- 一括操作（全サービスの開始/停止/再起動、全サービスのステータス更新）
- OS レベルのサービス登録・解除（sc create / sc delete）
- 最終ステータス変更時刻の表示（相対時間表示）
- サービス詳細情報パネル（選択したサービスの詳細を表示）
- IEC 60073 準拠のボタンカラー（緑=開始、赤=停止、黄=再起動）

#### 操作ログ・監査機能（/logs）
- **3種類のログ記録**:
  - **アクセスログ**: ログイン/ログアウトイベント（IP アドレス、タイムスタンプ、HTTP メソッド）
  - **操作ログ**: サービス操作（開始/停止/再起動/登録/削除）の成功/失敗記録
  - **ステータス変更ログ**: バックグラウンド監視による予期しないサービス状態変更の検出

- **ログ閲覧 UI**:
  - 3つのタブでログ表示（全ログ/ログタイプ別/日付範囲別）
  - ログタイプ・日付範囲によるフィルタリング
  - ページネーション対応（50件/ページ）
  - 色分けされたログタイプバッジ（青=アクセス、赤=操作、黄=ステータス変更）

- **ログストレージ**:
  - 日付ベースの JSON ファイル分割（`custom_logs_YYYYMMDD.json`）
  - ファイルサイズ上限到達時の自動連番（最大10,000ログ/ファイル）
  - スレッドセーフな並行アクセス制御（SemaphoreSlim）
  - 古いログの自動削除機能

#### 認証・セキュリティ
- Cookie ベース認証（60分スライディング有効期限）
- ログイン/ログアウトエンドポイント（POST /api/login, POST /logout）
- HttpOnly, SameSite=Lax クッキー
- すべてのサービス操作ページで認証必須

#### リアルタイム更新
- SignalR による自動プッシュ通知（`WindowsServiceMonitorHub` at `/servicemonitorhub`）
- バックグラウンド監視サービス（`BackgroundWindowsServiceMonitor`）による定期ポーリング
- 操作トラッキング機能による意図的な変更と予期しない変更の区別（30秒ウィンドウ）

#### Web API
- `GET /api/services` - 全監視サービスのステータス取得
- `GET /api/services/{serviceName}` - 特定サービスの詳細取得
- 認証必須（Authorize 属性）

### WindowsServiceMonitorClient (デスクトップ)
- **タスクトレイ常駐**:
  - 必須サービス停止時の視覚的通知（アイコンが緑→赤に変化）
  - ステータステキスト表示（「サービス監視 - 正常」/「サービス監視 - 必須サービス停止」）
  - コンテキストメニュー（ウィンドウ表示/非表示、services.msc 起動、終了）

- **WebView2 統合**:
  - Server UI の埋め込みブラウザ表示
  - Cookie 永続化による自動ログイン
  - ウィンドウ最小化・閉じるボタンでトレイに格納

- **ハイブリッド監視**:
  - SignalR リアルタイム更新 + HTTP ポーリングフォールバック
  - 自動再接続処理
  - 設定可能なポーリング間隔

### WindowsServiceMonitorLib (コアライブラリ)
- `System.ServiceProcess.ServiceController` による状態取得
- Win32 API (advapi32.dll) によるプロセス詳細情報取得（PID、メモリ、稼働時間）
- JSON ファイルによる監視対象サービス設定管理
- スレッドセーフな設定ファイル・ログファイル操作（SemaphoreSlim）
- 操作トラッカー（`OperationTracker`）による重複ステータス変更ログの防止

## ビルドと実行

### 前提条件
- .NET 10.0 SDK
- Windows OS（サービス管理機能のため）
- Microsoft Edge WebView2 Runtime（クライアントのみ）

### サーバーの起動

```bash
# ビルド
dotnet build "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorServer/Aloe.Apps.WindowsServiceMonitorServer.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorServer/Aloe.Apps.WindowsServiceMonitorServer.csproj"
```

起動後:
- HTTP: http://localhost:5298
- HTTPS: https://localhost:7147

### クライアントの起動

```bash
# ビルド
dotnet build "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorClient/Aloe.Apps.WindowsServiceMonitorClient.csproj"

# 実行
dotnet run --project "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorClient/Aloe.Apps.WindowsServiceMonitorClient.csproj"
```

クライアント設定（`appsettings.json`）で接続先サーバー URL を指定します。

### テスト用ダミーサービスの登録

```bash
# ビルド（Native AOT）
dotnet publish "src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.DummyService/Aloe.Apps.DummyService.csproj" -c Release

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
                                                    └> OperationTracker (操作追跡)

BackgroundWindowsServiceMonitor ─> ステータス変更検出 ─> LogRepository ─> custom_logs_YYYYMMDD.json
                                ↓
                        WindowsServiceMonitorHub (SignalR)
                                ↓
                        [Blazor UI / WPF Client]
```

### 主要インターフェース

| インターフェース | 実装 | 責務 |
|---|---|---|
| `IServiceManager` | `ServiceManager` | サービス操作の統括（開始/停止/再起動/登録/削除）、設定ソースの統合 |
| `IServiceRegistrar` | `ServiceRegistrar` | OS レベルのサービス登録・解除（sc.exe 実行） |
| `IWin32ServiceApi` | `Win32ServiceApi` | Win32 API (advapi32.dll) 呼び出し（PID、メモリ、稼働時間取得） |
| `IMonitoredServiceRepository` | `JsonMonitoredServiceRepository` | 監視対象サービス設定の CRUD（セマフォによるスレッドセーフ制御） |
| `ILogRepository` | `JsonLogRepository` | 操作ログの記録・取得・削除（日付ベースファイル分割、ページネーション対応） |
| `IOperationTracker` | `OperationTracker` | サービス操作の追跡、重複ステータス変更ログの防止 |

### リアルタイム更新

- **Server**:
  - `BackgroundWindowsServiceMonitor` が定期ポーリング（設定可能な間隔）
  - ステータス変更検出時、`OperationTracker` と照合して意図的変更か判定
  - `WindowsServiceMonitorHub` (SignalR at `/servicemonitorhub`) で全クライアントに配信
  - `ServiceStatusUpdated` メッセージを "ServiceMonitors" グループにブロードキャスト

- **Client (Blazor Web UI)**:
  - SignalR 接続による即座のステータス更新受信
  - `PeriodicTimer` (3秒間隔) によるポーリングバックアップ
  - 自動再接続処理

- **Client (WPF Desktop)**:
  - SignalR 受信 + HTTP ポーリング（フォールバック）
  - 自動再接続とエラーハンドリング
  - トレイアイコンの色更新（緑=正常、赤=必須サービス停止）

### 操作ログとステータス変更の追跡

- **OperationTracker**: サービス操作（開始/停止/再起動）を記録し、30秒間の期待される状態遷移を追跡
- **BackgroundWindowsServiceMonitor**: 検出したステータス変更が `OperationTracker` に記録された操作によるものか判定
  - 意図的な変更: ログ記録せず（操作ログで既に記録済み）
  - 予期しない変更: ステータス変更ログとして記録（旧ステータス → 新ステータス）

## 設定ファイル

### appsettings.json (Server)

```jsonc
{
  "Authentication": {
    "Password": "your-password-here"  // ログインパスワード
  },
  "WindowsServiceMonitor": {
    "MonitoredServices": ["ServiceName1", "ServiceName2"],  // 監視対象サービス初期リスト
    "PollingIntervalSeconds": 5,  // バックグラウンド監視のポーリング間隔
    "ServiceOperationTimeoutSeconds": 30,  // サービス操作のタイムアウト
    "EnableAutoRefresh": true  // バックグラウンド自動更新の有効化
  }
}
```

### appsettings.services.json (Server)

```jsonc
{
  "serviceDefaults": {
    "account": "LocalSystem",  // サービス登録時のデフォルトアカウント
    "password": ""  // サービス登録時のデフォルトパスワード
  },
  "services": [
    {
      "serviceName": "MyService",  // サービス識別子
      "displayName": "My Service",  // 表示名
      "description": "サービスの説明",  // 説明文
      "binaryPath": "C:\\path\\to\\service.exe",  // 実行ファイルパス（主）
      "binaryPathAlt": "C:\\alternate\\path\\to\\service.exe",  // 代替パス
      "critical": true  // 必須サービスフラグ（トレイ監視用）
    }
  ]
}
```

### appsettings.json (Client)

```jsonc
{
  "WindowsServiceMonitor": {
    "ServerUrl": "https://localhost:7147",  // 接続先サーバー URL
    "PollingIntervalSeconds": 10,  // HTTP ポーリング間隔
    "TrayIconUpdateIntervalSeconds": 5  // トレイアイコン更新間隔
  }
}
```

## 技術スタック

- **.NET 10.0** - すべてのプロジェクト
- **Blazor Server** - Interactive Server Rendering
- **WPF** - デスクトップクライアント (.NET 10.0-windows)
- **SignalR** - リアルタイム通信（ハイブリッド: WebSocket + HTTP ポーリング）
- **WebView2** - Web UI 埋め込み（WPF クライアント）
- **Pico.css** - Web UI フレームワーク（classless/セマンティック HTML）
- **System.ServiceProcess** - Windows サービス操作
- **Win32 API (advapi32.dll)** - プロセス詳細情報（PID、メモリ、稼働時間）
- **Serilog** - 構造化ログ（ファイル出力、日次ローテーション）
- **JSON ファイルストレージ** - 設定・ログの永続化
- **Cookie 認証** - セッション管理（60分スライディング有効期限）

## 詳細ドキュメント

各プロジェクトの詳細については、以下のドキュメントを参照してください。

- [WindowsServiceMonitorServer](src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorServer/README_WindowsServiceMonitorServer.md)
- [WindowsServiceMonitorClient](src/Aloe/Apps/WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitor/Aloe.Apps.WindowsServiceMonitorClient/README_WindowsServiceMonitorClient.md)

## 開発規約

- **UI テキストとログメッセージ**: 日本語を使用
- **コード識別子と型名**: 英語を使用
- **ボタンカラー**: IEC 60073 準拠
  - 緑 (success) = サービス開始
  - 赤 (danger) = サービス停止
  - 黄 (warning) = サービス再起動
- **サービス「登録/解除」の意味**: OS レベルの `sc create` / `sc delete`（JSON リスト管理とは別）
- **CSS フレームワーク**: Pico.css（classless/セマンティック HTML）
- **カスタムスタイル**: `wwwroot/app.css`（ボタンカラー、ステータスバッジ、テーブルレイアウト）

## パフォーマンスとスケーラビリティ

- **スレッドセーフ**: すべてのファイル操作で `SemaphoreSlim` 使用
- **リソース監視**: Win32 API による正確なメモリ・稼働時間計測
- **ログファイル分割**: 日付ベース + 上限到達時の自動連番（最大10,000ログ/ファイル）
- **並行処理**: `ConcurrentDictionary` による操作トラッキング
- **アトミックファイル書き込み**: 一時ファイル + リネームパターン

## エラーハンドリング

- Win32Exception メッセージのキャプチャ
- 診断モードでのサービス実行ファイル起動による詳細エラー出力の取得
- 標準例外メッセージへのフォールバック
- すべてのエラーをサービス名・操作タイプと共にログ記録
- SignalR 自動再接続（バックオフ付き）
- HTTP ポーリングフォールバック（SignalR 利用不可時）

## ライセンス

このプロジェクトは個人利用目的で開発されています。
