// <copyright file="WindowPreferencePresentation.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

namespace AIUsageTracker.UI.Slim;

internal sealed record WindowPreferencePresentation(
    bool Topmost,
    double Width,
    double Height,
    string? FontFamilyName,
    double? FontSize,
    FontWeight FontWeight,
    FontStyle FontStyle,
    bool AlwaysOnTopChecked);
