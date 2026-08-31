// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Outbox.InMemory;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// A decorator that declares no optional capability interface, resolving every capability from the store it
/// wraps -- the shape every observational outbox decorator in this framework has.
/// </summary>
/// <remarks>
/// The declaration list is the whole point of this type. A decorator's interface list is fixed at compile
/// time while the capability set of the store it wraps is not, so a decorator cannot declare the wrapped
/// store's capabilities and must not try. That is what makes a cast lossy through it, and it is what these
/// arms exist to hold the kit to.
/// </remarks>
/// <param name="inner"> The store being decorated. </param>
internal sealed class CapabilityOpaqueOutboxStoreDecorator(IOutboxStore inner) : OutboxStoreDecorator(inner);

/// <summary>
/// A delete-on-sent store, which declares <see cref="IOutboxStoreCapabilities"/> to say so.
/// </summary>
/// <param name="inner"> The store that performs the work; only the declaration matters here. </param>
internal sealed class DeleteOnSentOutboxStore(IOutboxStore inner) : OutboxStoreDecorator(inner), IOutboxStoreCapabilities
{
	/// <inheritdoc />
	public bool SupportsSentTracking => false;
}

/// <summary>
/// Holds the published outbox conformance kit to certifying a <em>decorated</em> store, so that a capability
/// the store provides is reached through the decorator rather than lost at it.
/// </summary>
/// <remarks>
/// <para>
/// Every other outbox conformance suite in this repository hands the kit a raw store built directly by its
/// own fixture. A consumer does not: a store obtained from the container arrives decorated, and the kit's
/// capability-gated arms then have to find a capability the outermost type does not declare. Two
/// registrations of one provider certified differently, and nothing failed, because an arm that returns
/// early for want of a capability reports exactly as an arm that ran and passed.
/// </para>
/// <para>
/// These arms make the difference observable: the suite records every skip the kit reports and asserts there
/// were none. A capability discovered by cast is unreachable through the decorator, so the arm skips and the
/// assertion fails; a capability discovered through <see cref="IServiceProvider.GetService(Type)"/> is
/// reached, so the arm runs. The assertion is on the skip rather than on the arm's own outcome, because the
/// defect being locked is silence rather than a wrong answer.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
// A conformance kit arm carries no runner attribute, so an arm this suite never wraps does not
// run, cannot fail, and reads in the results exactly like one that passed. This suite reaches a hand-picked set of kit arms THROUGH a decorator, to prove the decorator does
// not swallow them. It is not a second full conformance run of the outbox contract.
// Declared, so the omission is a recorded decision rather than silence:
// conformance-partial-suite: full coverage in InMemoryOutboxStoreConformanceShould
public sealed class DecoratedOutboxStoreConformanceShould : OutboxStoreConformanceTestKit
{
	private readonly List<ConformanceArmSkip> _skips = [];

	/// <inheritdoc />
	/// <remarks>
	/// Records locally rather than deferring to the base, which would write into the process-wide ledger
	/// shared with every other conformance suite in this assembly.
	/// </remarks>
	protected override void OnArmSkipped(ConformanceArmSkip skip) => _skips.Add(skip);

	/// <inheritdoc />
	protected override Task<IOutboxStore> CreateStoreAsync() =>
		Task.FromResult<IOutboxStore>(new CapabilityOpaqueOutboxStoreDecorator(NewInMemoryStore()));

	/// <inheritdoc />
	protected override Task ResetDataAsync() => Task.CompletedTask;

