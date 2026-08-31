// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxDrainLeaseTestBase"/> against a live SQL Server container.
/// </summary>
/// <remarks>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. SQL Server has a sound atomic claim
/// (<c>UPDATE … OUTPUT</c> with <c>READPAST, UPDLOCK, ROWLOCK</c>), which is the point — these arms fail on a
/// provider whose claim is correct, because the defect they bind is in the drain paths that never reached it.
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxDrainLeaseShould : OutboxDrainLeaseTestBase, IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="SqlServerOutboxDrainLeaseShould"/> class.</summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerOutboxDrainLeaseShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync(int? failureBackoffFloorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - the drain-lease lock is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
		};

		if (failureBackoffFloorSeconds.HasValue)
		{
			options.Processing.FailureBackoffFloorSeconds = failureBackoffFloorSeconds.Value;
		}

		return new SqlServerOutboxStore(Options.Create(options), NullLogger<SqlServerOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// The schema has to exist before it can be emptied: an arm may clean before it builds a store.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
