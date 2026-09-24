// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

using Tests.Shared.TestDoubles;

using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Threading;

/// <summary>
/// The failure policy and the caller-token contract of background execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>FAILURE POLICY IS HOST-LEVEL.</b> What happens when a background message fails is
/// <see cref="BackgroundExecutionOptions.ExceptionBehavior"/>, in the shape of
/// <c>HostOptions.BackgroundServiceExceptionBehavior</c>: <see cref="BackgroundExecutionExceptionBehavior.LogOnly"/>
/// logs, <see cref="BackgroundExecutionExceptionBehavior.StopHost"/> logs and stops the host, and StopHost without a
/// host lifetime is refused at startup rather than degrading silently at the first failure.
/// </para>
/// <para>
/// <b>THE CALLER'S TOKEN.</b> Already cancelled at dispatch: not accepted, said synchronously, handler never runs.
/// Once accepted: the work no longer follows the caller — a request-scoped token fires when the response carrying
/// the acceptance completes — and follows the shutdown drain instead.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class BackgroundExecutionPolicyShould
{
	private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

	/// <summary>
	/// SAFETY. Under StopHost, a failing background message stops the host.
	/// </summary>
	[Fact]
	public async Task StopTheHost_WhenABackgroundMessageFails_UnderStopHost()
	{
		var stopRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var lifetime = A.Fake<IHostApplicationLifetime>();
		A.CallTo(() => lifetime.StopApplication()).Invokes(() => stopRequested.TrySetResult());

		var middleware = CreateMiddleware(BackgroundExecutionExceptionBehavior.StopHost, lifetime);

		var result = await middleware.InvokeAsync(new BackgroundMessage(), CreateContext(), Throwing, CancellationToken.None)
			.ConfigureAwait(false);
		result.Disposition.ShouldBe(MessageDisposition.AcceptedForBackgroundExecution);

		await stopRequested.Task.WaitAsync(Settle).ConfigureAwait(false);
		A.CallTo(() => lifetime.StopApplication()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// LIVENESS for the arm above. Under the default LogOnly, the same failure is logged and the host keeps
	/// running — otherwise StopHost would be satisfied by a middleware that stops the host on every failure.
	/// </summary>
	[Fact]
	public async Task LogButNotStopTheHost_WhenABackgroundMessageFails_UnderLogOnly()
	{
		var lifetime = A.Fake<IHostApplicationLifetime>();
		var logger = new FakeLogger<BackgroundExecutionMiddleware>();
		var middleware = new BackgroundExecutionMiddleware(
			MicrosoftOptions.Create(new BackgroundExecutionOptions()),
			logger,
			lifetime);

		_ = await middleware.InvokeAsync(new BackgroundMessage(), CreateContext(), Throwing, CancellationToken.None)
			.ConfigureAwait(false);

		// Await the background work itself, so the assertions below cannot run before the failure was handled.
		await CreateDrainService().StopAsync(CancellationToken.None).WaitAsync(Settle).ConfigureAwait(false);

		logger.Collector.GetSnapshot().ShouldContain(
			r => r.Id.Id == CoreEventId.BackgroundExecutionFailed && r.Level == LogLevel.Error,
			"the failure must still be logged — LogOnly is not SilentOnly");
		A.CallTo(() => lifetime.StopApplication()).MustNotHaveHappened();
	}

	/// <summary>
	/// SAFETY. StopHost with no <see cref="IHostApplicationLifetime"/> is refused at STARTUP, through the
	/// documented entry point, rather than degrading to log-only at the first failure.
	/// </summary>
	[Fact]
	public void FailStartup_WhenStopHostIsConfiguredWithoutAHostLifetime()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static dispatch => dispatch.UseBackgroundExecution());
		_ = services.Configure<BackgroundExecutionOptions>(
			static o => o.ExceptionBehavior = BackgroundExecutionExceptionBehavior.StopHost);

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IStartupValidator>().Validate());
		ex.OptionsType.ShouldBe(typeof(BackgroundExecutionOptions));
	}

	/// <summary>
	/// LIVENESS for the arm above. With a host lifetime present, StopHost starts — otherwise the validator would
	/// be satisfied by refusing StopHost unconditionally.
	/// </summary>
	[Fact]
	public void PassStartup_WhenStopHostIsConfiguredWithAHostLifetime()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IHostApplicationLifetime>());
		_ = services.AddDispatch(static dispatch => dispatch.UseBackgroundExecution());
		_ = services.Configure<BackgroundExecutionOptions>(
			static o => o.ExceptionBehavior = BackgroundExecutionExceptionBehavior.StopHost);

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>
	/// SAFETY. A dispatch whose token is already cancelled is NOT accepted: the synchronous result says so and the
	/// handler never runs.
	/// </summary>
	[Fact]
	public async Task NotAcceptWork_WhenTheCallerTokenIsAlreadyCancelled()
	{
		var middleware = CreateMiddleware(BackgroundExecutionExceptionBehavior.LogOnly);
		var handlerRan = false;

		ValueTask<IMessageResult> Handler(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync().ConfigureAwait(false);

		var result = await middleware.InvokeAsync(new BackgroundMessage(), CreateContext(), Handler, cancelled.Token)
			.ConfigureAwait(false);

		result.Succeeded.ShouldBeFalse("a withdrawn dispatch must not be reported as accepted");
		result.Disposition.ShouldNotBe(MessageDisposition.AcceptedForBackgroundExecution);
		result.ShouldBeSameAs(MessageResult.Cancelled(), "the existing cancelled result is the vocabulary for this");

		// Anything accepted would now be in flight; wait for it before asserting nothing ran.
		await CreateDrainService().StopAsync(CancellationToken.None).WaitAsync(Settle).ConfigureAwait(false);
		handlerRan.ShouldBeFalse("work the caller withdrew before hand-over must never run");
	}

	/// <summary>
	/// SAFETY. Once accepted, cancelling the caller's token does NOT cancel the work. The caller was told the work
	/// was accepted; a request-scoped token fires when that response completes.
	/// </summary>
	[Fact]
	public async Task KeepRunningAcceptedWork_WhenTheCallerTokenIsCancelledAfterAcceptance()
	{
		var middleware = CreateMiddleware(BackgroundExecutionExceptionBehavior.LogOnly);
		var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		async ValueTask<IMessageResult> Handler(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			started.SetResult(ct);
			await release.Task.ConfigureAwait(false);
			_ = finished.TrySetResult(ct.IsCancellationRequested);
			return MessageResult.Success();
		}

		using var caller = new CancellationTokenSource();
		var result = await middleware.InvokeAsync(new BackgroundMessage(), CreateContext(), Handler, caller.Token)
			.ConfigureAwait(false);
		result.Disposition.ShouldBe(MessageDisposition.AcceptedForBackgroundExecution);

		var workToken = await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		// The response carrying "Accepted" has completed; a request-scoped token now fires.
		await caller.CancelAsync().ConfigureAwait(false);

		workToken.IsCancellationRequested.ShouldBeFalse(
			"accepted work must not follow the caller's token — the caller was told it was accepted");

		release.SetResult();
		var observedCancelled = await finished.Task.WaitAsync(Settle).ConfigureAwait(false);
		observedCancelled.ShouldBeFalse("the work ran to completion without being cancelled by the caller");
	}

	/// <summary>
	/// SAFETY. Accepted work follows the DRAIN: when the host's shutdown budget elapses with it still running, its
	/// token is cancelled so cooperative work can stop, rather than being cut off with no signal.
	/// </summary>
	[Fact]
	public async Task CancelAcceptedWork_WhenTheShutdownBudgetElapses()
	{
		var middleware = CreateMiddleware(BackgroundExecutionExceptionBehavior.LogOnly);
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var observedShutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		async ValueTask<IMessageResult> Handler(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			started.SetResult();
			try
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false); // delay-ok: InfiniteTimeSpan is block-until-cancelled -- the arm observes the CANCELLATION, never an elapsed time
			}
			catch (OperationCanceledException)
			{
				_ = observedShutdown.TrySetResult();
				throw;
			}

			return MessageResult.Success();
		}

		_ = await middleware.InvokeAsync(new BackgroundMessage(), CreateContext(), Handler, CancellationToken.None)
			.ConfigureAwait(false);
		await started.Task.WaitAsync(Settle).ConfigureAwait(false);

		using var budget = new CancellationTokenSource();
		await budget.CancelAsync().ConfigureAwait(false);
		await CreateDrainService().StopAsync(budget.Token).WaitAsync(Settle).ConfigureAwait(false);

		await observedShutdown.Task.WaitAsync(Settle).ConfigureAwait(false);
	}

	private static ValueTask<IMessageResult> Throwing(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
		throw new InvalidOperationException("background handler failed");

	private static BackgroundExecutionMiddleware CreateMiddleware(
		BackgroundExecutionExceptionBehavior behavior,
		IHostApplicationLifetime? lifetime = null) =>
		new(
			MicrosoftOptions.Create(new BackgroundExecutionOptions { ExceptionBehavior = behavior }),
			NullLogger<BackgroundExecutionMiddleware>.Instance,
			lifetime ?? A.Fake<IHostApplicationLifetime>());

	private static IHostedService CreateDrainService() =>
		new BackgroundTaskDrainService(NullLogger<BackgroundTaskDrainService>.Instance);

	private static TestMessageContext CreateContext() =>
		new() { MessageId = Guid.NewGuid().ToString(), MessageType = "Background" };

	private sealed class BackgroundMessage : IDispatchMessage, IExecuteInBackground;
}
