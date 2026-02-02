using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

/// <summary>
/// カスタムJSON形式のログリポジトリ実装
/// スレッドセーフなファイル操作を提供し、日付ベースのファイル分割を行う
/// </summary>
public class JsonLogRepository : ILogRepository
{
    private readonly string _logsDirectory;
    private readonly ILogger<JsonLogRepository> _logger;
    private readonly int _maxLogsPerFile;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private const string LogFilePrefix = "custom_logs_";
    private const string LogFilePattern = "custom_logs_*.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonLogRepository(string logsDirectory, ILogger<JsonLogRepository> logger, int maxLogsPerFile = 10000)
    {
        _logsDirectory = logsDirectory;
        _logger = logger;
        _maxLogsPerFile = maxLogsPerFile;
        Directory.CreateDirectory(_logsDirectory);
    }

    /// <summary>
    /// ログを追加する（カスタムJSON形式で保存）
    /// </summary>
    public async Task AddLogAsync(LogEntry logEntry)
    {
        await _semaphore.WaitAsync();
        try
        {
            await AddLogDirectlyAsync(logEntry);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// すべてのログを取得する
    /// </summary>
    public async Task<List<LogEntry>> GetAllLogsAsync()
    {
        return await GetLogsAsync();
    }

    /// <summary>
    /// フィルタリングされたログを取得する
    /// </summary>
    public async Task<List<LogEntry>> GetLogsAsync(
        LogType? logType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int skip = 0,
        int take = 50)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await GetLogsDirectlyAsync(logType, startDate, endDate, skip, take);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// ログ件数を取得する
    /// </summary>
    public async Task<int> GetLogCountAsync(
        LogType? logType = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await GetLogCountDirectlyAsync(logType, startDate, endDate);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 古いログファイルを削除する
    /// </summary>
    public async Task DeleteOldLogsAsync(int keepCount)
    {
        await _semaphore.WaitAsync();
        try
        {
            await DeleteOldLogsDirectlyAsync(keepCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ============================================
    // Private Methods (Directly = ロック内で呼ばれる)
    // ============================================

    /// <summary>
    /// ログを追加する（ロック内で呼ばれる）
    /// </summary>
    private async Task AddLogDirectlyAsync(LogEntry logEntry)
    {
        // 今日の日付でファイル名を生成
        var fileName = GetCurrentLogFileName();
        var filePath = Path.Combine(_logsDirectory, fileName);

        // 既存のLogStorageを読み込むか、新規作成
        var storage = await ReadLogStorageDirectlyAsync(filePath) ?? new LogStorage();

        // ファイルサイズ制限チェック
        if (storage.Logs.Count >= _maxLogsPerFile)
        {
            // 連番ファイルに切り替え
            fileName = GetNextSequencedFileName();
            filePath = Path.Combine(_logsDirectory, fileName);
            storage = new LogStorage();
        }

        // ログエントリを追加
        storage.Logs.Add(logEntry);

        // アトミックライトで保存
        await WriteLogStorageDirectlyAsync(filePath, storage);
    }

    /// <summary>
    /// フィルタリングされたログを取得する（ロック内で呼ばれる）
    /// </summary>
    private async Task<List<LogEntry>> GetLogsDirectlyAsync(
        LogType? logType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int skip = 0,
        int take = 50)
    {
        // 日付範囲から対象ファイルを取得
        var logFiles = GetLogFilesForDateRange(startDate, endDate);

        // すべてのログをマージ
        var allLogs = new List<LogEntry>();
        foreach (var filePath in logFiles)
        {
            var storage = await ReadLogStorageDirectlyAsync(filePath);
            if (storage != null)
            {
                allLogs.AddRange(storage.Logs);
            }
        }

        // フィルタリング
        var query = allLogs.AsEnumerable();

        if (logType.HasValue)
        {
            query = query.Where(l => l.Type == logType.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endDateTime = endDate.Value.Date.AddDays(1).AddSeconds(-1);
            query = query.Where(l => l.Timestamp <= endDateTime);
        }

        // 時刻降順でソート、ページネーション
        return query
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// ログ件数を取得する（ロック内で呼ばれる）
    /// </summary>
    private async Task<int> GetLogCountDirectlyAsync(
        LogType? logType = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        // 日付範囲から対象ファイルを取得
        var logFiles = GetLogFilesForDateRange(startDate, endDate);

        // すべてのログをマージ
        var allLogs = new List<LogEntry>();
        foreach (var filePath in logFiles)
        {
            var storage = await ReadLogStorageDirectlyAsync(filePath);
            if (storage != null)
            {
                allLogs.AddRange(storage.Logs);
            }
        }

        // フィルタリング
        var query = allLogs.AsEnumerable();

        if (logType.HasValue)
        {
            query = query.Where(l => l.Type == logType.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endDateTime = endDate.Value.Date.AddDays(1).AddSeconds(-1);
            query = query.Where(l => l.Timestamp <= endDateTime);
        }

        return query.Count();
    }

    /// <summary>
    /// 古いログファイルを削除する（ロック内で呼ばれる）
    /// </summary>
    private Task DeleteOldLogsDirectlyAsync(int keepDays)
    {
        if (!Directory.Exists(_logsDirectory))
        {
            return Task.CompletedTask;
        }

        var cutoffDate = DateTime.Now.Date.AddDays(-keepDays);
        var logFiles = Directory.GetFiles(_logsDirectory, LogFilePattern);

        foreach (var filePath in logFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var dateMatch = Regex.Match(fileName, @"custom_logs_(\d{8})");

            if (dateMatch.Success)
            {
                var dateStr = dateMatch.Groups[1].Value;
                if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate < cutoffDate)
                    {
                        try
                        {
                            File.Delete(filePath);
                            _logger.LogInformation("削除したログファイル: {FileName}", fileName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "ログファイルの削除に失敗: {FileName}", fileName);
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 今日の日付のファイル名を返す
    /// </summary>
    private static string GetCurrentLogFileName()
    {
        return $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}.json";
    }

    /// <summary>
    /// 連番付きファイル名を返す（今日の日付で最大の連番 + 1）
    /// </summary>
    private string GetNextSequencedFileName()
    {
        var todayPrefix = $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}";
        var pattern = $"{todayPrefix}_*.json";
        var existingFiles = Directory.GetFiles(_logsDirectory, pattern);

        var maxSequence = 0;
        foreach (var filePath in existingFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var sequenceMatch = Regex.Match(fileName, @"_(\d+)$");
            if (sequenceMatch.Success && int.TryParse(sequenceMatch.Groups[1].Value, out var sequence))
            {
                maxSequence = Math.Max(maxSequence, sequence);
            }
        }

        return $"{todayPrefix}_{maxSequence + 1:D2}.json";
    }

    /// <summary>
    /// 日付範囲内のログファイルリストを返す
    /// </summary>
    private List<string> GetLogFilesForDateRange(DateTime? startDate, DateTime? endDate)
    {
        if (!Directory.Exists(_logsDirectory))
        {
            return new List<string>();
        }

        var allFiles = Directory.GetFiles(_logsDirectory, LogFilePattern);
        var filteredFiles = new List<string>();

        foreach (var filePath in allFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var dateMatch = Regex.Match(fileName, @"custom_logs_(\d{8})");

            if (!dateMatch.Success)
            {
                continue;
            }

            var dateStr = dateMatch.Groups[1].Value;
            if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
            {
                continue;
            }

            // 日付範囲チェック
            if (startDate.HasValue && fileDate < startDate.Value.Date)
            {
                continue;
            }

            if (endDate.HasValue && fileDate > endDate.Value.Date)
            {
                continue;
            }

            filteredFiles.Add(filePath);
        }

        // 日付順でソート
        return filteredFiles.OrderBy(f => f).ToList();
    }

    /// <summary>
    /// ファイルからLogStorageを読み込む
    /// </summary>
    private async Task<LogStorage?> ReadLogStorageDirectlyAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<LogStorage>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ログファイルの読み込みに失敗: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// アトミックライトでLogStorageを保存する
    /// </summary>
    private async Task WriteLogStorageDirectlyAsync(string filePath, LogStorage storage)
    {
        var tempFilePath = filePath + ".tmp";

        try
        {
            // tempファイルに書き込み
            var json = JsonSerializer.Serialize(storage, JsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json);

            // 既存ファイルを削除してリネーム
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFilePath, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ログファイルの書き込みに失敗: {FilePath}", filePath);

            // tempファイルのクリーンアップ
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // 無視
                }
            }

            throw;
        }
    }
}
