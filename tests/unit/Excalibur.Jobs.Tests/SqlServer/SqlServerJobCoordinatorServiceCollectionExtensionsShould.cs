// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerJobCoordinatorServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterCoordinatorServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerJobCoordinator(options =>
		{
			options.ConnectionString = "Server=.;Database=Jobs;Trusted_Connection=True";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobCoordinator));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobLockProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobRegistry));
		services.ShouldContain(sd => sd.ServiceType == typeof(IJobDistributor));
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddSqlServerJobCoordinator(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerJobCoordinator(null!));
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerJobCoordinator(options =>
		{
			options.ConnectionString = "Server=.;Database=test";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}
}
