// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceServiceCollectionExtensionsShould
{
	[Fact]
	public void AddPersistence_RegistersCoreServices()
	{
		var services = new ServiceCollection();

		services.AddPersistence();

		services.ShouldContain(sd => sd.ServiceType == typeof(IPersistenceConfiguration));
		services.ShouldContain(sd => sd.ServiceType == typeof(IPersistenceProviderFactory));
		services.ShouldContain(sd => sd.ServiceType == typeof(IConnectionStringProvider));
	}

	[Fact]
	public void AddPersistence_WithConfigure_RegistersCoreServices()
	{
		var services = new ServiceCollection();

		services.AddPersistence(config =>
		{
			config.DefaultProvider = "test";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IPersistenceConfiguration));
	}

	[Fact]
	public void AddPersistence_ThrowsForNullServices()
	{
		ServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() => services!.AddPersistence());
	}

	[Fact]
	public void AddPersistence_ThrowsForNullConfigure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddPersistence((Action<PersistenceConfiguration>)null!));
	}

	[Fact]
	public void AddPersistence_ReturnsSameCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddPersistence();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddPersistence_RegistersConfigurationValidator()
	{
		var services = new ServiceCollection();

		services.AddPersistence();

		// The hosted service for validation should be registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
	}
}
