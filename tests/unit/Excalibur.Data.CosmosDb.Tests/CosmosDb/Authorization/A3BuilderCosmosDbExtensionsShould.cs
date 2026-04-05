// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

namespace Excalibur.Data.Tests.CosmosDb.Authorization;

/// <summary>
/// Unit tests for <see cref="A3BuilderCosmosDbExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class A3BuilderCosmosDbExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		IA3Builder? builder = null;

		Should.Throw<ArgumentNullException>(() =>
			builder!.UseCosmosDb(options => { options.Client.ConnectionString = "test"; }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb(null!));
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UseCosmosDb(options =>
		{
			options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
		});

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(builder);
	}

	[Fact]
	public void RegisterIGrantStore_AsCosmosDbGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseCosmosDb(options =>
		{
			options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(CosmosDbGrantStore));
	}

	[Fact]
	public void RegisterIActivityGroupGrantStore_AsCosmosDbActivityGroupGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseCosmosDb(options =>
		{
			options.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupGrantStore) &&
			sd.ImplementationType == typeof(CosmosDbActivityGroupGrantStore));
	}
}
