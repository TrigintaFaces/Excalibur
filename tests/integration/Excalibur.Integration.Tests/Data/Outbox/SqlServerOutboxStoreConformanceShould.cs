// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqlServerOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live SQL Server container.
/// </summary>
/// <remarks>
/// These tests verify that the SQL Server implementation correctly implements the
/// <see cref="IOutboxStore"/> contract — including the lease-based claim and atomic status transitions
/// — using TestContainers. They are never skipped: when Docker is unavailable the fixture fails fast,
/// so a missing container surfaces as a failure rather than a silent pass. The store is constructed via
/// its options-only constructor — the consumer-default surface — which builds a SqlConnection factory
/// from the bound connection string and falls back to the provider's default (System.Text.Json) payload
/// serialization. The fixture owns the schema because the SQL Server store does not self-create its
/// tables.
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerOutboxStoreConformanceShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra conformance is never skipped.");

		// Ensure the container is ready and the outbox schema exists (the store does not self-create it).
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			TransportsTableName = _fixture.TransportsTableName,
			CommandTimeoutSeconds = 30,
		});

		// Options-only constructor: the consumer-default surface — builds the SqlConnection factory from
		// the connection string and uses the default System.Text.Json payload serialization.
		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
