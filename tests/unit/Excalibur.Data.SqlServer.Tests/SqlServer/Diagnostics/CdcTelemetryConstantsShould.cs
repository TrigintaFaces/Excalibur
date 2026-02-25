// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Diagnostics;

namespace Excalibur.Data.Tests.SqlServer.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		CdcTelemetryConstants.MeterName.ShouldBe("Excalibur.Data.Cdc");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		CdcTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Data.Cdc");
	}

	[Fact]
	public void ExposeActivitySource()
	{
		CdcTelemetryConstants.ActivitySource.ShouldNotBeNull();
		CdcTelemetryConstants.ActivitySource.Name.ShouldBe("Excalibur.Data.Cdc");
	}

	[Fact]
	public void ExposeMeter()
	{
		CdcTelemetryConstants.Meter.ShouldNotBeNull();
		CdcTelemetryConstants.Meter.Name.ShouldBe("Excalibur.Data.Cdc");
	}

	[Fact]
	public void HaveCorrectMetricNames()
	{
		CdcTelemetryConstants.MetricNames.EventsProcessed.ShouldBe("excalibur.cdc.events.processed");
		CdcTelemetryConstants.MetricNames.EventsFailed.ShouldBe("excalibur.cdc.events.failed");
		CdcTelemetryConstants.MetricNames.BatchDuration.ShouldBe("excalibur.cdc.batch.duration");
		CdcTelemetryConstants.MetricNames.BatchSize.ShouldBe("excalibur.cdc.batch.size");
	}

	[Fact]
	public void HaveCorrectTagNames()
	{
		CdcTelemetryConstants.Tags.CaptureInstance.ShouldBe("cdc.capture_instance");
		CdcTelemetryConstants.Tags.Operation.ShouldBe("cdc.operation");
		CdcTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
	}
}
