// <copyright file="UpdateInstallPresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

namespace AIUsageTracker.UI.Slim;

internal static class UpdateInstallPresentationCatalog
{
    public static UpdateInstallConfirmPresentation CreateConfirm(string version)
    {
        return new UpdateInstallConfirmPresentation(
            Message: $"Download and install version {version}?\n\nThe application will restart after installation.",
            Title: "Confirm Update",
            Buttons: MessageBoxButton.YesNo,
            Icon: MessageBoxImage.Question);
    }

    public static bool ShouldProceed(MessageBoxResult result)
    {
        return result == MessageBoxResult.Yes;
    }

    public static UpdateInstallProgressPresentation CreateProgress(string version)
    {
        return new UpdateInstallProgressPresentation(
            WindowTitle: "Downloading Update",
            ProgressText: $"Downloading version {version}...");
    }

    public static UpdateInstallResultDialogPresentation CreateFailed()
    {
        return new UpdateInstallResultDialogPresentation(
            Message: "Failed to download or install the update. Please try again or download manually from the releases page.",
            Title: "Update Failed",
            Buttons: MessageBoxButton.OK,
            Icon: MessageBoxImage.Error);
    }

    public static UpdateInstallResultDialogPresentation CreateError(string exceptionMessage)
    {
        return new UpdateInstallResultDialogPresentation(
            Message: $"Update error: {exceptionMessage}",
            Title: "Update Error",
            Buttons: MessageBoxButton.OK,
            Icon: MessageBoxImage.Error);
    }
}
