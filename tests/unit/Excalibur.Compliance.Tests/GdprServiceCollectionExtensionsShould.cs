// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Retention;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Stores.MongoDb;
using Excalibur.Compliance.Stores.Postgres;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GdprServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterCascadeErasureService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCascadeErasure();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICascadeErasureService));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(CascadeErasureService));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddCascadeErasure()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddCascadeErasure());
	}

	[Fact]
	public void RegisterDataPortabilityService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataPortability();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataPortabilityService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterDataPortabilityWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDataPortability(opts => opts.MaxExportSize = 1024 * 1024);

		// Assert
		services.Any(d => d.ServiceType == typeof(IDataPortabilityService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddDataPortability()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddDataPortability());
	}

	[Fact]
	public void RegisterSubjectAccessRequests()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSubjectAccessRequests();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISubjectAccessService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterSubjectAccessRequestsWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSubjectAccessRequests(opts => opts.ResponseDeadlineDays = 15);

		// Assert
		services.Any(d => d.ServiceType == typeof(ISubjectAccessService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddSubjectAccessRequests()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddSubjectAccessRequests());
	}

	[Fact]
	public void RegisterAuditLogEncryption()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAuditLogEncryption();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditLogEncryptor));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void RegisterAuditLogEncryptionWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAuditLogEncryption(opts => opts.EncryptionAlgorithm = EncryptionAlgorithm.Aes256Gcm);

		// Assert
		services.Any(d => d.ServiceType == typeof(IAuditLogEncryptor)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddAuditLogEncryption()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddAuditLogEncryption());
	}

	[Fact]
	public void RegisterKeyEscrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddKeyEscrow();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKeyEscrowService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddKeyEscrow()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddKeyEscrow());
	}

	[Fact]
	public void RegisterBreachNotification()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddBreachNotification();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IBreachNotificationService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterBreachNotificationWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddBreachNotification(opts => opts.NotificationDeadlineHours = 48);

		// Assert
		services.Any(d => d.ServiceType == typeof(IBreachNotificationService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddBreachNotification()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddBreachNotification());
	}

	[Fact]
	public void RegisterRetentionEnforcement()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRetentionEnforcement();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRetentionEnforcementService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddRetentionEnforcement()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddRetentionEnforcement());
	}

	[Fact]
	public void RegisterConsentManagement()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConsentManagement();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConsentService));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterConsentManagementWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConsentManagement(opts => opts.DefaultExpirationDays = 365);

		// Assert
		services.Any(d => d.ServiceType == typeof(IConsentService)).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddConsentManagement()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddConsentManagement());
	}

	[Fact]
	public void RegisterPostgresComplianceStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresComplianceStore(opts => opts.ConnectionString = "Host=localhost");

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IComplianceStore));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddPostgresComplianceStore()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() =>
			services!.AddPostgresComplianceStore(opts => opts.ConnectionString = "Host=localhost"));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_AddPostgresComplianceStore()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddPostgresComplianceStore((Action<PostgresComplianceOptions>)null!));
	}

	[Fact]
	public void RegisterMongoDbComplianceStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMongoDbComplianceStore(opts => opts.ConnectionString = "mongodb://localhost");

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IComplianceStore));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_AddMongoDbComplianceStore()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() =>
			services!.AddMongoDbComplianceStore(opts => opts.ConnectionString = "mongodb://localhost"));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_AddMongoDbComplianceStore()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddMongoDbComplianceStore((Action<MongoDbComplianceOptions>)null!));
	}

	[Fact]
	public void RegisterRetentionEnforcementIdempotently()
	{
		// Arrange — calling AddRetentionEnforcement twice must not duplicate
		// the hosted service registration (Bug #18)
		var services = new ServiceCollection();

		// Act
		services.AddRetentionEnforcement();
		services.AddRetentionEnforcement();

		// Assert — exactly one RetentionEnforcementBackgroundService
		var bgServiceDescriptors = services
			.Where(d => d.ServiceType == typeof(RetentionEnforcementBackgroundService))
			.ToList();
		bgServiceDescriptors.Count.ShouldBe(1,
			"Duplicate AddRetentionEnforcement calls must not double-register the background service");
	}

	// ---- REAL-CONTAINER RESOLVE LOCKS ----------------------------------------------------------
	//
	// Every other arm in this file asserts REGISTRATION PRESENCE -- a non-null ServiceDescriptor
	// with the expected lifetime. A descriptor is still non-null when the registered type's
	// constructor dependencies CANNOT BE SATISFIED, so those arms stay green over a registration
	// that throws the first time a consumer resolves it. That is the whole defect class here: the
	// store works when a test hands it its dependencies, and says nothing about whether the real
	// container can supply them.
	//
	// These arms build a REAL ServiceProvider through the PRODUCTION registration path and resolve
	// the service, which is the only thing that distinguishes "registered" from "usable".
	//
	// MongoDbComplianceStore's constructors take ITenantContext. A nullable reference annotation
	// does NOT make a DI parameter optional -- Microsoft.Extensions.DependencyInjection has no
	// notion of an optional dependency -- so a registration that does not also register a tenant
	// context produces a descriptor that cannot be constructed.
	//
	// AddLogging() is deliberately present in each arm and is NOT part of the defect. The stores
	// take ILogger<T>, which only a host or an explicit AddLogging() supplies, and every realistic
	// consumer has one. Omitting it makes EVERY compliance-store registration unresolvable --
	// including the ones that are correct -- which turns this lock into a false accusation against
	// working code rather than a test of the registration under scrutiny. The arms assert what a
	// consumer with a normal host gets; the tenant context is the thing the registration itself
	// must supply.

	[Fact]
	public void ResolveMongoDbComplianceStoreFromARealContainer_ConfigureOptionsOverload()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddMongoDbComplianceStore(opts => opts.ConnectionString = "mongodb://localhost");

		using var provider = services.BuildServiceProvider(validateScopes: true);

		var store = Should.NotThrow(
			() => provider.GetRequiredService<IComplianceStore>(),
			"AddMongoDbComplianceStore must register everything MongoDbComplianceStore needs to be "
			+ "constructed. A consumer who calls this method and nothing else must get a working "
			+ "store, not an InvalidOperationException on first resolve.");

		store.ShouldBeOfType<MongoDbComplianceStore>();
	}

	[Fact]
	public void ResolveMongoDbComplianceStoreFromARealContainer_ConfigurationOverload()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionString"] = "mongodb://localhost"
			})
			.Build();

		services.AddMongoDbComplianceStore(configuration);

		using var provider = services.BuildServiceProvider(validateScopes: true);

		var store = Should.NotThrow(
			() => provider.GetRequiredService<IComplianceStore>(),
			"Both AddMongoDbComplianceStore overloads must be independently resolvable. Fixing only "
			+ "the overload a test happens to exercise leaves the other one broken for consumers.");

		store.ShouldBeOfType<MongoDbComplianceStore>();
	}

	// CONTROL -- the same property on the provider that already satisfies it. If this arm ever fails
	// alongside the Mongo arms, the fault is in this lock or in the shared registration path, not in
	// the Mongo-specific omission. It also stops the arms above from being "fixed" by weakening the
	// store's dependency instead of completing the registration.
	[Fact]
	public void ResolvePostgresComplianceStoreFromARealContainer_AsTheStructuralControl()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddPostgresComplianceStore(opts => opts.ConnectionString = "Host=localhost;Database=test");

		using var provider = services.BuildServiceProvider(validateScopes: true);

		var store = Should.NotThrow(() => provider.GetRequiredService<IComplianceStore>());

		store.ShouldBeOfType<PostgresComplianceStore>();
	}
}
