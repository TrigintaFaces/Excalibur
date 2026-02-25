// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Conformance tests for <see cref="InMemoryEventStore" />.
/// </summary>
/// <remarks>
/// These tests verify that InMemoryEventStore correctly implements the IEventStore contract.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InMemoryEventStoreConformanceShould : EventStoreConformanceTestBase
{
	private InMemoryEventStore? _store;

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		_store = new InMemoryEventStore();
		return Task.FromResult<IEventStore>(_store);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync()
	{
		_store?.Clear();
		return Task.CompletedTask;
	}
}
