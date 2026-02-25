// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.Telemetry;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SagaTelemetryExtensions"/>.
/// Verifies telemetry instrumentation registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTelemetryExtensionsShould
{
	#region AddSagaInstrumentation Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaTelemetryExtensions.AddSagaInstrumentation(null!));
	}

	[Fact]
	public void ReturnServiceCollection_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaInstrumentation();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void NotThrow_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			services.AddSagaInstrumentation();
			services.AddSagaInstrumentation();
			services.AddSagaInstrumentation();
		});
	}

	[Fact]
	public void InitializeSagaMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaInstrumentation();

		// Assert - verify SagaMetrics is accessible after initialization
		SagaMetrics.MeterName.ShouldBe("Excalibur.Dispatch.Sagas");
	}

	[Fact]
	public void InitializeSagaActivitySource()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaInstrumentation();

		// Assert - verify SagaActivitySource is accessible after initialization
		SagaActivitySource.SourceName.ShouldBe("Excalibur.Dispatch.Sagas");
	}

	[Fact]
	public void WorkWithEmptyServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaInstrumentation();
		var provider = services.BuildServiceProvider();

		// Assert
		result.ShouldNotBeNull();
		provider.ShouldNotBeNull();
	}

	[Fact]
	public void WorkWithPopulatedServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		// Act
		var result = services.AddSagaInstrumentation();

		// Assert
		result.ShouldNotBeNull();
	}

	#endregion
}
