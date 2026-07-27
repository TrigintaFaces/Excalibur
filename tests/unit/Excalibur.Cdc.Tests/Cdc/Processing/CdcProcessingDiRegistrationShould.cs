// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;
using Excalibur.Cdc.SqlServer;

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
	public void EnableBackgroundProcessing_WithProvider_RegistersHostedServiceDescriptor()
	{
		// Arrange & Act — new contract (bsiqh1): CdcProcessingHostedService is registered ONLY when a
		// provider supplies an ICdcBackgroundProcessor. A provider (UseSqlServer) is required.
		_ = Services.AddCdcProcessor(cdc =>
		{
			_ = cdc.UseSqlServer(sql => sql.ConnectionString("Server=localhost;Database=test;Trusted_Connection=true;"))
			   .EnableBackgroundProcessing();
		});

		// Assert — with a processor present, the hosted service descriptor should be registered
		var hostedServiceDescriptors = Services
			.Where(d => d.ServiceType == typeof(IHostedService))
			.ToList();

		hostedServiceDescriptors
			.ShouldContain(d => d.ImplementationType == typeof(CdcProcessingHostedService),
				"EnableBackgroundProcessing() with a background-processing provider must register CdcProcessingHostedService as IHostedService");
	}

	[Fact]
	public void EnableBackgroundProcessing_WithoutProvider_RegistersStartupValidatorAndHostedService()
	{
		// Arrange & Act — current contract (cs3948, order-independent): without a provider, BOTH the startup
		// validator AND CdcProcessingHostedService are registered. The host takes IServiceProvider and
		// resolves ICdcBackgroundProcessor lazily at StartAsync (so a provider registered AFTER
		// AddCdcProcessor() is still picked up); when no processor is registered at all it fails LOUD and
		// no-ops rather than validate-green-and-silently-skip. The startup validator runs first and throws
		// the actionable message at host start.
		_ = Services.AddCdcProcessor(cdc =>
		{
			_ = cdc.EnableBackgroundProcessing();
		});

		var hostedServiceImpls = Services
			.Where(d => d.ServiceType == typeof(IHostedService))
			.Select(d => d.ImplementationType)
			.ToList();

		hostedServiceImpls.ShouldContain(typeof(CdcBackgroundProcessingStartupValidator),
			"EnableBackgroundProcessing() without a provider must register the startup validator that fails fast");
		hostedServiceImpls.ShouldContain(typeof(CdcProcessingHostedService),
			"CdcProcessingHostedService is registered unconditionally (order-independent); it resolves the processor lazily and fails loud at runtime when none is registered");
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
			_ = cdc.UseSqlServer(sql => sql.ConnectionString("Server=localhost;Database=test;Trusted_Connection=true;"))
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
