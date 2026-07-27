// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Outbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MongoDbOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live MongoDB container.
/// </summary>
/// <remarks>
/// These tests verify that the MongoDB implementation correctly implements the
/// <see cref="IOutboxStore"/> contract — including atomic status transitions and concurrent
/// MarkSent — using TestContainers. They are never skipped: when Docker is unavailable the fixture
/// fails fast, so a missing container surfaces as a failure rather than a silent pass. The store is
/// constructed via its options-only constructor, which builds the provider's DEFAULT
/// <c>MongoClient</c> (and therefore the default serializer) from the connection string — the surface
/// a normal consumer uses. The store self-initializes its collection and indexes on first use.
/// </remarks>
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxStoreConformanceShould : OutboxStoreConformanceTestBase, IClassFixture<MongoDbOutboxStoreContainerFixture>
{
	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbOutboxStoreConformanceShould(MongoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});

		// Options-only constructor: the store builds the provider's DEFAULT MongoClient (default
		// serializer) from the connection string — the surface most consumers use. The store
		// self-initializes its collection and indexes on first use.
		return Task.FromResult<IOutboxStore>(
			new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// wseau9 (SA seam ruling): opt real MongoDB into the universal re-claim-floor property arms (R1 floor,
	/// R3 monotonic, and the owned-path liveness twin). Uses the DEFAULT <c>MongoClient</c> and the REAL system
	/// clock (default <see cref="System.TimeProvider"/>) so the base arms' real-time floor poll (F=1s) exercises
	/// the store's actual <c>NextAttemptAt</c> gate — never a fake clock (which the base's wall-clock poll could
	/// not advance). RED against pre-fix Mongo, whose claim filtered <c>Status==Staged</c> only and set no
	/// <c>NextAttemptAt</c> → a failed message stranded (§1.5).
	/// </remarks>
	protected override Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra re-claim-floor conformance is never skipped.");

		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return Task.FromResult<IOutboxStore?>(
			new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Reserve the message under a FOREIGN <c>ProcessorId</c> — a second store instance over the same
	/// collection whose owner token differs from the store that calls <c>MarkFailedAsync</c> — the only way to
	/// exercise the R2 ownership guard (<c>LeasedBy == null || LeasedBy == ProcessorId</c>,
	/// <c>MongoDbOutboxStore</c> claim/mark). The claiming store stamps <c>LeasedBy = ProcessorId</c>, so a
	/// distinct <c>ProcessorId</c> makes the subsequent non-owner mark a no-op (R2 safety), while the real owner
	/// can still mark its own claim (R2 liveness).
	/// </remarks>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		var foreignOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			ProcessorId = "conformance-foreign-leader",
		});

		var foreignStore = new MongoDbOutboxStore(foreignOptions, NullLogger<MongoDbOutboxStore>.Instance);
		var reserved = await foreignStore
			.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		return reserved.Any(m => m.Id == messageId);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// xnyhjd (REVIEW_CODE P1, cross-provider closure of bbazps/cys98n) — Mongo <see cref="MongoDbOutboxStore"/>
	/// <c>EnqueueAsync(IDispatchMessage, IMessageContext, …)</c> must derive the routing <c>Destination</c>
	/// from the message context (not silently pass the type name), falling back to the message TYPE name when
	/// the context carries none. SQL/Postgres were fixed this sprint (bbazps, Postgres-only); REVIEW_CODE caught
	/// Redis + Mongo still dropped it. Real-infra round-trip against a live MongoDB container.
	/// </summary>
	[Fact]
	public async Task EnqueueAsync_DerivesDestinationFromContext_ElseFallsBackToTypeName()
	{
		await CleanupAsync().ConfigureAwait(false);
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string ConfiguredDestination = "orders.commands.v1";

		// Case A: context carries a destination. Case B: none → fall back to the message type name.
		await store.EnqueueAsync(new DestinationDerivationTestMessage(), CreateContext("ctx-derived", ConfiguredDestination), CancellationToken.None).ConfigureAwait(false);
		await store.EnqueueAsync(new DestinationDerivationTestMessage(), CreateContext("ctx-fallback", destination: null), CancellationToken.None).ConfigureAwait(false);

		var messages = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();

		// pfgcj6: Mongo now falls back to the SIMPLE type name (message.GetType().Name), matching Postgres.
		messages.ShouldContain(
			m => m.Destination == ConfiguredDestination,
			"xnyhjd: Mongo EnqueueAsync must persist the destination derived from the context metadata.");
		messages.ShouldContain(
			m => m.Destination == nameof(DestinationDerivationTestMessage),
			"xnyhjd/pfgcj6: with no context destination, Mongo EnqueueAsync must fall back to the message TYPE name (simple, Postgres-parity), not drop it.");
	}

	private static IMessageContext CreateContext(string messageId, string? destination)
	{
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		if (destination is not null)
		{
			items[MetadataPropertyKeys.Destination] = destination;
		}

		// A bare fake returns "" for unconfigured strings, tripping ExtractMetadata's non-empty guards;
		// configure the direct-read properties: CorrelationId non-empty, CausationId null.
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns(messageId);
		_ = A.CallTo(() => context.CorrelationId).Returns(messageId);
		_ = A.CallTo(() => context.CausationId).Returns((string?)null);
		_ = A.CallTo(() => context.Items).Returns(items);
		return context;
	}

	private sealed record DestinationDerivationTestMessage : IDispatchMessage;
}
