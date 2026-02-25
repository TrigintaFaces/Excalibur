// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messages;

namespace Excalibur.Dispatch.Tests.Messaging.Messages;

/// <summary>
/// Unit tests for <see cref="GenericDispatchMessage"/>.
/// </summary>
/// <remarks>
/// Tests the generic dispatch message implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messages")]
[Trait("Priority", "0")]
public sealed class GenericDispatchMessageShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("TestMessage", "payload content");

		// Assert
		_ = message.ShouldNotBeNull();
		message.MessageType.ShouldBe("TestMessage");
		message.Payload.ShouldBe("payload content");
		message.Body.ShouldBe("payload content");
		message.MessageId.ShouldNotBeNullOrEmpty();
		_ = message.Headers.ShouldNotBeNull();
		_ = message.Features.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_GeneratesUniqueMessageId()
	{
		// Arrange & Act
		var message1 = new GenericDispatchMessage("Type1", "payload1");
		var message2 = new GenericDispatchMessage("Type2", "payload2");

		// Assert
		message1.MessageId.ShouldNotBe(message2.MessageId);
	}

	[Fact]
	public void Constructor_SetsTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var message = new GenericDispatchMessage("Test", "payload");

		// Assert
		var after = DateTimeOffset.UtcNow;
		message.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		message.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_WithNullMessageType_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new GenericDispatchMessage(null!, "payload"));
	}

	[Fact]
	public void Constructor_WithNullPayload_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new GenericDispatchMessage("Type", null!));
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_IsValidGuidFormat()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("Test", "payload");

		// Assert
		Guid.TryParse(message.MessageId, out _).ShouldBeTrue();
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_ReturnsGuidFromMessageId()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");
		var expectedGuid = Guid.Parse(message.MessageId);

		// Act
		var id = message.Id;

		// Assert
		id.ShouldBe(expectedGuid);
	}

	#endregion

	#region Kind Property Tests

	[Fact]
	public void Kind_ReturnsAction()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("Test", "payload");

		// Assert
		message.Kind.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region CorrelationId Property Tests

	[Fact]
	public void CorrelationId_DefaultsToNull()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("Test", "payload");

		// Assert
		message.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");

		// Act
		message.CorrelationId = "corr-12345";

		// Assert
		message.CorrelationId.ShouldBe("corr-12345");
	}

	[Fact]
	public void CorrelationId_CanBeCleared()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");
		message.CorrelationId = "corr-12345";

		// Act
		message.CorrelationId = null;

		// Assert
		message.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_AddsToHeaders()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");

		// Act
		message.CorrelationId = "corr-12345";

		// Assert
		message.Headers.ShouldContainKey("CorrelationId");
		message.Headers["CorrelationId"].ShouldBe("corr-12345");
	}

	[Fact]
	public void CorrelationId_RemovesFromHeadersWhenCleared()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");
		message.CorrelationId = "corr-12345";

		// Act
		message.CorrelationId = null;

		// Assert
		message.Headers.ShouldNotContainKey("CorrelationId");
	}

	#endregion

	#region AddHeader Tests

	[Fact]
	public void AddHeader_AddsHeaderToMessage()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");

		// Act
		message.AddHeader("CustomHeader", "CustomValue");

		// Assert
		message.Headers.ShouldContainKey("CustomHeader");
		message.Headers["CustomHeader"].ShouldBe("CustomValue");
	}

	[Fact]
	public void AddHeader_CanAddMultipleHeaders()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");

		// Act
		message.AddHeader("Header1", "Value1");
		message.AddHeader("Header2", "Value2");
		message.AddHeader("Header3", 123);

		// Assert
		message.Headers.Count.ShouldBe(3);
		message.Headers["Header1"].ShouldBe("Value1");
		message.Headers["Header2"].ShouldBe("Value2");
		message.Headers["Header3"].ShouldBe(123);
	}

	[Fact]
	public void AddHeader_OverwritesExistingHeader()
	{
		// Arrange
		var message = new GenericDispatchMessage("Test", "payload");
		message.AddHeader("Key", "Original");

		// Act
		message.AddHeader("Key", "Updated");

		// Assert
		message.Headers["Key"].ShouldBe("Updated");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var message = new GenericDispatchMessage("OrderCreated", "order-payload");

		// Act
		var result = message.ToString();

		// Assert
		result.ShouldContain("GenericDispatchMessage");
		result.ShouldContain("OrderCreated");
		result.ShouldContain(message.MessageId);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDispatchMessage()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("Test", "payload");

		// Assert
		_ = message.ShouldBeAssignableTo<IDispatchMessage>();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void JsonPayload_Scenario()
	{
		// Arrange & Act
		var jsonPayload = "{\"orderId\":\"12345\",\"amount\":99.99}";
		var message = new GenericDispatchMessage("OrderCreated", jsonPayload);

		// Assert
		message.Payload.ShouldContain("orderId");
		message.Payload.ShouldContain("12345");
	}

	[Fact]
	public void MessageWithCorrelationAndHeaders_Scenario()
	{
		// Arrange & Act
		var message = new GenericDispatchMessage("ProcessPayment", "payment-data");
		message.CorrelationId = "txn-abc-123";
		message.AddHeader("TenantId", "acme-corp");
		message.AddHeader("UserId", "user-456");

		// Assert
		message.CorrelationId.ShouldBe("txn-abc-123");
		message.Headers.Count.ShouldBe(3); // CorrelationId + TenantId + UserId
	}

	#endregion
}
