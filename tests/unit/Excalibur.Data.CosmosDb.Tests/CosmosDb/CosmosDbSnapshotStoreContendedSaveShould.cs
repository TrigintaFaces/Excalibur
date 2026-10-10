// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.Data.CosmosDb.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using FakeItEasy;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Pins the contended-save protocol of <see cref="CosmosDbSnapshotStore" /> against the three document
/// states a pass can observe, by scripting the container's responses rather than racing real writers.
/// </summary>
/// <remarks>
/// <para>
/// The conformance arm that covers this seam on real infrastructure drives ten concurrent writers and
/// asserts the highest version is readable afterwards. It detects an abandoned write only when the
/// interleaving that abandons one actually occurs, so it is a probabilistic detector: a run in which the
/// highest writer happens to win its first race passes over a store that would have abandoned it. These
/// arms script the interleaving instead, so each one fails every time the protocol regresses.
/// </para>
/// <para>
/// A faked container is the right instrument here and a real one is not, because what is under test is
/// this store's own control flow across a response sequence — not how the service behaves. The service's
/// contribution (which status code a vanished document answers with) is deliberately covered twice: the
/// store accepts either, and both arms below assert the write still lands.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Snapshots")]
public sealed class CosmosDbSnapshotStoreContendedSaveShould
{
	private const string AggregateId = "contended-aggregate";
	private const string AggregateType = "ContendedAggregate";

