// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.CosmosDb;

using Shouldly;

using Xunit;

namespace Excalibur.Data.CosmosDb.Tests.Outbox;

/// <summary>
/// Author≠impl lock on the cloud-native outbox document round-trip: a property of
/// <see cref="CloudOutboxMessage"/> cannot be persisted-but-never-read, read-but-never-persisted, or added in
/// future and silently left unmapped.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect class.</b> A store's write path and read path are two hand-written, independently maintained
/// field lists. A property added to the contract can be persisted and never read back — or never persisted at
/// all — and nothing catches it: the document still deserializes, the message simply arrives with a routing
/// field null, so the consumer is mis-routed rather than erroring. That is silent, which is what makes it
/// expensive.
/// </para>
/// <para>
/// <b>Why this runs with no emulator.</b> The mapping is a pure function in
/// <c>CosmosDbOutboxDocumentMap</c>, so the round-trip is assertable directly. That matters: the emulator
/// suites do not run in CI, so a lock that needed one would be a lock that never executes — the shape this
/// project keeps catching. This one runs on every build of this project. It does NOT replace an emulator
/// round-trip (it cannot see serializer settings the SDK applies, or a container's own indexing); it closes
/// the field-list-drift half, which is where the class actually hides.
/// </para>
/// <para>
/// <b>safety∧liveness.</b> The liveness arm sets every persisted field to a DISTINCT sentinel and asserts each
/// one survives — distinct values matter, because a test using the same string everywhere cannot detect two
/// fields being swapped. The safety arm is the reflection census: it fails on any property that is in neither
/// the round-tripped set nor the justified-absent set, so ADDING a property to the contract reddens this test
/// until someone classifies it. Without that arm the liveness arm would keep passing forever over a contract
/// that had grown three unmapped fields.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudOutboxMessageRoundTripShould
{
	/// <summary>
	/// Properties the SAVE path must carry intact. Each is asserted individually below — this set is the
	/// census key, not the assertion.
	/// </summary>
	private static readonly string[] RoundTrippedThroughSavePath =
	[
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
	];

	/// <summary>
	/// Properties the SAVE path deliberately does not carry, each with the reason it is not drift. An
	/// asymmetry with no stated reason is indistinguishable from a dropped field, which is exactly how the
	/// next person "fixes" it and re-opens a defect.
	/// </summary>
	private static readonly Dictionary<string, string> JustifiedAbsentFromSavePath = new(StringComparer.Ordinal)
	{
		[nameof(CloudOutboxMessage.ETag)] =
			"Provider-supplied concurrency token. Cosmos assigns _etag on write and the store reads it back " +
			"off the response; a client-supplied value would be meaningless and is never written.",

		[nameof(CloudOutboxMessage.LeasedAt)] =
			"Written by the CLAIM path under an ETag precondition, not by the save path. Staging a message " +
			"must not stamp a lease — that would hand out ownership nobody asked for.",

		[nameof(CloudOutboxMessage.LeasedBy)] =
			"Same as LeasedAt: stamped when a claimant wins the document. The contract itself calls it 'who " +
			"took it last, not a live ownership assertion', so the save path has nothing to say about it.",

		[nameof(CloudOutboxMessage.IsPublished)] =
			"Derived (=> PublishedAt.HasValue). It is written to the document for queryability but cannot be " +
			"read back into the record, because there is no setter. Persisting it is redundant, not drift.",
	};

	/// <summary>
	/// SAFETY — the census. Every property of the contract is classified, so a NEW property cannot arrive
	/// unmapped and unnoticed. This is the arm that makes the lock survive the next change.
	/// </summary>
	[Fact]
	public void ClassifyEveryPropertyOfTheContract()
	{
		var declared = typeof(CloudOutboxMessage)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => p.Name)
			.ToArray();

		// Guard the instrument before trusting it: a reflection census that finds nothing would pass
		// vacuously, and a zero here is indistinguishable from a clean tree.
		declared.Length.ShouldBeGreaterThan(10,
			"reflection found almost no properties on CloudOutboxMessage — the census is broken, not the contract clean");

		var classified = RoundTrippedThroughSavePath
			.Concat(JustifiedAbsentFromSavePath.Keys)
			.ToHashSet(StringComparer.Ordinal);

		var unclassified = declared.Where(name => !classified.Contains(name)).ToArray();

		unclassified.ShouldBeEmpty(
			$"CloudOutboxMessage has {unclassified.Length} property(ies) that are neither asserted to survive " +
			$"the round-trip nor recorded as justified-absent: {string.Join(", ", unclassified)}. " +
			"A property in neither set is one nothing checks is persisted or read back — the exact silent " +
			"drop this lock exists to prevent. Add it to RoundTrippedThroughSavePath (and assert it below), " +
			"or to JustifiedAbsentFromSavePath WITH the reason it is not drift.");

		// And the converse: a property removed from the contract must not leave a stale row behind claiming
		// coverage that no longer means anything.
		var stale = classified.Where(name => !declared.Contains(name, StringComparer.Ordinal)).ToArray();
		stale.ShouldBeEmpty(
			$"these names are classified here but no longer exist on CloudOutboxMessage: {string.Join(", ", stale)}");
	}

	/// <summary>
	/// LIVENESS — every persisted field survives write→read with a DISTINCT value, so a swap or a drop is
	/// detectable. Pre-fix for this class, a dropped field left the message deserializing cleanly with a null
	/// where a routing value belonged.
	/// </summary>
	[Fact]
	public void CarryEveryPersistedFieldThroughWriteThenRead()
	{
		var createdAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, 800, TimeSpan.Zero);
		var publishedAt = new DateTimeOffset(2027, 8, 9, 10, 11, 12, 130, TimeSpan.Zero);

		var original = new CloudOutboxMessage
		{
			MessageId = "sentinel-message-id",
			MessageType = "Sentinel.Message.Type",
			Payload = [0x01, 0x02, 0xFE, 0xFF],
			Headers = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["sentinel-header-one"] = "sentinel-value-one",
				["sentinel-header-two"] = "sentinel-value-two",
			},
			AggregateId = "sentinel-aggregate-id",
			AggregateType = "sentinel-aggregate-type",
			CorrelationId = "sentinel-correlation-id",
			CausationId = "sentinel-causation-id",
			TenantId = "sentinel-tenant-id",
			Destination = "sentinel-destination",
			CreatedAt = createdAt,
			PublishedAt = publishedAt,
			RetryCount = 7,
			LastError = "sentinel-last-error",
			PartitionKeyValue = "sentinel-partition-key",
		};

		var document = CosmosDbOutboxDocumentMap.ToDocument(
			original,
			new PartitionKey(original.PartitionKeyValue));

		var roundTripped = CosmosDbOutboxDocumentMap.FromDocument(document);

		// Asserted field by field rather than by record equality: a single ShouldBe on the whole record would
		// fail as one opaque diff, and the justified-absent fields would make it fail for the wrong reason.
		roundTripped.MessageId.ShouldBe(original.MessageId);
		roundTripped.MessageType.ShouldBe(original.MessageType);
		roundTripped.Payload.ShouldBe(original.Payload);
		roundTripped.AggregateId.ShouldBe(original.AggregateId);
		roundTripped.AggregateType.ShouldBe(original.AggregateType);
		roundTripped.CorrelationId.ShouldBe(original.CorrelationId);
		roundTripped.CausationId.ShouldBe(original.CausationId);
		roundTripped.TenantId.ShouldBe(original.TenantId);
		roundTripped.Destination.ShouldBe(original.Destination);
		roundTripped.CreatedAt.ShouldBe(original.CreatedAt);
		roundTripped.PublishedAt.ShouldBe(original.PublishedAt);
		roundTripped.RetryCount.ShouldBe(original.RetryCount);
		roundTripped.LastError.ShouldBe(original.LastError);
		roundTripped.PartitionKeyValue.ShouldBe(original.PartitionKeyValue);

		roundTripped.Headers.ShouldNotBeNull();
		roundTripped.Headers!.Count.ShouldBe(2);
		roundTripped.Headers["sentinel-header-one"].ShouldBe("sentinel-value-one");
		roundTripped.Headers["sentinel-header-two"].ShouldBe("sentinel-value-two");

		// IsPublished is derived, so it must follow PublishedAt rather than the document's stored copy.
		roundTripped.IsPublished.ShouldBeTrue();
	}

	/// <summary>
	/// LIVENESS — the timestamp fields survive as INSTANTS, not merely as non-null. A format that loses the
	/// offset or the sub-second component round-trips "successfully" while changing the value, which no
	/// null-check would catch.
	/// </summary>
	[Fact]
	public void PreserveTimestampsExactlyAndNotMerelyNonNull()
	{
		var createdAt = new DateTimeOffset(2026, 12, 31, 23, 59, 58, 765, TimeSpan.FromHours(5.5));

		var message = NewMinimalMessage() with { CreatedAt = createdAt, PublishedAt = null };

		var roundTripped = CosmosDbOutboxDocumentMap.FromDocument(
			CosmosDbOutboxDocumentMap.ToDocument(message, new PartitionKey(message.PartitionKeyValue)));

		// ToUniversalTime on both sides: the contract is the instant, not the textual offset it was written in.
		roundTripped.CreatedAt.ToUniversalTime().ShouldBe(createdAt.ToUniversalTime());
		roundTripped.CreatedAt.ToUnixTimeMilliseconds().ShouldBe(createdAt.ToUnixTimeMilliseconds());

		// An unpublished message must come back unpublished — not epoch, not DateTimeOffset.MinValue.
		roundTripped.PublishedAt.ShouldBeNull();
		roundTripped.IsPublished.ShouldBeFalse();
	}

	/// <summary>
	/// SAFETY — a null optional stays null. The inverse of a dropped field is a fabricated one: a mapper that
	/// substitutes empty string for null changes a "no destination" message into one routed to "".
	/// </summary>
	[Fact]
	public void KeepAbsentOptionalsAbsentRatherThanSubstitutingEmpty()
	{
		var roundTripped = CosmosDbOutboxDocumentMap.FromDocument(
			CosmosDbOutboxDocumentMap.ToDocument(
				NewMinimalMessage(),
				new PartitionKey("sentinel-partition-key")));

		roundTripped.Headers.ShouldBeNull();
		roundTripped.AggregateId.ShouldBeNull();
		roundTripped.AggregateType.ShouldBeNull();
		roundTripped.CorrelationId.ShouldBeNull();
		roundTripped.CausationId.ShouldBeNull();
		roundTripped.Destination.ShouldBeNull();
		roundTripped.PublishedAt.ShouldBeNull();
		roundTripped.LastError.ShouldBeNull();
	}

	/// <summary>
	/// SAFETY — the save path does not stamp a lease. Staging a message must not make it look claimed, or a
	/// processor reading it back would believe someone already owns it.
	/// </summary>
	[Fact]
	public void NotStampALeaseOnTheSavePath()
	{
		var roundTripped = CosmosDbOutboxDocumentMap.FromDocument(
			CosmosDbOutboxDocumentMap.ToDocument(
				NewMinimalMessage(),
				new PartitionKey("sentinel-partition-key")));

		roundTripped.LeasedAt.ShouldBeNull(
			"the save path stamped a lease; a staged message must not arrive looking claimed");
		roundTripped.LeasedBy.ShouldBeNull(
			"the save path stamped a lease holder; a staged message must not arrive looking claimed");
	}

	private static CloudOutboxMessage NewMinimalMessage() => new()
	{
		MessageId = "sentinel-message-id",
		MessageType = "Sentinel.Message.Type",
		Payload = [0x0A],
		CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
		PartitionKeyValue = "sentinel-partition-key",
	};
}
