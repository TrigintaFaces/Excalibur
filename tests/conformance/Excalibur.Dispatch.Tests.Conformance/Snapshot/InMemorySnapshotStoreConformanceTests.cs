// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Conformance.Snapshot;

/// <summary>
/// Conformance tests for InMemorySnapshotStore.
/// Demonstrates how to use the SnapshotConformanceTestBase for implementation testing.
/// </summary>
/// <remarks>
/// This class serves as both:
/// 1. A validation that InMemorySnapshotStore meets all R26 snapshot requirements
/// 2. An example for how to implement conformance tests for other snapshot stores
/// </remarks>
#pragma warning disable CA1001 // Disposable field managed by DisposeSnapshotStoreAsync
public sealed class InMemorySnapshotStoreConformanceTests : SnapshotConformanceTestBase
#pragma warning restore CA1001
{
	private InMemorySnapshotStore? _snapshotStore;

	/// <inheritdoc />
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemorySnapshotOptions());
		var logger = NullLogger<InMemorySnapshotStore>.Instance;
		_snapshotStore = new InMemorySnapshotStore(options, logger);
		return Task.FromResult<ISnapshotStore>(_snapshotStore);
	}

	/// <inheritdoc />
	protected override Task DisposeSnapshotStoreAsync()
	{
		// InMemorySnapshotStore doesn't require disposal
		_snapshotStore = null;
		return Task.CompletedTask;
	}
}
