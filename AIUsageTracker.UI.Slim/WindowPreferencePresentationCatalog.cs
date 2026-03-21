// <copyright file="WindowPreferencePresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim;

internal static class WindowPreferencePresentationCatalog
{
    public static WindowPreferencePresentation Create(AppPreferences preferences)
    {
        return new WindowPreferencePresentation(
            Topmost: preferences.AlwaysOnTop,
            Width: preferences.WindowWidth,
            Height: preferences.WindowHeight,
            FontFamilyName: string.IsNullOrWhiteSpace(preferences.FontFamily) ? null : preferences.FontFamily,
            FontSize: preferences.FontSize > 0 ? preferences.FontSize : null,
            FontWeight: preferences.FontBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle: preferences.FontItalic ? FontStyles.Italic : FontStyles.Normal,
            AlwaysOnTopChecked: preferences.AlwaysOnTop);
    }
}
