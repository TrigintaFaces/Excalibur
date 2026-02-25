// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MySql;

namespace Excalibur.Data.Tests.MySql;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MySqlServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull_ForConfigureOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddExcaliburMySql(opts => opts.ConnectionString = "Server=localhost;"));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddExcaliburMySql((Action<MySqlProviderOptions>)null!));
	}

	[Fact]
	public void RegisterMySqlServices()
	{
		var services = new ServiceCollection();

		services.AddExcaliburMySql(opts =>
		{
			opts.ConnectionString = "Server=localhost;Database=test;";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(MySqlPersistenceProvider));
	}

	[Fact]
	public void ReturnSameServiceCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddExcaliburMySql(opts =>
		{
			opts.ConnectionString = "Server=localhost;Database=test;";
		});

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_ForConfigSectionOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddExcaliburMySql("MySql"));
	}

	[Fact]
	public void ThrowWhenConfigSectionIsEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(
			() => services.AddExcaliburMySql(string.Empty));
	}

	[Fact]
	public void RegisterMySqlServicesFromConfigSection()
	{
		var services = new ServiceCollection();

		services.AddExcaliburMySql("MySql");

		services.ShouldContain(sd => sd.ServiceType == typeof(MySqlPersistenceProvider));
	}
}
