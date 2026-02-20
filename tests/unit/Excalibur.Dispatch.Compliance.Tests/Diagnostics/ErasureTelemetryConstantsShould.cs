using Excalibur.Dispatch.Compliance.Diagnostics;

namespace Excalibur.Dispatch.Compliance.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureTelemetryConstantsShould
{
	[Fact]
	public void Have_correct_meter_name()
	{
		ErasureTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Compliance.Erasure");
	}

	[Fact]
	public void Have_correct_activity_source_name()
	{
		ErasureTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Compliance.Erasure");
	}

	[Fact]
	public void Expose_non_null_activity_source()
	{
		ErasureTelemetryConstants.ActivitySource.ShouldNotBeNull();
		ErasureTelemetryConstants.ActivitySource.Name.ShouldBe(ErasureTelemetryConstants.ActivitySourceName);
	}

	[Fact]
	public void Expose_non_null_meter()
	{
		ErasureTelemetryConstants.Meter.ShouldNotBeNull();
		ErasureTelemetryConstants.Meter.Name.ShouldBe(ErasureTelemetryConstants.MeterName);
	}

	[Fact]
	public void Have_expected_metric_names()
	{
		ErasureTelemetryConstants.MetricNames.RequestsSubmitted.ShouldBe("dispatch.erasure.requests.submitted");
		ErasureTelemetryConstants.MetricNames.RequestsCompleted.ShouldBe("dispatch.erasure.requests.completed");
		ErasureTelemetryConstants.MetricNames.RequestsFailed.ShouldBe("dispatch.erasure.requests.failed");
		ErasureTelemetryConstants.MetricNames.RequestsBlocked.ShouldBe("dispatch.erasure.requests.blocked");
		ErasureTelemetryConstants.MetricNames.KeysDeleted.ShouldBe("dispatch.erasure.keys.deleted");
		ErasureTelemetryConstants.MetricNames.ExecutionDuration.ShouldBe("dispatch.erasure.execution.duration");
	}

	[Fact]
	public void Have_expected_tag_names()
	{
		ErasureTelemetryConstants.Tags.Scope.ShouldBe("erasure.scope");
		ErasureTelemetryConstants.Tags.ResultStatus.ShouldBe("erasure.result_status");
		ErasureTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
	}
}
