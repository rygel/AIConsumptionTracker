// <copyright file="MonitorJobSchedulerSnapshot.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

namespace AIUsageTracker.Monitor.Services;

public sealed class MonitorJobSchedulerSnapshot
{
    public int HighPriorityQueuedJobs { get; init; }

    public int NormalPriorityQueuedJobs { get; init; }

    public int LowPriorityQueuedJobs { get; init; }

    public int TotalQueuedJobs { get; init; }

    public int RecurringJobs { get; init; }

    public long ExecutedJobs { get; init; }

    public long FailedJobs { get; init; }
}
