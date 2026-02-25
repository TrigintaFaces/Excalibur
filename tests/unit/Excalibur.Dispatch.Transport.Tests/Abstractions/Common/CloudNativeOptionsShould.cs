// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Common;

/// <summary>
/// Unit tests for <see cref="CloudNativeOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class CloudNativeOptionsShould
{
	[Fact]
	public void HaveEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveUseCloudLoggingEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.UseCloudLogging.ShouldBeTrue();
	}

	[Fact]
	public void HaveUseCloudMetricsEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.UseCloudMetrics.ShouldBeTrue();
	}

	[Fact]
	public void HaveUseCloudTracingEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.UseCloudTracing.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyProvider_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.Provider.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveProductionEnvironment_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.Environment.ShouldBe("production");
	}

	[Fact]
	public void HaveEmptyRegion_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.Region.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyTags_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.Tags.ShouldNotBeNull();
		options.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void Have60SecondsMetricsFlushInterval_ByDefault()
	{
		// Arrange & Act
		var options = new CloudNativeOptions();

		// Assert
		options.MetricsFlushInterval.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void AllowDisablingEnabled()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingUseCloudLogging()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.UseCloudLogging = false;

		// Assert
		options.UseCloudLogging.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingUseCloudMetrics()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.UseCloudMetrics = false;

		// Assert
		options.UseCloudMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingUseCloudTracing()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.UseCloudTracing = false;

		// Assert
		options.UseCloudTracing.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingProvider()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Provider = "aws";

		// Assert
		options.Provider.ShouldBe("aws");
	}

	[Theory]
	[InlineData("aws")]
	[InlineData("azure")]
	[InlineData("gcp")]
	public void AllowSettingKnownProviders(string provider)
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Provider = provider;

		// Assert
		options.Provider.ShouldBe(provider);
	}

	[Fact]
	public void AllowSettingEnvironment()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Environment = "development";

		// Assert
		options.Environment.ShouldBe("development");
	}

	[Theory]
	[InlineData("development")]
	[InlineData("staging")]
	[InlineData("production")]
	public void AllowSettingKnownEnvironments(string environment)
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Environment = environment;

		// Assert
		options.Environment.ShouldBe(environment);
	}

	[Fact]
	public void AllowSettingRegion()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Region = "us-east-1";

		// Assert
		options.Region.ShouldBe("us-east-1");
	}

	[Theory]
	[InlineData("us-east-1")]
	[InlineData("westeurope")]
	[InlineData("asia-northeast1")]
	public void AllowSettingKnownRegions(string region)
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Region = region;

		// Assert
		options.Region.ShouldBe(region);
	}

	[Fact]
	public void AllowAddingTags()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.Tags["Environment"] = "Production";
		options.Tags["Project"] = "MyProject";

		// Assert
		options.Tags.ShouldContainKey("Environment");
		options.Tags["Environment"].ShouldBe("Production");
		options.Tags.ShouldContainKey("Project");
		options.Tags["Project"].ShouldBe("MyProject");
	}

	[Fact]
	public void AllowSettingMetricsFlushInterval()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.MetricsFlushInterval = TimeSpan.FromSeconds(30);

		// Assert
		options.MetricsFlushInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingMetricsFlushIntervalToMinimum()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.MetricsFlushInterval = TimeSpan.FromSeconds(10);

		// Assert
		options.MetricsFlushInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AllowSettingMetricsFlushIntervalToMaximum()
	{
		// Arrange
		var options = new CloudNativeOptions();

		// Act
		options.MetricsFlushInterval = TimeSpan.FromMinutes(5);

		// Assert
		options.MetricsFlushInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}
}
