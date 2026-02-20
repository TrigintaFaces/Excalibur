// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="ObservabilityOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ObservabilityOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableContextFlow.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingEnabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingTracing()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableTracing = false;

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingMetrics()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingContextFlow()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableContextFlow = false;

		// Assert
		options.EnableContextFlow.ShouldBeFalse();
	}

	[Theory]
	[InlineData(true, true, true, true)]
	[InlineData(false, false, false, false)]
	[InlineData(true, false, true, false)]
	[InlineData(false, true, false, true)]
	public void SupportVariousConfigurations(bool enabled, bool tracing, bool metrics, bool contextFlow)
	{
		// Arrange & Act
		var options = new ObservabilityOptions
		{
			Enabled = enabled,
			EnableTracing = tracing,
			EnableMetrics = metrics,
			EnableContextFlow = contextFlow
		};

		// Assert
		options.Enabled.ShouldBe(enabled);
		options.EnableTracing.ShouldBe(tracing);
		options.EnableMetrics.ShouldBe(metrics);
		options.EnableContextFlow.ShouldBe(contextFlow);
	}

	[Fact]
	public void AllowSelectiveFeatureDisabling()
	{
		// Arrange - Start with all enabled (defaults)
		var options = new ObservabilityOptions();

		// Act - Disable only metrics
		options.EnableMetrics = false;

		// Assert - Everything else stays enabled
		options.Enabled.ShouldBeTrue();
		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeFalse();
		options.EnableContextFlow.ShouldBeTrue();
	}
}
