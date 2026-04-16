// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify service collection extension methods.
/// Phase C rewire: Updated from AddFirestore(Action&lt;FirestoreOptions&gt;) to AddExcaliburFirestore(Action&lt;IFirestoreDataBuilder&gt;).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class FirestoreServiceCollectionExtensionsShould
{
	#region AddExcaliburFirestore with Builder Tests

	[Fact]
	public void AddExcaliburFirestore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddExcaliburFirestore(fs => fs.ProjectId("test-project")));
	}

	[Fact]
	public void AddExcaliburFirestore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburFirestore((Action<IFirestoreDataBuilder>)null!));
	}

	[Fact]
	public void AddExcaliburFirestore_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburFirestore(fs => fs.ProjectId("test-project"));

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburFirestore_RegistersFirestorePersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburFirestore(fs => fs.ProjectId("test-project"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestorePersistenceProvider));
	}

	[Fact]
	public void AddExcaliburFirestore_RegistersFirestoreHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburFirestore(fs => fs.ProjectId("test-project"));

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
