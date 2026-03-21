// <copyright file="WebUiErrorDialogPresentationCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class WebUiErrorDialogPresentationCatalogTests
{
    [Fact]
    public void Create_FormatsMessageAndDialogMetadata()
    {
        var presentation = WebUiErrorDialogPresentationCatalog.Create("boom");

        Assert.Equal("Failed to open Web UI: boom", presentation.Message);
        Assert.Equal("Error", presentation.Title);
        Assert.Equal(MessageBoxButton.OK, presentation.Buttons);
        Assert.Equal(MessageBoxImage.Error, presentation.Icon);
    }
}
