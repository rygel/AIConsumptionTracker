// <copyright file="WebUiErrorDialogPresentation.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows;

namespace AIUsageTracker.UI.Slim;

internal sealed record WebUiErrorDialogPresentation(
    string Message,
    string Title,
    MessageBoxButton Buttons,
    MessageBoxImage Icon);
