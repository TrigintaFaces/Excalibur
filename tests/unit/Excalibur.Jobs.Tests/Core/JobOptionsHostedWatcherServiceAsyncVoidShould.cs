// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Sprint 847 / Lane J (bead imtsrv) — author≠impl regression lock for the
/// <see cref="JobOptionsHostedWatcherService{TJob, TOptions}"/> async-void config-change fault (MS-J).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> the <c>IOptionsMonitor.OnChange</c> callback is an
/// <c>async</c> lambda bound as <c>async void</c>, and its catch block re-throws (<c>throw;</c>). A
/// scheduler fault during a config reload is therefore raised with no awaiter — an unobserved
/// fire-and-forget exception that crashes the host process.
/// </para>
/// <para>
/// <b>Fix (FR-J1..J3):</b> the callback is no longer <c>async void</c>; a fault is caught + logged and
/// never re-thrown / never escapes.
/// </para>
/// <para>
/// <b>Deterministic, host-safe discriminator:</b> we install an
/// <see cref="ExceptionCapturingSynchronizationContext"/> as current before firing the change. The faked
/// <see cref="IScheduler.PauseJob"/> throws <em>synchronously</em>, so the whole async-void state machine
/// runs inline on the test thread and (pre-fix) the re-thrown exception is routed through the
/// async-void builder's <c>SynchronizationContext.Post</c> into our capturing context — where we record
/// it safely instead of letting it terminate the test host. Pre-fix ⇒ an escaped exception is captured
/// (RED). Post-fix ⇒ caught + logged, nothing escapes (GREEN). This binds the escape behaviour (AC-J2)
/// without depending on the chosen fix shape and without GC/unobserved-finalizer flakiness.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Resilience")]
public sealed class JobOptionsHostedWatcherServiceAsyncVoidShould
{
	[Fact]
	public async Task NotLetAConfigChangeFaultEscapeAsUnobservedAsyncVoid()
	{
		// Arrange — initial config enabled (ResumeJob, succeeds); the change disables (PauseJob, throws).
		var scheduler = A.Fake<IScheduler>();
		_ = A.CallTo(() => scheduler.PauseJob(A<JobKey>._, A<CancellationToken>._))
			.Throws(() => new InvalidOperationException("scheduler fault during config reload"));

		var monitor = new TriggerableOptionsMonitor<AsyncVoidWatcherJobOptions>(
			new AsyncVoidWatcherJobOptions { Disabled = false });

		using var service = new JobOptionsHostedWatcherService<AsyncVoidWatcherJob, AsyncVoidWatcherJobOptions>(
			scheduler,
			monitor,
			NullLogger<JobOptionsHostedWatcherService<AsyncVoidWatcherJob, AsyncVoidWatcherJobOptions>>.Instance);

		await service.StartAsync(CancellationToken.None);

		// Act — fire the change under a capturing SynchronizationContext so an async-void re-throw is
		// routed to us deterministically (rather than crashing the host or going unobserved).
		var capturingContext = new ExceptionCapturingSynchronizationContext();
		var previous = SynchronizationContext.Current;
		SynchronizationContext.SetSynchronizationContext(capturingContext);
		try
		{
			monitor.TriggerChange(new AsyncVoidWatcherJobOptions { Disabled = true });
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(previous);
		}

		// Assert — no fault may escape the change handler (FR-J2 / AC-J2).
		capturingContext.CapturedExceptions.ShouldBeEmpty(
			"a config-change fault MUST be caught and logged inside the watcher, never escape as an " +
			"unobserved async-void throw that crashes the host.");
	}

	/// <summary>An <see cref="IOptionsMonitor{T}"/> whose change listeners can be fired on demand.</summary>
	private sealed class TriggerableOptionsMonitor<T>(T initialValue) : IOptionsMonitor<T>
	{
		private readonly List<Action<T, string?>> _listeners = [];

		public T CurrentValue { get; private set; } = initialValue;

		public T Get(string? name) => CurrentValue;

		public IDisposable OnChange(Action<T, string?> listener)
		{
			_listeners.Add(listener);
			return new Subscription(this, listener);
		}

		public void TriggerChange(T newValue)
		{
			CurrentValue = newValue;
			foreach (var listener in _listeners.ToArray())
			{
				listener(newValue, null);
			}
		}

		private sealed class Subscription(TriggerableOptionsMonitor<T> monitor, Action<T, string?> listener) : IDisposable
		{
			public void Dispose() => monitor._listeners.Remove(listener);
		}
	}

	/// <summary>
	/// Captures exceptions an async-void builder posts via <see cref="SynchronizationContext.Post"/>,
	/// instead of re-throwing them (which would terminate the test host).
	/// </summary>
	private sealed class ExceptionCapturingSynchronizationContext : SynchronizationContext
	{
		private readonly List<Exception> _captured = [];

		public IReadOnlyList<Exception> CapturedExceptions => _captured;

		public override void Post(SendOrPostCallback d, object? state)
		{
			ArgumentNullException.ThrowIfNull(d);
			try
			{
				d(state);
			}
			catch (Exception ex)
			{
				_captured.Add(ex);
			}
		}

		public override void Send(SendOrPostCallback d, object? state)
		{
			ArgumentNullException.ThrowIfNull(d);
			try
			{
				d(state);
			}
			catch (Exception ex)
			{
				_captured.Add(ex);
			}
		}
	}

	private sealed class AsyncVoidWatcherJobOptions : JobOptions
	{
		public AsyncVoidWatcherJobOptions()
		{
			JobName = "AsyncVoidWatcherJob";
			JobGroup = "S847LaneJ";
		}
	}

	private sealed class AsyncVoidWatcherJob : IConfigurableJob<AsyncVoidWatcherJobOptions>
	{
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
