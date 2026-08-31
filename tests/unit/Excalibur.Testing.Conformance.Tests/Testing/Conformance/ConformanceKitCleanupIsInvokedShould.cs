// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that a kit actually INVOKES the cleanup seam it asks consumers to override.
/// </summary>
/// <remarks>
/// <para>
/// The kits declared <c>CleanupAsync</c>, documented it in their consumer-facing examples, and never
/// called it. That failure is invisible from the deriver's side by construction: an override that is
/// never invoked is indistinguishable from one that works, because nothing reports the difference. The
/// arms simply share state, and a suite whose arms contaminate each other still reports green until one
/// of them happens to collide.
/// </para>
/// <para>
/// So the fix needs a test that observes the invocation rather than the absence of a symptom. This probe
/// counts calls. Against the kit as it stood before the lifecycle seam was wired, the count is zero and
/// every assertion below fails; that is the whole content of the fix.
/// </para>
/// <para>
/// Ordering is asserted too, and separately. Resetting only AFTER an arm makes every arm's starting
/// state a function of whether its predecessor finished cleanly — which is a weaker guarantee wearing the
/// same green.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceKitCleanupIsInvokedShould
{
	/// <summary>
	/// The seam runs at least once per arm — the property that was false for every kit it was declared on.
	/// </summary>
	[Fact]
	public async Task InvokeCleanupWhenAnArmObtainsItsStore()
	{
		var probe = new CountingInboxProbe();

		await probe.RunOneArmAsync().ConfigureAwait(false);

		probe.CleanupCalls.ShouldBeGreaterThan(
			0,
			"the kit must invoke the cleanup seam it instructs consumers to override; a seam that is "
			+ "declared, documented and never called is indistinguishable from one that works.");
	}

	/// <summary>
	/// Cleanup runs BEFORE the arm's work, not merely at some point.
	/// </summary>
	/// <remarks>
	/// The distinction is the whole value of the seam. Cleaning up afterwards leaves each arm's starting
	/// state dependent on its predecessor completing, so a suite stays green until an arm fails midway and
	/// silently poisons the next one.
	/// </remarks>
	[Fact]
	public async Task InvokeCleanupBeforeTheArmTouchesTheStore()
	{
		var probe = new CountingInboxProbe();

		await probe.RunOneArmAsync().ConfigureAwait(false);

		probe.FirstStoreCallSawCleanup.ShouldBeTrue(
			"cleanup must run before the arm's first store operation; resetting only afterwards makes "
			+ "every arm's starting state a function of whether its predecessor finished cleanly.");
	}

	/// <summary>
	/// Each arm gets its own cleanup, so arms cannot contaminate one another.
	/// </summary>
	[Fact]
	public async Task InvokeCleanupOncePerArmRatherThanOncePerSuite()
	{
		var probe = new CountingInboxProbe();

		await probe.RunOneArmAsync().ConfigureAwait(false);
		var afterFirst = probe.CleanupCalls;

		await probe.RunOneArmAsync().ConfigureAwait(false);

		probe.CleanupCalls.ShouldBeGreaterThan(
			afterFirst,
			"a second arm must get its own reset; a once-per-suite cleanup leaves every arm after the "
			+ "first starting on its predecessor's leftovers.");
	}

	#region Harness

	/// <summary>
	/// Drives a real kit arm and records whether the kit reached the cleanup seam, and when.
	/// </summary>
	private sealed class CountingInboxProbe : InboxStoreConformanceTestKit
	{
		private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

		/// <summary>Gets the number of times the kit invoked the cleanup seam.</summary>
		public int CleanupCalls { get; private set; }

		/// <summary>
		/// Gets a value indicating whether cleanup had already run by the time the store was first used.
		/// </summary>
		public bool FirstStoreCallSawCleanup { get; private set; } = true;

		protected override IInboxStore CreateStore() => new ObservingInboxStore(this, _seen);

		protected override Task CleanupAsync()
		{
			CleanupCalls++;

			return Task.CompletedTask;
		}

		/// <summary>Runs one real arm of the kit.</summary>
		/// <returns>A task that completes when the arm has run.</returns>
		public Task RunOneArmAsync() => TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();

		/// <summary>Called by the fake store on its first operation of an arm.</summary>
		internal void RecordStoreUse()
		{
			if (CleanupCalls == 0)
			{
				FirstStoreCallSawCleanup = false;
			}
		}
	}

	/// <summary>A minimal inbox store that reports when the arm first touches it.</summary>
	private sealed class ObservingInboxStore(
		CountingInboxProbe probe,
		ConcurrentDictionary<string, byte> processed) : IInboxStore
	{
		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.FromResult(
				new InboxEntry(messageId, handlerType, messageType, payload, metadata));
		}

		public ValueTask<bool> TryMarkAsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.FromResult(processed.TryAdd($"{messageId}|{handlerType}", 0));
		}

		public ValueTask<bool> IsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.FromResult(processed.ContainsKey($"{messageId}|{handlerType}"));
		}

		public ValueTask<InboxEntry?> GetEntryAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.FromResult<InboxEntry?>(null);
		}

		public ValueTask MarkProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.CompletedTask;
		}

		public ValueTask MarkFailedAsync(
			string messageId,
			string handlerType,
			string errorMessage,
			CancellationToken cancellationToken)
		{
			probe.RecordStoreUse();

			return ValueTask.CompletedTask;
		}
	}

	#endregion
}
