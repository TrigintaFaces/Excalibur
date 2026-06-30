// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the SQL Server saga store (mxozhv / keystone fc1c8a). Author≠impl
/// (TestsDeveloper); runs the shared <see cref="SagaStoreConformanceTestBase"/> contract with
/// <see cref="SupportsOptimisticConcurrency"/> enabled, so the version-gated <c>no-overwrite</c> and
/// <c>no-resurrect</c> facts are enforced against a real SQL Server container.
/// </summary>
/// <remarks>
/// The store performs a store-owns-increment compare-and-swap via a version-gated <c>MERGE</c>
/// (<c>SqlServerSagaStore.SaveAsync</c> surfaces a 0-row MERGE as <see cref="ConcurrencyException"/>),
/// and scopes loads to <c>{SagaId, SagaType}</c> for type-isolation — so both the optimistic-concurrency
/// and the type-isolation conformance facts hold. A truncated table per test gives isolation on the
/// shared container.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreConcurrencyConformanceShould(SqlServerSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra conformance lock is never skipped.");

		// The store does NOT auto-create its table; the fixture provisions [dispatch].[sagas] (the store's
		// default schema/table), so the simple connection-string constructor resolves to it.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		return new SqlServerSagaStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer());
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
