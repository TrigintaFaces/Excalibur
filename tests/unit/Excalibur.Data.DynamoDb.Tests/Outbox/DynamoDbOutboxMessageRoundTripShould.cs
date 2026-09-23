// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Amazon.DynamoDBv2.Model;

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.DynamoDb.Tests.Outbox;

/// <summary>
/// Field-fidelity lock on the DynamoDb outbox mapping: every persisted member of
/// <see cref="CloudOutboxMessage"/> survives the store's write path and comes back off its read path.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect class.</b> A store's write path and read path are two independently-maintained field
/// lists, so a property can be persisted and never read back — or never persisted at all — and nothing
/// catches it. The message still deserializes; it simply arrives with a routing field null, and the
/// consumer is mis-routed rather than erroring. That is silent, which is why a smoke test over one or two
/// fields does not close it.
/// </para>
/// <para>
/// <b>Reflection rather than widened visibility.</b> Both mappers are <c>private</c>. Our testing rules
/// prefer reflection over widening production visibility for exactly this case, so nothing in the shipped
/// surface changed to make this testable. The cost is that a rename breaks these arms with a
/// <see cref="MissingMethodException"/> rather than a compile error — so the lookups assert their target
/// exists before using it, and say what to do when it does not.
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
public sealed class DynamoDbOutboxMessageRoundTripShould
{
	private const string PartitionSentinel = "partition-sentinel";

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
		[nameof(CloudOutboxMessage.IsPublished)] =
			"computed from PublishedAt; it has no setter, so it cannot be round-tripped and does not "
			+ "need to be",
		[nameof(CloudOutboxMessage.ETag)] =
			"a concurrency token assigned by the server on write. DynamoDb has no equivalent attribute "
			+ "on this table, and a value set before the round-trip is not what would come back",
		[nameof(CloudOutboxMessage.LeasedAt)] = LeaseReason,
		[nameof(CloudOutboxMessage.LeasedBy)] = LeaseReason,
	};

	/// <summary>
	/// Why lease state cannot survive the STAGE mapper, recorded because the naive expectation is wrong
	/// in a way that reads exactly like a dropped field.
	/// </summary>
	/// <remarks>
	/// <c>ToAttributeMap</c> is the STAGE path, and a message being staged is unclaimed by definition, so
	/// it carries no lease. The lease is written by the CLAIM path, which updates the stored item
	/// directly — <c>DynamoDbOutboxStore.cs:466</c>, <c>SET #leasedAt = :now, #leasedBy = :claimant</c>
	/// under a condition expression — and never round-trips through this mapper. <c>FromAttributeMap</c>
	/// correctly READS both, because a claimed item has them.
	/// <para>
	/// This arm originally asserted them and went RED. That was the TEST being wrong, not the code —
	/// <b>and it is the second time</b>: the Cosmos round-trip arms record the identical finding, and I
	/// wrote them first. Restating an exemption per provider is how a reasoned exemption becomes a
	/// copied one, so it is written out here in full rather than cross-referenced.
	/// </para>
	/// </remarks>
	private const string LeaseReason =
		"written by the CLAIM path directly onto the stored item (a conditional UpdateItem), never by "
		+ "the stage mapper — a message being staged is unclaimed by definition";

	/// <summary>
	/// Every non-exempt member survives the round-trip with its own distinct value.
	/// </summary>
	[Fact]
	public void PreserveEveryPersistedMemberThroughTheWriteAndReadPaths()
	{
		var original = CreateFullyPopulatedMessage();

		var restored = FromAttributeMap(ToAttributeMap(original));

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
		restored.PartitionKeyValue.ShouldBe(PartitionSentinel);

		// LeasedAt/LeasedBy are deliberately NOT asserted here -- see LeaseReason. They are written by
		// the claim path, not by this one, so a message that has only been staged has no lease to carry.

		restored.Headers.ShouldNotBeNull();
		restored.Headers!["header-one"].ShouldBe("header-one-sentinel");
		restored.Headers["header-two"].ShouldBe("header-two-sentinel");
	}

	/// <summary>
	/// THE ARM THAT STOPS THIS RECURRING. Enumerates the contract from the TYPE, so a member added
	/// tomorrow is covered by this test the day it is added — without anyone remembering to extend it.
	/// </summary>
	/// <remarks>
	/// The arm above is a hand-written list and therefore carries exactly the weakness it exists to
	/// catch: it can fall behind the record. This one derives the population by reflection, so a new
	/// property is neither asserted above nor declared exempt and the suite goes RED naming it.
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
			nameof(CloudOutboxMessage.PartitionKeyValue),
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
	/// A dropped field is invisible when the value is the type's default, so the read path is also
	/// checked against a message whose nullable members are all absent.
	/// </summary>
	/// <remarks>
	/// The round-trip above populates everything, which catches a field that is never written. It does
	/// NOT catch a read path that throws on an absent optional attribute — and every optional attribute
	/// is absent for a message staged before that field existed. This arm is the upgrade path.
	/// </remarks>
	[Fact]
	public void ReadAMessageWhoseOptionalAttributesAreAllAbsent()
	{
		var minimal = new CloudOutboxMessage
		{
			MessageId = "message-id-sentinel",
			MessageType = "message-type-sentinel",
			Payload = [7],
			CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),

			// Required by the record, so it is not optional and cannot be omitted here. Everything else
			// below is genuinely absent, which is the state this arm exists to read back.
			PartitionKeyValue = PartitionSentinel,
		};

		var restored = FromAttributeMap(ToAttributeMap(minimal));

		restored.MessageId.ShouldBe(minimal.MessageId);
		restored.Payload.ShouldBe(minimal.Payload);
		restored.AggregateId.ShouldBeNull();
		restored.Destination.ShouldBeNull();
		restored.LeasedBy.ShouldBeNull();

		// Not null, and deliberately so: a missing tenant attribute folds onto the untenanted partition
		// through a total conversion rather than surfacing as null, so a row written before tenancy
		// existed reloads as untenanted rather than as "unknown".
		restored.TenantId.ShouldNotBeNull();
	}

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
		PartitionKeyValue = PartitionSentinel,
	};

	private static DynamoDbOutboxStore CreateStore() =>
		new(
			Options.Create(new DynamoDbOutboxOptions
			{
				// The options validate on construction and require an endpoint, so one is supplied. No
				// request is ever issued: these arms drive the two pure mapping methods and never touch
				// the client, which is what makes a round-trip testable without an emulator at all.
				Connection = new DynamoDbOutboxConnectionOptions { ServiceUrl = "http://localhost:8000" },
			}),
			NullLogger<DynamoDbOutboxStore>.Instance);

	private static Dictionary<string, AttributeValue> ToAttributeMap(CloudOutboxMessage message) =>
		(Dictionary<string, AttributeValue>)Invoke("ToAttributeMap", message, new StubPartitionKey())!;

	private static CloudOutboxMessage FromAttributeMap(Dictionary<string, AttributeValue> item) =>
		(CloudOutboxMessage)Invoke("FromAttributeMap", item)!;

	/// <summary>
	/// Calls a private mapper, asserting it exists first so a rename produces a readable failure.
	/// </summary>
	private static object? Invoke(string methodName, params object[] args)
	{
		var method = typeof(DynamoDbOutboxStore)
			.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull(
			$"DynamoDbOutboxStore.{methodName} was not found. These arms reach the mappers by reflection "
			+ "rather than widening production visibility, so a rename breaks them here instead of at "
			+ "compile time. If the method moved, point this lookup at its new name -- do not delete the "
			+ "arms, because the field-drift they detect is silent in production.");

		try
		{
			return method!.Invoke(CreateStore(), args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			// Surface the real failure rather than the reflection wrapper, so a mapper that throws
			// reports what it threw.
			throw ex.InnerException;
		}
	}

	private sealed class StubPartitionKey : IPartitionKey
	{
		public string Value => PartitionSentinel;

		public string Path => "/pk";
	}
}
