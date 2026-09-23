// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.CosmosDb;

using Shouldly;

using Xunit;

namespace Excalibur.Data.CosmosDb.Tests.Outbox;

/// <summary>
/// Field-fidelity lock on the Cosmos outbox mapping: every persisted member of
/// <see cref="CloudOutboxMessage"/> must survive <c>ToDocument</c> → <c>FromDocument</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect class.</b> A store's write path and read path are two independently-maintained field
/// lists, so a property can be persisted and never read back — or never persisted — and nothing catches
/// it. The message still deserializes; it simply arrives with a routing field null, and the consumer is
/// mis-routed rather than erroring. That is silent, and it is why a smoke test over one or two fields
/// does not close it.
/// </para>
/// <para>
/// <b>Why this is a UNIT test and not an emulator test.</b> The tracked bead asks for this "against each
/// provider's EMULATOR". It does not need one: <c>ToDocument</c> and <c>FromDocument</c> are <c>public
/// static</c> pure functions over a document type. The defect is drift between two field lists, and drift
/// is a property of the MAPPER, not of the store. Storage behaviour — that a write becomes visible to the
/// change feed, that a conditional write conflicts — is a different question needing different arms.
/// </para>
/// <para>
/// <b>DISTINCT SENTINELS ARE LOAD-BEARING.</b> Every field gets a different value. A round-trip using the
/// same string everywhere passes while two fields are SWAPPED, which is the failure mode most likely to
/// be introduced by a copy-paste edit to one of the two lists.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("SubComponent", "CloudNativeOutbox")]
public sealed class CosmosDbOutboxMessageRoundTripShould
{
	/// <summary>
	/// Members that legitimately do not survive a round-trip, each with the reason it cannot.
	/// </summary>
	/// <remarks>
	/// This set is asserted to be EXACTLY right by <see cref="FailWhenANewMemberIsNeitherRoundTrippedNorDeclaredExempt"/>.
	/// It is not a suppression list: adding a member here without a reason that survives review turns a
	/// silent drop into a documented one, which is the same defect wearing a comment.
	/// </remarks>
	private static readonly Dictionary<string, string> ExemptMembers = new(StringComparer.Ordinal)
	{
		[nameof(CloudOutboxMessage.PartitionKeyValue)] =
			"the partition key is supplied as an ARGUMENT to ToDocument and the argument is what is "
			+ "persisted, so the value on the message is deliberately not the source of truth",
		[nameof(CloudOutboxMessage.IsPublished)] =
			"computed from PublishedAt; it has no setter, so it cannot be round-tripped and does not "
			+ "need to be",
		[nameof(CloudOutboxMessage.ETag)] =
			"assigned by the SERVER on write; a value set before the round-trip is not what comes back",
		[nameof(CloudOutboxMessage.LeasedAt)] = LeaseReason,
		[nameof(CloudOutboxMessage.LeasedBy)] = LeaseReason,
	};

	/// <summary>
	/// Why lease state cannot survive the STAGE mapper, recorded because the naive expectation is wrong
	/// in a way that reads like a defect.
	/// </summary>
	/// <remarks>
	/// <c>ToDocument</c> is the STAGE path and a message being staged is by definition unclaimed, so it
	/// carries no lease. The lease is written by the CLAIM path, which mutates the stored document
	/// directly — <c>CosmosDbOutboxStore.cs:556</c> sets <c>document.LeasedAt</c> and <c>:561</c>
	/// <c>ReplaceItemAsync</c>s it under an ETag precondition — and never round-trips through this mapper.
	/// <c>FromDocument</c> correctly READS both, because a claimed document has them.
	/// <para>
	/// This arm originally asserted them and went RED. That was the TEST being wrong, not the code: the
	/// fixture modelled a message staged while already leased, a state the store cannot produce. Recorded
	/// rather than silently deleted, because a future reader will have the same naive expectation.
	/// </para>
	/// </remarks>
	private const string LeaseReason =
		"written by the CLAIM path directly onto the stored document (ReplaceItem under an ETag "
		+ "precondition), never by the stage mapper — a message being staged is unclaimed by definition";

