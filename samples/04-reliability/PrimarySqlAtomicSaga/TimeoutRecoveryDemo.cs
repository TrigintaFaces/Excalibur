using System.Runtime.ExceptionServices;
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace PrimarySqlAtomicSaga;

// Direct public storage protocol demonstration, not a hosted dispatch failure simulation.
internal static class TimeoutRecoveryDemo
{
    internal static async Task RunAsync(IServiceProvider provider, CancellationToken token)
    {
        using var tenant = TenantContextHolder.BeginScope("timeout-recovery-demo");
        var timers = provider.GetRequiredService<ISagaTimeoutStore>();
        var failures = ((IServiceProvider)timers).GetService(typeof(ISagaTimeoutFailureStore))
            as ISagaTimeoutFailureStore
            ?? throw new InvalidOperationException("Selected timeout store lacks failure hold support.");
        await failures.ValidateFailureStorageAsync(token);
        var sagaId = Guid.NewGuid().ToString();
        var timeoutId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        byte[] payload = [1, 2, 3];
        Exception? primary = null;
        Exception? cleanupFailure = null;
        try
        {
            await timers.ScheduleTimeoutAsync(new SagaTimeout(timeoutId, sagaId,
                "storage-protocol-demo", "simulated-poison", payload, now, now), token);
            var claim = (await timers.ClaimDueTimeoutsAsync(now, 1, token)).Single();
            Require(claim.Timeout.TimeoutId == timeoutId, "Use an idle dedicated database for this demo.");
            var observation = new SagaTimeoutFailureObservation(claim, MessageFailureKind.Poison,
                "Explicit simulated poison; no handler was invoked.");
            var operationId = Guid.NewGuid();
            var held = await failures.TryHoldFailureAsync(observation, claim, operationId, token);
            Require(held.Outcome == SagaTimeoutHoldOutcome.Held && held.Receipt is not null,
                "The selected store did not commit a hold.");
            Require((await timers.ClaimDueTimeoutsAsync(now, 1, token)).Count == 0,
                "Held source remained claimable.");
            var page = await failures.GetHeldFailuresAsync("timeout-recovery-demo", sagaId, 10, null, token);
            var expected = page.Items.Single();
            Require(expected.Timeout.TimeoutId == timeoutId && expected.Timeout.TimeoutData!.SequenceEqual(payload),
                "Held source did not preserve the original identity/bytes.");
            Console.WriteLine("TIMEOUT_POISON_HELD_AND_QUERIED");
            var commandId = Guid.NewGuid(); // Retain this and expected.Token across uncertain acknowledgments.
            var resumed = await failures.TryResumeHeldFailureAsync(expected.Token, commandId, token);
            Require(resumed?.State == SagaTimeoutHoldState.Resumed, "Conditional resume was refused.");
            var replay = await failures.TryResumeHeldFailureAsync(expected.Token, commandId, token);
            Require(replay?.State == SagaTimeoutHoldState.Resumed, "Resume receipt replay failed.");
            var fresh = (await timers.ClaimDueTimeoutsAsync(now, 1, token)).Single();
            Require(fresh.Timeout.TimeoutId == timeoutId && fresh.SourceEpoch == claim.SourceEpoch
                && fresh.DeliveryEpoch != claim.DeliveryEpoch
                && fresh.Timeout.TimeoutData!.SequenceEqual(payload), "Resume did not preserve source/change cycle.");
            Console.WriteLine("TIMEOUT_RESUMED_WITH_ORIGINAL_PAYLOAD_NEW_CYCLE");
        }
        catch (Exception error)
        {
            primary = error;
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await timers.CancelAllTimeoutsAsync(sagaId, cleanup.Token); }
            catch (Exception error) { cleanupFailure = error; }
        }
        if (primary is not null && cleanupFailure is not null)
        { throw new AggregateException("Demo failed and source cleanup also failed.", primary, cleanupFailure); }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
        if (cleanupFailure is not null) { ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
