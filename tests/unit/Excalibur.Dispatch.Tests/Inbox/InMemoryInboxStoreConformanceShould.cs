// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Conformance tests for <see cref="InMemoryInboxStore"/> using the Inbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the InMemory implementation correctly implements the
/// IInboxStore interface contract including idempotency, concurrent access handling,
/// and status transitions.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class InMemoryInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryInboxOptions
		{
			MaxEntries = 10000,
			EnableAutomaticCleanup = false,
			RetentionPeriod = TimeSpan.FromHours(24)
		});

		var logger = NullLogger<InMemoryInboxStore>.Instance;
		var store = new InMemoryInboxStore(options, logger, UntenantedContext.Instance);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		// InMemoryInboxStore is disposed in DisposeAsync by base class
		return Task.CompletedTask;
	}

	/// <summary>
	/// 2mek4x's durability fault-injection arm is N/A here, not merely unwired: the in-memory store IS the
	/// record, with no external persistence layer that can fail independently of the process. See the
	/// identical override on <c>Excalibur.Data.InMemory.Tests.InMemory.InMemoryInboxStoreConformanceShould</c>
	/// for the full rationale.
	/// </summary>
	public override async Task ThrowNotNoOpOnPersistenceFailure()
	{
		// Sanctioned by the base: this store has no external persistence layer to fault. But the base
		// sanctions the OVERRIDE, not a bare Task.CompletedTask -- a completed task is indistinguishable
		// from an arm that silently stopped verifying. Assert the fact the override rests on: there is
		// genuinely nothing here to fault, which is why the base's default refuses to pretend otherwise.
		_ = await Should.ThrowAsync<NotSupportedException>(
			async () => await InjectPersistenceFaultAsync());
	}
}
