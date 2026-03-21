// <copyright file="WebUiErrorDialogPresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

namespace AIUsageTracker.UI.Slim;

internal static class WebUiErrorDialogPresentationCatalog
{
    public static WebUiErrorDialogPresentation Create(string exceptionMessage)
    {
        return new WebUiErrorDialogPresentation(
            Message: $"Failed to open Web UI: {exceptionMessage}",
            Title: "Error",
            Buttons: MessageBoxButton.OK,
            Icon: MessageBoxImage.Error);
    }
}