	/// <summary>
	/// A writer that loses two consecutive ETag races to writers still behind it must keep going, not
	/// return as though a newer snapshot existed.
	/// </summary>
	/// <remarks>
	/// The interleaving: the document holds version 10; this writer carries 100. It reads 10, loses the
	/// replace to a writer at 20, re-reads 20, loses again to a writer at 90, and on its third pass must
	/// still store 100. A protocol that gives up after one retry returns here having written nothing and
	/// having established nothing — the stored version is whichever writer landed last, and the caller
	/// cannot tell.
	/// </remarks>
	[Fact]
	public async Task StoreTheSnapshot_WhenItLosesTwoConsecutiveEtagRaces()
	{
		var container = A.Fake<Container>();
		var store = BuildStore(container);
		var snapshot = SnapshotAtVersion(100);

		var reads = 0;
		A.CallTo(() => container.ReadItemAsync<CosmosDbSnapshotDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() => StoredAt(++reads switch { 1 => 10, 2 => 20, _ => 90 }));

		var replaces = 0;
		A.CallTo(() => container.ReplaceItemAsync(
				A<CosmosDbSnapshotDocument>._, A<string>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => ++replaces <= 2
				? throw Cosmos(HttpStatusCode.PreconditionFailed)
				: StoredAt(100));

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		replaces.ShouldBe(3, "the writer must keep re-reading and retrying until it stores or is superseded");
	}

	/// <summary>
	/// A document that disappears before the pass reads it must be created, not reported as a failure.
	/// </summary>
	/// <remarks>
	/// Reachable mid-protocol, not only on entry: a snapshot delete, the erasure contributor, or container
	/// time-to-live can empty the key between attempts, returning a later pass to the state the first one
	/// faced. A protocol that models only "stored version is newer" and "stored version is older" has
	/// nowhere to put this, so the not-found escapes the save as a raw service fault while the operation's
	/// own telemetry still records a success.
	/// </remarks>
	[Fact]
	public async Task StoreTheSnapshot_WhenTheDocumentIsDeletedBeforeTheRead()
	{
		var container = A.Fake<Container>();
		var store = BuildStore(container);
		var snapshot = SnapshotAtVersion(100);

		var reads = 0;
		A.CallTo(() => container.ReadItemAsync<CosmosDbSnapshotDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() => ++reads == 1
				? StoredAt(10)
				: throw Cosmos(HttpStatusCode.NotFound));

		A.CallTo(() => container.ReplaceItemAsync(
				A<CosmosDbSnapshotDocument>._, A<string>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.ThrowsAsync(Cosmos(HttpStatusCode.PreconditionFailed));

		A.CallTo(() => container.CreateItemAsync(
				A<CosmosDbSnapshotDocument>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.Returns(StoredAt(100));

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => container.CreateItemAsync(
				A<CosmosDbSnapshotDocument>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A document that disappears between the read and the conditional replace must be created, not
	/// reported as a failure.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the same vanish one step later, and it needs its own arm because the service answers it
	/// differently: resource-not-found outranks the precondition, so the conditional replace reports
	/// not-found rather than a precondition failure. A handler that re-evaluates only on a precondition
	/// failure therefore never sees this one.
	/// </para>
	/// <para>
	/// The vanish is scripted on the SECOND replace, after a lost ETag race, deliberately. On the first
	/// replace of a save a not-found is indistinguishable from the key having been empty all along, so
	/// the create-when-absent path absorbs it wherever that path happens to sit. It is the one on a later
	/// pass that has nowhere to go unless the retry loop itself handles both codes.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task StoreTheSnapshot_WhenTheDocumentIsDeletedBeforeTheReplace()
	{
		var container = A.Fake<Container>();
		var store = BuildStore(container);
		var snapshot = SnapshotAtVersion(100);

		var reads = 0;
		A.CallTo(() => container.ReadItemAsync<CosmosDbSnapshotDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() => ++reads switch
			{
				1 => StoredAt(10),
				2 => StoredAt(20),
				_ => throw Cosmos(HttpStatusCode.NotFound),
			});

		var replaces = 0;
		A.CallTo(() => container.ReplaceItemAsync(
				A<CosmosDbSnapshotDocument>._, A<string>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.ThrowsAsync(_ => Cosmos(++replaces == 1
				? HttpStatusCode.PreconditionFailed
				: HttpStatusCode.NotFound));

		A.CallTo(() => container.CreateItemAsync(
				A<CosmosDbSnapshotDocument>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.Returns(StoredAt(100));

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => container.CreateItemAsync(
				A<CosmosDbSnapshotDocument>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// A writer genuinely overtaken by a newer snapshot returns without writing — the liveness partner to
	/// the three arms above, which would all be satisfied by a store that retried forever.
	/// </summary>
	[Fact]
	public async Task NotWriteAnything_WhenAStoredVersionIsAlreadyAtLeastAsHigh()
	{
		var container = A.Fake<Container>();
		var store = BuildStore(container);
		var snapshot = SnapshotAtVersion(100);

		A.CallTo(() => container.ReadItemAsync<CosmosDbSnapshotDocument>(
				A<string>._, A<PartitionKey>._, A<ItemRequestOptions>._, A<CancellationToken>._))
			.Returns(StoredAt(100));

		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => container.ReplaceItemAsync(
				A<CosmosDbSnapshotDocument>._, A<string>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => container.CreateItemAsync(
				A<CosmosDbSnapshotDocument>._, A<PartitionKey?>._, A<ItemRequestOptions>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static CosmosDbSnapshotStore BuildStore(Container container)
	{
		var database = A.Fake<Database>();
		A.CallTo(() => database.GetContainer(A<string>._)).Returns(container);

		var client = A.Fake<CosmosClient>();
		A.CallTo(() => client.GetDatabase(A<string>._)).Returns(database);

		var options = new CosmosDbSnapshotStoreOptions { CreateContainerIfNotExists = false };
		options.Client.ConnectionString =
			"AccountEndpoint=https://localhost:8081/;AccountKey=Zm9vYmFyYmF6cXV4";

		var tenantContext = A.Fake<ITenantContext>();
		A.CallTo(() => tenantContext.TenantId).Returns("t1");
		A.CallTo(() => tenantContext.HasTenant).Returns(true);

		return new CosmosDbSnapshotStore(
			Options.Create(options),
			NullLogger<CosmosDbSnapshotStore>.Instance,
			tenantContext,
			client);
	}

	private static ISnapshot SnapshotAtVersion(long version)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => snapshot.AggregateId).Returns(AggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns(AggregateType);
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UnixEpoch);
		A.CallTo(() => snapshot.Data).Returns(new ReadOnlyMemory<byte>(new byte[] { 1 }));
		A.CallTo(() => snapshot.TenantId).Returns("t1");
		return snapshot;
	}

	private static ItemResponse<CosmosDbSnapshotDocument> StoredAt(long version) =>
		new StubItemResponse(
			CosmosDbSnapshotDocument.FromSnapshot(SnapshotAtVersion(version), "t1"),
			$"etag-{version}");

	private static CosmosException Cosmos(HttpStatusCode statusCode) =>
		new(statusCode.ToString(), statusCode, subStatusCode: 0, activityId: "test", requestCharge: 0);

	/// <summary>
	/// A hand-written response rather than a faked one: the response is generic over an internal document
	/// type, so faking it would require opening this assembly's internals to the proxy generator.
	/// </summary>
	private sealed class StubItemResponse(CosmosDbSnapshotDocument resource, string etag)
		: ItemResponse<CosmosDbSnapshotDocument>
	{
		public override CosmosDbSnapshotDocument Resource => resource;

		public override string ETag => etag;

		public override HttpStatusCode StatusCode => HttpStatusCode.OK;

		public override Headers Headers => new();

		public override double RequestCharge => 0;

		public override string ActivityId => "stub";

		public override CosmosDiagnostics Diagnostics => null!;
	}
}
