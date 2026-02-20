// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Aws;

namespace Excalibur.Dispatch.Observability.Tests.Aws;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsObservabilityEventIdShould
{
	[Fact]
	public void DefineXRayConfiguredEventId()
	{
		AwsObservabilityEventId.XRayConfigured.ShouldBe(93550);
	}

	[Fact]
	public void DefineCloudWatchMetricsConfiguredEventId()
	{
		AwsObservabilityEventId.CloudWatchMetricsConfigured.ShouldBe(93551);
	}

	[Fact]
	public void DefineXRayConfigurationFailedEventId()
	{
		AwsObservabilityEventId.XRayConfigurationFailed.ShouldBe(93555);
	}

	[Fact]
	public void DefineCloudWatchMetricsConfigurationFailedEventId()
	{
		AwsObservabilityEventId.CloudWatchMetricsConfigurationFailed.ShouldBe(93556);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		var ids = new[]
		{
			AwsObservabilityEventId.XRayConfigured,
			AwsObservabilityEventId.CloudWatchMetricsConfigured,
			AwsObservabilityEventId.XRayConfigurationFailed,
			AwsObservabilityEventId.CloudWatchMetricsConfigurationFailed,
		};

		ids.Distinct().Count().ShouldBe(ids.Length);
	}

	[Fact]
	public void BeInExpectedRange()
	{
		// Event IDs should be in the 93550-93569 range per documentation
		AwsObservabilityEventId.XRayConfigured.ShouldBeInRange(93550, 93569);
		AwsObservabilityEventId.CloudWatchMetricsConfigured.ShouldBeInRange(93550, 93569);
		AwsObservabilityEventId.XRayConfigurationFailed.ShouldBeInRange(93550, 93569);
		AwsObservabilityEventId.CloudWatchMetricsConfigurationFailed.ShouldBeInRange(93550, 93569);
	}
}
