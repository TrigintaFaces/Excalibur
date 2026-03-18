// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for AddSqlServerProjectionStore&lt;TProjection, TDb&gt; (Sprint 659 T.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerProjectionStoreIDbOverloadShould : UnitTestBase
{
	[Fact]
	public void RegisterProjectionStore_WithIDbTypedOverload()
	{
		var services = new ServiceCollection();

		services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>();

		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<ProjectionIDbTestRecord>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenServicesNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>());
	}

	[Fact]
	public void ReturnServicesForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>();

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AcceptConfigureOptions()
	{
		var services = new ServiceCollection();

		services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>(
			options => options.TableName = "CustomProjections");

		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<ProjectionIDbTestRecord>)).ShouldBeTrue();
	}

	[Fact]
	public void NotOverrideExistingRegistration()
	{
		var services = new ServiceCollection();
		services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>();
		services.AddSqlServerProjectionStore<ProjectionIDbTestRecord, IProjectionTestDb>();

		services.Count(sd =>
			sd.ServiceType == typeof(IProjectionStore<ProjectionIDbTestRecord>)).ShouldBe(1);
	}
}

// Test infrastructure (file-level to avoid CA1034)

public interface IProjectionTestDb : IDb;

public sealed class ProjectionIDbTestRecord
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
}
