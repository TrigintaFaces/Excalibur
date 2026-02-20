namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for MessageEnvelope functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageEnvelopeShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaultConstructor_GeneratesMessageIdAndTimestamp()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
		envelope.ReceivedTimestampUtc.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void SetItem_WithKey_StoresValue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.SetItem("testKey", "testValue");

		// Assert
		envelope.GetItem<string>("testKey").ShouldBe("testValue");
	}

	[Fact]
	public void GetItem_WithNonExistentKey_ReturnsDefault()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		var result = envelope.GetItem<string>("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetItem_WithDefaultValue_ReturnsDefaultWhenKeyNotFound()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		var result = envelope.GetItem("nonexistent", "defaultValue");

		// Assert
		result.ShouldBe("defaultValue");
	}

	[Fact]
	public void ContainsItem_WhenKeyExists_ReturnsTrue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SetItem("existingKey", 42);

		// Act
		var result = envelope.ContainsItem("existingKey");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsItem_WhenKeyDoesNotExist_ReturnsFalse()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		var result = envelope.ContainsItem("nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void RemoveItem_WhenKeyExists_RemovesItem()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SetItem("keyToRemove", "value");

		// Act
		envelope.RemoveItem("keyToRemove");

		// Assert
		envelope.ContainsItem("keyToRemove").ShouldBeFalse();
	}

	[Fact]
	public void Headers_CanAddAndRetrieveHeaders()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.Headers["X-Custom-Header"] = "CustomValue";

		// Assert
		envelope.Headers["X-Custom-Header"].ShouldBe("CustomValue");
	}

	[Fact]
	public void Properties_ExposesItems()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SetItem("propertyKey", "propertyValue");

		// Act & Assert
		envelope.Properties.ShouldContainKey("propertyKey");
	}

	[Fact]
	public void CorrelationId_CanBeSetAndRetrieved()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var correlationId = Guid.NewGuid().ToString();

		// Act
		envelope.CorrelationId = correlationId;

		// Assert
		envelope.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void TenantId_CanBeSetAndRetrieved()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.TenantId = "tenant-123";

		// Assert
		envelope.TenantId.ShouldBe("tenant-123");
	}

	[Fact]
	public void DeliveryCount_CanBeIncremented()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.DeliveryCount = 5;

		// Assert
		envelope.DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act & Assert - should not throw
		envelope.Dispose();
		envelope.Dispose();
	}

	[Fact]
	public void SetItem_WithNullValue_RemovesItem()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SetItem("key", "value");

		// Act
		envelope.SetItem<string?>("key", null);

		// Assert
		envelope.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void ProviderMetadata_CanStoreAndRetrieveTypedData()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.AllProviderMetadata["sequenceNumber"] = 42L;

		// Assert
		envelope.GetProviderMetadata<long>("sequenceNumber").ShouldBe(42L);
	}

	[Fact]
	public void ValidationResult_DefaultsToValid()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.ValidationResult.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void AuthorizationResult_DefaultsToAuthorized()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.AuthorizationResult.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void RoutingResult_DefaultsToSuccess()
	{
		// Arrange & Act
		using var envelope = new MessageEnvelope();

		// Assert
		envelope.RoutingDecision.IsSuccess.ShouldBeTrue();
	}
}
