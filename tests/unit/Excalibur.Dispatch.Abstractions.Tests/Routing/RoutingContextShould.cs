using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests.Routing;

/// <summary>
/// Unit tests for RoutingContext.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingContextShould : UnitTestBase
{
	[Fact]
	public void DefaultConstructor_SetsDefaults()
	{
		// Act
		var context = new RoutingContext();

		// Assert
		context.Timestamp.ShouldNotBe(default);
		context.CancellationToken.ShouldBe(CancellationToken.None);
		context.Source.ShouldBeNull();
		context.SourceEndpoint.ShouldBeNull();
		context.MessageType.ShouldBeNull();
		context.CorrelationId.ShouldBeNull();
		context.Properties.ShouldNotBeNull();
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void SourceEndpoint_IsAliasForSource()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.SourceEndpoint = "endpoint-1";

		// Assert
		context.Source.ShouldBe("endpoint-1");
		context.SourceEndpoint.ShouldBe("endpoint-1");
	}

	[Fact]
	public void Source_IsAliasForSourceEndpoint()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.Source = "source-1";

		// Assert
		context.SourceEndpoint.ShouldBe("source-1");
	}

	[Fact]
	public void Properties_CanBeModified()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.Properties["key"] = "value";

		// Assert
		context.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void CancellationToken_CanBeSet()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var context = new RoutingContext();

		// Act
		context.CancellationToken = cts.Token;

		// Assert
		context.CancellationToken.ShouldBe(cts.Token);
	}

	[Fact]
	public void MessageType_CanBeSet()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.MessageType = "OrderCreated";

		// Assert
		context.MessageType.ShouldBe("OrderCreated");
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var context = new RoutingContext();

		// Act
		context.CorrelationId = "corr-123";

		// Assert
		context.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var context = new RoutingContext();
		var time = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		context.Timestamp = time;

		// Assert
		context.Timestamp.ShouldBe(time);
	}
}
