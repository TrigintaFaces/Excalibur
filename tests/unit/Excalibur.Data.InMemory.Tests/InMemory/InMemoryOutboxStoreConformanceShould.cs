// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Outbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Outbox;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory.Outbox;

/// <summary>
/// Conformance tests for <see cref="InMemoryOutboxStore"/> using the Outbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Excalibur.Data.InMemory implementation correctly implements the
/// IOutboxStore interface contract including message staging, status transitions, cleanup,
/// and statistics tracking.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InMemoryOutboxStoreConformanceShould : OutboxStoreConformanceTestBase
{
	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync()
	{
		var options = Options.Create(new InMemoryOutboxOptions
		{
			MaxMessages = 10000,
			DefaultRetentionPeriod = TimeSpan.FromHours(24)
		});

		var logger = NullLogger<InMemoryOutboxStore>.Instance;
		var store = new InMemoryOutboxStore(options, logger);

		return Task.FromResult<IOutboxStore>(store);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		// InMemoryOutboxStore is disposed in DisposeAsync by base class
		return Task.CompletedTask;
	}
}
