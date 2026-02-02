using System.Collections.Generic;

namespace Aloe.Apps.WindowsServiceMonitorLib.Models;

/// <summary>
/// ログストレージモデル
/// </summary>
public class LogStorage
{
    /// <summary>ログリスト</summary>
    public List<LogEntry> Logs { get; set; } = new();

    /// <summary>バージョン</summary>
    public int Version { get; set; } = 1;
}
