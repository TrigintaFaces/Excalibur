// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MonitoringServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterComplianceMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddComplianceMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IComplianceMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddComplianceMetrics()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddComplianceMetrics());
	}

	[Fact]
	public void RegisterKeyRotationAlerts()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddComplianceMetrics();

		// Act
		services.AddKeyRotationAlerts();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<KeyRotationAlertService>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterKeyRotationAlertsWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddKeyRotationAlerts(opts =>
		{
			opts.AlertAfterFailures = 5;
			opts.ExpirationWarningDays = 14;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<KeyRotationAlertOptions>();
		options.ShouldNotBeNull();
		options.AlertAfterFailures.ShouldBe(5);
		options.ExpirationWarningDays.ShouldBe(14);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddKeyRotationAlerts()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddKeyRotationAlerts());
	}

	[Fact]
	public void RegisterCustomAlertHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddKeyRotationAlertHandler<LoggingAlertHandler>();

		// Assert
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IKeyRotationAlertHandler) &&
			d.ImplementationType == typeof(LoggingAlertHandler));
		descriptor.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddKeyRotationAlertHandler()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddKeyRotationAlertHandler<LoggingAlertHandler>());
	}

	[Fact]
	public void RegisterComplianceMonitoring()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddComplianceMonitoring();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IComplianceMetrics>().ShouldNotBeNull();
		provider.GetService<KeyRotationAlertService>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterComplianceMonitoringWithAlertOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddComplianceMonitoring(opts => opts.NotifyOnSuccess = true);

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IComplianceMetrics>().ShouldNotBeNull();
		var options = provider.GetService<KeyRotationAlertOptions>();
		options.ShouldNotBeNull();
		options.NotifyOnSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddComplianceMonitoring()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddComplianceMonitoring());
	}
}
