// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Data.SqlClient;

using Excalibur.Outbox.SqlServer;

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Unit tests for AddSqlServerOutboxStore&lt;TDb&gt; (Sprint 659 T.2, multi-transport outbox).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxIDbOverloadShould : UnitTestBase
{
	[Fact]
	public void RegisterOutboxStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddSqlServerOutboxStore<IOutboxTestDb>(
			options => options.ConnectionString = "Server=localhost;Database=TestDb;");

		services.Any(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerOutboxStore<IOutboxTestDb>(
				options => options.ConnectionString = "Server=localhost;"));
	}

	[Fact]
	public void ThrowWhenConfigureNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerOutboxStore<IOutboxTestDb>(
				(Action<SqlServerOutboxOptions>)null!));
	}

	[Fact]
	public void ReturnServicesForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerOutboxStore<IOutboxTestDb>(
			options => options.ConnectionString = "Server=localhost;");

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterMultiTransportOutboxStore()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddSqlServerOutboxStore<IOutboxTestDb>(
			options => options.ConnectionString = "Server=localhost;Database=TestDb;");

		services.Any(sd =>
			sd.ServiceType == typeof(IMultiTransportOutboxStore)).ShouldBeTrue();
	}
}

// Test infrastructure (file-level to avoid CA1034)

public interface IOutboxTestDb : IDb;
