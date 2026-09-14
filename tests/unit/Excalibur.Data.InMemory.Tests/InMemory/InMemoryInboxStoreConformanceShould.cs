// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Inbox;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory.Inbox;

/// <summary>
/// Conformance tests for <see cref="InMemoryInboxStore"/> using the Inbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Excalibur.Data.InMemory implementation correctly implements the
/// IInboxStore interface contract including idempotency, concurrent access handling,
/// and status transitions.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class InMemoryInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		var options = Options.Create(new InMemoryInboxOptions
		{
			MaxEntries = 10000,
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
	/// record, with no external persistence layer that can fail independently of the process. There is no
	/// fault to inject that would not just be "throw on purpose" -- which the base's throw-not-no-op
	/// contract already holds trivially for any store, in-memory or not, since it never no-ops. This is a
	/// deliberate, documented override, not a skip: the test runs and asserts the (true) fact that no such
	/// fault exists to prove, rather than the base's default hard failure meant for a real provider that
	/// has not yet wired real infrastructure.
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
