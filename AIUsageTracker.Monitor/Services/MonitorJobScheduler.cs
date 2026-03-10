// <copyright file="MonitorJobScheduler.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.Monitor.Services;

public sealed class MonitorJobScheduler : BackgroundService, IMonitorJobScheduler
{
    private readonly ILogger<MonitorJobScheduler> _logger;
    private readonly ConcurrentQueue<ScheduledJob> _highPriorityQueue = new();
    private readonly ConcurrentQueue<ScheduledJob> _normalPriorityQueue = new();
    private readonly ConcurrentQueue<ScheduledJob> _lowPriorityQueue = new();
    private readonly ConcurrentDictionary<string, byte> _coalescedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _queuedItemsSignal = new(0);
    private readonly object _recurringLock = new();
    private readonly List<RecurringJobRegistration> _recurringRegistrations = new();
    private readonly List<Task> _recurringTasks = new();
    private long _executedJobs;
    private long _failedJobs;
    private bool _isRunning;
    private CancellationToken _schedulerToken = CancellationToken.None;

    public MonitorJobScheduler(ILogger<MonitorJobScheduler> logger)
    {
        this._logger = logger;
    }

    public bool Enqueue(
        string jobName,
        Func<CancellationToken, Task> work,
        MonitorJobPriority priority = MonitorJobPriority.Normal,
        string? coalesceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(work);

        if (!string.IsNullOrWhiteSpace(coalesceKey) && !this._coalescedKeys.TryAdd(coalesceKey, 0))
        {
            this._logger.LogDebug("Skipped enqueue for coalesced job {JobName} ({CoalesceKey})", jobName, coalesceKey);
            return false;
        }

        var job = new ScheduledJob(jobName, priority, work, coalesceKey);
        switch (priority)
        {
            case MonitorJobPriority.High:
                this._highPriorityQueue.Enqueue(job);
                break;
            case MonitorJobPriority.Low:
                this._lowPriorityQueue.Enqueue(job);
                break;
            default:
                this._normalPriorityQueue.Enqueue(job);
                break;
        }

        this._queuedItemsSignal.Release();
        this._logger.LogDebug("Queued job {JobName} with priority {Priority}", jobName, priority);
        return true;
    }

    public void RegisterRecurringJob(
        string jobName,
        TimeSpan interval,
        Func<CancellationToken, Task> work,
        MonitorJobPriority priority = MonitorJobPriority.Normal,
        TimeSpan? initialDelay = null,
        string? coalesceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(work);

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Recurring interval must be greater than zero.");
        }

        var registration = new RecurringJobRegistration(
            jobName,
            interval,
            initialDelay ?? TimeSpan.Zero,
            priority,
            work,
            coalesceKey);

        lock (this._recurringLock)
        {
            this._recurringRegistrations.Add(registration);
            if (this._isRunning)
            {
                this._recurringTasks.Add(this.StartRecurringLoop(registration, this._schedulerToken));
            }
        }

        this._logger.LogInformation(
            "Registered recurring job {JobName} with interval {Interval} and priority {Priority}",
            jobName,
            interval,
            priority);
    }

    public MonitorJobSchedulerSnapshot GetSnapshot()
    {
        var high = this._highPriorityQueue.Count;
        var normal = this._normalPriorityQueue.Count;
        var low = this._lowPriorityQueue.Count;
        int recurringCount;

        lock (this._recurringLock)
        {
            recurringCount = this._recurringRegistrations.Count;
        }

        return new MonitorJobSchedulerSnapshot
        {
            HighPriorityQueuedJobs = high,
            NormalPriorityQueuedJobs = normal,
            LowPriorityQueuedJobs = low,
            TotalQueuedJobs = high + normal + low,
            RecurringJobs = recurringCount,
            ExecutedJobs = Interlocked.Read(ref this._executedJobs),
            FailedJobs = Interlocked.Read(ref this._failedJobs),
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Monitor job scheduler starting");
        this._schedulerToken = stoppingToken;

        lock (this._recurringLock)
        {
            this._isRunning = true;
            foreach (var registration in this._recurringRegistrations)
            {
                this._recurringTasks.Add(this.StartRecurringLoop(registration, stoppingToken));
            }
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await this._queuedItemsSignal.WaitAsync(stoppingToken).ConfigureAwait(false);
                if (!this.TryDequeueNext(out var job))
                {
                    continue;
                }

                try
                {
                    this._logger.LogDebug("Executing scheduled job {JobName} ({Priority})", job.Name, job.Priority);
                    await job.Work(stoppingToken).ConfigureAwait(false);
                    Interlocked.Increment(ref this._executedJobs);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref this._failedJobs);
                    this._logger.LogError(ex, "Scheduled job {JobName} failed", job.Name);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(job.CoalesceKey))
                    {
                        this._coalescedKeys.TryRemove(job.CoalesceKey, out _);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        finally
        {
            Task[] recurringTasks;
            lock (this._recurringLock)
            {
                this._isRunning = false;
                recurringTasks = this._recurringTasks.ToArray();
                this._recurringTasks.Clear();
            }

            try
            {
                await Task.WhenAll(recurringTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "Recurring scheduler loops ended with cancellation/error.");
            }

            this._logger.LogInformation("Monitor job scheduler stopped");
        }
    }

    private static Task StartRecurringDelayAsync(TimeSpan initialDelay, CancellationToken cancellationToken)
    {
        return initialDelay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(initialDelay, cancellationToken);
    }

    private Task StartRecurringLoop(RecurringJobRegistration registration, CancellationToken stoppingToken)
    {
        return Task.Run(
            async () =>
            {
                try
                {
                    await StartRecurringDelayAsync(registration.InitialDelay, stoppingToken).ConfigureAwait(false);
                    if (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _ = this.Enqueue(
                        registration.Name,
                        registration.Work,
                        registration.Priority,
                        registration.CoalesceKey);

                    using var timer = new PeriodicTimer(registration.Interval);
                    while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    {
                        _ = this.Enqueue(
                            registration.Name,
                            registration.Work,
                            registration.Priority,
                            registration.CoalesceKey);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Normal shutdown path.
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Recurring job loop failed for {JobName}", registration.Name);
                }
            },
            stoppingToken);
    }

    private bool TryDequeueNext(out ScheduledJob job)
    {
        if (this._highPriorityQueue.TryDequeue(out job))
        {
            return true;
        }

        if (this._normalPriorityQueue.TryDequeue(out job))
        {
            return true;
        }

        return this._lowPriorityQueue.TryDequeue(out job);
    }

    private sealed record ScheduledJob(
        string Name,
        MonitorJobPriority Priority,
        Func<CancellationToken, Task> Work,
        string? CoalesceKey);

    private sealed record RecurringJobRegistration(
        string Name,
        TimeSpan Interval,
        TimeSpan InitialDelay,
        MonitorJobPriority Priority,
        Func<CancellationToken, Task> Work,
        string? CoalesceKey);
}
