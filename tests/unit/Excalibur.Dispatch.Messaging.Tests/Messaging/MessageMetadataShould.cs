// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageMetadata"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageMetadataShould
{
	[Fact]
	public void StoreMessageIdProperty()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();

		// Act
		var metadata = CreateTestMetadata(messageId: messageId);

		// Assert
		metadata.MessageId.ShouldBe(messageId);
	}

	[Fact]
	public void StoreCorrelationIdProperty()
	{
		// Arrange
		var correlationId = Guid.NewGuid().ToString();

		// Act
		var metadata = CreateTestMetadata(correlationId: correlationId);

		// Assert
		metadata.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void StoreCausationIdProperty()
	{
		// Arrange
		var causationId = Guid.NewGuid().ToString();

		// Act
		var metadata = CreateTestMetadata(causationId: causationId);

		// Assert
		metadata.CausationId.ShouldBe(causationId);
	}

	[Fact]
	public void StoreTraceParentProperty()
	{
		// Arrange
		var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

		// Act
		var metadata = CreateTestMetadata(traceParent: traceParent);

		// Assert
		metadata.TraceParent.ShouldBe(traceParent);
	}

	[Fact]
	public void StoreTenantIdProperty()
	{
		// Arrange
		var tenantId = "tenant-123";

		// Act
		var metadata = CreateTestMetadata(tenantId: tenantId);

		// Assert
		metadata.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void StoreUserIdProperty()
	{
		// Arrange
		var userId = "user-456";

		// Act
		var metadata = CreateTestMetadata(userId: userId);

		// Assert
		metadata.UserId.ShouldBe(userId);
	}

	[Fact]
	public void StoreContentTypeProperty()
	{
		// Arrange
		var contentType = "application/json";

		// Act
		var metadata = CreateTestMetadata(contentType: contentType);

		// Assert
		metadata.ContentType.ShouldBe(contentType);
	}

	[Fact]
	public void StoreSerializerVersionProperty()
	{
		// Arrange
		var serializerVersion = "2.0.0";

		// Act
		var metadata = CreateTestMetadata(serializerVersion: serializerVersion);

		// Assert
		metadata.SerializerVersion.ShouldBe(serializerVersion);
	}

	[Fact]
	public void StoreMessageVersionProperty()
	{
		// Arrange
		var messageVersion = "3.0.0";

		// Act
		var metadata = CreateTestMetadata(messageVersion: messageVersion);

		// Assert
		metadata.MessageVersion.ShouldBe(messageVersion);
	}

	[Fact]
	public void StoreContractVersionProperty()
	{
		// Arrange
		var contractVersion = "2.1.0";

		// Act
		var metadata = CreateTestMetadata(contractVersion: contractVersion);

		// Assert
		metadata.ContractVersion.ShouldBe(contractVersion);
	}

	[Fact]
	public void HaveDefaultContractVersionOfOneZeroZero()
	{
		// Arrange & Act
		var metadata = new MessageMetadata(
			MessageId: "msg-1",
			CorrelationId: "corr-1",
			CausationId: null,
			TraceParent: null,
			TenantId: null,
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0");

		// Assert
		metadata.ContractVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void AllowNullCausationId()
	{
		// Arrange & Act
		var metadata = CreateTestMetadata(causationId: null);

		// Assert
		metadata.CausationId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullTraceParent()
	{
		// Arrange & Act
		var metadata = CreateTestMetadata(traceParent: null);

		// Assert
		metadata.TraceParent.ShouldBeNull();
	}

	[Fact]
	public void AllowNullTenantId()
	{
		// Arrange & Act
		var metadata = CreateTestMetadata(tenantId: null);

		// Assert
		metadata.TenantId.ShouldBeNull();
	}

	[Fact]
	public void AllowNullUserId()
	{
		// Arrange & Act
		var metadata = CreateTestMetadata(userId: null);

		// Assert
		metadata.UserId.ShouldBeNull();
	}

	[Fact]
	public void ImplementITransportMessageMetadata()
	{
		// Arrange & Act
		var metadata = CreateTestMetadata();

		// Assert
		metadata.ShouldBeAssignableTo<ITransportMessageMetadata>();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var metadata1 = new MessageMetadata(
			MessageId: "msg-1",
			CorrelationId: "corr-1",
			CausationId: "caus-1",
			TraceParent: "trace-1",
			TenantId: "tenant-1",
			UserId: "user-1",
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0",
			ContractVersion: "1.0.0");

		var metadata2 = new MessageMetadata(
			MessageId: "msg-1",
			CorrelationId: "corr-1",
			CausationId: "caus-1",
			TraceParent: "trace-1",
			TenantId: "tenant-1",
			UserId: "user-1",
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0",
			ContractVersion: "1.0.0");

		// Assert
		metadata1.ShouldBe(metadata2);
		metadata1.GetHashCode().ShouldBe(metadata2.GetHashCode());
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var metadata1 = CreateTestMetadata(messageId: "msg-1");
		var metadata2 = CreateTestMetadata(messageId: "msg-2");

		// Assert
		metadata1.ShouldNotBe(metadata2);
	}

	[Fact]
	public void SupportRecordWithExpression()
	{
		// Arrange
		var original = CreateTestMetadata(
			messageId: "original-id",
			tenantId: "tenant-1");

		// Act
		var modified = original with { TenantId = "tenant-2" };

		// Assert
		modified.MessageId.ShouldBe("original-id");
		modified.TenantId.ShouldBe("tenant-2");
		original.TenantId.ShouldBe("tenant-1"); // Original unchanged
	}

	[Fact]
	public void FromContextThrowsOnNullContext()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			MessageMetadata.FromContext(null!));
	}

	[Fact]
	public void FromContextCreatesMetadataFromContext()
	{
		// Arrange - use internal key names from MessageContextExtensions
		var properties = new Dictionary<string, object?>
		{
			["__SerializerVersion"] = "2.0.0",
			["__MessageVersion"] = "3.0.0",
			["__ContractVersion"] = "4.0.0",
		};

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-123");
		A.CallTo(() => context.CorrelationId).Returns("corr-456");
		A.CallTo(() => context.CausationId).Returns("caus-789");
		A.CallTo(() => context.TraceParent).Returns("trace-abc");
		A.CallTo(() => context.TenantId).Returns("tenant-xyz");
		A.CallTo(() => context.UserId).Returns("user-000");
		A.CallTo(() => context.ContentType).Returns("application/xml");
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var metadata = MessageMetadata.FromContext(context);

		// Assert
		metadata.MessageId.ShouldBe("msg-123");
		metadata.CorrelationId.ShouldBe("corr-456");
		metadata.CausationId.ShouldBe("caus-789");
		metadata.TraceParent.ShouldBe("trace-abc");
		metadata.TenantId.ShouldBe("tenant-xyz");
		metadata.UserId.ShouldBe("user-000");
		metadata.ContentType.ShouldBe("application/xml");
		metadata.SerializerVersion.ShouldBe("2.0.0");
		metadata.MessageVersion.ShouldBe("3.0.0");
		metadata.ContractVersion.ShouldBe("4.0.0");
	}

	[Fact]
	public void FromContextUsesDefaultsForNullValues()
	{
		// Arrange
		var properties = new Dictionary<string, object?>(); // Empty - no version keys set

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns((string?)null);
		A.CallTo(() => context.CorrelationId).Returns((string?)null);
		A.CallTo(() => context.CausationId).Returns((string?)null);
		A.CallTo(() => context.TraceParent).Returns((string?)null);
		A.CallTo(() => context.TenantId).Returns((string?)null);
		A.CallTo(() => context.UserId).Returns((string?)null);
		A.CallTo(() => context.ContentType).Returns((string?)null);
		A.CallTo(() => context.Properties).Returns(properties);

		// Act
		var metadata = MessageMetadata.FromContext(context);

		// Assert
		metadata.MessageId.ShouldNotBeNullOrEmpty(); // Generated GUID
		metadata.CorrelationId.ShouldNotBeNullOrEmpty(); // Generated GUID
		metadata.ContentType.ShouldBe("application/json");
		metadata.SerializerVersion.ShouldBe("1.0.0");
		metadata.MessageVersion.ShouldBe("1.0.0");
		metadata.ContractVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void SimulateTypicalWebApiScenario()
	{
		// Arrange & Act - Typical metadata for a web API request
		var metadata = new MessageMetadata(
			MessageId: Guid.NewGuid().ToString(),
			CorrelationId: "request-trace-123",
			CausationId: null,
			TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
			TenantId: "acme-corp",
			UserId: "admin@acme.com",
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "1.0.0",
			ContractVersion: "2.0.0");

		// Assert
		metadata.TenantId.ShouldBe("acme-corp");
		metadata.UserId.ShouldBe("admin@acme.com");
		metadata.TraceParent.ShouldStartWith("00-");
	}

	[Fact]
	public void SimulateTypicalEventSourcingScenario()
	{
		// Arrange & Act - Event sourcing with causation chain
		var commandId = Guid.NewGuid().ToString();
		var eventId = Guid.NewGuid().ToString();

		var metadata = new MessageMetadata(
			MessageId: eventId,
			CorrelationId: "order-flow-456",
			CausationId: commandId,
			TraceParent: null,
			TenantId: "tenant-1",
			UserId: null,
			ContentType: "application/json",
			SerializerVersion: "1.0.0",
			MessageVersion: "2.0.0",
			ContractVersion: "1.0.0");

		// Assert
		metadata.MessageId.ShouldBe(eventId);
		metadata.CausationId.ShouldBe(commandId);
		metadata.CorrelationId.ShouldBe("order-flow-456");
	}

	private static MessageMetadata CreateTestMetadata(
		string messageId = "msg-default",
		string correlationId = "corr-default",
		string? causationId = "caus-default",
		string? traceParent = "trace-default",
		string? tenantId = "tenant-default",
		string? userId = "user-default",
		string contentType = "application/json",
		string serializerVersion = "1.0.0",
		string messageVersion = "1.0.0",
		string contractVersion = "1.0.0")
	{
		return new MessageMetadata(
			MessageId: messageId,
			CorrelationId: correlationId,
			CausationId: causationId,
			TraceParent: traceParent,
			TenantId: tenantId,
			UserId: userId,
			ContentType: contentType,
			SerializerVersion: serializerVersion,
			MessageVersion: messageVersion,
			ContractVersion: contractVersion);
	}
}
