// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxBackoffFloorClampShould"/> against a live Postgres container.
/// </summary>
/// <remarks>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. Postgres advertises the backoff capability, so the processor prefers
/// the path these arms drive.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBackoffFloorClampShould : OutboxBackoffFloorClampShould, IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="PostgresOutboxBackoffFloorClampShould"/> class.</summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresOutboxBackoffFloorClampShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - the backoff floor lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
			FailureBackoffFloorSeconds = floorSeconds,
		};

		return new PostgresOutboxStore(db, Options.Create(options), NullLogger<PostgresOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// The schema has to exist before it can be emptied: an arm may clean before it builds a store.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
