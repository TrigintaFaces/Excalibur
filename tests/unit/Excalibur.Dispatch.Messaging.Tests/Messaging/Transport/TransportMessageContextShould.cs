// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportMessageContext"/>.
/// </summary>
/// <remarks>
/// Tests the base transport message context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportMessageContextShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithMessageId_SetsMessageId()
	{
		// Arrange
		var messageId = "test-message-id";

		// Act
		var context = new TransportMessageContext(messageId);

		// Assert
		context.MessageId.ShouldBe(messageId);
	}

	[Fact]
	public void Constructor_WithMessageId_SetsTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var context = new TransportMessageContext("test-id");
		var after = DateTimeOffset.UtcNow;

		// Assert
		context.Timestamp.ShouldBeInRange(before, after);
	}

	[Fact]
	public void Constructor_Default_GeneratesGuidMessageId()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.MessageId.ShouldNotBeNullOrWhiteSpace();
		context.MessageId.Length.ShouldBe(32); // GUID without hyphens (N format)
	}

	[Fact]
	public void Constructor_Default_GeneratesUniqueMessageIds()
	{
		// Arrange & Act
		var context1 = new TransportMessageContext();
		var context2 = new TransportMessageContext();

		// Assert
		context1.MessageId.ShouldNotBe(context2.MessageId);
	}

	[Fact]
	public void Constructor_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TransportMessageContext(null!));
	}

	[Fact]
	public void Constructor_WithEmptyMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TransportMessageContext(string.Empty));
	}

	[Fact]
	public void Constructor_WithWhitespaceMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new TransportMessageContext("   "));
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void Default_HasNullCorrelationId()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullCausationId()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.CausationId.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullSourceTransport()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.SourceTransport.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullTargetTransport()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.TargetTransport.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullContentType()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.ContentType.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyHeaders()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		context.Headers.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.CorrelationId = "correlation-123";

		// Assert
		context.CorrelationId.ShouldBe("correlation-123");
	}

	[Fact]
	public void CausationId_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.CausationId = "causation-456";

		// Assert
		context.CausationId.ShouldBe("causation-456");
	}

	[Fact]
	public void SourceTransport_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.SourceTransport = "rabbitmq";

		// Assert
		context.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void TargetTransport_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.TargetTransport = "kafka";

		// Assert
		context.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void ContentType_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.ContentType = "application/json";

		// Assert
		context.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var context = new TransportMessageContext();
		var timestamp = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		context.Timestamp = timestamp;

		// Assert
		context.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Header Tests

	[Fact]
	public void SetHeader_AddsHeader()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.SetHeader("x-custom", "value");

		// Assert
		context.Headers.ShouldContainKey("x-custom");
		context.Headers["x-custom"].ShouldBe("value");
	}

	[Fact]
	public void SetHeader_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("key", "value1");

		// Act
		context.SetHeader("key", "value2");

		// Assert
		context.Headers["key"].ShouldBe("value2");
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_IsCaseInsensitive()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("X-Header", "value1");

		// Act
		context.SetHeader("x-header", "value2");

		// Assert
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(null!, "value"));
	}

	[Fact]
	public void SetHeader_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(string.Empty, "value"));
	}

	[Fact]
	public void SetHeader_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader("   ", "value"));
	}

	[Fact]
	public void SetHeaders_AddsMultipleHeaders()
	{
		// Arrange
		var context = new TransportMessageContext();
		var headers = new Dictionary<string, string>
		{
			["header1"] = "value1",
			["header2"] = "value2",
			["header3"] = "value3",
		};

		// Act
		context.SetHeaders(headers);

		// Assert
		context.Headers.Count.ShouldBe(3);
		context.Headers["header1"].ShouldBe("value1");
		context.Headers["header2"].ShouldBe("value2");
		context.Headers["header3"].ShouldBe("value3");
	}

	[Fact]
	public void SetHeaders_MergesWithExisting()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("existing", "existing-value");
		var headers = new Dictionary<string, string>
		{
			["new"] = "new-value",
		};

		// Act
		context.SetHeaders(headers);

		// Assert
		context.Headers.Count.ShouldBe(2);
		context.Headers["existing"].ShouldBe("existing-value");
		context.Headers["new"].ShouldBe("new-value");
	}

	[Fact]
	public void SetHeaders_OverwritesExisting()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("shared", "original");
		var headers = new Dictionary<string, string>
		{
			["shared"] = "updated",
		};

		// Act
		context.SetHeaders(headers);

		// Assert
		context.Headers["shared"].ShouldBe("updated");
	}

	[Fact]
	public void SetHeaders_WithNull_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.SetHeaders(null!));
	}

	[Fact]
	public void SetHeaders_WithEmpty_DoesNothing()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("existing", "value");

		// Act
		context.SetHeaders([]);

		// Assert
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void RemoveHeader_RemovesExistingHeader()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("to-remove", "value");

		// Act
		var result = context.RemoveHeader("to-remove");

		// Assert
		result.ShouldBeTrue();
		context.Headers.ShouldNotContainKey("to-remove");
	}

	[Fact]
	public void RemoveHeader_ReturnsFalseForNonExisting()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		var result = context.RemoveHeader("non-existing");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void RemoveHeader_IsCaseInsensitive()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetHeader("X-Header", "value");

		// Act
		var result = context.RemoveHeader("x-header");

		// Assert
		result.ShouldBeTrue();
		context.Headers.ShouldBeEmpty();
	}

	[Fact]
	public void RemoveHeader_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.RemoveHeader(null!));
	}

	[Fact]
	public void RemoveHeader_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.RemoveHeader(string.Empty));
	}

	[Fact]
	public void RemoveHeader_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.RemoveHeader("   "));
	}

	#endregion

	#region Transport Property Tests

	[Fact]
	public void SetTransportProperty_SetsValue()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.SetTransportProperty("prop", "value");

		// Assert
		context.GetTransportProperty<string>("prop").ShouldBe("value");
	}

	[Fact]
	public void SetTransportProperty_WithIntValue()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.SetTransportProperty("count", 42);

		// Assert
		context.GetTransportProperty<int>("count").ShouldBe(42);
	}

	[Fact]
	public void SetTransportProperty_WithBoolValue()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		context.SetTransportProperty("enabled", true);

		// Assert
		context.GetTransportProperty<bool>("enabled").ShouldBeTrue();
	}

	[Fact]
	public void SetTransportProperty_OverwritesExisting()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("prop", "original");

		// Act
		context.SetTransportProperty("prop", "updated");

		// Assert
		context.GetTransportProperty<string>("prop").ShouldBe("updated");
	}

	[Fact]
	public void SetTransportProperty_IsCaseInsensitive()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("Prop", "value1");

		// Act
		context.SetTransportProperty("prop", "value2");

		// Assert
		context.GetTransportProperty<string>("PROP").ShouldBe("value2");
	}

	[Fact]
	public void SetTransportProperty_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetTransportProperty(null!, "value"));
	}

	[Fact]
	public void SetTransportProperty_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetTransportProperty(string.Empty, "value"));
	}

	[Fact]
	public void SetTransportProperty_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetTransportProperty("   ", "value"));
	}

	[Fact]
	public void GetTransportProperty_ReturnsDefaultForNonExisting()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		var result = context.GetTransportProperty<string>("non-existing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTransportProperty_ReturnsDefaultForWrongType()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("prop", "string-value");

		// Act - Try to get as int
		var result = context.GetTransportProperty<int>("prop");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void GetTransportProperty_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.GetTransportProperty<string>(null!));
	}

	[Fact]
	public void GetTransportProperty_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.GetTransportProperty<string>(string.Empty));
	}

	[Fact]
	public void GetTransportProperty_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.GetTransportProperty<string>("   "));
	}

	[Fact]
	public void HasTransportProperty_ReturnsTrueForExisting()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("existing", "value");

		// Act
		var result = context.HasTransportProperty("existing");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasTransportProperty_ReturnsFalseForNonExisting()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		var result = context.HasTransportProperty("non-existing");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasTransportProperty_IsCaseInsensitive()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("Prop", "value");

		// Act
		var result = context.HasTransportProperty("prop");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasTransportProperty_WithNullName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.HasTransportProperty(null!));
	}

	[Fact]
	public void HasTransportProperty_WithEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.HasTransportProperty(string.Empty));
	}

	[Fact]
	public void HasTransportProperty_WithWhitespaceName_ThrowsArgumentException()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.HasTransportProperty("   "));
	}

	[Fact]
	public void GetAllTransportProperties_ReturnsAllProperties()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("prop1", "value1");
		context.SetTransportProperty("prop2", 42);
		context.SetTransportProperty("prop3", true);

		// Act
		var result = context.GetAllTransportProperties();

		// Assert
		result.Count.ShouldBe(3);
		result["prop1"].ShouldBe("value1");
		result["prop2"].ShouldBe(42);
		result["prop3"].ShouldBe(true);
	}

	[Fact]
	public void GetAllTransportProperties_ReturnsEmptyWhenNoProperties()
	{
		// Arrange
		var context = new TransportMessageContext();

		// Act
		var result = context.GetAllTransportProperties();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllTransportProperties_ReturnsCopy()
	{
		// Arrange
		var context = new TransportMessageContext();
		context.SetTransportProperty("prop", "original");

		// Act
		var result = context.GetAllTransportProperties();
		context.SetTransportProperty("prop", "updated");

		// Assert - Result should still have original value
		result["prop"].ShouldBe("original");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsITransportMessageContext()
	{
		// Arrange & Act
		var context = new TransportMessageContext();

		// Assert
		_ = context.ShouldBeAssignableTo<ITransportMessageContext>();
	}

	#endregion
}
