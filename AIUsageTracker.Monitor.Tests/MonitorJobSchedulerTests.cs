// <copyright file="MonitorJobSchedulerTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using AIUsageTracker.Monitor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIUsageTracker.Monitor.Tests;

public class MonitorJobSchedulerTests
{
    [Fact]
    public async Task Enqueue_HighPriorityRunsBeforeLowPriorityAsync()
    {
        var logger = new Mock<ILogger<MonitorJobScheduler>>();
        var scheduler = new MonitorJobScheduler(logger.Object);
        var executionOrder = new ConcurrentQueue<string>();
        var completionSignal = new SemaphoreSlim(0, 2);

        _ = scheduler.Enqueue(
            "low-priority-job",
            _ =>
            {
                executionOrder.Enqueue("low");
                completionSignal.Release();
                return Task.CompletedTask;
            },
            MonitorJobPriority.Low);

        _ = scheduler.Enqueue(
            "high-priority-job",
            _ =>
            {
                executionOrder.Enqueue("high");
                completionSignal.Release();
                return Task.CompletedTask;
            },
            MonitorJobPriority.High);

        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await completionSignal.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await completionSignal.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.True(executionOrder.TryDequeue(out var first));
            Assert.Equal("high", first);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RegisterRecurringJob_ExecutesScheduledWorkAsync()
    {
        var logger = new Mock<ILogger<MonitorJobScheduler>>();
        var scheduler = new MonitorJobScheduler(logger.Object);
        var firstRun = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        scheduler.RegisterRecurringJob(
            "recurring-test-job",
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                firstRun.TrySetResult(true);
                return Task.CompletedTask;
            },
            MonitorJobPriority.Normal,
            initialDelay: TimeSpan.FromMilliseconds(10));

        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            var completed = await Task.WhenAny(firstRun.Task, Task.Delay(TimeSpan.FromSeconds(2))) == firstRun.Task;
            Assert.True(completed, "Recurring job did not execute within timeout.");

            var snapshot = scheduler.GetSnapshot();
            Assert.Equal(1, snapshot.RecurringJobs);
            Assert.True(snapshot.ExecutedJobs >= 1);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Enqueue_WithCoalesceKey_DeduplicatesPendingJobsAsync()
    {
        var logger = new Mock<ILogger<MonitorJobScheduler>>();
        var scheduler = new MonitorJobScheduler(logger.Object);
        var executionCount = 0;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstQueued = scheduler.Enqueue(
            "coalesced-job-1",
            _ =>
            {
                if (Interlocked.Increment(ref executionCount) == 1)
                {
                    completion.TrySetResult(true);
                }

                return Task.CompletedTask;
            },
            MonitorJobPriority.Normal,
            coalesceKey: "refresh-key");

        var secondQueued = scheduler.Enqueue(
            "coalesced-job-2",
            _ =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            },
            MonitorJobPriority.High,
            coalesceKey: "refresh-key");

        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(2))) == completion.Task;
            Assert.True(completed, "Expected first coalesced job to execute.");
            Assert.True(firstQueued);
            Assert.False(secondQueued);
            Assert.Equal(1, executionCount);
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_StopsRecurringJobExecutionAsync()
    {
        var logger = new Mock<ILogger<MonitorJobScheduler>>();
        var scheduler = new MonitorJobScheduler(logger.Object);
        var firstRun = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runCount = 0;

        scheduler.RegisterRecurringJob(
            "stop-behavior-job",
            TimeSpan.FromMilliseconds(20),
            _ =>
            {
                if (Interlocked.Increment(ref runCount) == 1)
                {
                    firstRun.TrySetResult(true);
                }

                return Task.CompletedTask;
            },
            MonitorJobPriority.Normal,
            initialDelay: TimeSpan.FromMilliseconds(5));

        await scheduler.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(firstRun.Task, Task.Delay(TimeSpan.FromSeconds(2))) == firstRun.Task;
        Assert.True(completed, "Recurring job did not run before stop.");

        await scheduler.StopAsync(CancellationToken.None);
        var executedAfterStop = scheduler.GetSnapshot().ExecutedJobs;

        await Task.Delay(150);

        Assert.Equal(executedAfterStop, scheduler.GetSnapshot().ExecutedJobs);
    }
}
