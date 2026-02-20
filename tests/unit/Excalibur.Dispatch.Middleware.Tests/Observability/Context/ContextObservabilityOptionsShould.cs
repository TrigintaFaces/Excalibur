// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextObservabilityOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextObservabilityOptionsShould : UnitTestBase
{
	#region Default Value Tests

	[Fact]
	public void HaveEnabledTrueByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveValidateContextIntegrityTrueByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.ValidateContextIntegrity.ShouldBeTrue();
	}

	[Fact]
	public void HaveFailOnIntegrityViolationFalseByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.FailOnIntegrityViolation.ShouldBeFalse();
	}

	[Fact]
	public void HaveCaptureCustomItemsTrueByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.CaptureCustomItems.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmitDiagnosticEventsTrueByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.EmitDiagnosticEvents.ShouldBeTrue();
	}

	[Fact]
	public void HaveCaptureErrorStatesTrueByDefault()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.CaptureErrorStates.ShouldBeTrue();
	}

	#endregion

	#region Sub-Option Object Tests

	[Fact]
	public void HaveNonNullSubOptionObjects()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Tracing.ShouldNotBeNull();
		options.Limits.ShouldNotBeNull();
		options.Fields.ShouldNotBeNull();
		options.Export.ShouldNotBeNull();
	}

	[Fact]
	public void SupportNestedInitializerSyntax()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Tracing = { IncludeCustomItemsInTraces = true, MaxCustomItemsInTraces = 25 },
			Limits = { MaxContextSizeBytes = 200_000, MaxAnomalyQueueSize = 500 },
			Fields = { RequiredContextFields = ["CorrelationId"], CriticalFields = ["TenantId"] },
			Export = { ServiceName = "TestSvc", ExportToPrometheus = false },
		};

		// Assert - sub-option values
		options.Tracing.IncludeCustomItemsInTraces.ShouldBeTrue();
		options.Tracing.MaxCustomItemsInTraces.ShouldBe(25);
		options.Limits.MaxContextSizeBytes.ShouldBe(200_000);
		options.Limits.MaxAnomalyQueueSize.ShouldBe(500);
		options.Fields.RequiredContextFields.ShouldBe(["CorrelationId"]);
		options.Fields.CriticalFields.ShouldBe(["TenantId"]);
		options.Export.ServiceName.ShouldBe("TestSvc");
		options.Export.ExportToPrometheus.ShouldBeFalse();
	}

	#endregion

	#region Property Setting Tests

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingFieldsRequiredContextFields()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		var fields = new[] { "MessageId", "CorrelationId" };

		// Act
		options.Fields.RequiredContextFields = fields;

		// Assert
		options.Fields.RequiredContextFields.ShouldBe(fields);
	}

	[Fact]
	public void AllowSettingFieldsCriticalFields()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		var fields = new[] { "TenantId", "UserId" };

		// Act
		options.Fields.CriticalFields = fields;

		// Assert
		options.Fields.CriticalFields.ShouldBe(fields);
	}

	[Fact]
	public void AllowSettingLimitsMaxContextSizeBytes()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Limits.MaxContextSizeBytes = 500_000;

		// Assert
		options.Limits.MaxContextSizeBytes.ShouldBe(500_000);
	}

	[Fact]
	public void AllowSettingLimitsSnapshotRetentionPeriod()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		var retention = TimeSpan.FromMinutes(30);

		// Act
		options.Limits.SnapshotRetentionPeriod = retention;

		// Assert
		options.Limits.SnapshotRetentionPeriod.ShouldBe(retention);
	}

	[Fact]
	public void AllowSettingExportOtlpEndpoint()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Export.OtlpEndpoint = "http://localhost:4317";

		// Assert
		options.Export.OtlpEndpoint.ShouldBe("http://localhost:4317");
	}

	[Fact]
	public void AllowSettingExportApplicationInsightsConnectionString()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		var connectionString = "InstrumentationKey=12345;IngestionEndpoint=https://dc.services.visualstudio.com/";

		// Act
		options.Export.ApplicationInsightsConnectionString = connectionString;

		// Assert
		options.Export.ApplicationInsightsConnectionString.ShouldBe(connectionString);
	}

	[Fact]
	public void AllowAddingExportResourceAttributes()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Export.ResourceAttributes["environment"] = "production";
		options.Export.ResourceAttributes["instance"] = "app-01";

		// Assert
		options.Export.ResourceAttributes.Count.ShouldBe(2);
		options.Export.ResourceAttributes["environment"].ShouldBe("production");
		options.Export.ResourceAttributes["instance"].ShouldBe("app-01");
	}

	#endregion

	#region Sub-Option Default Value Tests

	[Fact]
	public void ContextTracingOptions_HaveCorrectDefaults()
	{
		// Arrange & Act
		var tracing = new ContextTracingOptions();

		// Assert
		tracing.IncludeCustomItemsInTraces.ShouldBeFalse();
		tracing.MaxCustomItemsInTraces.ShouldBe(10);
		tracing.IncludeStackTraceInErrors.ShouldBeFalse();
		tracing.StoreMutationsInContext.ShouldBeFalse();
		tracing.IncludeNullFields.ShouldBeFalse();
		tracing.PreserveUnknownBaggageItems.ShouldBeTrue();
		tracing.SensitiveFieldPatterns.ShouldNotBeNull();
		tracing.SensitiveFieldPatterns.ShouldContain("(?i)password");
		tracing.SensitiveFieldPatterns.ShouldContain("(?i)secret");
		tracing.SensitiveFieldPatterns.ShouldContain("(?i)token");
	}

	[Fact]
	public void ContextLimitsOptions_HaveCorrectDefaults()
	{
		// Arrange & Act
		var limits = new ContextLimitsOptions();

		// Assert
		limits.MaxCustomItemsToCapture.ShouldBe(20);
		limits.MaxContextSizeBytes.ShouldBe(100_000);
		limits.FailOnSizeThresholdExceeded.ShouldBeFalse();
		limits.SnapshotRetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
		limits.MaxSnapshotsPerLineage.ShouldBe(100);
		limits.MaxHistoryEventsPerContext.ShouldBe(50);
		limits.MaxAnomalyQueueSize.ShouldBe(1000);
	}

	[Fact]
	public void ContextFieldOptions_HaveCorrectDefaults()
	{
		// Arrange & Act
		var fields = new ContextFieldOptions();

		// Assert
		fields.RequiredContextFields.ShouldBeNull();
		fields.CriticalFields.ShouldBeNull();
		fields.TrackedFields.ShouldBeNull();
	}

	[Fact]
	public void ContextExportOptions_HaveCorrectDefaults()
	{
		// Arrange & Act
		var export = new ContextExportOptions();

		// Assert
		export.OtlpEndpoint.ShouldBeNull();
		export.ServiceName.ShouldBe("Excalibur.Dispatch");
		export.ServiceVersion.ShouldBe("1.0.0");
		export.ExportToPrometheus.ShouldBeTrue();
		export.PrometheusScrapePath.ShouldBe("/metrics");
		export.ExportToApplicationInsights.ShouldBeFalse();
		export.ApplicationInsightsConnectionString.ShouldBeNull();
		export.ResourceAttributes.ShouldNotBeNull();
		export.ResourceAttributes.ShouldBeEmpty();
	}

	#endregion
}
