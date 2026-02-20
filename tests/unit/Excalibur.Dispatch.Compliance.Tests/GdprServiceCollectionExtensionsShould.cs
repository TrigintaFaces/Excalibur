// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compliance.Tests;

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
			services.AddPostgresComplianceStore(null!));
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
			services.AddMongoDbComplianceStore(null!));
	}
}
