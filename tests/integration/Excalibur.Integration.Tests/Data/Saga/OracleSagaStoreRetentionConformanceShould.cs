// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// r26wo1 — retention conformance for the Oracle saga store, against a real Oracle container. Completes the
/// provider trio: SQL Server (the corrected provider, DATETIMEOFFSET) and Postgres (the control, TIMESTAMPTZ)
/// already run <see cref="SagaStoreRetentionConformanceTestBase"/>; Oracle did not.
/// </summary>
/// <remarks>
/// <para>
/// Oracle declares <c>CompletedAt TIMESTAMP WITH TIME ZONE</c> (<c>Scripts/01-SagaSchema.sql</c>), which
/// stores an instant rather than a wall-clock reading, so both arms of the base class are expected to pass
/// here — a third independent confirmation of the same property Postgres already establishes as the control.
/// </para>
/// <para>
/// Also proves the tenant-scoped purge safety+liveness arm
/// (<see cref="SagaStoreRetentionConformanceTestBase.PurgeCompletedBeforeAsync_PurgesOnlyTheCallingTenantsSagas"/>,
/// ux3bxi) against real Oracle for the first time — SQL Server and Postgres already covered it.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Oracle")]
[Collection("Oracle SagaStore Integration Tests")]
public sealed class OracleSagaStoreRetentionConformanceShould
	: SagaStoreRetentionConformanceTestBase, IClassFixture<OracleSagaStoreContainerFixture>
{
	private readonly OracleSagaStoreContainerFixture _fixture;

	public OracleSagaStoreRetentionConformanceShould(OracleSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync(ITenantContext ambientTenant)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — this real-infra conformance lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new OracleSagaStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = _fixture.TableName,
		});

		return new OracleSagaStore(
			_fixture.ConnectionString,
			options,
			NullLogger<OracleSagaStore>.Instance,
			new DispatchJsonSerializer(),
			ambientTenant);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
