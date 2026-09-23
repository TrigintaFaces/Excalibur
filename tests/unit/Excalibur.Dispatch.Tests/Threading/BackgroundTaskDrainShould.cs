// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Threading;

/// <summary>
/// Work accepted for background execution is awaited when the host stops gracefully.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> <c>RunDetachedInBackground</c> started work on the thread pool and tracked nothing —
/// no registry, no shutdown hook, no wait. A deploy or a scale-in abandoned it, and the caller had already
/// been told the dispatch succeeded. The consumer was left with a message that was never processed and no
/// record that it was lost: silent, and indistinguishable from success.
/// </para>
/// <para>
/// <b>WHAT IS AND IS NOT CLAIMED.</b> This binds best-effort-within-the-process, drained on GRACEFUL
/// shutdown. It does not make background execution durable — an abrupt termination still loses the work,
/// which is inherent to running in-process. There is deliberately no arm asserting durability, because the
/// contract does not offer it.
/// </para>
/// <para>
/// <b>THE ARM THAT MATTERS IS THE ONE THAT PROVES A WAIT HAPPENED.</b> A drain that returned immediately
/// would satisfy any assertion about the host stopping cleanly. The safety arm therefore observes the work
/// as INCOMPLETE before stop and COMPLETE after it, so the only way to pass is to have actually waited.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class BackgroundTaskDrainShould
{
	private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

	private static IHostedService CreateDrainService() =>
		new BackgroundTaskDrainService(NullLogger<BackgroundTaskDrainService>.Instance);

	/// <summary>
	/// SAFETY — the headline. Stopping must not return until accepted work has finished.
	/// </summary>
	[Fact]
	public async Task WaitForAcceptedWorkBeforeShutdownCompletes()
	{
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var finished = false;

		BackgroundTaskRunner.RunDetachedInBackground(
			async _ =>
			{
				started.SetResult();
				await release.Task.ConfigureAwait(false);
				finished = true;
			},
			CancellationToken.None);

		// Do not stop until the work is genuinely running, or the arm could pass by draining nothing.
		await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		finished.ShouldBeFalse("precondition: the work must still be in flight when shutdown begins");

		var drain = CreateDrainService().StopAsync(CancellationToken.None);

		// The drain must still be waiting. If it has already returned, it waited for nothing and every
		// assertion below would pass against a no-op.
		drain.IsCompleted.ShouldBeFalse(
			"StopAsync must not return while accepted background work is still running — returning here is "
			+ "the abandonment this guard exists to prevent");

		release.SetResult();
		await drain.WaitAsync(Settle).ConfigureAwait(false);

		finished.ShouldBeTrue("the work must have completed before shutdown returned");
	}

	/// <summary>
	/// SAFETY. The wait is BOUNDED. A drain that could hang shutdown indefinitely would be a worse defect
	/// than the one it fixes, so cancelling the budget must return rather than block.
	/// </summary>
	[Fact]
	public async Task ReturnWhenTheShutdownBudgetElapses_RatherThanHangingTheHost()
	{
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		BackgroundTaskRunner.RunDetachedInBackground(
			async _ =>
			{
				started.SetResult();
				await release.Task.ConfigureAwait(false);
			},
			CancellationToken.None);

		await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		using var budget = new CancellationTokenSource();
		var drain = CreateDrainService().StopAsync(budget.Token);

		await budget.CancelAsync().ConfigureAwait(false);

		// Must return, and must NOT surface the cancellation as a fault: a host shutting down has no use
		// for an exception from the component whose job was to make shutdown orderly.
		await Should.NotThrowAsync(() => drain.WaitAsync(Settle));

		release.SetResult();
	}

	/// <summary>
	/// LIVENESS, and without it both arms above are satisfied by a drain that blocks forever on nothing.
	/// With no work outstanding, shutdown must complete promptly.
	/// </summary>
	[Fact]
	public async Task CompleteImmediatelyWhenNoWorkIsOutstanding() =>
		await Should.NotThrowAsync(() => CreateDrainService().StopAsync(CancellationToken.None).WaitAsync(Settle));

	/// <summary>
	/// LIVENESS. A faulting background task must not break the drain for everything else — otherwise one
	/// bad handler reinstates the abandonment at shutdown, which is the failure in a costume.
	/// </summary>
	[Fact]
	public async Task StillDrainTheRemainingWorkWhenOneBackgroundTaskFaults()
	{
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var survivorFinished = false;

		BackgroundTaskRunner.RunDetachedInBackground(
			static _ => throw new InvalidOperationException("background handler threw"),
			CancellationToken.None,
			onError: static _ => Task.CompletedTask);

		BackgroundTaskRunner.RunDetachedInBackground(
			async _ =>
			{
				await release.Task.ConfigureAwait(false);
				survivorFinished = true;
			},
			CancellationToken.None);

		var drain = CreateDrainService().StopAsync(CancellationToken.None);
		release.SetResult();

		await Should.NotThrowAsync(() => drain.WaitAsync(Settle));

		survivorFinished.ShouldBeTrue(
			"the faulting task must not abandon the healthy one — its exception is already reported at its "
			+ "own start site");
	}

	/// <summary>
	/// SAFETY — THE WIRING, which every arm above is blind to. They construct the service directly, so all
	/// four stay green with the registration deleted: the drain would be correct and never run.
	/// </summary>
	/// <remarks>
	/// Resolves through the REAL registration path rather than asserting a descriptor, because a descriptor
	/// check passes on a registration the container cannot actually satisfy. Both public entry points are
	/// covered: registering one and not the other is the half-wire this arm exists to catch.
	/// </remarks>
	[Fact]
	public void BeRegisteredAsAHostedServiceByTheThreadingRegistration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchThreading();

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IHostedService>().OfType<BackgroundTaskDrainService>().ShouldHaveSingleItem(
			"AddDispatchThreading must register the drain as an IHostedService. Without it the drain is correct "
			+ "code that never runs, and background work is abandoned at shutdown exactly as before — the "
			+ "advertised-but-unwired shape, pointed at a guard.");
	}

	/// <summary>
	/// SAFETY — THE WIRING, for the other <c>AddDispatchThreading</c> overload. Registering one overload and
	/// not the other is the half-wire; the arm above covers only the <c>Action</c> form.
	/// </summary>
	[Fact]
	public void BeRegisteredAsAHostedServiceByTheConfigurationBoundThreadingRegistration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatchThreading(new ConfigurationBuilder().Build());

		using var provider = services.BuildServiceProvider();

		provider.GetServices<IHostedService>().OfType<BackgroundTaskDrainService>().ShouldHaveSingleItem(
			"the IConfiguration overload of AddDispatchThreading must register the drain as well");
	}

	/// <summary>
	/// SAFETY — THE DOCUMENTED ENTRY POINT, end to end. <c>UseBackgroundExecution()</c> is how the
	/// middleware documentation tells a consumer to turn background execution on, so it must bring the drain
	/// with it. A consumer who follows the documentation and never calls <c>AddDispatchThreading</c> must
	/// still have a background-dispatched message awaited at shutdown.
	/// </summary>
	/// <remarks>
	/// Dispatches through the middleware resolved from the real registration and stops through the drain
	/// resolved from the same container, so it binds the wiring AND the behaviour on the path a consumer
	/// actually takes.
	/// </remarks>
	[Fact]
	public async Task DrainABackgroundMessage_WhenEnabledThroughUseBackgroundExecution()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static dispatch => dispatch.UseBackgroundExecution());

		await using var provider = services.BuildServiceProvider();

		var drainService = provider.GetServices<IHostedService>().OfType<BackgroundTaskDrainService>()
			.ShouldHaveSingleItem(
				"UseBackgroundExecution() must register the shutdown drain. Without it the documented way to "
				+ "enable background execution abandons in-flight messages at shutdown.");

		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var handled = false;

		async ValueTask<IMessageResult> Handler(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			started.SetResult();
			await release.Task.ConfigureAwait(false);
			handled = true;
			return MessageResult.Success();
		}

		await using var scope = provider.CreateAsyncScope();
		var middleware = scope.ServiceProvider.GetRequiredService<BackgroundExecutionMiddleware>();
		var context = new TestMessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "Background" };

		var result = await middleware.InvokeAsync(new BackgroundMessage(), context, Handler, CancellationToken.None)
			.ConfigureAwait(false);
		result.Disposition.ShouldBe(MessageDisposition.AcceptedForBackgroundExecution);

		await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		var drain = drainService.StopAsync(CancellationToken.None);
		drain.IsCompleted.ShouldBeFalse(
			"shutdown must not complete while the background-dispatched message is still being handled");

		release.SetResult();
		await drain.WaitAsync(Settle).ConfigureAwait(false);

		handled.ShouldBeTrue("the handler must have finished before shutdown returned");
	}

	/// <summary>
	/// SAFETY — NEVER SILENT. Work still running when the shutdown budget elapses is lost, which is the
	/// documented limit of in-process execution. Lost must be REPORTED: an error naming how many were
	/// abandoned, distinct from the informational record of a drain that finished.
	/// </summary>
	[Fact]
	public async Task ReportWorkAbandonedWhenTheShutdownBudgetElapses()
	{
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		BackgroundTaskRunner.RunDetachedInBackground(
			async _ =>
			{
				started.SetResult();
				await release.Task.ConfigureAwait(false);
			},
			CancellationToken.None);

		await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		var logger = new FakeLogger<BackgroundTaskDrainService>();
		using var budget = new CancellationTokenSource();
		await budget.CancelAsync().ConfigureAwait(false);

		try
		{
			await new BackgroundTaskDrainService(logger).StopAsync(budget.Token).WaitAsync(Settle).ConfigureAwait(false);

			var abandoned = logger.Collector.GetSnapshot()
				.Where(r => r.Id.Id == CoreEventId.BackgroundWorkAbandonedOnShutdown)
				.ShouldHaveSingleItem(
					"work still running when the shutdown budget elapsed must be reported, never dropped silently");
			abandoned.Level.ShouldBe(LogLevel.Error);
		}
		finally
		{
			release.SetResult();
		}
	}

	/// <summary>
	/// LIVENESS for the arm above. A drain that FINISHED must not report abandonment — otherwise the error
	/// is emitted on every shutdown and an operator learns to ignore the one that means work was lost.
	/// </summary>
	[Fact]
	public async Task NotReportAbandonmentWhenEveryTaskFinishedWithinTheBudget()
	{
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		BackgroundTaskRunner.RunDetachedInBackground(_ => release.Task, CancellationToken.None);

		var logger = new FakeLogger<BackgroundTaskDrainService>();
		var drain = new BackgroundTaskDrainService(logger).StopAsync(CancellationToken.None);
		release.SetResult();
		await drain.WaitAsync(Settle).ConfigureAwait(false);

		logger.Collector.GetSnapshot().ShouldNotContain(r => r.Id.Id == CoreEventId.BackgroundWorkAbandonedOnShutdown);
	}

	/// <summary>
	/// SAFETY. Background work may itself start background work — a background handler that dispatches
	/// another background message. Work accepted WHILE the drain is waiting was accepted before shutdown
	/// completed, so it is owed the same wait; a drain that only awaited a snapshot taken at the start would
	/// abandon it.
	/// </summary>
	[Fact]
	public async Task AlsoAwaitWorkStartedWhileTheDrainIsWaiting()
	{
		var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondFinished = false;

		BackgroundTaskRunner.RunDetachedInBackground(
			async _ =>
			{
				await releaseFirst.Task.ConfigureAwait(false);

				BackgroundTaskRunner.RunDetachedInBackground(
					async _ =>
					{
						secondStarted.SetResult();
						await releaseSecond.Task.ConfigureAwait(false);
						secondFinished = true;
					},
					CancellationToken.None);
			},
			CancellationToken.None);

		var drain = CreateDrainService().StopAsync(CancellationToken.None);

		releaseFirst.SetResult();
		await secondStarted.Task.WaitAsync(Settle).ConfigureAwait(false);

		drain.IsCompleted.ShouldBeFalse(
			"work started while the drain was waiting must be awaited too, not abandoned because it missed the "
			+ "snapshot");

		releaseSecond.SetResult();
		await drain.WaitAsync(Settle).ConfigureAwait(false);

		secondFinished.ShouldBeTrue();
	}

	/// <summary>
	/// SAFETY — NEVER SILENT, at the start site. A token that is already cancelled when work is handed over
	/// used to cancel the thread-pool task before the work ran, which reached neither the error handler nor
	/// the logger: the work vanished with no record. Cancellation is the work's to observe and report.
	/// </summary>
	[Fact]
	public async Task ReportRatherThanSilentlyDiscardWorkHandedAnAlreadyCancelledToken()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync().ConfigureAwait(false);
		var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

		BackgroundTaskRunner.RunDetachedInBackground(
			static ct =>
			{
				ct.ThrowIfCancellationRequested();
				return Task.CompletedTask;
			},
			cancelled.Token,
			onError: ex =>
			{
				_ = reported.TrySetResult(ex);
				return Task.CompletedTask;
			});

		var exception = await reported.Task.WaitAsync(Settle).ConfigureAwait(false);
		exception.ShouldBeAssignableTo<OperationCanceledException>();
	}

	private sealed class BackgroundMessage : IDispatchMessage, IExecuteInBackground;
}
