// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Locks the Cosmos DB inbox document's conversion back to an <see cref="InboxEntry"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>handler_type</c> field is the container's partition key, so the store overwrites it with the
/// shared partition value whenever one is configured — and a shared partition key is exactly what gates
/// the transactional-batch capability. The logical handler type therefore lives in its own field, and the
/// conversion reads that field rather than parsing it out of the composite id: the id's tenant and
/// message-id terms may each contain a colon, so no length- or split-based rule can recover its segments.
/// </para>
/// <para>
/// The document type is internal, so it is exercised through reflection rather than by widening
/// production visibility.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait(TraitNames.Feature, TestFeatures.Inbox)]
public sealed class CosmosDbInboxDocumentRoundTripShould
{
	/// <summary>The value the store stamps on <c>handler_type</c> when a shared partition key is configured.</summary>
	private const string SharedPartitionValue = "inbox-shared-partition";

	private const string RealHandlerType = "Orders.OrderPlacedHandler";

	private static readonly Type DocumentType =
		typeof(CosmosDbInboxOptions).Assembly.GetType("Excalibur.Inbox.CosmosDb.CosmosDbInboxDocument")!;

	[Fact]
	public void ReturnTheRealHandlerType_WhenTenantScopedAndThePartitionKeyIsShared()
	{
		// The store writes this shape whenever SharedPartitionKey is set: a three-segment id, and a
		// handler_type field carrying the shared partition value instead of the handler.
		var document = NewDocument(
			("Id", CreateId("msg-123", RealHandlerType, "tenant-a")),
			("MessageId", "msg-123"),
			("TenantId", "tenant-a"),
			("HandlerType", SharedPartitionValue),
			("LogicalHandlerType", RealHandlerType));

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
		entry.HandlerType.ShouldNotBe(
			SharedPartitionValue,
			"the partition value is placement, not identity — returning it mislabels every entry read back " +
			"in the one mode that supports transactional processing");
	}

	[Fact]
	public void ReturnTheRealHandlerType_WhenTheEntryIsUntenanted()
	{
		// No ambient tenant still yields a three-segment id: the keyed partition seam binds the reserved
		// sentinel rather than omitting the term, so there is no two-segment case left in production.
		var document = NewDocument(
			("Id", CreateId("msg-123", RealHandlerType, TenantScope.UntenantedSentinel)),
			("MessageId", "msg-123"),
			("TenantId", TenantScope.UntenantedSentinel),
			("HandlerType", SharedPartitionValue),
			("LogicalHandlerType", RealHandlerType));

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
	}

	[Fact]
	public void ReturnTheRealHandlerType_WhenTheTenantTermIsAsLongAsTheMessageId()
	{
		// "tenant-a" and "msg-1234" are both 8 characters, so the character at the message id's length is
		// the separator after the TENANT term. A rule keyed on that position reads the id as two segments
		// and slices from the wrong offset, yielding "msg-1234:Orders.OrderPlacedHandler".
		var document = NewDocument(
			("Id", CreateId("msg-1234", RealHandlerType, "tenant-a")),
			("MessageId", "msg-1234"),
			("TenantId", "tenant-a"),
			("HandlerType", SharedPartitionValue),
			("LogicalHandlerType", RealHandlerType));

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
	}

	[Fact]
	public void ReturnTheRealHandlerType_WhenTheTenantAndMessageIdThemselvesContainColons()
	{
		// The reason the id is never parsed: its terms are not colon-free, so the segment boundaries are
		// not a function of the id at all. No length rule and no split rule recovers the handler type here.
		var document = NewDocument(
			("Id", CreateId("urn:msg:123", RealHandlerType, "region:eu:tenant-a")),
			("MessageId", "urn:msg:123"),
			("TenantId", "region:eu:tenant-a"),
			("HandlerType", SharedPartitionValue),
			("LogicalHandlerType", RealHandlerType));

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
	}

