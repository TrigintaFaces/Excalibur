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

	// The durable OutboxFence control table now backs the SqlServer fence high-water (it survives the
	// cleanup that purges token-bearing message rows), so Fencing_HighWaterSurvivesCleanup runs here with
	// no documented-pending gap — the SqlServer store is held to the same durable-fence contract as Mongo
	// and Postgres.

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
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
		});

		// Options-only constructor: the consumer-default surface — builds the SqlConnection factory from
		// the connection string and uses the default System.Text.Json payload serialization.
		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - real-infra re-claim-floor conformance is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new SqlServerOutboxOptions
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
		});

		return new SqlServerOutboxStore(options, NullLogger<SqlServerOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		// SqlServer exposes no explicit-dispatcher reserve method (unlike Postgres/Oracle's
		// ReserveOutboxMessagesAsync(dispatcherId, ...)), but reservation ownership keys on
		// SqlServerOutboxOptions.ProcessorId — the value written to LeasedBy on claim, and the R2 guard in
		// MarkMessageFailedRequest is "WHERE Id = @MessageId AND (LeasedBy IS NULL OR LeasedBy = @LeasedBy)".
		// So build a SECOND store over the SAME fixture DB with a DISTINCT ProcessorId and claim the row
		// through its GetUnsentMessages lease path: the row is now owned by a FOREIGN ProcessorId, different
		// from `store`'s. This is NON-VACUOUS precisely because ProcessorId is a settable per-options value —
		// two SqlServer stores do NOT share it (contrast Postgres/Oracle, whose static per-process DispatcherId
		// two in-process instances DO share, making a two-instance reserve vacuous there). SqlServerOutboxStore
		// holds no disposable state (connections are opened+disposed per call), so the foreign store needs no
		// disposal; the lease it writes is a persisted DB column that outlives it.
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var foreignOptions = Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Tables =
			{
				SchemaName = _fixture.SchemaName,
				OutboxTableName = _fixture.OutboxTableName,
				TransportsTableName = _fixture.TransportsTableName,
			},
			Processing = { CommandTimeoutSeconds = 30 },
			ProcessorId = "conformance-foreign-leader",
		});

		var foreignStore = new SqlServerOutboxStore(foreignOptions, NullLogger<SqlServerOutboxStore>.Instance);

		// Claiming under the foreign ProcessorId leases the row (LeasedBy = "conformance-foreign-leader"),
		// giving it an owner distinct from the caller of IOutboxStore.MarkFailedAsync (whose ProcessorId is
		// the fixture default) — the only way to actually exercise the R2 ownership guard.
		var claimed = await foreignStore.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		return claimed.Any(m => m.Id == messageId);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}
}
