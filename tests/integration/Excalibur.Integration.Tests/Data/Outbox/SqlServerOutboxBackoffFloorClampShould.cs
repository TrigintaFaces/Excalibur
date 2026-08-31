// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxBackoffFloorClampShould"/> against a live SQL Server container.
/// </summary>
/// <remarks>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. SQL Server advertises the backoff capability, so the processor prefers
/// the path these arms drive.
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBackoffFloorClampShould : OutboxBackoffFloorClampShould, IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="SqlServerOutboxBackoffFloorClampShould"/> class.</summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerOutboxBackoffFloorClampShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - the backoff floor lock is never skipped.");

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
			Processing =
			{
				CommandTimeoutSeconds = 30,
				FailureBackoffFloorSeconds = floorSeconds,
			},
		};

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