	[Fact]
	public void FallBackToThePartitionKeyField_WhenTheDocumentPredatesTheLogicalHandlerTypeField()
	{
		// A document written before the field existed deserializes with it absent. Without a shared
		// partition key, handler_type holds the true handler type, so the fallback is exactly right — and
		// that is the only mode in which a legacy document retained the value anywhere at all.
		var document = NewDocument(
			("Id", CreateId("msg-123", RealHandlerType, "tenant-a")),
			("MessageId", "msg-123"),
			("TenantId", "tenant-a"),
			("HandlerType", RealHandlerType),
			("LogicalHandlerType", null));

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
	}

	[Fact]
	public void RoundTripEveryFieldOfANormalEntry()
	{
		// LIVENESS: a conversion that returned nothing useful would satisfy every arm above, since each
		// only asserts that one field is not one particular wrong value. This arm asserts the entry is whole.
		var receivedAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
		var lastAttemptAt = receivedAt.AddMinutes(1);
		var processedAt = receivedAt.AddMinutes(2);
		byte[] payload = [1, 2, 250, 0, 99];
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["source"] = "orders-queue",
			["attempt"] = 3
		};

		var document = NewDocument(
			("Id", CreateId("msg-123", RealHandlerType, "tenant-a")),
			("MessageId", "msg-123"),
			("TenantId", "tenant-a"),
			("HandlerType", SharedPartitionValue),
			("LogicalHandlerType", RealHandlerType),
			("MessageType", "Orders.OrderPlaced"),
			("Payload", Convert.ToBase64String(payload)),
			("Metadata", metadata),
			("Status", (int)InboxStatus.Failed),
			("ReceivedAt", receivedAt),
			("LastAttemptAt", lastAttemptAt),
			("ProcessedAt", processedAt),
			("RetryCount", 4),
			("LastError", "handler threw"));

		var entry = ToEntry(document);

		entry.MessageId.ShouldBe("msg-123");
		entry.HandlerType.ShouldBe(RealHandlerType);
		entry.MessageType.ShouldBe("Orders.OrderPlaced");
		entry.Payload.ShouldBe(payload);
		entry.Metadata.ShouldBe(metadata);
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.ReceivedAt.ShouldBe(receivedAt);
		entry.LastAttemptAt.ShouldBe(lastAttemptAt);
		entry.ProcessedAt.ShouldBe(processedAt);
		entry.RetryCount.ShouldBe(4);
		entry.LastError.ShouldBe("handler threw");
	}

	[Fact]
	public void RoundTripAnEntryThroughTheDocumentFactory()
	{
		// The factory is the other producer of documents, so it must populate the logical handler type too.
		var original = new InboxEntry("msg-123", RealHandlerType, "Orders.OrderPlaced", [7, 8, 9]);
		var document = DocumentType
			.GetMethod("FromInboxEntry", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [original])!;

		var entry = ToEntry(document);

		entry.HandlerType.ShouldBe(RealHandlerType);
		entry.MessageId.ShouldBe("msg-123");
		entry.MessageType.ShouldBe("Orders.OrderPlaced");
		entry.Payload.ShouldBe(new byte[] { 7, 8, 9 });
	}

	private static string CreateId(string messageId, string handlerType, string tenantId) =>
		(string)DocumentType
			.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [messageId, handlerType, tenantId])!;

	private static object NewDocument(params (string Name, object? Value)[] properties)
	{
		var document = Activator.CreateInstance(DocumentType)!;

		foreach (var (name, value) in properties)
		{
			var property = DocumentType.GetProperty(name)
				?? throw new InvalidOperationException($"CosmosDbInboxDocument has no '{name}' property.");

			property.SetValue(document, value);
		}

		return document;
	}

	private static InboxEntry ToEntry(object document) =>
		(InboxEntry)DocumentType
			.GetMethod("ToInboxEntry", BindingFlags.Public | BindingFlags.Instance)!
			.Invoke(document, null)!;
}
