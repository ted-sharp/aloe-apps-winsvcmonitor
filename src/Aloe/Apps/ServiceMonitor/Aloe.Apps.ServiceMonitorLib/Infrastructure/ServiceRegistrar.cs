using Aloe.Apps.ServiceMonitorLib.Interfaces;
using Aloe.Apps.ServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.ServiceMonitorLib.Infrastructure;

public class ServiceRegistrar : IServiceRegistrar
{
    private readonly ILogger<ServiceRegistrar> _logger;

    public ServiceRegistrar(ILogger<ServiceRegistrar> logger)
    {
        _logger = logger;
    }

    public async Task<ServiceOperationResult> RegisterServiceAsync(ServiceRegistrationRequest request)
    {
        try
        {
            ValidateRequest(request);

            var scCreateCommand = $"create \"{request.ServiceName}\" binPath= \"{request.BinaryPath}\"";

            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                scCreateCommand += $" DisplayName= \"{request.DisplayName}\"";
            }

            var result = await ExecuteScCommand(scCreateCommand);

            if (!result.Success)
            {
                return ServiceOperationResult.FailureResult($"サービス登録に失敗しました: {result.Message}");
            }

            if (!string.IsNullOrEmpty(request.StartupType))
            {
                var scConfigCommand = $"config \"{request.ServiceName}\" start= {GetStartupTypeValue(request.StartupType)}";
                var configResult = await ExecuteScCommand(scConfigCommand);

                if (!configResult.Success)
                {
                    _logger.LogWarning("起動タイプの設定に失敗しました。サービス自体は登録されました。");
                }
            }

            _logger.LogInformation("サービス '{ServiceName}' が登録されました", request.ServiceName);
            return ServiceOperationResult.SuccessResult($"サービス '{request.ServiceName}' を登録しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス登録中にエラーが発生しました");
            return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
        }
    }

    public async Task<ServiceOperationResult> UnregisterServiceAsync(string serviceName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return ServiceOperationResult.FailureResult("サービス名は空にできません");
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_\-]+$"))
            {
                return ServiceOperationResult.FailureResult("無効なサービス名です");
            }

            var result = await ExecuteScCommand($"delete \"{serviceName}\"");

            if (!result.Success)
            {
                return ServiceOperationResult.FailureResult($"サービス削除に失敗しました: {result.Message}");
            }

            _logger.LogInformation("サービス '{ServiceName}' が削除されました", serviceName);
            return ServiceOperationResult.SuccessResult($"サービス '{serviceName}' を削除しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "サービス削除中にエラーが発生しました");
            return ServiceOperationResult.FailureResult($"エラーが発生しました: {ex.Message}");
        }
    }

    private void ValidateRequest(ServiceRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            throw new ArgumentException("サービス名は空にできません", nameof(request.ServiceName));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.ServiceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException("サービス名に無効な文字が含まれています", nameof(request.ServiceName));
        }

        if (string.IsNullOrWhiteSpace(request.BinaryPath))
        {
            throw new ArgumentException("バイナリパスは空にできません", nameof(request.BinaryPath));
        }

        if (!System.IO.File.Exists(request.BinaryPath))
        {
            throw new FileNotFoundException($"バイナリファイルが見つかりません: {request.BinaryPath}");
        }
    }

    private string GetStartupTypeValue(string startupType)
    {
        return startupType.ToLower() switch
        {
            "automatic" or "auto" => "auto",
            "manual" => "demand",
            "disabled" => "disabled",
            "automatic delayed" or "delayed" => "delayed",
            _ => "demand"
        };
    }

    private async Task<(bool Success, string Message)> ExecuteScCommand(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add(arguments);

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                return (false, "プロセスの起動に失敗しました");
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return (true, "成功");
            }

            var error = await process.StandardError.ReadToEndAsync();
            var output = await process.StandardOutput.ReadToEndAsync();

            return (false, $"Exit Code: {process.ExitCode}. Error: {error ?? output}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "sc.exeコマンド実行エラー");
            return (false, ex.Message);
        }
    }
}
