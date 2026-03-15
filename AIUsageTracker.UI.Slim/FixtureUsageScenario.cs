// <copyright file="FixtureUsageScenario.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

#pragma warning disable CS0618 // RequestsPercentage: fixture uses legacy field for UI testing

namespace AIUsageTracker.UI.Slim;

internal sealed record FixtureUsageScenario(
    double RequestsPercentage = 0,
    double RequestsUsed = 0,
    double RequestsAvailable = 0,
    string Description = "Connected",
    int? ResetHours = null);
