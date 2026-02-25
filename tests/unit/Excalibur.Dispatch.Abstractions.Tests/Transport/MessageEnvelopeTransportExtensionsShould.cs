namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for MessageEnvelopeTransportExtensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageEnvelopeTransportExtensionsShould : UnitTestBase
{
	[Fact]
	public void SetScheduledDeliveryTime_StoresValue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var deliveryTime = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		envelope.SetScheduledDeliveryTime(deliveryTime);

		// Assert
		envelope.GetScheduledDeliveryTime().ShouldBe(deliveryTime);
	}

	[Fact]
	public void SetScheduledDeliveryTime_WithNull_DoesNotStore()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.SetScheduledDeliveryTime(null);

		// Assert
		envelope.GetScheduledDeliveryTime().ShouldBeNull();
	}

	[Fact]
	public void GetScheduledDeliveryTime_WhenNotSet_ReturnsNull()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		var result = envelope.GetScheduledDeliveryTime();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SetTimeToLive_StoresValue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var ttl = TimeSpan.FromMinutes(30);

		// Act
		envelope.SetTimeToLive(ttl);

		// Assert
		envelope.GetTimeToLive().ShouldBe(ttl);
	}

	[Fact]
	public void SetTimeToLive_WithNull_DoesNotStore()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.SetTimeToLive(null);

		// Assert
		envelope.GetTimeToLive().ShouldBeNull();
	}

	[Fact]
	public void SetSessionId_StoresValue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.SetSessionId("session-123");

		// Assert
		envelope.SessionId.ShouldBe("session-123");
	}

	[Fact]
	public void SetSessionId_WithNull_DoesNotStore()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SessionId = "existing";

		// Act
		envelope.SetSessionId(null);

		// Assert
		envelope.SessionId.ShouldBe("existing");
	}

	[Fact]
	public void SetSessionId_WithEmpty_DoesNotStore()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		envelope.SessionId = "existing";

		// Act
		envelope.SetSessionId(string.Empty);

		// Assert
		envelope.SessionId.ShouldBe("existing");
	}

	[Fact]
	public void SetTraceContext_StoresValue()
	{
		// Arrange
		using var envelope = new MessageEnvelope();
		var traceContext = new { TraceId = "abc123" };

		// Act
		envelope.SetTraceContext(traceContext);

		// Assert
		envelope.GetTraceContext().ShouldBe(traceContext);
	}

	[Fact]
	public void SetTraceContext_WithNull_DoesNotStore()
	{
		// Arrange
		using var envelope = new MessageEnvelope();

		// Act
		envelope.SetTraceContext(null);

		// Assert
		envelope.GetTraceContext().ShouldBeNull();
	}

	[Fact]
	public void SetScheduledDeliveryTime_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.SetScheduledDeliveryTime(null!, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void GetScheduledDeliveryTime_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.GetScheduledDeliveryTime(null!));
	}

	[Fact]
	public void SetTimeToLive_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.SetTimeToLive(null!, TimeSpan.FromMinutes(1)));
	}

	[Fact]
	public void GetTimeToLive_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.GetTimeToLive(null!));
	}

	[Fact]
	public void SetSessionId_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.SetSessionId(null!, "session"));
	}

	[Fact]
	public void SetTraceContext_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.SetTraceContext(null!, new object()));
	}

	[Fact]
	public void GetTraceContext_ThrowsOnNullEnvelope()
	{
		Should.Throw<ArgumentNullException>(
			() => MessageEnvelopeTransportExtensions.GetTraceContext(null!));
	}
}
