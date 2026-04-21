// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbServiceCollectionExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Phase C rewire: Updated from AddDynamoDb(Action&lt;DynamoDbOptions&gt;) to
/// AddExcaliburDynamoDb(Action&lt;IDynamoDBDataBuilder&gt;).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class DynamoDbServiceCollectionExtensionsShould
{
	#region AddExcaliburDynamoDb with Builder Tests

	[Fact]
	public void AddExcaliburDynamoDb_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services!.AddExcaliburDynamoDb(db => db.ServiceUrl("http://localhost:8000")));
	}

	[Fact]
	public void AddExcaliburDynamoDb_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburDynamoDb((Action<IDynamoDBDataBuilder>)null!));
	}

	[Fact]
	public void AddExcaliburDynamoDb_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburDynamoDb(db =>
			db.ServiceUrl("http://localhost:8000"));

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburDynamoDb_RegistersDynamoDbPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburDynamoDb(db =>
			db.ServiceUrl("http://localhost:8000"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbPersistenceProvider));
	}

	[Fact]
	public void AddExcaliburDynamoDb_RegistersDynamoDbHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburDynamoDb(db =>
			db.ServiceUrl("http://localhost:8000"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbHealthCheck));
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(DynamoDbServiceCollectionExtensions).IsAbstract.ShouldBeTrue();
		typeof(DynamoDbServiceCollectionExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbServiceCollectionExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
