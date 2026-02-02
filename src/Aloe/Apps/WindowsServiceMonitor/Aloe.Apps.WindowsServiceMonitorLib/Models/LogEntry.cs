using System;

namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

/// <summary>
/// ログエントリのタイプ
/// </summary>
public enum LogType
{
    /// <summary>アクセスログ</summary>
    Access,
    /// <summary>操作ログ</summary>
    Operation,
    /// <summary>ステータス変化ログ</summary>
    StatusChange
}

/// <summary>
/// ログエントリデータモデル
/// </summary>
public class LogEntry
{
    /// <summary>タイムスタンプ</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>ログタイプ</summary>
    public LogType Type { get; set; }

    /// <summary>メッセージ</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>サービス名（操作ログ、ステータス変化ログ用）</summary>
    public string? ServiceName { get; set; }

    /// <summary>ユーザー名（アクセスログ、操作ログ用）</summary>
    public string? UserName { get; set; }

    /// <summary>IPアドレス（アクセスログ用）</summary>
    public string? IpAddress { get; set; }

    /// <summary>HTTPメソッド（アクセスログ用）</summary>
    public string? HttpMethod { get; set; }

    /// <summary>リクエストパス（アクセスログ用）</summary>
    public string? RequestPath { get; set; }

    /// <summary>ステータスコード（アクセスログ用）</summary>
    public int? StatusCode { get; set; }

    /// <summary>操作結果（操作ログ用）</summary>
    public string? Result { get; set; }

    /// <summary>旧ステータス（ステータス変化ログ用）</summary>
    public string? OldStatus { get; set; }

    /// <summary>新ステータス（ステータス変化ログ用）</summary>
    public string? NewStatus { get; set; }
}