	/// <inheritdoc />
	protected override Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds) =>
		Task.FromResult<IOutboxStore?>(new CapabilityOpaqueOutboxStoreDecorator(NewInMemoryStore(floorSeconds)));

	[Fact]
	public async Task ReachFencingThroughADecorator_StaleToken() =>
		await RunAndRequireExecutionAsync(Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation).ConfigureAwait(false);

	[Fact]
	public async Task ReachFencingThroughADecorator_Refusal() =>
		await RunAndRequireExecutionAsync(Fencing_Refusal_ShouldReportTheHighWaterMark).ConfigureAwait(false);

	[Fact]
	public async Task ReachFencingThroughADecorator_CurrentLeaderToken() =>
		await RunAndRequireExecutionAsync(Fencing_CurrentLeaderToken_ShouldClaimAndComplete).ConfigureAwait(false);

	[Fact]
	public async Task ReachFencingThroughADecorator_HighWaterMarkSurvivesCleanup() =>
		await RunAndRequireExecutionAsync(Fencing_HighWaterMark_ShouldSurviveCleanup).ConfigureAwait(false);

	[Fact]
	public async Task ReachFencingThroughADecorator_SupersededLeader() =>
		await RunAndRequireExecutionAsync(Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage).ConfigureAwait(false);

	[Fact]
	public async Task ReachTerminalDeadLetteringThroughADecorator() =>
		await RunAndRequireExecutionAsync(DeadLettered_ShouldBeTerminalOnBothRetrievalPaths).ConfigureAwait(false);

	[Fact]
	public async Task ReachTheAdminFacetThroughADecorator() =>
		await RunAndRequireExecutionAsync(GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts).ConfigureAwait(false);

	/// <summary>
	/// A delete-on-sent declaration made behind a decorator must still be read, or the kit holds the store to
	/// a retaining contract it never claimed.
	/// </summary>
	[Fact]
	public void ReadTheSentTrackingDeclarationThroughADecorator()
	{
		var deleteOnSent = new DeleteOnSentOutboxStore(NewInMemoryStore());
		var decorated = new CapabilityOpaqueOutboxStoreDecorator(deleteOnSent);

		SupportsSentTracking(deleteOnSent).ShouldBeFalse(
			"the store declares IOutboxStoreCapabilities directly, so its declaration must be read");
		SupportsSentTracking(decorated).ShouldBeFalse(
			"a declaration made behind a decorator is still a declaration; falling through to the retaining "
			+ "default here would assert retention against a store that deletes on sent");
	}

	/// <summary>
	/// Keeps the arms above non-vacuous: they prove something only while the decorator they run through
	/// declares no capability of its own and the store beneath it provides several.
	/// </summary>
	/// <remarks>
	/// A later edit that made the test decorator implement a capability interface would make every arm above
	/// pass by cast, restoring the defect while the suite stayed green. This arm fails first.
	/// </remarks>
	[Fact]
	public void DecorateWithoutDeclaringAnyCapability()
	{
		Type[] capabilities =
		[
			typeof(IFencedOutboxStore),
			typeof(IOutboxStoreAdmin),
			typeof(IOutboxStoreBatch),
			typeof(IDeadLetterableOutboxStore),
			typeof(IBackoffSchedulableOutboxStore),
			typeof(IOutboxStoreCapabilities),
		];

		var store = new CapabilityOpaqueOutboxStoreDecorator(NewInMemoryStore());

		foreach (var capability in capabilities)
		{
			capability.IsInstanceOfType(store).ShouldBeFalse(
				$"the decorator must not declare {capability.Name}; a cast that succeeds through it proves nothing");
		}

		// ...and the capabilities the wrapped store does provide must still be reachable, or the arms above
		// would pass by there being nothing to find.
		store.GetService(typeof(IFencedOutboxStore)).ShouldNotBeNull();
		store.GetService(typeof(IOutboxStoreAdmin)).ShouldNotBeNull();
		store.GetService(typeof(IDeadLetterableOutboxStore)).ShouldNotBeNull();
	}

	private static InMemoryOutboxStore NewInMemoryStore(int? floorSeconds = null)
	{
		var options = new InMemoryOutboxOptions();
		if (floorSeconds is { } floor)
		{
			options.FailureBackoffFloorSeconds = floor;
		}

		return new InMemoryOutboxStore(Options.Create(options), NullLogger<InMemoryOutboxStore>.Instance);
	}

	private async Task RunAndRequireExecutionAsync(Func<Task> arm)
	{
		_skips.Clear();

		await arm().ConfigureAwait(false);

		_skips.ShouldBeEmpty(
			string.Format(
				CultureInfo.InvariantCulture,
				"The arm did not run. It reported: {0}. The store beneath the decorator provides the "
				+ "capability, so the arm was not skipped because the capability is absent -- it was skipped "
				+ "because the kit could not find it through the decorator. Discovering a capability by "
				+ "casting the store sees only the outermost type; use GetService(Type).",
				string.Join(
					"; ",
					_skips.Select(s => $"{s.Arm} [{s.Capability?.Name ?? "unnamed"}]: {s.Reason}"))));
	}
}
