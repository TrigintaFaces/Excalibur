// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Aws;

namespace Excalibur.Dispatch.Observability.Tests.Aws;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsObservabilityOptionsShould
{
	[Fact]
	public void DefaultEnableXRayToTrue()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.EnableXRay.ShouldBeTrue();
	}

	[Fact]
	public void DefaultEnableCloudWatchMetricsToTrue()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.EnableCloudWatchMetrics.ShouldBeTrue();
	}

	[Fact]
	public void DefaultSamplingRateToFivePercent()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.SamplingRate.ShouldBe(0.05);
	}

	[Fact]
	public void DefaultMetricsNamespaceToDispatchCustom()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.MetricsNamespace.ShouldBe("Dispatch/Custom");
	}

	[Fact]
	public void DefaultRegionToNull()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.Region.ShouldBeNull();
	}

	[Fact]
	public void DefaultXRayDaemonEndpointToNull()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.XRayDaemonEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingServiceName()
	{
		var options = new AwsObservabilityOptions { ServiceName = "my-service" };
		options.ServiceName.ShouldBe("my-service");
	}

	[Fact]
	public void AllowSettingRegion()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.Region = "us-east-1";
		options.Region.ShouldBe("us-east-1");
	}

	[Fact]
	public void AllowSettingSamplingRate()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.SamplingRate = 1.0;
		options.SamplingRate.ShouldBe(1.0);
	}

	[Fact]
	public void AllowSettingMetricsNamespace()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.MetricsNamespace = "MyApp/Metrics";
		options.MetricsNamespace.ShouldBe("MyApp/Metrics");
	}

	[Fact]
	public void AllowSettingXRayDaemonEndpoint()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.XRayDaemonEndpoint = "10.0.0.1:3000";
		options.XRayDaemonEndpoint.ShouldBe("10.0.0.1:3000");
	}

	[Fact]
	public void AllowDisablingXRay()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.EnableXRay = false;
		options.EnableXRay.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingCloudWatchMetrics()
	{
		var options = new AwsObservabilityOptions { ServiceName = "test" };
		options.EnableCloudWatchMetrics = false;
		options.EnableCloudWatchMetrics.ShouldBeFalse();
	}
}
