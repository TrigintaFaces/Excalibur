// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class DataProviderCircuitBreakerServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterCircuitBreakerServices()
	{
		var services = new ServiceCollection();

		services.AddDataProviderCircuitBreaker(opts =>
		{
			opts.FailureThreshold = 5;
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(CircuitBreakerDataProvider));
	}

	[Fact]
	public void ThrowForNullServices()
	{
		ServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(
			() => services!.AddDataProviderCircuitBreaker(_ => { }));
	}

	[Fact]
	public void ThrowForNullConfigure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddDataProviderCircuitBreaker(null!));
	}

	[Fact]
	public void ReturnSameServiceCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddDataProviderCircuitBreaker(_ => { });

		result.ShouldBeSameAs(services);
	}
}
