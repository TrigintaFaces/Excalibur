// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExcaliburDataServiceCollectionExtensionsShould
{
	[Fact]
	public void AddExcaliburDataServices_RegistersJsonSerializer()
	{
		var services = new ServiceCollection();

		services.AddExcaliburDataServices();

		services.ShouldContain(sd => sd.ServiceType == typeof(IJsonSerializer));
	}

	[Fact]
	public void AddExcaliburDataServices_ReturnsSameCollection()
	{
		var services = new ServiceCollection();

		var result = services.AddExcaliburDataServices();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburDataServicesWithPersistence_ActionOverload_RegistersServices()
	{
		var services = new ServiceCollection();

		services.AddExcaliburDataServicesWithPersistence(config =>
		{
			config.DefaultProvider = "test";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IJsonSerializer));
	}

	[Fact]
	public void AddExcaliburDataServicesWithPersistence_ActionOverload_ThrowsForNullAction()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddExcaliburDataServicesWithPersistence((Action<PersistenceConfiguration>)null!));
	}
}
