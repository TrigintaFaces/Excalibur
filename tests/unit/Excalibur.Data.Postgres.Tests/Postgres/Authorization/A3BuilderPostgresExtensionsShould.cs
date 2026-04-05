// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Postgres.Authorization;

namespace Excalibur.Data.Tests.Postgres.Authorization;

/// <summary>
/// Unit tests for <see cref="A3BuilderPostgresExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class A3BuilderPostgresExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		IA3Builder? builder = null;

		Should.Throw<ArgumentNullException>(() => builder!.UsePostgres());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UsePostgres();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(builder);
	}

	[Fact]
	public void RegisterIGrantStore_AsPostgresGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UsePostgres();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(PostgresGrantStore));
	}

	[Fact]
	public void RegisterIActivityGroupStore_AsPostgresActivityGroupStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UsePostgres();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupStore) &&
			sd.ImplementationType == typeof(PostgresActivityGroupStore));
	}
}
