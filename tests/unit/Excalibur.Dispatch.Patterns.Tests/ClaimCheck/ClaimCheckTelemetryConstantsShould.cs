namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ClaimCheckTelemetryConstantsShould
{
	[Fact]
	public void Have_correct_meter_name()
	{
		// Assert
		ClaimCheckTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Patterns.ClaimCheck");
	}

	[Fact]
	public void Have_correct_activity_source_name()
	{
		// Assert
		ClaimCheckTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Patterns.ClaimCheck");
	}

	[Fact]
	public void Have_metric_names_with_correct_prefix()
	{
		// Assert
		ClaimCheckTelemetryConstants.MetricNames.PayloadsStored.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.PayloadsRetrieved.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.PayloadsDeleted.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.OperationsFailed.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.StoreDuration.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.RetrieveDuration.ShouldStartWith("dispatch.claimcheck.");
		ClaimCheckTelemetryConstants.MetricNames.PayloadSize.ShouldStartWith("dispatch.claimcheck.");
	}

	[Fact]
	public void Have_unique_metric_names()
	{
		// Arrange
		var names = new[]
		{
			ClaimCheckTelemetryConstants.MetricNames.PayloadsStored,
			ClaimCheckTelemetryConstants.MetricNames.PayloadsRetrieved,
			ClaimCheckTelemetryConstants.MetricNames.PayloadsDeleted,
			ClaimCheckTelemetryConstants.MetricNames.OperationsFailed,
			ClaimCheckTelemetryConstants.MetricNames.StoreDuration,
			ClaimCheckTelemetryConstants.MetricNames.RetrieveDuration,
			ClaimCheckTelemetryConstants.MetricNames.PayloadSize
		};

		// Assert
		names.Distinct().Count().ShouldBe(names.Length);
	}

	[Fact]
	public void Have_tag_constants()
	{
		// Assert
		ClaimCheckTelemetryConstants.Tags.Operation.ShouldBe("dispatch.claimcheck.operation");
		ClaimCheckTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
	}
}
