// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify service collection extension methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "DependencyInjection")]
public sealed class FirestoreServiceCollectionExtensionsShould
{
	#region AddFirestore with Action Tests

	[Fact]
	public void AddFirestore_WithAction_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore(options => { }));
	}

	[Fact]
	public void AddFirestore_WithAction_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore((Action<FirestoreOptions>)null!));
	}

	[Fact]
	public void AddFirestore_WithAction_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddFirestore_WithAction_RegistersFirestorePersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestorePersistenceProvider));
	}

	[Fact]
	public void AddFirestore_WithAction_RegistersFirestoreHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestore(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestoreHealthCheck));
	}

	#endregion

	#region AddFirestore with Configuration Tests

	[Fact]
	public void AddFirestore_WithConfiguration_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore(configuration));
	}

	[Fact]
	public void AddFirestore_WithConfiguration_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore((IConfiguration)null!));
	}

	[Fact]
	public void AddFirestore_WithConfiguration_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ProjectId"] = "test-project"
			})
			.Build();

		// Act
		var result = services.AddFirestore(configuration);

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddFirestore with Configuration and Section Tests

	[Fact]
	public void AddFirestore_WithSection_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore(configuration, "Firestore"));
	}

	[Fact]
	public void AddFirestore_WithSection_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestore(null!, "Firestore"));
	}

	[Fact]
	public void AddFirestore_WithSection_ThrowsArgumentException_WhenSectionNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestore(configuration, null!));
	}

	[Fact]
	public void AddFirestore_WithSection_ThrowsArgumentException_WhenSectionNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddFirestore(configuration, string.Empty));
	}

	[Fact]
	public void AddFirestore_WithSection_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Firestore:ProjectId"] = "test-project"
			})
			.Build();

		// Act
		var result = services.AddFirestore(configuration, "Firestore");

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddFirestoreWithDatabase Tests

	[Fact]
	public void AddFirestoreWithDatabase_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreWithDatabase(options => { }));
	}

	[Fact]
	public void AddFirestoreWithDatabase_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddFirestoreWithDatabase(null!));
	}

	[Fact]
	public void AddFirestoreWithDatabase_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddFirestoreWithDatabase(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddFirestoreWithDatabase_RegistersFirestoreHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddFirestoreWithDatabase(options =>
		{
			options.ProjectId = "test-project";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestoreHealthCheck));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(FirestoreServiceCollectionExtensions).IsAbstract.ShouldBeTrue();
		typeof(FirestoreServiceCollectionExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(FirestoreServiceCollectionExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
