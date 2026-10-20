// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling.PoisonMessage;

[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
[Trait("Pattern", "Regression")]
public sealed class PoisonMessageQuarantineRetentionShould
{
    [Fact]
    public Task PreserveUnreplayedQuarantineByDefaultAfterAnObservedPoll() =>
        ObservePollingAsync(null, expectRetained: true);

    [Fact]
    public Task DeleteOldQuarantineOnlyWhenAutomaticCleanupIsExplicitlyEnabled() =>
        ObservePollingAsync(true, expectRetained: false);

    [Fact]
    public Task ContinueAlertPollingWhenAutomaticCleanupIsDisabled() =>
        ObservePollingAsync(false, expectRetained: true);

    [Fact]
    public async Task FinishWithoutWorkWhenCleanupAndAlertingAreBothDisabled()
    {
        var handler = A.Fake<IPoisonMessageHandler>();
        using var provider = CreateServices(handler, options =>
        {
            options.Cleanup.EnableAutoCleanup = false;
            options.Alerting.EnableAlerting = false;
        });
        var worker = provider.GetServices<IHostedService>().OfType<BackgroundService>().Single();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
            A.CallTo(() => handler.GetStatisticsAsync(A<CancellationToken>._)).MustNotHaveHappened();
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task ObservePollingAsync(bool? enableCleanup, bool expectRetained)
    {
        var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = A.Fake<IPoisonMessageHandler>();
        A.CallTo(() => handler.GetStatisticsAsync(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            polled.TrySetResult();
            return Task.FromResult(new PoisonMessageStatistics());
        });
        using var provider = CreateServices(handler, options =>
        {
            if (enableCleanup.HasValue) options.Cleanup.EnableAutoCleanup = enableCleanup.Value;
            options.Cleanup.AutoCleanupInterval = TimeSpan.FromMilliseconds(1);
        });
        var store = provider.GetRequiredService<IDeadLetterStore>();
        var message = new DeadLetterMessage
        {
            MessageId = Guid.NewGuid().ToString("N"), MessageType = "quarantine.fixture",
            MessageBody = "{}", MessageMetadata = "{}", Reason = "unprocessed obligation",
            MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-60), IsReplayed = false,
        };
        await store.StoreAsync(message, CancellationToken.None);
        var beforeStart = await store.GetByIdAsync(message.MessageId, CancellationToken.None);
        beforeStart.ShouldNotBeNull();
        beforeStart.IsReplayed.ShouldBeFalse();
        beforeStart.MovedToDeadLetterAt.ShouldBe(message.MovedToDeadLetterAt);
        var recent = new DeadLetterMessage
        {
            MessageId = Guid.NewGuid().ToString("N"), MessageType = "quarantine.fixture",
            MessageBody = "{}", MessageMetadata = "{}", Reason = "recent unprocessed obligation",
            MovedToDeadLetterAt = DateTimeOffset.UtcNow, IsReplayed = false,
        };
        await store.StoreAsync(recent, CancellationToken.None);
        var worker = provider.GetServices<IHostedService>().OfType<BackgroundService>().Single();
        await worker.StartAsync(CancellationToken.None);
        try
        {
            // Statistics follows cleanup in each poll. This signal witnesses actual loop progress;
            // the deadline bounds a broken worker, rather than standing in for evidence of a poll.
            await polled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            // Drain before inspecting retention or disposing the container, including failing controls.
            await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        var retained = await store.GetByIdAsync(message.MessageId, CancellationToken.None);
        if (expectRetained) retained.ShouldNotBeNull();
        else retained.ShouldBeNull();
        (await store.GetByIdAsync(recent.MessageId, CancellationToken.None)).ShouldNotBeNull(
            "explicit cleanup still respects retention and must not delete recent quarantine");
    }

    private static ServiceProvider CreateServices(IPoisonMessageHandler handler, Action<PoisonMessageOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        // TryAdd inside the real extension preserves this controlled statistics observer.
        _ = services.AddSingleton(handler);
        _ = services.AddPoisonMessageHandling(configure);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
