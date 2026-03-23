// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests;

/// <summary>
/// Regression tests for <see cref="TransportContextFactory"/> (Sprint 670, T.1/T.2).
/// Verifies that CorrelationId and TenantId are preserved from message metadata
/// instead of generating new GUIDs -- the root cause of broken distributed tracing
/// across all 8 transport adapters.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "TransportContext")]
public sealed class TransportContextFactoryShould
{
	private readonly IServiceProvider _fakeServiceProvider = A.Fake<IServiceProvider>();

	#region CreateForReceive -- CorrelationId (T.1)

	[Fact]
	public void CreateForReceive_PreserveCorrelationId_WhenPresentInDomainEventMetadata()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object> { ["CorrelationId"] = "original-corr-123" };
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert -- CorrelationId preserved from metadata, NOT replaced with messageId
		context.CorrelationId.ShouldBe("original-corr-123");
		context.MessageId.ShouldBe(messageId);
	}

	[Fact]
	public void CreateForReceive_FallBackToMessageId_WhenNoCorrelationIdInMetadata()
	{
		// Arrange -- domain event with no CorrelationId in metadata
		var domainEvent = A.Fake<IDomainEvent>();
		A.CallTo(() => domainEvent.Metadata).Returns(new Dictionary<string, object>());
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert -- root message starts new correlation chain
		context.CorrelationId.ShouldBe(messageId);
	}

	[Fact]
	public void CreateForReceive_FallBackToMessageId_WhenMetadataIsNull()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		A.CallTo(() => domainEvent.Metadata).Returns(null);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.CorrelationId.ShouldBe(messageId);
	}

	[Fact]
	public void CreateForReceive_FallBackToMessageId_WhenCorrelationIdIsEmpty()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object> { ["CorrelationId"] = "" };
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.CorrelationId.ShouldBe(messageId);
	}

	[Fact]
	public void CreateForReceive_FallBackToMessageId_WhenMessageIsNotDomainEvent()
	{
		// Arrange -- plain IDispatchMessage, not IDomainEvent
		var message = A.Fake<IDispatchMessage>();
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(message, _fakeServiceProvider, messageId);

		// Assert
		context.CorrelationId.ShouldBe(messageId);
	}

	#endregion

	#region CreateForReceive -- TenantId (T.2)

	[Fact]
	public void CreateForReceive_PreserveTenantId_WhenPresentInDomainEventMetadata()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object> { ["TenantId"] = "tenant-abc" };
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.GetOrCreateIdentityFeature().TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void CreateForReceive_NotSetTenantId_WhenNotInMetadata()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		A.CallTo(() => domainEvent.Metadata).Returns(new Dictionary<string, object>());
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert -- TenantId should be null/empty when not in metadata
		context.GetOrCreateIdentityFeature().TenantId.ShouldBeNullOrEmpty();
	}

	[Fact]
	public void CreateForReceive_PreserveBothCorrelationIdAndTenantId()
	{
		// Arrange -- both fields present
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object>
		{
			["CorrelationId"] = "corr-xyz",
			["TenantId"] = "tenant-456"
		};
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert -- both preserved
		context.CorrelationId.ShouldBe("corr-xyz");
		context.GetOrCreateIdentityFeature().TenantId.ShouldBe("tenant-456");
	}

	#endregion

	#region CreateForSend -- CorrelationId and TenantId

	[Fact]
	public void CreateForSend_PreserveCorrelationId_WhenPresentInDomainEventMetadata()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object> { ["CorrelationId"] = "send-corr-789" };
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForSend(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.CorrelationId.ShouldBe("send-corr-789");
	}

	[Fact]
	public void CreateForSend_PreserveTenantId_WhenPresentInDomainEventMetadata()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		var metadata = new Dictionary<string, object> { ["TenantId"] = "send-tenant-abc" };
		A.CallTo(() => domainEvent.Metadata).Returns(metadata);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForSend(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.GetOrCreateIdentityFeature().TenantId.ShouldBe("send-tenant-abc");
	}

	[Fact]
	public void CreateForSend_FallBackToMessageId_WhenNoMetadata()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForSend(message, _fakeServiceProvider, messageId);

		// Assert
		context.CorrelationId.ShouldBe(messageId);
	}

	#endregion

	#region CreateForReceive -- General Context Properties

	[Fact]
	public void CreateForReceive_SetMessageType_FromMessageRuntimeType()
	{
		// Arrange
		var domainEvent = A.Fake<IDomainEvent>();
		A.CallTo(() => domainEvent.Metadata).Returns(null);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		context.GetMessageType().ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CreateForReceive_SetReceivedTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var domainEvent = A.Fake<IDomainEvent>();
		A.CallTo(() => domainEvent.Metadata).Returns(null);
		var messageId = Guid.NewGuid().ToString();

		// Act
		var context = TransportContextFactory.CreateForReceive(domainEvent, _fakeServiceProvider, messageId);

		// Assert
		var after = DateTimeOffset.UtcNow;
		var receivedTs = context.GetReceivedTimestampUtc();
		receivedTs.ShouldNotBeNull();
		receivedTs.Value.ShouldBeGreaterThanOrEqualTo(before);
		receivedTs.Value.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion
}
