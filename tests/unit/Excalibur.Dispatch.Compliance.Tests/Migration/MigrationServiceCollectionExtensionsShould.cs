// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterMigrationServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddComplianceEncryption();

		// Act
		services.AddEncryptionMigration();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMigrationService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterMigrationServicesWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddComplianceEncryption();

		// Act
		services.AddEncryptionMigration(opts =>
		{
			opts.TargetVersion = EncryptionVersion.Version11;
			opts.MaxConcurrentMigrations = 8;
			opts.EnableLazyReEncryption = true;
		});

		// Assert
		services.Any(d => d.ServiceType == typeof(IMigrationService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddEncryptionMigration());
	}

	[Fact]
	public void ResolveMigrationServiceFromProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddComplianceEncryption();
		services.AddComplianceMetrics();
		services.AddEncryptionMigration();

		var provider = services.BuildServiceProvider();

		// Act
		var migrationService = provider.GetService<IMigrationService>();

		// Assert
		migrationService.ShouldNotBeNull();
		migrationService.ShouldBeOfType<MigrationService>();
	}
}
