// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Regression lock for uco9lt: the SQS channel hosted service must bound its shutdown drain. The pre-fix
/// <c>ExecuteAsync</c> finally stopped the processor with <see cref="CancellationToken.None"/> (an
/// uncancellable token), so a stalled SQS <c>StopAsync</c> could block process exit indefinitely,
/// ignoring the host shutdown deadline. The fix passes a deadline-bounded (cancellable) token.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsChannelProcessorHostedServiceShutdownShould
{
	[Fact]
	public async Task Pass_a_bounded_cancellable_token_to_the_processor_stop_on_shutdown()
	{
		var processor = A.Fake<ISqsChannelProcessor>();

		// Observe STARTUP too, and wait for it before shutting down.
		//
		// BackgroundService.StartAsync does not wait for ExecuteAsync to reach its body -- it kicks the
		// task off and returns. Stopping immediately can therefore cancel the service before ExecuteAsync
		// has run at all, and a body that never ran has no finally to drain through. That is what was
		// happening: the failure reported ZERO recorded calls on this fake, not a missing StopAsync among
		// present ones, so the processor was never started either.
		//
		// It reproduced only alongside unrelated fast tests (Kafka.SchemaRegistry) because those change
		// how soon the shutdown lands relative to the startup continuation -- pure scheduling, no shared
		// state, which is why the test passed 6/6 alone and 4/4 across its own namespace.
		var startObserved = new TaskCompletionSource();
		A.CallTo(() => processor.StartAsync(A<CancellationToken>._))
			.Invokes(() => startObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		CancellationToken stopToken = default;
		var stopObserved = new TaskCompletionSource();
		A.CallTo(() => processor.StopAsync(A<CancellationToken>._))
			.Invokes((CancellationToken t) =>
			{
				stopToken = t;
				stopObserved.TrySetResult();
			})
			.Returns(Task.CompletedTask);

		var options = Microsoft.Extensions.Options.Options.Create(
			new SqsProcessorOptions { DrainTimeoutSeconds = 30 });
		var service = new SqsChannelProcessorHostedService(
			processor, options, NullLogger<SqsChannelProcessorHostedService>.Instance);

		await service.StartAsync(CancellationToken.None);

		// The service is only genuinely running once ExecuteAsync has entered its body. Bounded so a
		// regression that never starts fails in seconds with this message instead of hanging the host.
		await startObserved.Task.WaitAsync(TimeSpan.FromSeconds(30));

		// Trigger shutdown → ExecuteAsync's finally stops the processor with the bounded drain token.
		//
		// This await is BOUNDED on purpose, and the bound is not about flakiness -- it is about what an
		// unbounded await costs when its premise is wrong.
		//
		// The premise is that BackgroundService.StopAsync awaits ExecuteAsync to completion, so the finally
		// that sets stopObserved has already run by the time StopAsync returns, making this task
		// already-completed. When that holds, the bound is never reached and costs nothing. When it does
		// not, a bare `await stopObserved.Task` waits FOREVER: the test never fails, it hangs, and it takes
		// the whole assembly's test host with it -- 4,500 unrelated tests in this run reported as a host
		// crash with no failing test attached, which is far more expensive and far harder to diagnose than
		// the flake the bound was removed to avoid.
		//
		// A generous bound gets both: no flake under parallel load, and a diagnosable failure rather than a
		// dead host if the premise ever breaks.
		await service.StopAsync(CancellationToken.None);

		// Assert on the RECORDED invocation, not on a continuation.
		//
		// FakeItEasy records the call synchronously the moment the finally invokes StopAsync, so once
		// service.StopAsync has returned the evidence either exists or it does not -- there is no
		// continuation left to schedule and therefore nothing to wait on. Awaiting a
		// TaskCompletionSource here made the test depend on when that continuation happened to run: bare,
		// it hung the entire test host, and bounded, it spent a minute before failing.
		A.CallTo(() => processor.StopAsync(A<CancellationToken>._)).MustHaveHappened();
		stopObserved.Task.IsCompleted.ShouldBeTrue(
			"the shutdown drain must have invoked the processor's StopAsync by the time the hosted "
			+ "service's StopAsync returns");

		// Non-vacuity: pre-fix the finally passed CancellationToken.None (CanBeCanceled == false).
		stopToken.CanBeCanceled.ShouldBeTrue();

		service.Dispose();
	}
}
