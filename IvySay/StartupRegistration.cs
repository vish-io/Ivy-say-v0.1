using System.IO;
using Microsoft.Win32;

namespace IvySay;
// Per-user Run entry; no elevation, task, service, API key or dictation content.
internal static class StartupRegistration
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "IvySay";
    internal sealed record Previous(object? Value, RegistryValueKind Kind, bool Existed);
    public static Previous Read(string keyPath = RunKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, false);
        if (key == null || !key.GetValueNames().Contains(ValueName, StringComparer.OrdinalIgnoreCase)) return new(null, RegistryValueKind.None, false);
        return new(key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames), key.GetValueKind(ValueName), true);
    }
    public static void Set(bool enabled, string? executable = null, string keyPath = RunKey)
    {
        string? command = null;
        if (enabled)
        {
            executable ??= Environment.ProcessPath;
            command = StartupCommand.ForExecutable(executable);
            if (command == null || !File.Exists(executable)) throw new InvalidOperationException("Startup requires the published Ivy Say executable in a permanent location.");
        }
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true) ?? throw new InvalidOperationException("Windows startup settings are unavailable.");
        if (enabled) key.SetValue(ValueName, command!, RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
    public static void Restore(Previous previous, string keyPath = RunKey)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true) ?? throw new InvalidOperationException("Windows startup settings are unavailable.");
        if (previous.Existed && previous.Value != null) key.SetValue(ValueName, previous.Value, previous.Kind);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
