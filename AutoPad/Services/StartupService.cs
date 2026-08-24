using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AutoPad.Services;

internal enum StartupRegistrationState
{
    Enabled,
    Disabled,
    DisabledByUser,
    DisabledByPolicy,
    Unavailable
}

internal readonly record struct StartupRegistrationResult(
    bool Success,
    StartupRegistrationState State,
    string? ErrorMessage = null)
{
    public bool IsEnabled => State == StartupRegistrationState.Enabled;
}

/// <summary>
/// Windows 시작 프로그램의 실제 등록 상태를 조회하고 변경합니다.
/// MSIX에서는 StartupTask를, 비패키지 실행에서는 HKCU Run을 사용합니다.
/// </summary>
internal static class StartupService
{
    private const string StartupTaskId = "AutoPadStartup";
    private const string AppName = "AutoPad";
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const int AppModelErrorNoPackage = 15700;

    public static async Task<StartupRegistrationResult> GetStatusAsync()
    {
        try
        {
            if (IsRunningAsMsix())
            {
                var task = await global::Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                return FromMsixState(task.State);
            }

            using var runKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
            var command = runKey?.GetValue(AppName) as string;
            if (string.IsNullOrWhiteSpace(command) || !TargetsCurrentExecutable(command))
            {
                return new StartupRegistrationResult(true, StartupRegistrationState.Disabled);
            }

            using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRegistryKey, writable: false);
            if (approvedKey?.GetValue(AppName) is byte[] approval && approval.Length > 0 && approval[0] == 0x03)
            {
                return new StartupRegistrationResult(true, StartupRegistrationState.DisabledByUser);
            }

            return new StartupRegistrationResult(true, StartupRegistrationState.Enabled);
        }
        catch (Exception ex)
        {
            return new StartupRegistrationResult(false, StartupRegistrationState.Unavailable, ex.Message);
        }
    }

    public static async Task<StartupRegistrationResult> SetEnabledAsync(bool enable)
    {
        try
        {
            return IsRunningAsMsix()
                ? await SetMsixEnabledAsync(enable)
                : SetRegistryEnabled(enable);
        }
        catch (Exception ex)
        {
            return new StartupRegistrationResult(false, StartupRegistrationState.Unavailable, ex.Message);
        }
    }

    private static async Task<StartupRegistrationResult> SetMsixEnabledAsync(bool enable)
    {
        var task = await global::Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
        var current = FromMsixState(task.State);

        if (!enable)
        {
            if (task.State == global::Windows.ApplicationModel.StartupTaskState.EnabledByPolicy)
            {
                return new StartupRegistrationResult(false, StartupRegistrationState.DisabledByPolicy);
            }

            if (current.IsEnabled)
            {
                task.Disable();
            }

            return new StartupRegistrationResult(true, StartupRegistrationState.Disabled);
        }

        if (current.IsEnabled)
        {
            return current;
        }

        if (current.State is StartupRegistrationState.DisabledByUser or StartupRegistrationState.DisabledByPolicy)
        {
            return new StartupRegistrationResult(false, current.State);
        }

        var requestedState = await task.RequestEnableAsync();
        var requested = FromMsixState(requestedState);
        return requested.IsEnabled
            ? requested
            : new StartupRegistrationResult(false, requested.State);
    }

    private static StartupRegistrationResult SetRegistryEnabled(bool enable)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(StartupRegistryKey, writable: true);
        if (runKey == null)
        {
            return new StartupRegistrationResult(false, StartupRegistrationState.Unavailable, "Cannot access registry.");
        }

        if (enable)
        {
            var command = BuildStartupCommand();
            if (string.IsNullOrEmpty(command))
            {
                return new StartupRegistrationResult(false, StartupRegistrationState.Unavailable, "Cannot find executable path.");
            }

            runKey.SetValue(AppName, command, RegistryValueKind.String);

            // 작업 관리자/Windows 설정에서 비활성화했던 흔적이 남아 있으면
            // Run 값을 다시 써도 실행되지 않으므로 승인 값을 제거해 다시 활성화합니다.
            using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRegistryKey, writable: true);
            approvedKey?.DeleteValue(AppName, throwOnMissingValue: false);

            var registered = runKey.GetValue(AppName) as string;
            var success = registered != null && TargetsCurrentExecutable(registered);
            return new StartupRegistrationResult(
                success,
                success ? StartupRegistrationState.Enabled : StartupRegistrationState.Unavailable,
                success ? null : "Startup registration could not be verified.");
        }

        runKey.DeleteValue(AppName, throwOnMissingValue: false);
        using (var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRegistryKey, writable: true))
        {
            approvedKey?.DeleteValue(AppName, throwOnMissingValue: false);
        }

        return new StartupRegistrationResult(true, StartupRegistrationState.Disabled);
    }

    private static StartupRegistrationResult FromMsixState(global::Windows.ApplicationModel.StartupTaskState state)
    {
        return state switch
        {
            global::Windows.ApplicationModel.StartupTaskState.Enabled or
            global::Windows.ApplicationModel.StartupTaskState.EnabledByPolicy =>
                new StartupRegistrationResult(true, StartupRegistrationState.Enabled),
            global::Windows.ApplicationModel.StartupTaskState.DisabledByUser =>
                new StartupRegistrationResult(true, StartupRegistrationState.DisabledByUser),
            global::Windows.ApplicationModel.StartupTaskState.DisabledByPolicy =>
                new StartupRegistrationResult(true, StartupRegistrationState.DisabledByPolicy),
            _ => new StartupRegistrationResult(true, StartupRegistrationState.Disabled)
        };
    }

    private static string? BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var entryPath = Assembly.GetEntryAssembly()?.Location;
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entryPath))
        {
            return $"\"{processPath}\" \"{entryPath}\" --startup";
        }

        return $"\"{processPath}\" --startup";
    }

    private static bool TargetsCurrentExecutable(string command)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        var entryPath = Assembly.GetEntryAssembly()?.Location;
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entryPath))
        {
            return command.Contains(processPath, StringComparison.OrdinalIgnoreCase)
                && command.Contains(entryPath, StringComparison.OrdinalIgnoreCase);
        }

        return command.Contains(processPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRunningAsMsix()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            return result != AppModelErrorNoPackage;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
