// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxDrainLeaseTestBase"/> against a live Postgres container.
/// </summary>
/// <remarks>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. Postgres has a sound atomic claim (<c>FOR UPDATE SKIP LOCKED</c>), which
/// is the point — these arms fail on a provider whose claim is correct, because the defect they bind is in the
/// drain paths that never reached the claim.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxDrainLeaseShould : OutboxDrainLeaseTestBase, IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="PostgresOutboxDrainLeaseShould"/> class.</summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresOutboxDrainLeaseShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync(int? failureBackoffFloorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - the drain-lease lock is never skipped.");

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
		};

		if (failureBackoffFloorSeconds.HasValue)
		{
			options.FailureBackoffFloorSeconds = failureBackoffFloorSeconds.Value;
		}

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
