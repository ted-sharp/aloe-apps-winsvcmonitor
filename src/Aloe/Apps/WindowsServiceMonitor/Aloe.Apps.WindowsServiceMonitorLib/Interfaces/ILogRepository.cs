using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

/// <summary>
/// ログリポジトリインターフェース
/// </summary>
public interface ILogRepository
{
    /// <summary>
    /// ログを追加する
    /// </summary>
    Task AddLogAsync(LogEntry logEntry);

    /// <summary>
    /// すべてのログを取得する
    /// </summary>
    Task<List<LogEntry>> GetAllLogsAsync();

    /// <summary>
    /// フィルタリングされたログを取得する
    /// </summary>
    /// <param name="logType">ログタイプ（null: すべて）</param>
    /// <param name="startDate">開始日（null: 制限なし）</param>
    /// <param name="endDate">終了日（null: 制限なし）</param>
    /// <param name="skip">スキップ件数</param>
    /// <param name="take">取得件数</param>
    Task<List<LogEntry>> GetLogsAsync(LogType? logType = null, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50);

    /// <summary>
    /// ログ件数を取得する
    /// </summary>
    /// <param name="logType">ログタイプ（null: すべて）</param>
    /// <param name="startDate">開始日（null: 制限なし）</param>
    /// <param name="endDate">終了日（null: 制限なし）</param>
    Task<int> GetLogCountAsync(LogType? logType = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 古いログを削除する
    /// </summary>
    /// <param name="keepCount">保持するログ件数</param>
    Task DeleteOldLogsAsync(int keepCount);
}
