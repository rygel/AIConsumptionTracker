// <copyright file="UpdateInstallProgressPresentation.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.UI.Slim;

internal sealed record UpdateInstallProgressPresentation(
    string WindowTitle,
    string ProgressText);
