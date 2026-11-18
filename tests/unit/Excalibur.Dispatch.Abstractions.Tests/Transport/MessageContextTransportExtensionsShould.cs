namespace Excalibur.Dispatch.Tests.Transport;

/// <summary>
/// Unit tests for MessageContextTransportExtensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextTransportExtensionsShould : UnitTestBase
{
	[Fact]
	public void GetScheduledDeliveryTime_WhenNotSet_ReturnsNull()
	{
		// Arrange
		var context = CreateFakeContext();

		// Act
		var result = context.GetScheduledDeliveryTime();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetScheduledDeliveryTime_WhenSet_ReturnsValue()
	{
		// Arrange
		var context = CreateFakeContext();
		var expected = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
		context.SetItem("__ScheduledDeliveryTime", (DateTimeOffset?)expected);

		// Act
		var result = context.GetScheduledDeliveryTime();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void GetTimeToLive_WhenNotSet_ReturnsNull()
	{
		// Arrange
		var context = CreateFakeContext();

		// Act
		var result = context.GetTimeToLive();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTimeToLive_WhenSet_ReturnsValue()
	{
		// Arrange
		var context = CreateFakeContext();
		var expected = TimeSpan.FromMinutes(30);
		context.SetItem("__TimeToLive", (TimeSpan?)expected);

		// Act
		var result = context.GetTimeToLive();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void GetTraceContext_WhenNotSet_ReturnsNull()
	{
		// Arrange
		var context = CreateFakeContext();

		// Act
		var result = context.GetTraceContext();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTraceContext_WhenSet_ReturnsValue()
	{
		// Arrange
		var context = CreateFakeContext();
		var expected = new { TraceId = "abc123", SpanId = "def456" };
		context.SetItem("__TraceContext", (object)expected);

		// Act
		var result = context.GetTraceContext();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void GetScheduledDeliveryTime_ThrowsOnNullContext()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => MessageContextTransportExtensions.GetScheduledDeliveryTime(null!));
	}

	[Fact]
	public void GetTimeToLive_ThrowsOnNullContext()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => MessageContextTransportExtensions.GetTimeToLive(null!));
	}

	[Fact]
	public void GetTraceContext_ThrowsOnNullContext()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => MessageContextTransportExtensions.GetTraceContext(null!));
	}

	private static MessageEnvelope CreateFakeContext()
	{
		return new MessageEnvelope();
	}
}
