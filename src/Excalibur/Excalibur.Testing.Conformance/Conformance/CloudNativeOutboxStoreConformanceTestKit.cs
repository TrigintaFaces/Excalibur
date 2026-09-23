// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Testing;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for <c>ICloudNativeOutboxStore</c> conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// <c>ICloudNativeOutboxStore</c> is a change-feed-oriented outbox contract for serverless/document
/// databases (Cosmos DB, DynamoDB, Firestore) - a different shape from the polling-based
/// <c>IOutboxStore</c> this package also certifies (<see cref="OutboxStoreConformanceTestKit"/>): different
/// message type, different return types (every operation reports request cost), and every member takes an
/// explicit <see cref="IPartitionKey"/> rather than resolving one from ambient state.
/// </para>
/// <para>
/// Two capabilities are optional and gated: <see cref="ICloudNativeOutboxStoreBatch"/> (batch add/mark/
/// cleanup, and the retry-count floor) and <see cref="ICloudNativeOutboxStoreClaim"/> (the atomic claim a
/// self-managed poller needs instead of the provider's change-feed trigger). A store that does not
/// implement one reports its arms as skipped via <see cref="ConformanceTestKit.OnArmSkipped"/> rather than
/// failing - see that type's remarks for how to make a required capability failing instead of skipped.
/// </para>
/// <para>
/// The atomic-claim arms are the ones a mocked client cannot certify: the exclusion property depends on the
/// provider's own conditional-write primitive rejecting a losing claimant's write, which a test double
/// cannot reproduce by construction. Certify those arms against a real provider endpoint or its emulator.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the outbox-store contract
/// rather than about the kit.</b> The arms read stored messages back through the store, and a conformant store deserializes the message payload. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Cloud-native outbox conformance arms read stored messages back through the store, which deserializes the message payload reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class CloudNativeOutboxStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh <see cref="ICloudNativeOutboxStore"/> instance for testing, addressed at a partition
	/// no other arm has touched.
	/// </summary>
	/// <returns>A store implementation to test.</returns>
	/// <remarks>
	/// Asynchronous for the same reason as the polling kit's seam: a real provider's construction (starting
	/// an emulator, waiting for its data plane, creating a container/table) is an await, and a synchronous
	/// seam would force every deriver into sync-over-async.
	/// </remarks>
	protected abstract Task<ICloudNativeOutboxStore> CreateStoreAsync();

	/// <summary>
	/// Creates a fresh, arm-private <see cref="IPartitionKey"/> so concurrent arms (and repeated runs) never
	/// share state.
	/// </summary>
	/// <returns>A new partition key.</returns>
	protected virtual IPartitionKey CreatePartitionKey() => new PartitionKey($"pk-{Guid.NewGuid():N}");

	/// <summary>Creates a test message addressed at <paramref name="partitionKey"/>.</summary>
	/// <param name="partitionKey">The partition the message is staged into.</param>
	/// <returns>A new <see cref="CloudOutboxMessage"/> carrying representative field values.</returns>
	protected virtual CloudOutboxMessage CreateTestMessage(IPartitionKey partitionKey) => new()
	{
		MessageId = $"msg-{Guid.NewGuid():N}",
		MessageType = "ConformanceTestMessageType",
		Payload = "conformance-payload"u8.ToArray(),
		Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["header-one"] = "value-one" },
		AggregateId = "aggregate-1",
		AggregateType = "ConformanceAggregate",
		CorrelationId = "corr-1",
		CausationId = "cause-1",
		TenantId = "tenant-1",
		Destination = "conformance-destination",
		CreatedAt = DateTimeOffset.UtcNow,
		PartitionKeyValue = partitionKey.Value,
	};

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new TestFixtureAssertionException(message);
		}
	}

	#region Contract-member round-trip - reflective enumeration

	/// <summary>
	/// Every settable member of the message contract, read from the type rather than listed in source.
	/// </summary>
	/// <remarks>
	/// <c>CanWrite</c> is the discriminator between a persisted member and a computed one, and it is
	/// structural rather than a judgement: <c>IsPublished</c> is <c>=&gt; PublishedAt.HasValue</c> with no
	/// setter, so it cannot be assigned, cannot be restored, and correctly never appears here.
	/// </remarks>
	private static readonly System.Reflection.PropertyInfo[] ContractMembers =
		[.. typeof(CloudOutboxMessage)
			.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
			.Where(static p => p.CanWrite)
			.OrderBy(static p => p.Name, StringComparer.Ordinal)];

	/// <summary>
	/// The members the staging path carries, and which a conformant store must therefore restore.
	/// </summary>
	private static readonly HashSet<string> MembersCarriedByStaging = new(StringComparer.Ordinal)
	{
		nameof(CloudOutboxMessage.MessageId),
		nameof(CloudOutboxMessage.MessageType),
		nameof(CloudOutboxMessage.Payload),
		nameof(CloudOutboxMessage.Headers),
		nameof(CloudOutboxMessage.AggregateId),
		nameof(CloudOutboxMessage.AggregateType),
		nameof(CloudOutboxMessage.CorrelationId),
		nameof(CloudOutboxMessage.CausationId),
		nameof(CloudOutboxMessage.TenantId),
		nameof(CloudOutboxMessage.Destination),
		nameof(CloudOutboxMessage.CreatedAt),
		nameof(CloudOutboxMessage.RetryCount),
		nameof(CloudOutboxMessage.LastError),
		nameof(CloudOutboxMessage.PartitionKeyValue),
	};

	/// <summary>
	/// The members the staging path does NOT carry, each against the reason asserting it here would bind a
	/// property the contract does not state.
	/// </summary>
	/// <remarks>
	/// An entry here is a claim about the CONTRACT, not an allowance for a provider: each names the arm or
	/// the contract clause that binds the member on the path which does carry it. Adding a member here to
	/// quiet a failure would certify the drift instead of detecting it.
	/// </remarks>
	private static readonly Dictionary<string, string> MembersNotCarriedByStaging = new(StringComparer.Ordinal)
	{
		[nameof(CloudOutboxMessage.PublishedAt)] =
			"a message carrying a publish instant is by definition not pending, so the read under test "
			+ "excludes it; MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage binds that property",
		[nameof(CloudOutboxMessage.ETag)] =
			"a concurrency token the SERVER assigns on write, not one carried from the caller's message; "
			+ "asserting a caller-supplied sentinel would require a store to store and hand back a token "
			+ "its own provider never issued",
		[nameof(CloudOutboxMessage.LeasedAt)] =
			"stamped by ICloudNativeOutboxStoreClaim.ClaimPendingAsync and never by staging - the contract "
			+ "states only claim-implementing stores ever set it; ClaimPendingAsync_StampsLeaseOwnerAndInstant "
			+ "binds it on the path that does",
		[nameof(CloudOutboxMessage.LeasedBy)] =
			"stamped by ICloudNativeOutboxStoreClaim.ClaimPendingAsync and never by staging, exactly as "
			+ "LeasedAt; ClaimPendingAsync_StampsLeaseOwnerAndInstant binds it on the path that does",
	};

	/// <summary>
	/// Builds a message carrying a DISTINCT non-default sentinel in every member the staging path carries.
	/// </summary>
	/// <param name="partitionKey">The partition the message is staged into.</param>
	/// <returns>The sentinel-bearing message.</returns>
	/// <remarks>
	/// Distinct values are what make a TRANSPOSITION detectable: a message using one string everywhere
	/// round-trips identically whether or not a mapping swapped two of its fields.
	/// <c>PartitionKeyValue</c> is the one member whose sentinel is not free — a store records the
	/// partition it was ASKED to write to, so the value it must restore is the argument's.
	/// </remarks>
	private static CloudOutboxMessage CreateSentinelMessage(IPartitionKey partitionKey) => new()
	{
		MessageId = $"sentinel-messageid-{Guid.NewGuid():N}",
		MessageType = "sentinel-messagetype",
		Payload = "sentinel-payload"u8.ToArray(),
		Headers = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["sentinel-headerkey"] = "sentinel-headervalue",
		},
		AggregateId = "sentinel-aggregateid",
		AggregateType = "sentinel-aggregatetype",
		CorrelationId = "sentinel-correlationid",
		CausationId = "sentinel-causationid",
		TenantId = "sentinel-tenantid",
		Destination = "sentinel-destination",
		CreatedAt = new DateTimeOffset(2021, 2, 3, 4, 5, 6, 7, TimeSpan.Zero),
		RetryCount = 9,
		LastError = "sentinel-lasterror",
		PartitionKeyValue = partitionKey.Value,
	};

	/// <summary>
	/// Asserts the classification is exhaustive and the sentinels usable, then compares every carried
	/// member of <paramref name="staged"/> against <paramref name="reloaded"/>.
	/// </summary>
	/// <param name="staged">The sentinel-bearing message that was written.</param>
	/// <param name="reloaded">The message as the read path under test reconstituted it.</param>
	/// <param name="readPath">A description of the read path, named in any failure.</param>
	private static void AssertEveryContractMemberRoundTripped(
		CloudOutboxMessage staged,
		CloudOutboxMessage reloaded,
		string readPath)
	{
		var contractMemberNames = new HashSet<string>(ContractMembers.Select(static p => p.Name), StringComparer.Ordinal);

		// (1) EXHAUSTIVENESS. This is the arm's reason for existing: a member added to the contract and
		// classified nowhere fails here, by name, before any store is judged.
		var unclassified = ContractMembers
			.Where(static p => !MembersCarriedByStaging.Contains(p.Name) && !MembersNotCarriedByStaging.ContainsKey(p.Name))
			.Select(static p => p.Name)
			.ToArray();

		Assert(
			unclassified.Length == 0,
			$"{unclassified.Length} member(s) of CloudOutboxMessage are classified neither as carried by "
			+ "staging nor as justifiably absent from it, so nothing in this suite establishes whether a "
			+ $"store persists and restores them: {string.Join(", ", unclassified)}. Add each to "
			+ "MembersCarriedByStaging with a distinct sentinel in CreateSentinelMessage, or to "
			+ "MembersNotCarriedByStaging with the contract reason it cannot be asserted here. This is the "
			+ "recurrence guard - the last three members added to this contract went unmapped in two "
			+ "providers precisely because no list knew to mention them.");

		// A classification naming a member the contract no longer has is a stale list pretending to cover
		// something. Caught here rather than silently reducing the population.
		var phantom = MembersCarriedByStaging.Concat(MembersNotCarriedByStaging.Keys)
			.Where(name => !contractMemberNames.Contains(name))
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToArray();

		Assert(
			phantom.Length == 0,
			"this suite classifies member(s) that CloudOutboxMessage no longer declares, so the "
			+ $"classification has drifted from the contract: {string.Join(", ", phantom)}");

		// (2) NON-DEFAULTNESS. Without this a member could be classified as carried and never given a
		// sentinel, and the comparison below would hold null against null and report a pass.
		var unsentinelled = ContractMembers
			.Where(static p => MembersCarriedByStaging.Contains(p.Name))
			.Where(p => IsDefaultValue(p.GetValue(staged)))
			.Select(static p => p.Name)
			.ToArray();

		Assert(
			unsentinelled.Length == 0,
			$"{unsentinelled.Length} member(s) are classified as carried by staging but hold a default "
			+ "value on the staged message, so comparing them proves nothing: "
			+ $"{string.Join(", ", unsentinelled)}. Give each a distinct non-default sentinel in "
			+ "CreateSentinelMessage.");

		// (3) DISTINCTNESS. Two members sharing a sentinel cannot distinguish a faithful mapping from one
		// that transposed them.
		var collisions = ContractMembers
			.Where(static p => MembersCarriedByStaging.Contains(p.Name))
			.GroupBy(p => Render(p.GetValue(staged)), StringComparer.Ordinal)
			.Where(static g => g.Count() > 1)
			.Select(static g => $"[{string.Join(" == ", g.Select(static p => p.Name))}]")
			.ToArray();

		Assert(
			collisions.Length == 0,
			$"{collisions.Length} group(s) of members share a sentinel value, so a mapping that transposed "
			+ $"them would still read back as a match: {string.Join(", ", collisions)}");

		// Only now is the store judged, and every carried member is judged - not a list of them.
		var dropped = new List<string>();
		foreach (var member in ContractMembers)
		{
			if (!MembersCarriedByStaging.Contains(member.Name))
			{
				continue;
			}

			var expected = member.GetValue(staged);
			var actual = member.GetValue(reloaded);

			if (!ValuesEqual(expected, actual))
			{
				dropped.Add($"{member.Name}: staged '{Render(expected)}', read back '{Render(actual)}'");
			}
		}

		Assert(
			dropped.Count == 0,
			$"{dropped.Count} of {MembersCarriedByStaging.Count} contract member(s) did not survive the "
			+ $"round trip through {readPath}. A member that is written and not read back - or never "
			+ "written - reaches the consumer as a null the message itself was carrying, which mis-routes "
			+ "or mis-attributes rather than erroring, so nothing downstream reports it. "
			+ $"{string.Join("; ", dropped)}");
	}

	/// <summary>Reports whether a member holds its type's default, i.e. carries no sentinel.</summary>
	/// <param name="value">The member's value on the staged message.</param>
	/// <returns><see langword="true"/> when the value could not distinguish a drop from a pass.</returns>
	private static bool IsDefaultValue(object? value) => value switch
	{
		null => true,
		string text => text.Length == 0,
		byte[] bytes => bytes.Length == 0,
		IDictionary<string, string> map => map.Count == 0,
		int number => number == 0,
		DateTimeOffset instant => instant == default,
		_ => false,
	};

	/// <summary>Compares two member values by their own semantics rather than by reference.</summary>
	/// <param name="left">The staged value.</param>
	/// <param name="right">The value read back.</param>
	/// <returns><see langword="true"/> when the member survived the round trip.</returns>
	private static bool ValuesEqual(object? left, object? right) => (left, right) switch
	{
		(null, null) => true,
		(null, _) or (_, null) => false,
		(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b),
		(IDictionary<string, string> a, IDictionary<string, string> b) =>
			a.Count == b.Count
			&& a.All(entry => b.TryGetValue(entry.Key, out var other)
				&& string.Equals(other, entry.Value, StringComparison.Ordinal)),
		_ => left.Equals(right),
	};

	/// <summary>Renders a member value for a failure message and for the distinctness check.</summary>
	/// <param name="value">The value to render.</param>
	/// <returns>A stable, human-readable rendering.</returns>
	private static string Render(object? value) => value switch
	{
		null => "(null)",
		byte[] bytes => Convert.ToBase64String(bytes),
		IDictionary<string, string> map => string.Join(
			";",
			map.OrderBy(static entry => entry.Key, StringComparer.Ordinal)
				.Select(static entry => $"{entry.Key}={entry.Value}")),
		IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "(null)",
	};

	#endregion

	#region Core arms - required (ICloudNativeOutboxStore)

	/// <summary>Adding a new message succeeds.</summary>
	public virtual async Task AddAsync_NewMessage_ReturnsSuccessResult()
	{
		RecordArmExecuted(nameof(AddAsync_NewMessage_ReturnsSuccessResult));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);

		var result = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		Assert(result.Success, "adding a new, valid message must succeed");
	}

	/// <summary>LIVENESS: a staged message is returned by a subsequent pending read of its partition.</summary>
	public virtual async Task AddAsync_ThenGetPending_ReturnsTheStagedMessage()
	{
		RecordArmExecuted(nameof(AddAsync_ThenGetPending_ReturnsTheStagedMessage));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Any(m => m.MessageId == message.MessageId),
			$"expected staged message '{message.MessageId}' to be returned by a pending read of its partition");
	}

	/// <summary>
	/// LIVENESS. The pending read is not confined to a subset of tenants: two messages staged into one
	/// partition under DIFFERENT tenants must both come back.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm exists because the interface predicts the exact failure it catches.</b>
	/// <c>ICloudNativeOutboxStore</c> states that <c>GetPendingAsync</c> takes no tenant, that ownership
	/// is carried on the row, and that "a store confining these reads to an ambient tenant would read that
	/// tenant as absent on the trigger path, return the empty set, and stall publication for every
	/// tenant." Nothing here asserted that, so the prediction was unenforced on all three cloud-native
	/// providers.
	/// </para>
	/// <para>
	/// <b>It varies the MESSAGE, never the host.</b> Tenancy on this contract belongs to the row, so an
	/// arm that instead resolved an ambient tenant would be asserting a property the contract forbids -
	/// and a store built to satisfy that reading is precisely what produces the stall above. The
	/// partition key is held constant for the same reason: it is physical placement, not tenancy, and
	/// varying it would test partitioning while appearing to test tenants.
	/// </para>
	/// <para>
	/// <b>It is a liveness arm with no safety twin, and that is deliberate.</b> The safety reading -
	/// "tenant B must not see tenant A's row" - is satisfied by a store that returns nothing to anybody,
	/// which is the stall itself wearing a passing assertion. On this contract the dangerous direction is
	/// under-returning, so the arm that can fail is the one that demands both.
	/// </para>
	/// </remarks>
	public virtual async Task GetPendingAsync_MustReturnMessagesFromEveryTenant()
	{
		RecordArmExecuted(nameof(GetPendingAsync_MustReturnMessagesFromEveryTenant));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();

		var first = CreateTestMessage(partitionKey) with { TenantId = "conformance-tenant-a" };
		var second = CreateTestMessage(partitionKey) with { TenantId = "conformance-tenant-b" };

		_ = await store.AddAsync(first, partitionKey, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AddAsync(second, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 100, ct),
			p => p.Documents.Any(m => m.MessageId == first.MessageId)
				&& p.Documents.Any(m => m.MessageId == second.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		var sawFirst = pending.Documents.Any(m => m.MessageId == first.MessageId);
		var sawSecond = pending.Documents.Any(m => m.MessageId == second.MessageId);

		Assert(
			sawFirst && sawSecond,
			"The pending read is confined to a subset of tenants. Two messages were staged into ONE "
			+ $"partition under different tenants and the read returned tenant-a={sawFirst}, "
			+ $"tenant-b={sawSecond}. One publisher serves every tenant on this contract, so a read that "
			+ "drops a tenant stalls publication for it permanently while the row stays pending and no "
			+ "other assertion fails.");
	}

	/// <summary>
	/// LIVENESS and REPRESENTATION: a message staged with NO tenant is returned by the pending read, and
	/// is returned carrying the reserved untenanted sentinel rather than an absence.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Liveness first, because it is the one that breaks a deployment.</b> A single-tenant host stages
	/// every message this way, so a store that drops the untenanted partition delivers nothing at all for
	/// one — and, as with the cross-tenant arm above, it does so while every other assertion passes.
	/// </para>
	/// <para>
	/// <b>The contract fixes the representation as well as the partition</b>, and this arm binds the same
	/// one its sibling <c>OutboxStoreConformanceTestKit.UntenantedPartition_MustRoundTripItsOwnMessage</c>
	/// binds: untenanted is a VALUE, not an absence. When absence has two spellings something has to fold
	/// between them, and every fold is a place the two can disagree — a consumer handler written as
	/// <c>msg.TenantId is null</c> must not re-establish a different partition depending on which store is
	/// underneath it. Fold a caller-supplied null through <c>KeyedTenantPartition.FromStoredValue</c> when
	/// persisting and again when reading back, and this arm passes; all three cloud-native stores already
	/// do exactly that on both paths.
	/// </para>
	/// <para>
	/// <b>It varies the MESSAGE, never the host</b> — the same discipline as the cross-tenant arm. Nothing
	/// here resolves an ambient tenant, and a store is not asked to.
	/// </para>
	/// </remarks>
	public virtual async Task UntenantedPartition_MustRoundTripItsOwnMessage()
	{
		RecordArmExecuted(nameof(UntenantedPartition_MustRoundTripItsOwnMessage));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey) with { TenantId = null };

		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		var own = pending.Documents.SingleOrDefault(m => m.MessageId == message.MessageId);

		Assert(
			own is not null,
			$"the untenanted partition does not round-trip: message '{message.MessageId}' was staged with "
			+ "no tenant and the pending read did not return it. A single-tenant host stages every message "
			+ "this way, so this store would never publish anything at all for one.");

		var carried = own!.TenantId is null ? "<null>" : $"'{own.TenantId}'";

		Assert(
			string.Equals(own.TenantId, Excalibur.Dispatch.TenantScope.UntenantedSentinel, StringComparison.Ordinal),
			$"a message staged with no tenant was read back carrying tenant {carried}, but the contract "
			+ $"requires the reserved untenanted partition '{Excalibur.Dispatch.TenantScope.UntenantedSentinel}'. "
			+ (string.IsNullOrWhiteSpace(own.TenantId)
				? "This store round-trips absence as an absence, so a consumer cannot tell an untenanted "
				+ "message from one whose tenant this store simply does not carry, and the same handler "
				+ "behaves differently against a provider that stores the sentinel."
				: "This store invented an owner the caller never supplied, and a consumer will "
				+ "re-establish that fabricated tenant."));
	}

	/// <summary>
	/// Round-trip fidelity: every canonical field on a staged message survives being read back - not just
	/// the identifier.
	/// </summary>
	public virtual async Task AddAsync_PreservesCanonicalFields_OnRoundTrip()
	{
		RecordArmExecuted(nameof(AddAsync_PreservesCanonicalFields_OnRoundTrip));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Any(m => m.MessageId == message.MessageId),
			$"the staged message '{message.MessageId}' was not returned by a pending read of its own "
			+ "partition, so no field could be compared");

		var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);

		Assert(reloaded.MessageType == message.MessageType, "MessageType must round-trip");
		Assert(reloaded.Payload.SequenceEqual(message.Payload), "Payload must round-trip byte-identical");
		Assert(reloaded.AggregateId == message.AggregateId, "AggregateId must round-trip");
		Assert(reloaded.AggregateType == message.AggregateType, "AggregateType must round-trip");
		Assert(reloaded.CorrelationId == message.CorrelationId, "CorrelationId must round-trip");
		Assert(reloaded.CausationId == message.CausationId, "CausationId must round-trip");
		Assert(reloaded.TenantId == message.TenantId, "TenantId must round-trip");
		Assert(reloaded.Destination == message.Destination, "Destination must round-trip");
		Assert(reloaded.PartitionKeyValue == message.PartitionKeyValue, "PartitionKeyValue must round-trip");
		Assert(!reloaded.IsPublished, "a freshly-staged message must not read back as published");
	}

	/// <summary>
	/// STRUCTURAL round-trip lock. Every member of the message contract is either carried by the staging
	/// path and asserted here against a distinct sentinel, or named in the justified-absent set with the
	/// reason it cannot be. A member in neither fails this arm by name.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this is not another hand-written field list.</b> The sibling arm
	/// <see cref="AddAsync_PreservesCanonicalFields_OnRoundTrip"/> asserts nine fields it names in source.
	/// That certifies the fields which existed the day it was written and is silent about the next one
	/// added — and this contract has already outlived exactly that defence. Three members (<c>ETag</c>,
	/// <c>LeasedAt</c>, <c>LeasedBy</c>) were added to the message after its round-trip risk was first
	/// raised; the audit that followed enumerated the older list, and every genuine mapping gap it
	/// eventually found lay in one of the three nobody was enumerating. A list cannot notice what is
	/// missing from itself, so this arm enumerates the type instead.
	/// </para>
	/// <para>
	/// <b>What makes it fail-closed.</b> Three structural properties are asserted before anything is
	/// asserted about a store. (1) Every settable member is classified — a newly added member appears in
	/// neither set and fails by name, which is the recurrence this arm exists to prevent. (2) Every member
	/// classified as carried holds a non-default sentinel — so a member cannot be classified and then left
	/// unset, which would compare null against null and pass. (3) No two sentinels are equal — so two
	/// fields transposed in a mapping cannot read back as a match. <c>IsPublished</c> needs no entry in
	/// either set: it is computed from <c>PublishedAt</c> and has no setter, so the enumeration never sees
	/// it.
	/// </para>
	/// <para>
	/// <b>The classification is kit-owned and deliberately not overridable.</b> A store that could nominate
	/// its own justified absentees could excuse precisely the drift this arm detects.
	/// </para>
	/// </remarks>
	public virtual async Task AddAsync_RoundTripsEveryContractMember_OnThePendingRead()
	{
		RecordArmExecuted(nameof(AddAsync_RoundTripsEveryContractMember_OnThePendingRead));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateSentinelMessage(partitionKey);

		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		var reloaded = pending.Documents.SingleOrDefault(m => m.MessageId == message.MessageId);

		Assert(
			reloaded is not null,
			$"the staged sentinel message '{message.MessageId}' was not returned by a pending read of its "
			+ "own partition, so not one member could be compared");

		AssertEveryContractMemberRoundTripped(message, reloaded!, "the store's own pending read (GetPendingAsync)");
	}


	/// <summary>
	/// How long an arm that reads back something it has just written keeps re-reading before it accepts
	/// what it last saw.
	/// </summary>
	/// <remarks>
	/// A store that orders its pending read through a separately-maintained, eventually-consistent index
	/// can omit a just-staged message from that read for a brief interval after staging returns. Raise
	/// this if your provider's index propagates more slowly than the default allows; the arms remain
	/// correct at any value, because each one still asserts the property it is there to check.
	/// </remarks>
	protected virtual TimeSpan PendingReadPropagationTimeout => TimeSpan.FromSeconds(10);

	/// <summary>A partition nothing was ever staged into reads back empty, not an error.</summary>
	public virtual async Task GetPendingAsync_EmptyPartition_ReturnsEmpty()
	{
		RecordArmExecuted(nameof(GetPendingAsync_EmptyPartition_ReturnsEmpty));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();

		// This arm reads ONCE, deliberately, and must not be converted to a bounded poll: nothing is
		// staged here, so there is no write whose propagation could be awaited. Polling until the read
		// came back empty would be satisfied by the first attempt and would assert nothing.
		var pending = await store.GetPendingAsync(partitionKey, 10, CancellationToken.None).ConfigureAwait(false);

		Assert(pending.Documents.Count == 0, "a never-staged partition must read back empty");
	}

	/// <summary>LIVENESS: messages read back in the order they were staged (FIFO).</summary>
	public virtual async Task GetPendingAsync_ReturnsMessagesInFifoOrder()
	{
		RecordArmExecuted(nameof(GetPendingAsync_ReturnsMessagesInFifoOrder));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var staged = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var message = CreateTestMessage(partitionKey);
			staged.Add(message.MessageId);
			_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		// Wait for every staged message to become visible BEFORE judging order: a partial read is ordered
		// correctly and still fails a whole-sequence comparison, which would report an ordering defect
		// that is really an incomplete read.
		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => staged.TrueForAll(id => p.Documents.Any(m => m.MessageId == id)),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Select(static m => m.MessageId).SequenceEqual(staged),
			"pending messages must read back in the order they were staged (FIFO)");
	}

	/// <summary>SAFETY: a published message is no longer returned by a pending read of its partition.</summary>
	public virtual async Task MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage()
	{
		RecordArmExecuted(nameof(MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		// Establish that the message IS pending before publishing it. An absence is only evidence of
		// exclusion if the thing was once present: on a store whose pending read is ordered through an
		// eventually-consistent index, a message that has not yet propagated is absent for a reason that
		// has nothing to do with publishing, and this arm would report success without ever observing
		// the property it exists to check.
		var staged = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			staged.Documents.Any(m => m.MessageId == message.MessageId),
			$"the staged message '{message.MessageId}' never appeared in a pending read, so its later "
			+ "absence could not show that publishing excluded it");

		var marked = await store.MarkAsPublishedAsync(message.MessageId, partitionKey, CancellationToken.None)
			.ConfigureAwait(false);
		Assert(marked.Success, $"marking an existing message as published must succeed. {marked.ErrorMessage}");

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => !p.Documents.Any(m => m.MessageId == message.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			!pending.Documents.Any(m => m.MessageId == message.MessageId),
			"a published message must not still appear in a pending read of its partition");
	}

	/// <summary>Marking a message that does not exist reports failure rather than a false success.</summary>
	public virtual async Task MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult()
	{
		RecordArmExecuted(nameof(MarkAsPublishedAsync_UnknownMessage_ReturnsFailureResult));

		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = CreatePartitionKey();

		var result = await store.MarkAsPublishedAsync($"nonexistent-{Guid.NewGuid():N}", partitionKey, CancellationToken.None)
			.ConfigureAwait(false);

		Assert(!result.Success, "marking a message that was never staged must not report success");
	}

	#endregion

	#region Batch arms - optional (ICloudNativeOutboxStoreBatch)

	/// <summary>Batch-adding several messages stages every one of them.</summary>
	public virtual async Task AddBatchAsync_AddsAllMessages_AndTheyAreAllPending()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(AddBatchAsync_AddsAllMessages_AndTheyAreAllPending), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(AddBatchAsync_AddsAllMessages_AndTheyAreAllPending));
		var partitionKey = CreatePartitionKey();
		var messages = Enumerable.Range(0, 3).Select(_ => CreateTestMessage(partitionKey)).ToList();

		var result = await batch.AddBatchAsync(messages, partitionKey, CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, "batch-adding valid messages must succeed");

		var staged = messages.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => staged.IsSubsetOf(p.Documents.Select(static m => m.MessageId)),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		var pendingIds = pending.Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var expectedIds = messages.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		Assert(pendingIds.SetEquals(expectedIds), "every message in the batch must be pending after AddBatchAsync");
	}

	/// <summary>Batch-marking several messages as published removes every one of them from the pending set.</summary>
	public virtual async Task MarkBatchAsPublishedAsync_MarksAllAsPublished()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(MarkBatchAsPublishedAsync_MarksAllAsPublished), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(MarkBatchAsPublishedAsync_MarksAllAsPublished));
		var partitionKey = CreatePartitionKey();
		var messages = Enumerable.Range(0, 3).Select(_ => CreateTestMessage(partitionKey)).ToList();
		_ = await batch.AddBatchAsync(messages, partitionKey, CancellationToken.None).ConfigureAwait(false);

		// Establish that all three ARE pending before publishing them, for the same reason as the
		// single-message arm: an empty pending read proves nothing if the batch never became visible.
		var ids = messages.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var staged = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => ids.IsSubsetOf(p.Documents.Select(static m => m.MessageId)),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			ids.IsSubsetOf(staged.Documents.Select(static m => m.MessageId)),
			$"only {staged.Documents.Count} of the {ids.Count} batched messages ever appeared in a pending "
			+ "read, so an empty read after publishing could not show that publishing removed them");

		var result = await batch.MarkBatchAsPublishedAsync(
			messages.Select(static m => m.MessageId), partitionKey, CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, $"batch-marking existing messages as published must succeed. {result.ErrorMessage}");

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Count == 0,
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(pending.Documents.Count == 0, "every batch-marked message must be gone from the pending set");
	}

	/// <summary>
	/// SAFETY: cleanup deletes only PUBLISHED messages older than the retention window - never an
	/// unpublished message, and never a published message still inside the window.
	/// </summary>
	public virtual async Task CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(CleanupOldMessagesAsync_DeletesOnlyPublishedMessagesOlderThanRetention));
		var partitionKey = CreatePartitionKey();

		var unpublished = CreateTestMessage(partitionKey);
		var publishedRecent = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(unpublished, partitionKey, CancellationToken.None).ConfigureAwait(false);
		_ = await store.AddAsync(publishedRecent, partitionKey, CancellationToken.None).ConfigureAwait(false);
		_ = await store.MarkAsPublishedAsync(publishedRecent.MessageId, partitionKey, CancellationToken.None).ConfigureAwait(false);

		// A retention window far longer than "just published" - a correct cleanup must leave this alone.
		_ = await batch.CleanupOldMessagesAsync(partitionKey, TimeSpan.FromDays(365), CancellationToken.None).ConfigureAwait(false);

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == unpublished.MessageId),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Any(m => m.MessageId == unpublished.MessageId),
			"cleanup must never delete an unpublished message, regardless of retention window");
	}

	/// <summary>Incrementing the retry count on a message records it and the error.</summary>
	public virtual async Task IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(IncrementRetryCountAsync_IncrementsRetryCountAndRecordsError));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var result = await batch.IncrementRetryCountAsync(
			message.MessageId, partitionKey, "transient publish failure", CancellationToken.None).ConfigureAwait(false);
		Assert(result.Success, "incrementing the retry count on an existing message must succeed");

		var pending = await ReadUntilAsync(
			ct => store.GetPendingAsync(partitionKey, 10, ct),
			p => p.Documents.Any(m => m.MessageId == message.MessageId && m.RetryCount >= 1),
			PendingReadPropagationTimeout,
			CancellationToken.None).ConfigureAwait(false);

		Assert(
			pending.Documents.Any(m => m.MessageId == message.MessageId),
			$"the staged message '{message.MessageId}' was not returned by a pending read, so the "
			+ "recorded retry count could not be read");

		var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);
		Assert(reloaded.RetryCount == 1, $"expected RetryCount 1 after one increment, got {reloaded.RetryCount}");
		Assert(reloaded.LastError == "transient publish failure", "the recorded error message must round-trip");
	}

	/// <summary>
	/// The retry-visibility floor: the retry count is MONOTONIC across repeated failures of the same
	/// message - it never resets or is overwritten with a smaller value, mirroring the guarantee the
	/// flat (non-cloud-native) backends provide via <c>NextAttemptAt</c> in <c>MarkFailedAsync</c>.
	/// </summary>
	public virtual async Task IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreBatch batch)
		{
			SkipArm(nameof(IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures), typeof(ICloudNativeOutboxStoreBatch),
				"store does not implement ICloudNativeOutboxStoreBatch");
			return;
		}

		RecordArmExecuted(nameof(IncrementRetryCountAsync_IsMonotonic_AcrossRepeatedFailures));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var previous = 0;
		for (var attempt = 1; attempt <= 3; attempt++)
		{
			_ = await batch.IncrementRetryCountAsync(
				message.MessageId, partitionKey, $"failure #{attempt}", CancellationToken.None).ConfigureAwait(false);

			var floor = previous;
			var pending = await ReadUntilAsync(
				ct => store.GetPendingAsync(partitionKey, 10, ct),
				p => p.Documents.Any(m => m.MessageId == message.MessageId && m.RetryCount > floor),
				PendingReadPropagationTimeout,
				CancellationToken.None).ConfigureAwait(false);

			Assert(
				pending.Documents.Any(m => m.MessageId == message.MessageId),
				$"the staged message '{message.MessageId}' was not returned by a pending read on attempt "
				+ $"{attempt}, so the retry count could not be read");

			var reloaded = pending.Documents.Single(m => m.MessageId == message.MessageId);

			Assert(
				reloaded.RetryCount > previous,
				$"the retry count must strictly increase on each failure (was {previous}, now {reloaded.RetryCount}), never reset");
			previous = reloaded.RetryCount;
		}

		Assert(previous == 3, $"expected 3 after three increments, got {previous}");
	}

	#endregion

	#region Claim arms - optional (ICloudNativeOutboxStoreClaim)

	/// <summary>An unclaimed message is returned by a claim, up to the requested batch size.</summary>
	public virtual async Task ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_ReturnsUnclaimedMessages_UpToBatchSize));
		var partitionKey = CreatePartitionKey();
		for (var i = 0; i < 5; i++)
		{
			_ = await store.AddAsync(CreateTestMessage(partitionKey), partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		var claimed = await claimable.ClaimPendingAsync(partitionKey, 3, "claimant-1", CancellationToken.None).ConfigureAwait(false);

		Assert(claimed.Documents.Count == 3, $"expected exactly 3 claimed messages (batchSize), got {claimed.Documents.Count}");
	}

	/// <summary>A claimed message carries the lease owner and instant the claim stamped.</summary>
	public virtual async Task ClaimPendingAsync_StampsLeaseOwnerAndInstant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_StampsLeaseOwnerAndInstant), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_StampsLeaseOwnerAndInstant));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var before = DateTimeOffset.UtcNow;
		var claimed = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-owner", CancellationToken.None).ConfigureAwait(false);

		var stamped = claimed.Documents.Single(m => m.MessageId == message.MessageId);
		Assert(stamped.LeasedBy == "claimant-owner", "a claimed message must be stamped with the claiming caller's id");
		Assert(stamped.LeasedAt.HasValue, "a claimed message must be stamped with a lease instant");
		Assert(
			stamped.LeasedAt!.Value >= before.AddSeconds(-5),
			"the stamped lease instant must be at or after the claim call, not stale/default data");
	}

	/// <summary>SAFETY: a message already claimed within its lease window is not handed to another claimant.</summary>
	public virtual async Task ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_DoesNotReturnAlreadyClaimedMessages_WithinTheLeaseWindow));
		var partitionKey = CreatePartitionKey();
		var message = CreateTestMessage(partitionKey);
		_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);

		var first = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-first", CancellationToken.None).ConfigureAwait(false);
		Assert(first.Documents.Any(m => m.MessageId == message.MessageId), "the first claimant must win the only staged message");

		var second = await claimable.ClaimPendingAsync(partitionKey, 10, "claimant-second", CancellationToken.None).ConfigureAwait(false);
		Assert(
			!second.Documents.Any(m => m.MessageId == message.MessageId),
			"a message claimed within its lease window must not be handed to a second claimant");
	}

	/// <summary>
	/// SAFETY and LIVENESS together (the atomic-claim property this contract exists to provide): two
	/// claimants racing over one partition must, between them, hand out every message exactly once.
	/// Disjointness alone is satisfied by a store that claims nothing for anybody - the union of what both
	/// claimants won must also equal every message staged.
	/// </summary>
	public virtual async Task ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		if (store is not ICloudNativeOutboxStoreClaim claimable)
		{
			SkipArm(nameof(ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets), typeof(ICloudNativeOutboxStoreClaim),
				"store does not implement ICloudNativeOutboxStoreClaim");
			return;
		}

		RecordArmExecuted(nameof(ClaimPendingAsync_ConcurrentClaimants_ReceiveDisjointSets));
		var partitionKey = CreatePartitionKey();
		const int StagedCount = 10;
		var staged = new List<string>();
		for (var i = 0; i < StagedCount; i++)
		{
			var message = CreateTestMessage(partitionKey);
			staged.Add(message.MessageId);
			_ = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		}

		var claimATask = claimable.ClaimPendingAsync(partitionKey, StagedCount, "claimant-a", CancellationToken.None);
		var claimBTask = claimable.ClaimPendingAsync(partitionKey, StagedCount, "claimant-b", CancellationToken.None);
		var results = await Task.WhenAll(claimATask, claimBTask).ConfigureAwait(false);

		var idsA = results[0].Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);
		var idsB = results[1].Documents.Select(static m => m.MessageId).ToHashSet(StringComparer.Ordinal);

		Assert(!idsA.Overlaps(idsB), "two concurrent claimants must never both win the same message");
		Assert(idsA.Union(idsB).ToHashSet(StringComparer.Ordinal).SetEquals(staged), "between them, the claimants must win every staged message");
	}

	#endregion

	#region Harness guard

	#endregion
}
