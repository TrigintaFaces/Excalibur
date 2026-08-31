// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Cosmos-emulator lock on the change-feed path: a message staged through the store arrives on the
/// feed carrying <b>every</b> field the store wrote.
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>production</b> path for this provider — the pattern the architecture notes direct
/// consumers to — and until this lock existed nothing exercised it at all. Nothing in the repository drove
/// <c>SubscribeToNewMessagesAsync</c>.
/// </para>
/// <para>
/// It went untested while the subscription maintained its <i>own private copy</i> of the stored document
/// shape, independent of the one the store writes through. Two representations of one document, updated by
/// hand, drift silently: a field added to the store's shape is simply absent from every message the feed
/// produces, and no build breaks. That had already happened twice — the lease fields, and the concurrency
/// token, both written by the store and both invisible on the feed.
/// </para>
/// <para>
/// The assertion is deliberately field-by-field rather than a spot check. A round-trip test that inspects
/// two or three properties is satisfied by a mapping that drops the rest, which is the exact defect this
/// exists to catch.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbOutboxChangeFeedRoundTripShould
	: IClassFixture<CosmosDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbOutboxStoreContainerFixture _fixture;
	private readonly List<(CosmosDbOutboxStore Store, string ContainerName)> _created = [];

	public CosmosDbOutboxChangeFeedRoundTripShould(CosmosDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		var containers = new HashSet<string>(StringComparer.Ordinal);

		foreach (var (store, containerName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			_ = containers.Add(containerName);
		}

		foreach (var containerName in containers)
		{
			await _fixture.CleanupContainerAsync(containerName).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: every field the store wrote reaches the consumer through the change feed.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The message is claimed before reading the feed, for two reasons. It populates the lease fields, which
	/// are the ones a divergent copy of the document shape drops; and claiming rewrites the document, which
	/// is what puts it on the feed a second time — the same mechanism the recovery path depends on.
	/// </remarks>
	[Fact]
	public async Task CarryEveryFieldTheStoreWrote_WhenAMessageArrivesOnTheFeed()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, _) = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateFullyPopulatedMessage(partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
		addResult.Success.ShouldBeTrue($"staging must succeed: {addResult.ErrorMessage}");

		var claimed = await store.ClaimPendingAsync(partitionKey, 10, "feed-claimant", ct).ConfigureAwait(false);
		claimed.Documents.Count.ShouldBe(1, "the staged message must be claimable");

		var observed = await ReadFromFeedAsync(store, message.MessageId, ct).ConfigureAwait(false);

		observed.ShouldNotBeNull(
			"a staged message must reach the change feed -- this is the delivery path for this provider.");

		// Caller-supplied content.
		observed.MessageType.ShouldBe(message.MessageType);
		observed.Payload.ShouldBe(message.Payload);
		observed.AggregateId.ShouldBe(message.AggregateId);
		observed.AggregateType.ShouldBe(message.AggregateType);
		observed.CorrelationId.ShouldBe(message.CorrelationId);
		observed.CausationId.ShouldBe(message.CausationId);
		observed.TenantId.ShouldBe(message.TenantId);
		observed.Destination.ShouldBe(message.Destination);
		observed.PartitionKeyValue.ShouldBe(partitionKey.Value);

		observed.Headers.ShouldNotBeNull("headers the caller supplied must survive the round trip");
		observed.Headers!["header-one"].ShouldBe("value-one");

		// Store-assigned state. These are the fields a divergent copy of the document shape silently drops.
		observed.LeasedBy.ShouldBe(
			"feed-claimant",
			"the lease owner must be visible on the feed. A subscription mapping through its own copy of the "
			+ "document shape does not carry this field at all, so a consumer cannot tell a claimed message "
			+ "from an unclaimed one.");

		observed.LeasedAt.ShouldNotBeNull("the lease instant must be visible on the feed");

		observed.ETag.ShouldNotBeNullOrEmpty(
			"the concurrency token must be visible on the feed -- a consumer publishing from the trigger "
			+ "path needs it just as much as one sweeping for pending messages.");
	}

	private static async Task<CloudOutboxMessage?> ReadFromFeedAsync(
		CosmosDbOutboxStore store,
		string messageId,
		CancellationToken cancellationToken)
	{
		// From the beginning, so the read does not race the writes above.
		var subscription = await store
			.SubscribeToNewMessagesAsync(ChangeFeedOptions.FromBeginning, cancellationToken)
			.ConfigureAwait(false);

		await using (subscription.ConfigureAwait(false))
		{
			// The feed is an unbounded stream; bound the read so a miss fails the arm rather than hanging.
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			deadline.CancelAfter(TimeSpan.FromSeconds(30));

			var reader = (CosmosDbOutboxChangeFeedSubscription)subscription;

			try
			{
				await foreach (var change in reader.ReadChangesAsync(deadline.Token).ConfigureAwait(false))
				{
					if (string.Equals(change.Document.MessageId, messageId, StringComparison.Ordinal))
					{
						return change.Document;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Deadline hit without seeing the message; the arm reports it as a miss.
			}
		}

		return null;
	}

	private static CloudOutboxMessage CreateFullyPopulatedMessage(string partitionKeyValue) =>
		new()
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["header-one"] = "value-one" },
			AggregateId = "agg-1",
			AggregateType = "AggType",
			CorrelationId = "corr-1",
			CausationId = "caus-1",
			TenantId = "tenant-1",
			Destination = "orders",
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKeyValue
		};

	private async Task<(CosmosDbOutboxStore Store, string ContainerName)> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("The Cosmos DB emulator must be available -- never skipped.");

		var container = $"outbox_{Guid.NewGuid():N}";
		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = container,
			CreateContainerIfNotExists = true,
			UseDirectMode = false,
			HttpClientFactory = () => _fixture.EmulatorHttpClient,
			Connection = new CosmosDbOutboxConnectionOptions { ConnectionString = _fixture.ConnectionString }
		};

		var store = new CosmosDbOutboxStore(Options.Create(options), NullLogger<CosmosDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, container));
		return (store, container);
	}
}
