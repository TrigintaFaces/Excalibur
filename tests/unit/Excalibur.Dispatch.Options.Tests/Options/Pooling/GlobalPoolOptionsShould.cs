// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="GlobalPoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class GlobalPoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_EnableTelemetry_IsFalse()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableDetailedMetrics_IsFalse()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.EnableDetailedMetrics.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableDiagnostics_IsTrue()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.EnableDiagnostics.ShouldBeTrue();
	}

	[Fact]
	public void Default_DiagnosticsInterval_IsFiveMinutes()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.DiagnosticsInterval.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_MemoryPressureThreshold_IsPointEight()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.MemoryPressureThreshold.ShouldBe(0.8);
	}

	[Fact]
	public void Default_EnableAdaptiveSizing_IsTrue()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.EnableAdaptiveSizing.ShouldBeTrue();
	}

	[Fact]
	public void Default_AdaptationInterval_IsOneMinute()
	{
		// Arrange & Act
		var options = new GlobalPoolOptions();

		// Assert
		options.AdaptationInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void EnableTelemetry_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.EnableTelemetry = true;

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void EnableDetailedMetrics_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.EnableDetailedMetrics = true;

		// Assert
		options.EnableDetailedMetrics.ShouldBeTrue();
	}

	[Fact]
	public void EnableDiagnostics_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.EnableDiagnostics = false;

		// Assert
		options.EnableDiagnostics.ShouldBeFalse();
	}

	[Fact]
	public void DiagnosticsInterval_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.DiagnosticsInterval = TimeSpan.FromMinutes(10);

		// Assert
		options.DiagnosticsInterval.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void MemoryPressureThreshold_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.MemoryPressureThreshold = 0.9;

		// Assert
		options.MemoryPressureThreshold.ShouldBe(0.9);
	}

	[Fact]
	public void EnableAdaptiveSizing_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.EnableAdaptiveSizing = false;

		// Assert
		options.EnableAdaptiveSizing.ShouldBeFalse();
	}

	[Fact]
	public void AdaptationInterval_CanBeSet()
	{
		// Arrange
		var options = new GlobalPoolOptions();

		// Act
		options.AdaptationInterval = TimeSpan.FromSeconds(30);

		// Assert
		options.AdaptationInterval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new GlobalPoolOptions
		{
			EnableTelemetry = true,
			EnableDetailedMetrics = true,
			EnableDiagnostics = false,
			DiagnosticsInterval = TimeSpan.FromMinutes(1),
			MemoryPressureThreshold = 0.7,
			EnableAdaptiveSizing = false,
			AdaptationInterval = TimeSpan.FromSeconds(45),
		};

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
		options.EnableDetailedMetrics.ShouldBeTrue();
		options.EnableDiagnostics.ShouldBeFalse();
		options.DiagnosticsInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.MemoryPressureThreshold.ShouldBe(0.7);
		options.EnableAdaptiveSizing.ShouldBeFalse();
		options.AdaptationInterval.ShouldBe(TimeSpan.FromSeconds(45));
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_HasTelemetryEnabled()
	{
		// Act
		var options = new GlobalPoolOptions
		{
			EnableTelemetry = true,
			EnableDetailedMetrics = true,
			EnableDiagnostics = true,
		};

		// Assert
		options.EnableTelemetry.ShouldBeTrue();
		options.EnableDetailedMetrics.ShouldBeTrue();
		options.EnableDiagnostics.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForMemoryConstrained_HasLowerThreshold()
	{
		// Act
		var options = new GlobalPoolOptions
		{
			MemoryPressureThreshold = 0.6,
			EnableAdaptiveSizing = true,
			AdaptationInterval = TimeSpan.FromSeconds(15),
		};

		// Assert
		options.MemoryPressureThreshold.ShouldBeLessThan(0.8);
		options.AdaptationInterval.ShouldBeLessThan(TimeSpan.FromMinutes(1));
	}

	#endregion
}
