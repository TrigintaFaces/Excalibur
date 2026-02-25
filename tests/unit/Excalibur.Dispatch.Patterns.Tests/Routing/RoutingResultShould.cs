namespace Excalibur.Dispatch.Patterns.Tests.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingResultShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var result = new RoutingResult();

		// Assert
		result.SelectedRoute.ShouldBeNull();
		result.AlternativeRoutes.ShouldBeEmpty();
		result.AppliedRule.ShouldBeNull();
		result.Timestamp.ShouldNotBe(default);
		result.Metadata.ShouldBeEmpty();
		result.IsSuccess.ShouldBeFalse();
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_success_status()
	{
		// Arrange & Act
		var result = new RoutingResult { IsSuccess = true };

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void Allow_setting_failure_reason()
	{
		// Arrange & Act
		var result = new RoutingResult { FailureReason = "No matching route" };

		// Assert
		result.FailureReason.ShouldBe("No matching route");
	}

	[Fact]
	public void Allow_adding_metadata()
	{
		// Arrange
		var result = new RoutingResult();

		// Act
		result.Metadata["region"] = "us-east-1";
		result.Metadata["priority"] = 1;

		// Assert
		result.Metadata.Count.ShouldBe(2);
		result.Metadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void Allow_adding_alternative_routes()
	{
		// Arrange
		var result = new RoutingResult();

		// Act
		result.AlternativeRoutes.Add(new Excalibur.Dispatch.Abstractions.Routing.RouteDefinition());

		// Assert
		result.AlternativeRoutes.Count.ShouldBe(1);
	}

	[Fact]
	public void Have_timestamp_close_to_now()
	{
		// Arrange & Act
		var result = new RoutingResult();

		// Assert
		result.Timestamp.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddSeconds(-5),
			DateTimeOffset.UtcNow.AddSeconds(5));
	}
}
