// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Migration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class MigrationServiceCollectionExtensionsShould
{
	[Theory]
	[InlineData(false, 0)]
	[InlineData(false, -1)]
	[InlineData(true, 0)]
	[InlineData(true, -1)]
	public void RefuseNonPositiveConcurrencyThroughPublicCompositionAtStartup(bool configuration, int concurrency)
	{
		var services = new ServiceCollection();
		RegisterOptions(services, configuration, concurrency);
		using var provider = services.BuildServiceProvider();
		var validator = provider.GetRequiredService<IStartupValidator>();
		var error = Should.Throw<OptionsValidationException>(() => validator.Validate());
		error.OptionsType.ShouldBe(typeof(MigrationOptions));
		error.Failures.ShouldContain(failure => failure.Contains(nameof(MigrationOptions.MaxConcurrentMigrations), StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ValidatePositiveConcurrencyWithoutResolvingMigrationInfrastructure(bool configuration)
	{
		var services = new ServiceCollection();
		RegisterOptions(services, configuration, 8);
		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IStartupValidator>().Validate();
		var options = provider.GetRequiredService<IOptions<MigrationOptions>>().Value;
		options.MaxConcurrentMigrations.ShouldBe(8);
		options.MigrationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ValidateDefaultMigrationOptionsAtStartup()
	{
		var services = new ServiceCollection();
		services.AddEncryptionMigration();
		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IStartupValidator>().Validate();
		provider.GetRequiredService<IOptions<MigrationOptions>>().Value.MaxConcurrentMigrations.ShouldBe(4);
	}

	private static void RegisterOptions(IServiceCollection services, bool configuration, int concurrency)
	{
		if (configuration)
		{
			var settings = new Dictionary<string, string?> { [nameof(MigrationOptions.MaxConcurrentMigrations)] = concurrency.ToString(System.Globalization.CultureInfo.InvariantCulture) };
			services.AddEncryptionMigration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
		}
		else
		{
			services.AddEncryptionMigration(options => options.MaxConcurrentMigrations = concurrency);
		}
	}

	[Fact]
	public void RegisterMigrationServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddComplianceEncryption(_ => { });

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
		services.AddComplianceEncryption(_ => { });

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
		services.AddComplianceEncryption(_ => { });
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
