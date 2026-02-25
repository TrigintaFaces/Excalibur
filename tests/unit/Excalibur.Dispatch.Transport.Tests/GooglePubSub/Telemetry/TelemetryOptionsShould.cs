// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Telemetry;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TelemetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new TelemetryOptions();

		// Assert
		options.EnableOpenTelemetry.ShouldBeFalse();
		options.ExportToCloudMonitoring.ShouldBeFalse();
		options.OtlpEndpoint.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new TelemetryOptions
		{
			EnableOpenTelemetry = true,
			ExportToCloudMonitoring = true,
			OtlpEndpoint = "http://localhost:4317",
		};

		// Assert
		options.EnableOpenTelemetry.ShouldBeTrue();
		options.ExportToCloudMonitoring.ShouldBeTrue();
		options.OtlpEndpoint.ShouldBe("http://localhost:4317");
	}
}
