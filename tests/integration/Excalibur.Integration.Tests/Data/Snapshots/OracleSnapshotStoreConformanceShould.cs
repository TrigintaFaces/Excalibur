// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="OracleSnapshotStore"/> using the Snapshot
/// Conformance Test Kit against a real Oracle container.
/// </summary>
/// <remarks>
/// <para>
/// Before this suite existed there were nine snapshot conformance suites and none of them was Oracle,
/// so a tenant regression in the Oracle store was silent by construction — no fixture ever executed
/// its tenant-scoped path.
/// </para>
/// <para>
/// <b>Never skipped.</b> Docker availability is asserted, not used as a skip condition. A suite that
/// passes by not running reports the same green as one that ran, which is the precise failure this
/// kit exists to prevent.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<OracleSnapshotStoreFixture>
{
	private readonly OracleSnapshotStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle snapshot store fixture.</param>
	public OracleSnapshotStoreConformanceShould(OracleSnapshotStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The tenant context is load-bearing, not decoration. The store resolves its tenant per call via
	/// <c>TenantScope.FromContext</c>; constructed without one its scope accessor falls back to <c>None</c>, the row key
	/// omits the tenant, and every tenant collides on a single row — the untenanted path, exercised
	/// silently by a suite whose entire purpose is to prove the tenanted one.
	/// </para>
	/// <para>
	/// <b>The connection-factory overload is required to pass it.</b> The simpler
	/// <c>OracleSnapshotStore(string, ILogger)</c> constructor delegates without a tenant context, so a
	/// fixture that reaches for the obvious constructor silently tests the untenanted path. Production
	/// registers the ambient context in DI, so a fixture that omits it is not testing what ships.
	/// </para>
	/// <para>
	/// The schema is the container's resolved connecting user rather than the store's <c>EXCALIBUR</c>
	/// default, which does not exist in a stock <c>oracle-free</c> image.
	/// </para>
	/// </remarks>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle snapshot conformance runs against real infrastructure and is never skipped: a skipped " +
			"suite reports the same green as a passing one.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var logger = NullLogger<OracleSnapshotStore>.Instance;

		return new OracleSnapshotStore(
			_fixture.CreateConnection,
			logger,
			schema: _fixture.Schema,
			table: _fixture.TableName,
			tenantContext: CreateAmbientTenantContext());
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
