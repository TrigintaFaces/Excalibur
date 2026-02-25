// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Diagnostics;

namespace Excalibur.Data.Tests.ElasticSearch.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class AuditTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		AuditTelemetryConstants.MeterName.ShouldBe("Excalibur.Data.Audit");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		AuditTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Data.Audit");
	}

	[Fact]
	public void ExposeSharedActivitySource()
	{
		AuditTelemetryConstants.ActivitySource.ShouldNotBeNull();
		AuditTelemetryConstants.ActivitySource.Name.ShouldBe("Excalibur.Data.Audit");
	}

	[Fact]
	public void ExposeSharedMeter()
	{
		AuditTelemetryConstants.Meter.ShouldNotBeNull();
		AuditTelemetryConstants.Meter.Name.ShouldBe("Excalibur.Data.Audit");
	}

	[Fact]
	public void DefineMetricNames()
	{
		AuditTelemetryConstants.MetricNames.EventsRecorded.ShouldBe("excalibur.audit.events.recorded");
		AuditTelemetryConstants.MetricNames.EventsFailed.ShouldBe("excalibur.audit.events.failed");
		AuditTelemetryConstants.MetricNames.ReportDuration.ShouldBe("excalibur.audit.report.duration");
		AuditTelemetryConstants.MetricNames.BulkStoreSize.ShouldBe("excalibur.audit.bulk_store.size");
	}

	[Fact]
	public void DefineTagNames()
	{
		AuditTelemetryConstants.Tags.EventType.ShouldBe("audit.event_type");
		AuditTelemetryConstants.Tags.Severity.ShouldBe("audit.severity");
		AuditTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
	}
}