	/// <summary>
	/// Every non-exempt member survives the round-trip with its own distinct value.
	/// </summary>
	[Fact]
	public void PreserveEveryPersistedMemberThroughToDocumentAndBack()
	{
		var original = CreateFullyPopulatedMessage();

		var restored = CosmosDbOutboxDocumentMap.FromDocument(
			CosmosDbOutboxDocumentMap.ToDocument(original, new PartitionKey("partition-sentinel")));

		restored.MessageId.ShouldBe(original.MessageId);
		restored.MessageType.ShouldBe(original.MessageType);
		restored.Payload.ShouldBe(original.Payload);
		restored.AggregateId.ShouldBe(original.AggregateId);
		restored.AggregateType.ShouldBe(original.AggregateType);
		restored.CorrelationId.ShouldBe(original.CorrelationId);
		restored.CausationId.ShouldBe(original.CausationId);
		restored.TenantId.ShouldBe(original.TenantId);
		restored.Destination.ShouldBe(original.Destination);
		restored.CreatedAt.ShouldBe(original.CreatedAt);
		restored.PublishedAt.ShouldBe(original.PublishedAt);
		restored.RetryCount.ShouldBe(original.RetryCount);
		restored.LastError.ShouldBe(original.LastError);

		restored.Headers.ShouldNotBeNull();
		restored.Headers!["header-one"].ShouldBe("header-one-sentinel");
		restored.Headers["header-two"].ShouldBe("header-two-sentinel");
	}

	/// <summary>
	/// THE ARM THAT STOPS THIS RECURRING. Enumerates the contract from the TYPE, so a member added
	/// tomorrow is covered by this test the day it is added — without anyone remembering to extend it.
	/// </summary>
	/// <remarks>
	/// The arm above is a hand-written list and therefore has exactly the weakness it exists to catch: it
	/// can fall behind the record. This one derives the population by reflection, so a new property is
	/// neither asserted above nor declared exempt and the suite goes RED naming it. Without this the bead
	/// recurs the next time someone extends the message.
	/// </remarks>
	[Fact]
	public void FailWhenANewMemberIsNeitherRoundTrippedNorDeclaredExempt()
	{
		var assertedAbove = new HashSet<string>(StringComparer.Ordinal)
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
			nameof(CloudOutboxMessage.PublishedAt),
			nameof(CloudOutboxMessage.RetryCount),
			nameof(CloudOutboxMessage.LastError),
		};

		var contractMembers = typeof(CloudOutboxMessage)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(static p => p.Name)
			.ToList();

		var unaccounted = contractMembers
			.Where(name => !assertedAbove.Contains(name) && !ExemptMembers.ContainsKey(name))
			.ToList();

		unaccounted.ShouldBeEmpty(
			"every member of CloudOutboxMessage must either be asserted in the round-trip arm or declared "
			+ "exempt with a reason. A member that is neither was added without anyone deciding whether it "
			+ "survives persistence, which is exactly how a routing field gets silently dropped: "
			+ string.Join(", ", unaccounted));

		// The exemption list must not outlive the members it excuses, or it silently starts excusing
		// nothing while looking like coverage.
		var staleExemptions = ExemptMembers.Keys.Where(name => !contractMembers.Contains(name)).ToList();

		staleExemptions.ShouldBeEmpty(
			"an exemption naming a member that no longer exists is dead and must be removed: "
			+ string.Join(", ", staleExemptions));
	}

	/// <summary>
	/// A message with a DISTINCT value in every nullable member, so a swap between two of them fails.
	/// </summary>
	private static CloudOutboxMessage CreateFullyPopulatedMessage() => new()
	{
		MessageId = "message-id-sentinel",
		MessageType = "message-type-sentinel",
		Payload = [1, 2, 3, 4],
		Headers = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["header-one"] = "header-one-sentinel",
			["header-two"] = "header-two-sentinel",
		},
		AggregateId = "aggregate-id-sentinel",
		AggregateType = "aggregate-type-sentinel",
		CorrelationId = "correlation-id-sentinel",
		CausationId = "causation-id-sentinel",
		TenantId = "tenant-id-sentinel",
		Destination = "destination-sentinel",
		CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
		PublishedAt = new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero),
		RetryCount = 7,
		LastError = "last-error-sentinel",
		PartitionKeyValue = "partition-sentinel",
		LeasedAt = new DateTimeOffset(2026, 9, 10, 11, 12, 13, TimeSpan.Zero),
		LeasedBy = "leased-by-sentinel",
	};
}
