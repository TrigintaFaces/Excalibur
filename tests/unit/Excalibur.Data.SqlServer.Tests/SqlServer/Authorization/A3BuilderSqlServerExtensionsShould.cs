// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.SqlServer.Authorization;

namespace Excalibur.Data.Tests.SqlServer.Authorization;

/// <summary>
/// Unit tests for <see cref="A3BuilderSqlServerExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
[Trait("Feature", "DependencyInjection")]
public sealed class A3BuilderSqlServerExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		IA3Builder? builder = null;

		Should.Throw<ArgumentNullException>(() => builder!.UseSqlServer());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddExcaliburA3();

		// Act
		var result = builder.UseSqlServer();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(builder);
	}

	[Fact]
	public void RegisterIGrantStore_AsSqlServerGrantStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseSqlServer();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IGrantStore) &&
			sd.ImplementationType == typeof(SqlServerGrantStore));
	}

	[Fact]
	public void RegisterIActivityGroupStore_AsSqlServerActivityGroupStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburA3().UseSqlServer();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IActivityGroupStore) &&
			sd.ImplementationType == typeof(SqlServerActivityGroupStore));
	}
}
