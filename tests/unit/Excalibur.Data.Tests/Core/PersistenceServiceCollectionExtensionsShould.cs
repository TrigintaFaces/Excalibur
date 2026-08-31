// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PersistenceServiceCollectionExtensionsShould
{
	[Fact]
	public void AddPersistence_RegistersSharedServices()
	{
		var services = new ServiceCollection();

		services.AddPersistence();

		services.ShouldContain(sd => sd.ServiceType == typeof(IConnectionStringProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IStartupPrerequisiteValidator));
	}

	[Fact]
	public void AddPersistence_RegistersNonKeyedProviderAliasForwardingToDefault()
	{
		var services = new ServiceCollection();

		services.AddPersistence();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPersistenceProvider) && sd.ImplementationFactory != null);
	}

	[Fact]
	public void AddPersistence_ThrowsForNullServices()
	{
		ServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() => services!.AddPersistence());
	}

	[Fact]
	public void AddPersistence_ReturnsSameCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddPersistence();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddPersistence_RegistersPrerequisiteValidatorAsHostedService()
	{
		var services = new ServiceCollection();

		services.AddPersistence();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
			&& sd.ImplementationType == typeof(PersistencePrerequisiteValidator));
	}
}
