// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-DynamoDB (LocalStack) locks on the atomic claim: two claimants polling the same partition receive
/// disjoint sets, and the lease they stamp expires rather than stranding a message.
/// </summary>
/// <remarks>
/// <para>
/// A mocked <c>IAmazonDynamoDB</c> cannot prove any of this. The exclusion is performed by the server
/// evaluating a <c>ConditionExpression</c> and raising <c>ConditionalCheckFailedException</c> at exactly the
/// moment two writers collide; a mock returns whatever it was told and would certify a store whose update
/// carries no condition at all — which is the shape this store had before, and under which both claimants
/// publish every message.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbOutboxStoreClaimAtomicityShould
	: IClassFixture<DynamoDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	/// <summary>How many messages the concurrency arm stages. Enough that a broken claim collides on most.</summary>
	private const int StagedMessageCount = 8;

	private readonly DynamoDbOutboxStoreContainerFixture _fixture;
	private readonly List<(DynamoDbOutboxStore Store, string TableName)> _created = [];

	public DynamoDbOutboxStoreClaimAtomicityShould(DynamoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		var tables = new HashSet<string>(StringComparer.Ordinal);

		foreach (var (store, tableName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			_ = tables.Add(tableName);
		}

		foreach (var tableName in tables)
		{
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS together: two claimants racing over one partition must between them hand out
	/// every message exactly once.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Both halves are asserted here deliberately. Disjointness alone is satisfied perfectly by a store that
	/// claims nothing, for anybody, forever — so the arm also requires that the union of the two claimants'
	/// results is every message staged. A store that returns nothing fails the second half; a store whose
	/// update carries no condition fails the first.
	/// </remarks>
	[Fact]
	public async Task HandEachMessageToExactlyOneClaimant_WhenTwoClaimantsClaimConcurrently()
	{
		var ct = TestContext.Current.CancellationToken;
		var table = await CreateTableNameAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");

		var stagingStore = await CreateStoreAsync(table, leaseTimeoutSeconds: 120).ConfigureAwait(false);
		var staged = new List<string>(StagedMessageCount);

		for (var i = 0; i < StagedMessageCount; i++)
		{
			var message = CreateMessage(partitionKey.Value);
			var addResult = await stagingStore.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
			addResult.Success.ShouldBeTrue($"staging must succeed: {addResult.ErrorMessage}");
			staged.Add(message.MessageId);
		}

		// Two independent store instances, as two dispatcher processes would be.
		var claimantA = await CreateStoreAsync(table, leaseTimeoutSeconds: 120).ConfigureAwait(false);
		var claimantB = await CreateStoreAsync(table, leaseTimeoutSeconds: 120).ConfigureAwait(false);

		var claimedIds = new ConcurrentBag<string>();
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var raceA = Task.Run(() => ClaimAllAsync(claimantA, "claimant-a", partitionKey, claimedIds, start.Task, ct), ct);
		var raceB = Task.Run(() => ClaimAllAsync(claimantB, "claimant-b", partitionKey, claimedIds, start.Task, ct), ct);

		start.SetResult();
		await Task.WhenAll(raceA, raceB).ConfigureAwait(false);

		// SAFETY -- no message was claimed by both.
		var duplicates = claimedIds
			.GroupBy(id => id, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.ToList();

		duplicates.ShouldBeEmpty(
			"Two concurrent claimants received the same message. The claim must be a conditional update "
			+ "whose condition names the lease: an unconditional update has no precondition to fail, so both "
			+ "claimants succeed and both publish.");

		// LIVENESS -- every message was in fact handed to somebody. A store that claims nothing would
		// satisfy the assertion above and fail this one.
		claimedIds.Order(StringComparer.Ordinal).ShouldBe(
			staged.Order(StringComparer.Ordinal),
			"every staged message must be claimed by exactly one claimant -- disjointness is worthless if "
			+ "work is never handed out.");
	}

	/// <summary>
	/// SAFETY: a message under a live lease is not handed to a second claimant.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task RefuseToReclaimAMessage_WhileItsLeaseIsStillLive()
	{
		var ct = TestContext.Current.CancellationToken;
		var table = await CreateTableNameAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");

		var store = await CreateStoreAsync(table, leaseTimeoutSeconds: 120).ConfigureAwait(false);
		var message = CreateMessage(partitionKey.Value);
		_ = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);

		var first = await store.ClaimPendingAsync(partitionKey, 10, "claimant-a", ct).ConfigureAwait(false);
		first.Documents.Count.ShouldBe(1, "the first claimant must win the only staged message");
		first.Documents[0].LeasedBy.ShouldBe("claimant-a", "the claim must record who holds the lease");
		first.Documents[0].LeasedAt.ShouldNotBeNull("the claim must stamp a lease instant");

		var second = await store.ClaimPendingAsync(partitionKey, 10, "claimant-b", ct).ConfigureAwait(false);

		second.Documents.ShouldBeEmpty(
			"a message under a live lease must not be claimable by a second claimant -- the lease timeout is "
			+ "what bounds the duplicate window, and reclaiming inside it discards that bound.");
	}

	/// <summary>
	/// LIVENESS: a claimant that dies mid-delivery releases its messages by letting the lease age out.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This is the arm that separates a lease from a permanent lock. Without expiry the safety arm above is
	/// still satisfied — nobody else ever gets the message — and the outbox stops delivering the moment a
	/// claimant crashes. The lease is set to one second so the wait is observable without being long.
	/// </remarks>
	[Fact]
	public async Task ReclaimAMessage_AfterItsLeaseExpires()
	{
		var ct = TestContext.Current.CancellationToken;
		var table = await CreateTableNameAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");

		var store = await CreateStoreAsync(table, leaseTimeoutSeconds: 1).ConfigureAwait(false);
		var message = CreateMessage(partitionKey.Value);
		_ = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);

		var first = await store.ClaimPendingAsync(partitionKey, 10, "claimant-a", ct).ConfigureAwait(false);
		first.Documents.Count.ShouldBe(1, "the first claimant must win the only staged message");

		// Poll rather than sleeping once: the lease becomes claimable at an instant, and a single fixed wait
		// either races it or wastes time.
		var reclaimed = await WaitForReclaimAsync(store, partitionKey, "claimant-b", ct).ConfigureAwait(false);

		reclaimed.ShouldNotBeNull(
			"an expired lease must be reclaimable -- otherwise a claimant that dies mid-delivery strands its "
			+ "messages permanently, and no amount of disjointness makes that correct.");
		reclaimed.MessageId.ShouldBe(message.MessageId);
		reclaimed.LeasedBy.ShouldBe("claimant-b", "the reclaim must re-stamp the lease with the new owner");
	}

	/// <summary>
	/// SAFETY: the lease a message is handed must be anchored at the write that establishes it, not at the
	/// instant the drain began.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// The invariant: for a message claimed at instant <c>t_write</c>, no second dispatcher may take it
	/// before <c>t_write + LeaseTimeoutSeconds</c>. The lease's protective interval has to be measured from
	/// the write that created it.
	/// </para>
	/// <para>
	/// Anchoring the whole batch at the drain's start breaks that. The stamp is taken once, before the
	/// candidate query even runs, and then written to all N messages; so the last message of an N-message
	/// batch is handed a lease that has already spent the query round-trip plus N-1 conditional writes. Its
	/// remaining protection shrinks as the batch grows, which makes the duplicate window a function of a
	/// consumer-configurable batch size against a remote service rather than a bound the guarantee names.
	/// </para>
	/// <para>
	/// The arm is deterministic rather than timing-tuned. Under batch anchoring every stamp is written from
	/// one captured value, so the N stamps are byte-identical and the distinct count is 1. Under per-write
	/// anchoring each stamp is read separately, on opposite sides of a network round-trip that costs orders
	/// of magnitude more than the clock's resolution, so the N stamps are distinct and non-decreasing. The
	/// assertion is identity-versus-distinctness, not a tolerance.
	/// </para>
	/// <para>
	/// The eligibility cutoff is deliberately not re-anchored, and this arm does not ask it to be: an older
	/// cutoff judges fewer leases expired, which is the conservative direction and cannot admit a live lease.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task AnchorEachLeaseAtItsOwnWrite_RatherThanAtTheStartOfTheDrain()
	{
		var ct = TestContext.Current.CancellationToken;
		var table = await CreateTableNameAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");

		var store = await CreateStoreAsync(table, leaseTimeoutSeconds: 120).ConfigureAwait(false);

		for (var i = 0; i < StagedMessageCount; i++)
		{
			var message = CreateMessage(partitionKey.Value);
			var addResult = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
			addResult.Success.ShouldBeTrue($"staging must succeed: {addResult.ErrorMessage}");
		}

		var preDrain = DateTimeOffset.UtcNow;
		var claimed = await store
			.ClaimPendingAsync(partitionKey, StagedMessageCount, "claimant-a", ct)
			.ConfigureAwait(false);
		var postDrain = DateTimeOffset.UtcNow;

		// LIVENESS -- the whole batch was handed out. Every assertion below is satisfied vacuously by a
		// store that claims nothing, so this comes first.
		claimed.Documents.Count.ShouldBe(
			StagedMessageCount,
			"the drain must hand out the whole batch -- a store that claims nothing satisfies every lease "
			+ "assertion below without providing a lease at all.");

		var stamps = new List<DateTimeOffset>(claimed.Documents.Count);

		foreach (var document in claimed.Documents)
		{
			document.LeasedAt.ShouldNotBeNull("every claimed message must carry a lease instant.");
			stamps.Add(document.LeasedAt.Value);
		}

		// Each stamp belongs to this drain, and none was invented after it finished.
		foreach (var stamp in stamps)
		{
			stamp.ShouldBeGreaterThanOrEqualTo(
				preDrain,
				"a lease stamped before the drain began would already be partly elapsed on arrival.");
			stamp.ShouldBeLessThanOrEqualTo(
				postDrain,
				"a lease cannot be stamped after the drain that produced it returned.");
		}

		// THE ARM. Batch anchoring writes one captured value to all N messages, so the distinct count
		// collapses to 1. Per-write anchoring reads the clock once per write, so it is N.
		stamps.Distinct().Count().ShouldBe(
			StagedMessageCount,
			"every message in the batch carries the same lease instant, so the stamp was captured once at "
			+ "the drain's start and copied. The last message of the batch is therefore holding a lease "
			+ "that has already spent the query round-trip and every preceding conditional write, and the "
			+ "amount it has lost grows with the batch size.");

		// The stamps are handed out in write order, so they must not go backwards.
		for (var i = 1; i < stamps.Count; i++)
		{
			stamps[i].ShouldBeGreaterThan(
				stamps[i - 1],
				$"lease {i} was stamped no later than lease {i - 1}, so the stamps are not being taken at "
				+ "their own writes.");
		}

		// Stated as the property rather than the mechanism: the last message of the batch did not inherit
		// the first message's instant.
		stamps[^1].ShouldBeGreaterThan(
			stamps[0],
			"the last message of the batch was handed the first message's lease instant, which is exactly "
			+ "the elapsed drain time it has been silently charged.");
	}

	private static async Task ClaimAllAsync(
		DynamoDbOutboxStore store,
		string claimantId,
		IPartitionKey partitionKey,
		ConcurrentBag<string> claimedIds,
		Task start,
		CancellationToken cancellationToken)
	{
		await start.ConfigureAwait(false);

		// Drain until this claimant is told there is nothing left for it. Both claimants run this, so the
		// union is the whole partition only if the claim actually hands work out.
		for (var pass = 0; pass < StagedMessageCount + 2; pass++)
		{
			var result = await store
				.ClaimPendingAsync(partitionKey, StagedMessageCount, claimantId, cancellationToken)
				.ConfigureAwait(false);

			if (result.Documents.Count == 0)
			{
				return;
			}

			foreach (var claimed in result.Documents)
			{
				claimed.LeasedBy.ShouldBe(claimantId, "a claimed message must carry the lease of its claimant");
				claimedIds.Add(claimed.MessageId);
			}
		}
	}

	private static async Task<CloudOutboxMessage?> WaitForReclaimAsync(
		DynamoDbOutboxStore store,
		IPartitionKey partitionKey,
		string claimantId,
		CancellationToken cancellationToken)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(20);

		while (DateTimeOffset.UtcNow < deadline)
		{
			var result = await store
				.ClaimPendingAsync(partitionKey, 10, claimantId, cancellationToken)
				.ConfigureAwait(false);

			if (result.Documents.Count > 0)
			{
				return result.Documents[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static CloudOutboxMessage CreateMessage(string partitionKeyValue) =>
		new()
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKeyValue
		};

	private Task<string> CreateTableNameAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("LocalStack DynamoDB must be available -- never skipped.");
		return Task.FromResult($"outbox_{Guid.NewGuid():N}");
	}

	private async Task<DynamoDbOutboxStore> CreateStoreAsync(string table, int leaseTimeoutSeconds)
	{
		var opts = new DynamoDbOutboxOptions
		{
			TableName = table,
			CreateTableIfNotExists = true,
			EnableStreams = false,
			LeaseTimeoutSeconds = leaseTimeoutSeconds,
			Connection = new DynamoDbOutboxConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				AccessKey = "test",
				SecretKey = "test"
			}
		};

		var store = new DynamoDbOutboxStore(Options.Create(opts), NullLogger<DynamoDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, table));
		return store;
	}
}
