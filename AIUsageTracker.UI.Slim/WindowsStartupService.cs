// <copyright file="WindowsStartupService.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.IO;

using Microsoft.Win32;

namespace AIUsageTracker.UI.Slim;

internal static class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UiValueName = "AI Usage Tracker";

    public static bool IsUiStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(UiValueName) != null;
    }

    public static void Apply(bool startUi)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key == null)
        {
            return;
        }

        var exePath = Path.Combine(AppContext.BaseDirectory, "AIUsageTracker.exe");

        if (startUi && File.Exists(exePath))
        {
            key.SetValue(UiValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(UiValueName, throwOnMissingValue: false);
        }
    }
}
