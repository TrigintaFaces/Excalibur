// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;

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
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class InMemorySnapshotStoreConformanceTests : SnapshotConformanceTestBase
#pragma warning restore CA1001
{
	private InMemorySnapshotStore? _snapshotStore;

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// Deliberately passes NO <c>ITenantContext</c>. An earlier revision of this fixture supplied one, on
	/// the reasoning that a store built without it leaves <c>TenantScope.FromContext(null)</c> ==
	/// <c>None</c> — key omits the tenant, every tenant collides on one entry — so the suite proves the
	/// untenanted path while claiming to prove the tenanted one. That reasoning is correct and the remedy
	/// was still wrong.
	/// </para>
	/// <para>
	/// Supplying a context is a per-store MODE SWITCH: <c>FromContext</c> returns <c>None</c> for a null
	/// context but FAILS CLOSED for a non-null context resolving no tenant. The 13 arms here that
	/// establish no tenant scope therefore all raise <c>TenantRequiredException</c> — measured 13 failed /
	/// 4 passed, against 3 failed / 14 passed without it. The change was verified only under a filter
	/// matching the 3 tenant arms, which is why it read as green; the filter was narrower than the blast
	/// radius. Restored to the untenanted construction, where the isolation arms fail HONESTLY and report
	/// a real gap, until the suite-wide mode question is ruled.
	/// </para>
	/// </remarks>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemorySnapshotOptions());
		var logger = NullLogger<InMemorySnapshotStore>.Instance;
		_snapshotStore = new InMemorySnapshotStore(options, logger, CreateAmbientTenantContext());
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
