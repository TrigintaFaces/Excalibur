// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Retention conformance for the SQL Server saga store. Author≠impl (TestsDeveloper), against a real SQL Server
/// container, on the schema the package actually ships.
/// </summary>
/// <remarks>
/// <para>
/// This is the binding lock for the <c>CompletedAt DATETIME2</c> defect. <c>DATETIME2</c> has no offset:
/// SqlClient converts the consumer's <c>DateTimeOffset</c> by keeping the local wall-clock and discarding the
/// zone. Both arms of <see cref="SagaStoreRetentionConformanceTestBase"/> are RED against it, in opposite
/// directions — a consumer west of UTC loses sagas early, a consumer east of it never purges any.
/// </para>
/// <para>
/// The lock only means anything because <see cref="SqlServerSagaStoreContainerFixture"/> now executes the
/// shipped <c>Scripts/01-SagaSchema.sql</c> rather than an inline copy of it. Against the copy, the column
/// could be corrected in the product and left broken in the test, or the reverse, and nothing would report it.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreRetentionConformanceShould
	: SagaStoreRetentionConformanceTestBase, IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreRetentionConformanceShould(SqlServerSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync(ITenantContext ambientTenant)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra retention lock is never skipped. "
			+ "A skip-gated lock on a DELETE path is a lock that has never run.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		return new SqlServerSagaStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer(),
			ambientTenant);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
