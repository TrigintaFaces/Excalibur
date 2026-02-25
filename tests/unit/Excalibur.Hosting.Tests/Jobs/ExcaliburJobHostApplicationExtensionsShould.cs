// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Metrics;

namespace Excalibur.Hosting.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="ExcaliburJobHostApplicationExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Jobs")]
[Trait("Feature", "Middleware")]
public sealed class ExcaliburJobHostApplicationExtensionsShould : UnitTestBase
{
	#region UseExcaliburJobHost Tests

	[Fact]
	public void UseExcaliburJobHost_ThrowsArgumentNullException_WhenAppIsNull()
	{
		// Arrange
		IApplicationBuilder? app = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			app.UseExcaliburJobHost());
	}

	[Fact]
	public void UseExcaliburJobHost_ThrowInvalidOperation_WhenMetricsServicesAreMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();
		_ = services.AddRouting();
		_ = services.AddHealthChecks();
		var app = new ApplicationBuilder(services.BuildServiceProvider());

		// Act
		var ex = Should.Throw<InvalidOperationException>(() => app.UseExcaliburJobHost());

		// Assert
		ex.Message.ShouldContain("MeterProvider");
	}

	[Fact]
	public void UseExcaliburJobHost_ReturnsSameBuilder_WhenRequiredServicesAreRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services);
		_ = services.AddLogging();
		_ = services.AddHealthChecks();
		_ = services.AddOpenTelemetry().WithMetrics(static metrics => metrics.AddPrometheusExporter());
		var app = new ApplicationBuilder(services.BuildServiceProvider());

		// Act
		var result = app.UseExcaliburJobHost();

		// Assert
		result.ShouldBeSameAs(app);
	}

	#endregion
}
