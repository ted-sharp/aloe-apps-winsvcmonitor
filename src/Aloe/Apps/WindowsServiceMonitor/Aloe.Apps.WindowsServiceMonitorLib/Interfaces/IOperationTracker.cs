using Aloe.Apps.WindowsServiceMonitorLib.Models;

namespace Aloe.Apps.WindowsServiceMonitorLib.Interfaces;

/// <summary>
/// サービス操作を追跡し、操作による期待される状態変化を記録するサービス
/// </summary>
public interface IOperationTracker
{
    /// <summary>
    /// サービス操作による期待される状態遷移を記録する。
    /// 操作後のターゲットステータスのみ追跡される。
    /// </summary>
    void RegisterExpectedTransition(string serviceName, ServiceStatus fromStatus, ServiceStatus toStatus);

    /// <summary>
    /// 操作によるターゲットステータスを取得し、記録をクリアする。
    /// バックグラウンドモニターが _previousStatuses を更新するために使用する。
    /// </summary>
    /// <param name="serviceName">サービス名</param>
    /// <param name="withinSeconds">何秒以内の操作を有効とみなすか（デフォルト: 30秒）</param>
    /// <returns>ターゲットステータス。操作がない場合は null</returns>
    ServiceStatus? ConsumeTargetStatus(string serviceName, int withinSeconds = 30);
}
