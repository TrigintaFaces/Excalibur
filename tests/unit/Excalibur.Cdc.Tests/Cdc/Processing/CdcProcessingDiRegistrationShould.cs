// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;
using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Integration tests verifying DI registration for CDC background processing.
/// Ensures that <c>EnableBackgroundProcessing()</c> correctly registers
/// the hosted service and that provider extensions register <see cref="ICdcBackgroundProcessor"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CdcProcessing")]
[Trait("Priority", "0")]
public sealed class CdcProcessingDiRegistrationShould : UnitTestBase
{
	[Fact]
	public void EnableBackgroundProcessing_RegistersHostedServiceDescriptor()
	{
		// Arrange & Act
		_ = Services.AddCdcProcessor(cdc =>
		{
			_ = cdc.EnableBackgroundProcessing();
		});

		// Assert — the hosted service descriptor should be registered
		var hostedServiceDescriptors = Services
			.Where(d => d.ServiceType == typeof(IHostedService))
			.ToList();

		hostedServiceDescriptors
			.ShouldContain(d => d.ImplementationType == typeof(CdcProcessingHostedService),
				"EnableBackgroundProcessing() should register CdcProcessingHostedService as IHostedService");
	}

	[Fact]
	public void EnableBackgroundProcessing_RegistersCdcProcessingOptions()
	{
		// Arrange & Act
		_ = Services.AddCdcProcessor(cdc =>
		{
			_ = cdc.EnableBackgroundProcessing();
		});

		// Assert — CdcProcessingOptions should be registered
		BuildServiceProvider();
		var options = GetRequiredService<IOptions<CdcProcessingOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
		options.Value.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void WithoutEnableBackgroundProcessing_DoesNotRegisterHostedService()
	{
		// Arrange & Act
		_ = Services.AddCdcProcessor(cdc =>
		{
			// Do not call EnableBackgroundProcessing()
		});

		// Assert — no hosted service should be registered
		var hostedServiceDescriptors = Services
			.Where(d => d.ServiceType == typeof(IHostedService)
				&& d.ImplementationType == typeof(CdcProcessingHostedService))
			.ToList();

		hostedServiceDescriptors.ShouldBeEmpty(
			"Without EnableBackgroundProcessing(), CdcProcessingHostedService should not be registered");
	}

	[Fact]
	public void UseSqlServer_RegistersICdcBackgroundProcessor()
	{
		// Arrange & Act
		_ = Services.AddCdcProcessor(cdc =>
		{
			_ = cdc.UseSqlServer("Server=localhost;Database=test;Trusted_Connection=true;")
			   .EnableBackgroundProcessing();
		});

		// Assert — ICdcBackgroundProcessor descriptor should be present
		var processorDescriptors = Services
			.Where(d => d.ServiceType == typeof(ICdcBackgroundProcessor))
			.ToList();

		processorDescriptors.ShouldNotBeEmpty(
			"UseSqlServer() should register ICdcBackgroundProcessor for the hosted service");
	}
}
