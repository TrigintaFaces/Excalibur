// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterSoc2ComplianceServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2Compliance();

		// Assert
		services.Any(d => d.ServiceType == typeof(ISoc2ComplianceService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IControlValidationService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(ISoc2ReportGenerator)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(ISoc2ReportExporter)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterSoc2ComplianceWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2Compliance(opts => opts.EnableContinuousMonitoring = false);

		// Assert
		services.Any(d => d.ServiceType == typeof(ISoc2ComplianceService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterInMemorySoc2ReportStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddInMemorySoc2ReportStore();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<ISoc2ReportStore>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCustomReportStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2ReportStore<InMemorySoc2ReportStore>();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISoc2ReportStore));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(InMemorySoc2ReportStore));
	}

	[Fact]
	public void RegisterControlValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddControlValidator<EncryptionControlValidator>();

		// Assert
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IControlValidator) &&
			d.ImplementationType == typeof(EncryptionControlValidator));
		descriptor.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterSoc2ComplianceWithBuiltInValidators()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2ComplianceWithBuiltInValidators();

		// Assert
		var validatorDescriptors = services.Where(d => d.ServiceType == typeof(IControlValidator)).ToList();
		validatorDescriptors.Count.ShouldBeGreaterThanOrEqualTo(5);
	}

	[Fact]
	public void RegisterSoc2ContinuousMonitoring()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2ContinuousMonitoring();

		// Assert
		services.Any(d => d.ServiceType == typeof(IComplianceAlertHandler)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IHostedService)).ShouldBeTrue();
	}

	[Fact]
	public void RegisterCustomAlertHandler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddComplianceAlertHandler<LoggingComplianceAlertHandler>();

		// Assert
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(IComplianceAlertHandler) &&
			d.ImplementationType == typeof(LoggingComplianceAlertHandler));
		descriptor.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterSoc2ComplianceWithMonitoring()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSoc2ComplianceWithMonitoring();

		// Assert
		services.Any(d => d.ServiceType == typeof(ISoc2ComplianceService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IControlValidator)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IHostedService)).ShouldBeTrue();
		services.Any(d => d.ServiceType == typeof(IComplianceAlertHandler)).ShouldBeTrue();
	}
}
