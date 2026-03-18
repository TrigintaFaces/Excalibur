// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for AddSqlServerSagaStore&lt;TDb&gt; (Sprint 659 T.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSagaStoreIDbOverloadShould : UnitTestBase
{
	[Fact]
	public void RegisterSagaStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new Excalibur.Dispatch.Serialization.DispatchJsonSerializer());

		services.AddSqlServerSagaStore<ISagaTestDb>();

		services.Any(sd =>
			sd.ServiceType == typeof(SqlServerSagaStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerSagaStore<ISagaTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerSagaStore<ISagaTestDb>();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AcceptConfigureOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new Excalibur.Dispatch.Serialization.DispatchJsonSerializer());

		services.AddSqlServerSagaStore<ISagaTestDb>(
			options => options.TableName = "CustomSagas");

		services.Any(sd =>
			sd.ServiceType == typeof(SqlServerSagaStore)).ShouldBeTrue();
	}

	[Fact]
	public void NotOverrideExistingRegistration()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new Excalibur.Dispatch.Serialization.DispatchJsonSerializer());
		services.AddSqlServerSagaStore<ISagaTestDb>();
		services.AddSqlServerSagaStore<ISagaTestDb>();

		services.Count(sd =>
			sd.ServiceType == typeof(SqlServerSagaStore)).ShouldBe(1);
	}
}

// Test infrastructure (file-level to avoid CA1034)

public interface ISagaTestDb : IDb;
