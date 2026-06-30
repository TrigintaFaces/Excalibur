// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="PostgresOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live Postgres container.
/// </summary>
/// <remarks>
/// These tests verify that the Postgres implementation correctly implements the
/// <see cref="IOutboxStore"/> contract — including the atomic SKIP LOCKED claim and concurrent
/// status transitions — using TestContainers. They are never skipped: when Docker is unavailable the
/// fixture fails fast, so a missing container surfaces as a failure rather than a silent pass. The
/// store is constructed via its consumer-default surface — an <see cref="IDb"/> over a Npgsql
/// connection factory plus an <c>IOptions&lt;PostgresOutboxStoreOptions&gt;</c> bound to the fixture's
/// connection string — using the provider's default (System.Text.Json) metadata serialization. The
/// fixture owns the schema because the Postgres store does not self-create its tables.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresOutboxStoreConformanceShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra conformance is never skipped.");

		// Ensure the container is ready and the outbox schema exists (the store does not self-create it).
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Npgsql connection per access
		// (a fresh connection is required for the concurrent-staging conformance cases, which drive
		// multiple operations through IDb.Connection in parallel).
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
