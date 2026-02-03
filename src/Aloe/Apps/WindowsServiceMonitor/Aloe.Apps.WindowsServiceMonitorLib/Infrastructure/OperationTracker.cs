using System.Collections.Concurrent;
using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

/// <summary>
/// サービス操作を追跡し、操作による期待される状態変化を記録するサービス。
/// 操作後のターゲットステータスのみ保持し、バックグラウンドモニターが
/// _previousStatuses を更新することでステータス変化の誤検知を防ぐ。
/// </summary>
public class OperationTracker : IOperationTracker
{
    private readonly ConcurrentDictionary<string, (ServiceStatus Status, DateTime Timestamp)> _targetStatuses = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterExpectedTransition(string serviceName, ServiceStatus fromStatus, ServiceStatus toStatus)
    {
        // 同一サービスで複数の操作が連続する場合（例: stop → delete）、
        // 最後のターゲットステータスだけが重要
        _targetStatuses[serviceName] = (toStatus, DateTime.Now);
    }

    public ServiceStatus? ConsumeTargetStatus(string serviceName, int withinSeconds = 30)
    {
        if (_targetStatuses.TryRemove(serviceName, out var entry))
        {
            if ((DateTime.Now - entry.Timestamp).TotalSeconds <= withinSeconds)
                return entry.Status;
        }

        return null;
    }
}
