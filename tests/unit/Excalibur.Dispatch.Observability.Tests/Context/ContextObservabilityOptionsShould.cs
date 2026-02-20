// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextObservabilityOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextObservabilityOptionsShould
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
	public void HaveNonNullTracingSubOptions()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Tracing.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullLimitsSubOptions()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Limits.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullFieldsSubOptions()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Fields.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullExportSubOptions()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions();

		// Assert
		options.Export.ShouldNotBeNull();
	}

	#endregion

	#region Nested Initializer Tests

	[Fact]
	public void SupportNestedInitializerForTracing()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Tracing = { IncludeCustomItemsInTraces = true, MaxCustomItemsInTraces = 25, StoreMutationsInContext = true },
		};

		// Assert
		options.Tracing.IncludeCustomItemsInTraces.ShouldBeTrue();
		options.Tracing.MaxCustomItemsInTraces.ShouldBe(25);
		options.Tracing.StoreMutationsInContext.ShouldBeTrue();
	}

	[Fact]
	public void SupportNestedInitializerForLimits()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Limits = { MaxContextSizeBytes = 200_000, MaxSnapshotsPerLineage = 50, MaxAnomalyQueueSize = 500 },
		};

		// Assert
		options.Limits.MaxContextSizeBytes.ShouldBe(200_000);
		options.Limits.MaxSnapshotsPerLineage.ShouldBe(50);
		options.Limits.MaxAnomalyQueueSize.ShouldBe(500);
	}

	[Fact]
	public void SupportNestedInitializerForFields()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Fields =
			{
				RequiredContextFields = ["CorrelationId"],
				CriticalFields = ["TenantId"],
				TrackedFields = ["UserId"],
			},
		};

		// Assert
		options.Fields.RequiredContextFields.ShouldBe(["CorrelationId"]);
		options.Fields.CriticalFields.ShouldBe(["TenantId"]);
		options.Fields.TrackedFields.ShouldBe(["UserId"]);
	}

	[Fact]
	public void SupportNestedInitializerForExport()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Export =
			{
				OtlpEndpoint = "http://otel:4317",
				ServiceName = "MySvc",
				ServiceVersion = "2.0.0",
				ExportToPrometheus = false,
				ExportToApplicationInsights = true,
				ApplicationInsightsConnectionString = "InstrumentationKey=abc",
			},
		};

		// Assert
		options.Export.OtlpEndpoint.ShouldBe("http://otel:4317");
		options.Export.ServiceName.ShouldBe("MySvc");
		options.Export.ServiceVersion.ShouldBe("2.0.0");
		options.Export.ExportToPrometheus.ShouldBeFalse();
		options.Export.ExportToApplicationInsights.ShouldBeTrue();
		options.Export.ApplicationInsightsConnectionString.ShouldBe("InstrumentationKey=abc");
	}

	[Fact]
	public void SupportCombinedNestedInitializerForAllSubOptions()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = false,
			Tracing =
			{
				IncludeCustomItemsInTraces = true,
				PreserveUnknownBaggageItems = false,
			},
			Limits =
			{
				MaxContextSizeBytes = 50_000,
				MaxAnomalyQueueSize = 100,
			},
			Fields =
			{
				RequiredContextFields = ["CorrelationId"],
			},
			Export =
			{
				ServiceName = "CombinedTest",
				ExportToPrometheus = false,
			},
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ValidateContextIntegrity.ShouldBeFalse();
		options.Tracing.IncludeCustomItemsInTraces.ShouldBeTrue();
		options.Tracing.PreserveUnknownBaggageItems.ShouldBeFalse();
		options.Limits.MaxContextSizeBytes.ShouldBe(50_000);
		options.Limits.MaxAnomalyQueueSize.ShouldBe(100);
		options.Fields.RequiredContextFields.ShouldBe(["CorrelationId"]);
		options.Export.ServiceName.ShouldBe("CombinedTest");
		options.Export.ExportToPrometheus.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions { Enabled = false };

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingLimitsMaxContextSizeBytes()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions { Limits = { MaxContextSizeBytes = 500_000 } };

		// Assert
		options.Limits.MaxContextSizeBytes.ShouldBe(500_000);
	}

	[Fact]
	public void AllowSettingFieldsRequiredContextFields()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Fields = { RequiredContextFields = ["CorrelationId", "RequestId"] },
		};

		// Assert
		options.Fields.RequiredContextFields.ShouldBe(["CorrelationId", "RequestId"]);
	}

	[Fact]
	public void AllowSettingFieldsCriticalFields()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Fields = { CriticalFields = ["TenantId", "UserId"] },
		};

		// Assert
		options.Fields.CriticalFields.ShouldBe(["TenantId", "UserId"]);
	}

	[Fact]
	public void AllowSettingExportOtlpEndpoint()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Export = { OtlpEndpoint = "http://localhost:4317" },
		};

		// Assert
		options.Export.OtlpEndpoint.ShouldBe("http://localhost:4317");
	}

	[Fact]
	public void AllowSettingExportApplicationInsightsConnectionString()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Export = { ApplicationInsightsConnectionString = "InstrumentationKey=xxx" },
		};

		// Assert
		options.Export.ApplicationInsightsConnectionString.ShouldBe("InstrumentationKey=xxx");
	}

	[Fact]
	public void AllowAddingExportResourceAttributes()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Export.ResourceAttributes["environment"] = "production";
		options.Export.ResourceAttributes["region"] = "us-west-2";

		// Assert
		options.Export.ResourceAttributes.Count.ShouldBe(2);
		options.Export.ResourceAttributes["environment"].ShouldBe("production");
		options.Export.ResourceAttributes["region"].ShouldBe("us-west-2");
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = true,
			FailOnIntegrityViolation = true,
			CaptureCustomItems = true,
			EmitDiagnosticEvents = true,
			CaptureErrorStates = true,
			Tracing =
			{
				IncludeCustomItemsInTraces = true,
				MaxCustomItemsInTraces = 25,
				IncludeStackTraceInErrors = true,
				StoreMutationsInContext = true,
				IncludeNullFields = true,
				PreserveUnknownBaggageItems = false,
				SensitiveFieldPatterns = ["*Password*", "*Secret*"],
			},
			Limits =
			{
				MaxCustomItemsToCapture = 50,
				MaxContextSizeBytes = 200_000,
				FailOnSizeThresholdExceeded = true,
				SnapshotRetentionPeriod = TimeSpan.FromHours(2),
				MaxSnapshotsPerLineage = 200,
				MaxHistoryEventsPerContext = 100,
				MaxAnomalyQueueSize = 2000,
			},
			Fields =
			{
				RequiredContextFields = ["CorrelationId"],
				CriticalFields = ["TenantId"],
				TrackedFields = ["UserId"],
			},
			Export =
			{
				OtlpEndpoint = "http://otel-collector:4317",
				ServiceName = "MyService",
				ServiceVersion = "2.0.0",
				ExportToPrometheus = true,
				PrometheusScrapePath = "/prometheus-metrics",
				ExportToApplicationInsights = true,
				ApplicationInsightsConnectionString = "InstrumentationKey=abc123",
			},
		};

		// Assert - spot check several properties
		options.Limits.MaxContextSizeBytes.ShouldBe(200_000);
		options.Export.ServiceName.ShouldBe("MyService");
		options.FailOnIntegrityViolation.ShouldBeTrue();
		options.Tracing.SensitiveFieldPatterns.ShouldContain("*Password*");
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
		tracing.SensitiveFieldPatterns.Length.ShouldBe(6);
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

	#region Property Count Validation Tests (ISP Gate Compliance)

	[Fact]
	public void ContextTracingOptions_HaveAtMost10Properties()
	{
		// Arrange
		var properties = typeof(ContextTracingOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// Assert - ISP gate: each sub-option <= 10 properties
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"ContextTracingOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void ContextLimitsOptions_HaveAtMost10Properties()
	{
		// Arrange
		var properties = typeof(ContextLimitsOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// Assert - ISP gate: each sub-option <= 10 properties
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"ContextLimitsOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void ContextFieldOptions_HaveAtMost10Properties()
	{
		// Arrange
		var properties = typeof(ContextFieldOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// Assert - ISP gate: each sub-option <= 10 properties
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"ContextFieldOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void ContextExportOptions_HaveAtMost10Properties()
	{
		// Arrange
		var properties = typeof(ContextExportOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		// Assert - ISP gate: each sub-option <= 10 properties
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"ContextExportOptions has {properties.Length} properties: " +
			$"{string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void RootOptions_HaveAtMost10Properties()
	{
		// Arrange - root has 6 core bools + 4 sub-option accessors = 10
		var allProperties = typeof(ContextObservabilityOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert - ISP gate: root <= 10 properties
		allProperties.Length.ShouldBeLessThanOrEqualTo(10,
			$"ContextObservabilityOptions has {allProperties.Length} properties: " +
			$"{string.Join(", ", allProperties.Select(p => p.Name))}");
	}

	#endregion

	#region ResourceAttributes Dictionary Tests

	[Fact]
	public void ResourceAttributes_UseOrdinalStringComparer()
	{
		// Arrange
		var options = new ContextObservabilityOptions();

		// Act
		options.Export.ResourceAttributes["key"] = "value1";
		options.Export.ResourceAttributes["Key"] = "value2";

		// Assert - ordinal comparer means "key" != "Key"
		options.Export.ResourceAttributes.Count.ShouldBe(2);
		options.Export.ResourceAttributes["key"].ShouldBe("value1");
		options.Export.ResourceAttributes["Key"].ShouldBe("value2");
	}

	#endregion
}
