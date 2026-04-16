// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerLeaderElectionBuilderExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerLeaderElectionExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=test;Integrated Security=true;";
	private const string TestLockResource = "TestApp.Leader";

	[Fact]
	public void UseSqlServer_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(le =>
			le.UseSqlServer(sql => sql
				.ConnectionString(TestConnectionString)
				.LockResource(TestLockResource)));

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(SqlServerLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void UseSqlServer_WithOptions_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(le =>
			le.UseSqlServer(sql => sql
				.ConnectionString(TestConnectionString)
				.LockResource(TestLockResource))
			.WithOptions(options =>
			{
				options.LeaseDuration = TimeSpan.FromSeconds(30);
			}));

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(SqlServerLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullBuilder()
	{
		// Arrange
		ILeaderElectionBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql => sql
				.ConnectionString(TestConnectionString)
				.LockResource(TestLockResource)));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new LeaderElectionBuilder(services);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(null!));
	}

	[Fact]
	public void UseSqlServer_ConnectionString_ThrowsOnEmpty()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
		{
			var services = new ServiceCollection();
			_ = services.AddExcaliburLeaderElection(le =>
				le.UseSqlServer(sql => sql
					.ConnectionString("")
					.LockResource(TestLockResource)));
		});
	}

	[Fact]
	public void UseSqlServer_ConnectionString_ThrowsOnWhitespace()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
		{
			var services = new ServiceCollection();
			_ = services.AddExcaliburLeaderElection(le =>
				le.UseSqlServer(sql => sql
					.ConnectionString("   ")
					.LockResource(TestLockResource)));
		});
	}

	[Fact]
	public void UseSqlServer_LockResource_ThrowsOnNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
		{
			var services = new ServiceCollection();
			_ = services.AddExcaliburLeaderElection(le =>
				le.UseSqlServer(sql => sql
					.ConnectionString(TestConnectionString)
					.LockResource(null!)));
		});
	}

	[Fact]
	public void UseSqlServer_LockResource_ThrowsOnEmpty()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
		{
			var services = new ServiceCollection();
			_ = services.AddExcaliburLeaderElection(le =>
				le.UseSqlServer(sql => sql
					.ConnectionString(TestConnectionString)
					.LockResource("")));
		});
	}

	[Fact]
	public void UseSqlServer_ResolvesAsTelemetryWrapper()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddExcaliburLeaderElection(le =>
			le.UseSqlServer(sql => sql
				.ConnectionString(TestConnectionString)
				.LockResource(TestLockResource)));

		// Assert — Verify keyed ILeaderElection("sqlserver") descriptor is registered with factory
		// that produces TelemetryLeaderElection. Cannot resolve without real SQL Server,
		// so check descriptor registration instead.
		var descriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.IsKeyedService &&
			sd.ServiceKey is string key &&
			key == "sqlserver" &&
			sd.Lifetime == ServiceLifetime.Singleton);
		descriptor.ShouldNotBeNull("ILeaderElection should be registered as keyed 'sqlserver'");

		// Also verify the "default" fallback keyed registration exists
		services.Any(sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.IsKeyedService &&
			sd.ServiceKey is string defaultKey &&
			defaultKey == "default").ShouldBeTrue("ILeaderElection should have 'default' keyed registration");
	}

	[Fact]
	public async Task UseSqlServer_InnerIsSqlServerImplementation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddExcaliburLeaderElection(le =>
			le.UseSqlServer(sql => sql
				.ConnectionString(TestConnectionString)
				.LockResource(TestLockResource)));

		// Act
		await using var sp = services.BuildServiceProvider();
		var concrete = sp.GetRequiredService<SqlServerLeaderElection>();

		// Assert — concrete SqlServerLeaderElection should also be resolvable
		concrete.ShouldNotBeNull();
		concrete.ShouldBeOfType<SqlServerLeaderElection>();
	}

	[Fact]
	public void UseSqlServerFactory_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburLeaderElection(le =>
			le.UseSqlServerFactory(sql => sql
				.ConnectionString(TestConnectionString)));

		// Assert - Check factory is registered as keyed service
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.IsKeyedService &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void UseSqlServerFactory_ThrowsOnNullBuilder()
	{
		// Arrange
		ILeaderElectionBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServerFactory(sql => sql
				.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UseSqlServerFactory_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new LeaderElectionBuilder(services);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServerFactory(null!));
	}
}
