// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;

using global::Marten;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MartenOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live PostgreSQL container.
/// </summary>
/// <remarks>
/// Marten runs on PostgreSQL, so this deriver reuses the same shared Postgres container fixture as
/// <see cref="PostgresOutboxStoreConformanceShould"/>, but self-creates its own document schema (a
/// dedicated schema, isolated from the Dapper-managed <c>outbox</c>/<c>outbox_dead_letters</c> tables
/// the sibling deriver owns) rather than requiring pre-existing tables. These tests are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a failure
/// rather than a silent pass. The store is constructed via its consumer-default surface — a real
/// <see cref="IDocumentStore"/> built with <c>DocumentStore.For</c> against the fixture's connection
/// string plus an <c>IOptions&lt;MartenOutboxStoreOptions&gt;</c> — proving the store against Marten's
/// real unit-of-work session and conditional-insert semantics rather than a mock.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private static IDocumentStore? SharedDocumentStore;
	private IDocumentStore? _documentStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="MartenOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public MartenOutboxStoreConformanceShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// Marten documented-pending conformance gaps. Marten does not yet implement the failure-anchored re-claim
	/// floor (it does not override <c>CreateStoreWithReclaimFloorAsync</c>), so its seven re-claim-floor arms are
	/// declared here rather than skipping silently. The floor is a REQUIRED contract behaviour, not a
	/// capability-gate: every provider that implements it still runs and must pass these arms.
	/// </summary>
	protected override System.Collections.Generic.IReadOnlyDictionary<string, string> PendingConformanceGaps =>
		new System.Collections.Generic.Dictionary<string, string>
		{
			[nameof(MarkFailed_NotReclaimableWithinTheFloor_ReservedPath)] = "n7g57m",
			[nameof(MarkFailed_NotReclaimableWithinTheFloor_UnreservedInputPath)] = "n7g57m",
			[nameof(MarkFailed_EventuallyReclaimableAfterTheFloorElapses)] = "n7g57m",
			[nameof(MarkFailed_OwnedPath_RecordsFailureAndReclaimsAfterTheFloor)] = "n7g57m",
			[nameof(MarkFailed_DoesNotDecreaseRetryCount_OnAStaleLateReport)] = "n7g57m",
			[nameof(DeadLettered_NeverReclaimed_ByEitherClaimPath)] = "n7g57m",
			[nameof(MarkFailed_ByANonOwningDispatcher_DoesNotStealTheLease_R2)] = "n7g57m",
		};

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra conformance is never skipped.");

		// Marten owns its own schema and self-creates the document storage; it does not depend on the
		// Dapper-managed outbox tables the sibling Postgres deriver creates via EnsureInitializedAsync.
		//
		// IDocumentStore is an expensive, thread-safe SINGLETON and is built once for the whole run.
		// DocumentStore.For builds an NpgsqlDataSource, and Npgsql pools that data source BY CONNECTION
		// STRING -- so disposing the store after one test disposes the pool every LATER test in this
		// collection still resolves, and they all fail with
		//   ObjectDisposedException: Cannot access a disposed object (Npgsql.PoolingDataSource).
		// Per-test isolation comes from DeleteAllDocumentsAsync in CleanupAsync, not from rebuilding
		// the store. xUnit constructs a fresh test-class instance per test, so a per-instance store
		// would rebuild-and-dispose on every single arm.
		_documentStore = SharedDocumentStore ??= DocumentStore.For(opts =>
		{
			opts.Connection(_fixture.ConnectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = "marten_outbox";
		});

		var options = Options.Create(new MartenOutboxStoreOptions());

		return await Task.FromResult(
			new MartenOutboxStore(_documentStore, options, NullLogger<MartenOutboxStore>.Instance)).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_documentStore is not null)
		{
			// Documents only. The store itself is the shared singleton above and is deliberately NOT
			// disposed here -- disposing it releases the connection-string-pooled NpgsqlDataSource that
			// every later arm in this collection still needs.
			await _documentStore.Advanced.Clean.DeleteAllDocumentsAsync().ConfigureAwait(false);
		}
	}
}
