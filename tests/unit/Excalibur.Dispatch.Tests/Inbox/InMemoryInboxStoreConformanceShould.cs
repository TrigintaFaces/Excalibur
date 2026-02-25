// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Inbox;

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
[Trait("Category", "Unit")]
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
		var store = new InMemoryInboxStore(options, logger);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		// InMemoryInboxStore is disposed in DisposeAsync by base class
		return Task.CompletedTask;
	}
}
