// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="ObservabilityOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class ObservabilityOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveEnableMetricsTrueByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void HaveEnableTracingTrueByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.EnableTracing.ShouldBeTrue();
	}

	[Fact]
	public void HaveEnableLoggingTrueByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.EnableLogging.ShouldBeTrue();
	}

	[Fact]
	public void HaveActivitySourceNameDispatchByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.ActivitySourceName.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveDefaultMeterName()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.MeterName.ShouldBe(DispatchMetrics.MeterName);
	}

	[Fact]
	public void HaveServiceNameDispatchByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.ServiceName.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void HaveServiceVersion1_0_0ByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.ServiceVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void HaveEnableDetailedTimingFalseByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.EnableDetailedTiming.ShouldBeFalse();
	}

	[Fact]
	public void HaveIncludeSensitiveDataFalseByDefault()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingEnableMetrics()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { EnableMetrics = false };

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnableTracing()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { EnableTracing = false };

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnableLogging()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { EnableLogging = false };

		// Assert
		options.EnableLogging.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingActivitySourceName()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { ActivitySourceName = "MyApp.Dispatch" };

		// Assert
		options.ActivitySourceName.ShouldBe("MyApp.Dispatch");
	}

	[Fact]
	public void AllowSettingMeterName()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { MeterName = "MyApp.Excalibur.Dispatch.Metrics" };

		// Assert
		options.MeterName.ShouldBe("MyApp.Excalibur.Dispatch.Metrics");
	}

	[Fact]
	public void AllowSettingServiceName()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { ServiceName = "OrderService" };

		// Assert
		options.ServiceName.ShouldBe("OrderService");
	}

	[Fact]
	public void AllowSettingServiceVersion()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { ServiceVersion = "2.5.3" };

		// Assert
		options.ServiceVersion.ShouldBe("2.5.3");
	}

	[Fact]
	public void AllowSettingEnableDetailedTiming()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { EnableDetailedTiming = true };

		// Assert
		options.EnableDetailedTiming.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingIncludeSensitiveData()
	{
		// Arrange & Act
		var options = new ObservabilityOptions { IncludeSensitiveData = true };

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Complete Configuration Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ObservabilityOptions
		{
			EnableMetrics = true,
			EnableTracing = true,
			EnableLogging = true,
			ActivitySourceName = "CustomActivitySource",
			MeterName = "CustomMeter",
			ServiceName = "CustomService",
			ServiceVersion = "3.0.0",
			EnableDetailedTiming = true,
			IncludeSensitiveData = false,
		};

		// Assert
		options.EnableMetrics.ShouldBeTrue();
		options.EnableTracing.ShouldBeTrue();
		options.EnableLogging.ShouldBeTrue();
		options.ActivitySourceName.ShouldBe("CustomActivitySource");
		options.MeterName.ShouldBe("CustomMeter");
		options.ServiceName.ShouldBe("CustomService");
		options.ServiceVersion.ShouldBe("3.0.0");
		options.EnableDetailedTiming.ShouldBeTrue();
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void SupportDisablingAllFeatures()
	{
		// Arrange & Act
		var options = new ObservabilityOptions
		{
			EnableMetrics = false,
			EnableTracing = false,
			EnableLogging = false,
		};

		// Assert
		options.EnableMetrics.ShouldBeFalse();
		options.EnableTracing.ShouldBeFalse();
		options.EnableLogging.ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(ObservabilityOptions).IsSealed.ShouldBeTrue();
	}

	#endregion
}
