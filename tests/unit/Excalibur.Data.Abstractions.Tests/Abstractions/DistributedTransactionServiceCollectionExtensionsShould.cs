// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Transactions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class DistributedTransactionServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterTransactionServicesWithConfiguration()
	{
		var services = new ServiceCollection();

		services.AddDistributedTransactions(opts =>
		{
			opts.Timeout = TimeSpan.FromSeconds(60);
		});

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDistributedTransactionCoordinator));
	}

	[Fact]
	public void RegisterTransactionServicesWithDefaults()
	{
		var services = new ServiceCollection();

		services.AddDistributedTransactions();

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDistributedTransactionCoordinator));
	}

	[Fact]
	public void ThrowForNullServicesWithConfig()
	{
		ServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(
			() => services!.AddDistributedTransactions(_ => { }));
	}

	[Fact]
	public void ThrowForNullConfigure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddDistributedTransactions(null!));
	}

	[Fact]
	public void ReturnSameServiceCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddDistributedTransactions(_ => { });

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ReturnSameServiceCollectionForDefaultOverload()
	{
		var services = new ServiceCollection();

		var result = services.AddDistributedTransactions();

		result.ShouldBeSameAs(services);
	}
}
