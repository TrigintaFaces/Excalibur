// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;
using Excalibur.Dispatch;

using Shouldly;

using Tests.Shared.Conformance.Cdc;
using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Per-provider real-store durability conformance for the Postgres <see cref="ICdcStateStore"/>
/// implementation, driven by the shared <see cref="CdcProviderConformanceTestBase"/> kit against a
/// real PostgreSQL container.
/// </summary>
/// <remarks>
/// <para>
/// Before this deriver the CDC state-store contract (save/get/overwrite/delete/resume/GetAll/concurrent)
/// was exercised only in-memory; no per-provider <em>real-store</em> durability test existed. Running the
/// kit against the real store verifies the checkpoint round-trip actually survives the database — not a
/// mock — per the real-infra verification bar.
/// </para>
/// <para>
/// Each test instance gets a unique state table so the run is isolated and
/// <c>GetAllPositions_EmptyStore_ReturnsEmpty</c> holds; Docker is a hard requirement (never skipped).
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcStateStoreConformanceShould : CdcProviderConformanceTestBase
{
	private readonly PostgresFixture _fixture;
	private readonly string _tableName = $"cdc_state_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcStateStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresCdcStateStoreConformanceShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - the CDC state-store durability conformance is a real-infra lock and must never be skipped.");

		ICdcStateStore store = new PostgresCdcStateStore(
			_fixture.ConnectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = "public", TableName = _tableName }));
		return Task.FromResult(store);
	}

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index) =>
		// A valid, strictly-increasing LSN (index 0 must still be valid: LSN 0 == Invalid, so offset by 1).
		// Round-trip through PostgresCdcPosition so the token format matches what the store persists.
		new PostgresCdcPosition((ulong)((index + 1) * 1000)).ToChangePosition();

	/// <inheritdoc/>
	protected override Task CleanupAsync() =>
		// Each instance uses a unique table name, so state is naturally isolated; the container teardown
		// reclaims the tables. No per-test cleanup required.
		Task.CompletedTask;
}
