// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PubSubTelemetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubTelemetryOptions();

		// Assert
		options.EnableOpenTelemetry.ShouldBeTrue();
		options.ExportToCloudMonitoring.ShouldBeFalse();
		options.OtlpEndpoint.ShouldBeNull();
		options.TelemetryExportIntervalSeconds.ShouldBe(60);
		options.EnableTracePropagation.ShouldBeTrue();
		options.TelemetryResourceLabels.ShouldBeEmpty();
		options.IncludeMessageAttributesInTraces.ShouldBeFalse();
		options.TracingSamplingRatio.ShouldBe(0.1);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubTelemetryOptions
		{
			EnableOpenTelemetry = false,
			ExportToCloudMonitoring = true,
			OtlpEndpoint = "http://otel-collector:4317",
			TelemetryExportIntervalSeconds = 30,
			EnableTracePropagation = false,
			TelemetryResourceLabels = new Dictionary<string, string> { ["env"] = "test" },
			IncludeMessageAttributesInTraces = true,
			TracingSamplingRatio = 1.0,
		};

		// Assert
		options.EnableOpenTelemetry.ShouldBeFalse();
		options.ExportToCloudMonitoring.ShouldBeTrue();
		options.OtlpEndpoint.ShouldBe("http://otel-collector:4317");
		options.TelemetryExportIntervalSeconds.ShouldBe(30);
		options.EnableTracePropagation.ShouldBeFalse();
		options.TelemetryResourceLabels.Count.ShouldBe(1);
		options.IncludeMessageAttributesInTraces.ShouldBeTrue();
		options.TracingSamplingRatio.ShouldBe(1.0);
	}
}
