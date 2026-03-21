// <copyright file="UpdateInstallPresentationCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class UpdateInstallPresentationCatalogTests
{
    [Fact]
    public void CreateConfirm_FormatsMessageAndDialogMetadata()
    {
        var presentation = UpdateInstallPresentationCatalog.CreateConfirm("2.8.0");

        Assert.Equal(
            "Download and install version 2.8.0?\n\nThe application will restart after installation.",
            presentation.Message);
        Assert.Equal("Confirm Update", presentation.Title);
        Assert.Equal(MessageBoxButton.YesNo, presentation.Buttons);
        Assert.Equal(MessageBoxImage.Question, presentation.Icon);
    }

    [Theory]
    [InlineData(MessageBoxResult.Yes, true)]
    [InlineData(MessageBoxResult.No, false)]
    [InlineData(MessageBoxResult.Cancel, false)]
    public void ShouldProceed_OnlyWhenYes(MessageBoxResult result, bool expected)
    {
        var shouldProceed = UpdateInstallPresentationCatalog.ShouldProceed(result);

        Assert.Equal(expected, shouldProceed);
    }

    [Fact]
    public void CreateProgress_FormatsTitleAndText()
    {
        var presentation = UpdateInstallPresentationCatalog.CreateProgress("2.8.0");

        Assert.Equal("Downloading Update", presentation.WindowTitle);
        Assert.Equal("Downloading version 2.8.0...", presentation.ProgressText);
    }

    [Fact]
    public void CreateFailed_UsesExpectedDialogContent()
    {
        var presentation = UpdateInstallPresentationCatalog.CreateFailed();

        Assert.Equal(
            "Failed to download or install the update. Please try again or download manually from the releases page.",
            presentation.Message);
        Assert.Equal("Update Failed", presentation.Title);
        Assert.Equal(MessageBoxButton.OK, presentation.Buttons);
        Assert.Equal(MessageBoxImage.Error, presentation.Icon);
    }

    [Fact]
    public void CreateError_IncludesExceptionMessage()
    {
        var presentation = UpdateInstallPresentationCatalog.CreateError("boom");

        Assert.Equal("Update error: boom", presentation.Message);
        Assert.Equal("Update Error", presentation.Title);
        Assert.Equal(MessageBoxButton.OK, presentation.Buttons);
        Assert.Equal(MessageBoxImage.Error, presentation.Icon);
    }
}
