using Aloe.Apps.WindowsServiceMonitorLib.Interfaces;
using Aloe.Apps.WindowsServiceMonitorLib.Models;
using Microsoft.Extensions.Logging;

namespace Aloe.Apps.WindowsServiceMonitorLib.Infrastructure;

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
            var binaryPath = ResolveBinaryPath(request.BinaryPath, request.BinaryPathAlt);
            ValidateRequest(request, binaryPath);

            var scCreateCommand = $"create \"{request.ServiceName}\" binPath= \"{binaryPath}\"";

            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                scCreateCommand += $" DisplayName= \"{request.DisplayName}\"";
            }

            // Add account and password if specified
            if (!string.IsNullOrEmpty(request.Account))
            {
                scCreateCommand += $" obj= \"{request.Account}\"";

                // Add password if account is specified and password is not empty
                if (!string.IsNullOrEmpty(request.Password))
                {
                    scCreateCommand += $" password= \"{request.Password}\"";
                }
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

    private static string ResolveBinaryPath(string binaryPath, string? binaryPathAlt)
    {
        var primary = ResolveSinglePath(binaryPath);
        if (!string.IsNullOrEmpty(primary) && File.Exists(primary))
            return primary;
        if (!string.IsNullOrWhiteSpace(binaryPathAlt))
        {
            var alt = ResolveSinglePath(binaryPathAlt);
            if (File.Exists(alt))
                return alt;
            if (string.IsNullOrEmpty(primary))
                return alt; // バリデーションエラー用に解決済みパスを返す
        }
        return primary;
    }

    private static string ResolveSinglePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private void ValidateRequest(ServiceRegistrationRequest request, string resolvedBinaryPath)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            throw new ArgumentException("サービス名は空にできません", nameof(request.ServiceName));
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.ServiceName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException("サービス名に無効な文字が含まれています", nameof(request.ServiceName));
        }

        if (string.IsNullOrWhiteSpace(request.BinaryPath) && string.IsNullOrWhiteSpace(request.BinaryPathAlt))
        {
            throw new ArgumentException("バイナリパスは空にできません", nameof(request.BinaryPath));
        }

        if (!File.Exists(resolvedBinaryPath))
        {
            throw new FileNotFoundException($"バイナリファイルが見つかりません: {resolvedBinaryPath}");
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

    // sc.exe は非標準的な引数パーシングを使うため、ArgumentList ではなく Arguments 文字列を使用すること。
    // sc.exe は "binPath= \"値\"" のように key= と値が別トークンである必要があるが、
    // ArgumentList を使うと "binPath= 値" が1つの引用符付き引数になり、正しくパースされない。
    // 入力値のバリデーションは ServiceManager 側の正規表現チェックで担保されている。
    private async Task<(bool Success, string Message)> ExecuteScCommand(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

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
