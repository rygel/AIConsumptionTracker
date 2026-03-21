// <copyright file="MonitorToggleButtonPresentationCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class MonitorToggleButtonPresentationCatalogTests
{
    [Fact]
    public void Create_WhenRunning_ReturnsStopPresentation()
    {
        var presentation = MonitorToggleButtonPresentationCatalog.Create(isRunning: true);

        Assert.Equal("\uE71A", presentation.IconGlyph);
        Assert.Equal("Stop Monitor", presentation.ToolTip);
    }

    [Fact]
    public void Create_WhenStopped_ReturnsStartPresentation()
    {
        var presentation = MonitorToggleButtonPresentationCatalog.Create(isRunning: false);

        Assert.Equal("\uE768", presentation.IconGlyph);
        Assert.Equal("Start Monitor", presentation.ToolTip);
    }
}
