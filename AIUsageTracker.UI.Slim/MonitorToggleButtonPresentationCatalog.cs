// <copyright file="MonitorToggleButtonPresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.UI.Slim;

internal static class MonitorToggleButtonPresentationCatalog
{
    public static MonitorToggleButtonPresentation Create(bool isRunning)
    {
        return isRunning
            ? new MonitorToggleButtonPresentation(
                IconGlyph: "\uE71A",
                ToolTip: "Stop Monitor")
            : new MonitorToggleButtonPresentation(
                IconGlyph: "\uE768",
                ToolTip: "Start Monitor");
    }
}
