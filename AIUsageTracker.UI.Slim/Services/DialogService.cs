// <copyright file="DialogService.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;
using Microsoft.Win32;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Service for showing application dialogs.
/// </summary>
public class DialogService : IDialogService
{
    private readonly ISettingsWindowFactory _settingsWindowFactory;
    private readonly IInfoDialogFactory _infoDialogFactory;

    public DialogService(ISettingsWindowFactory settingsWindowFactory, IInfoDialogFactory infoDialogFactory)
    {
        this._settingsWindowFactory = settingsWindowFactory;
        this._infoDialogFactory = infoDialogFactory;
    }

    /// <inheritdoc />
    public Task<bool?> ShowSettingsAsync(Window? owner = null)
    {
        var settingsWindow = this._settingsWindowFactory.Create();

        if (owner != null)
        {
            settingsWindow.Owner = owner;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        var result = settingsWindow.ShowDialog();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task ShowInfoAsync(Window? owner = null)
    {
        var infoDialog = this._infoDialogFactory.Create();

        if (owner != null)
        {
            infoDialog.Owner = owner;
            infoDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        infoDialog.ShowDialog();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> ShowSaveFileDialogAsync(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
        };

        var result = dialog.ShowDialog();
        return Task.FromResult(result == true ? dialog.FileName : null);
    }

    /// <inheritdoc />
    public Task<string?> ShowOpenFileDialogAsync(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
        };

        var result = dialog.ShowDialog();
        return Task.FromResult(result == true ? dialog.FileName : null);
    }
}
