// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Audit;
using Excalibur.Dispatch.Compliance;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresAuditExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull_ForConfigureOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresAuditStore(opts => opts.ConnectionString = "Host=localhost;"));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresAuditStore((Action<PostgresAuditOptions>)null!));
	}

	[Fact]
	public void RegisterAuditStoreServices()
	{
		var services = new ServiceCollection();

		services.AddPostgresAuditStore(opts =>
		{
			opts.ConnectionString = "Host=localhost;Database=test;";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(PostgresAuditStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditStore));
	}

	[Fact]
	public void ReturnSameServiceCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddPostgresAuditStore(opts =>
		{
			opts.ConnectionString = "Host=localhost;Database=test;";
		});

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_ForConnectionStringOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresAuditStore("Host=localhost;"));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(
			() => services.AddPostgresAuditStore(string.Empty));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsWhitespace()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(
			() => services.AddPostgresAuditStore("   "));
	}

	[Fact]
	public void RegisterAuditStoreServicesFromConnectionString()
	{
		var services = new ServiceCollection();

		services.AddPostgresAuditStore("Host=localhost;Database=test;");

		services.ShouldContain(sd => sd.ServiceType == typeof(PostgresAuditStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditStore));
	}

	[Fact]
	public async Task ResolveAuditStoreFromServiceProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPostgresAuditStore("Host=localhost;Database=test;");

		await using var provider = services.BuildServiceProvider();
		var auditStore = provider.GetRequiredService<IAuditStore>();
		var options = provider.GetRequiredService<IOptions<PostgresAuditOptions>>().Value;

		auditStore.ShouldBeOfType<PostgresAuditStore>();
		options.ConnectionString.ShouldBe("Host=localhost;Database=test;");
	}
}
