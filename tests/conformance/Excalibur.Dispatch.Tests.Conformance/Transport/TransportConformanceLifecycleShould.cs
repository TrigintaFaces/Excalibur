// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Pins the lifecycle behaviour of <see cref="TransportConformanceTestBase{TSender,TReceiver}" /> that no
/// conformance arm can observe about itself: that one failed restart cannot silently disable the rest of a
/// transport's suite, and that a half-built transport is still torn down.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportConformanceLifecycleShould
{
	/// <summary>
	/// A failing restart inside <c>Should_Support_Graceful_Shutdown</c> must not latch the per-closed-generic
	/// static failure cache.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The restart used to run through <c>InitializeAsync</c>, which writes <c>s_transportInitialized</c> --
	/// a STATIC, per closed generic, that nothing ever resets. So a single flaky container restart in one
	/// fact disabled every fact of that transport that happened to run afterwards, and because xUnit does not
	/// guarantee intra-class ordering, WHICH facts were lost varied run to run. The suite grew quieter under
	/// load, which is precisely when it should grow louder.
	/// </para>
	/// <para>
	/// RED before the fix: the third initialization takes the poisoned fast path and never calls the factory,
	/// leaving the count at 2.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Disable_Later_Initializations_When_A_Restart_Fails()
	{
		var probe = new RestartProbe();

		// 1st factory call: initial initialization succeeds.
		await probe.InitializeAsync();
		probe.SenderCreations.ShouldBe(1);

		// 2nd factory call: the restart throws, and that is this fact's own failure to report.
		_ = await Should.ThrowAsync<InvalidOperationException>(probe.Should_Support_Graceful_Shutdown);
		probe.SenderCreations.ShouldBe(2);

		// 3rd factory call: a fresh instance of the SAME closed generic must still try.
		var next = new RestartProbe();
		await next.InitializeAsync();

		RestartProbe.TotalSenderCreations.ShouldBe(
			3,
			"a failed restart must not latch the static failure cache: the next instance of this transport "
			+ "has to attempt initialization, or one flaky restart silently disables the rest of the suite.");
	}

	/// <summary>
	/// Disposal must run when initialization built some resources and then threw, or the container the
	/// half-built transport is holding leaks for the rest of the run.
	/// </summary>
	/// <remarks>RED before the fix: disposal was gated on the availability flag, which a failed init leaves false.</remarks>
	[Fact]
	public async Task Dispose_A_Transport_Whose_Initialization_Failed_Halfway()
	{
		var probe = new HalfBuiltProbe();

		// Fail-closed is the default, so a broken initialization surfaces rather than skipping.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () => await probe.InitializeAsync());

		await probe.DisposeAsync();

		probe.Disposed.ShouldBeTrue(
			"the sender was already built when the receiver threw, so something is holding infrastructure; "
			+ "gating disposal on successful initialization leaks it.");
	}

	// The base class's failure cache is static PER CLOSED GENERIC, so each probe needs its own type
	// arguments. Sharing them would let one probe's failure latch the other's cache and make these two
	// tests order-dependent on each other -- the very defect the first one exists to pin.
	private sealed class RestartSender : IChannelSender
	{
		public Task SendAsync<T>(T message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class RestartReceiver : IChannelReceiver
	{
		public Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken) => Task.FromResult<T?>(default);
	}

	private sealed class HalfBuiltSender : IChannelSender
	{
		public Task SendAsync<T>(T message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class HalfBuiltReceiver : IChannelReceiver
	{
		public Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken) => Task.FromResult<T?>(default);
	}

	/// <summary>
	/// Fails only its SECOND sender construction, which is the restart inside the graceful-shutdown arm.
	/// </summary>
	private sealed class RestartProbe : TransportConformanceTestBase<RestartSender, RestartReceiver>
	{
		private static int s_totalSenderCreations;

		internal static int TotalSenderCreations => s_totalSenderCreations;

		internal int SenderCreations => s_totalSenderCreations;

		// Keeps the probe out of the real run's liveness ledger: it is not a shipping transport, and
		// recording it as a selected broker suite that never executes an arm would fail the liveness gate.
		protected override bool UsesExternalBroker => false;

		protected override Task<RestartSender> CreateSenderAsync() =>
			Interlocked.Increment(ref s_totalSenderCreations) == 2
				? throw new InvalidOperationException("simulated restart failure")
				: Task.FromResult(new RestartSender());

		protected override Task<RestartReceiver> CreateReceiverAsync() => Task.FromResult(new RestartReceiver());

		protected override Task<IDeadLetterQueueManager?> CreateDlqManagerAsync() =>
			Task.FromResult<IDeadLetterQueueManager?>(null);

		protected override Task DisposeTransportAsync() => Task.CompletedTask;
	}

	/// <summary>Builds a sender, then throws while building the receiver.</summary>
	private sealed class HalfBuiltProbe : TransportConformanceTestBase<HalfBuiltSender, HalfBuiltReceiver>
	{
		internal bool Disposed { get; private set; }

		protected override bool UsesExternalBroker => false;

		protected override Task<HalfBuiltSender> CreateSenderAsync() => Task.FromResult(new HalfBuiltSender());

		protected override Task<HalfBuiltReceiver> CreateReceiverAsync() =>
			throw new InvalidOperationException("simulated receiver construction failure");

		protected override Task<IDeadLetterQueueManager?> CreateDlqManagerAsync() =>
			Task.FromResult<IDeadLetterQueueManager?>(null);

		protected override Task DisposeTransportAsync()
		{
			Disposed = true;
			return Task.CompletedTask;
		}
	}
}
