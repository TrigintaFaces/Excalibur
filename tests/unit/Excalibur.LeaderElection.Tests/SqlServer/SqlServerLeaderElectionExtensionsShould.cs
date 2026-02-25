// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerLeaderElectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerLeaderElectionExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=test;Integrated Security=true;";
	private const string TestLockResource = "TestApp.Leader";

	[Fact]
	public void AddSqlServerLeaderElection_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource);

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(SqlServerLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddSqlServerLeaderElection_WithConfigure_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource, options =>
		{
			options.LeaseDuration = TimeSpan.FromSeconds(30);
		});

		// Assert - Check service is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(SqlServerLeaderElection) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddSqlServerLeaderElection_WithConfigure_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource, options =>
		{
			options.LeaseDuration = TimeSpan.FromSeconds(30);
		});

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<LeaderElectionOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource, _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnNullConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElection(null!, TestLockResource, _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnEmptyConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElection("", TestLockResource, _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnWhitespaceConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElection("   ", TestLockResource, _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnNullLockResource()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElection(TestConnectionString, null!, _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnEmptyLockResource()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElection(TestConnectionString, "", _ => { }));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource, null!));
	}

	[Fact]
	public void AddSqlServerLeaderElection_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public async Task AddSqlServerLeaderElection_ResolvesAsTelemetryWrapper()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource);

		// Act
		await using var sp = services.BuildServiceProvider();
		var le = sp.GetRequiredService<ILeaderElection>();

		// Assert — DI should auto-wrap with TelemetryLeaderElection (AD-536.1)
		le.ShouldBeOfType<LeaderElection.Diagnostics.TelemetryLeaderElection>();
	}

	[Fact]
	public async Task AddSqlServerLeaderElection_InnerIsSqlServerImplementation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddSqlServerLeaderElection(TestConnectionString, TestLockResource);

		// Act
		await using var sp = services.BuildServiceProvider();
		var concrete = sp.GetRequiredService<SqlServerLeaderElection>();

		// Assert — concrete SqlServerLeaderElection should also be resolvable
		concrete.ShouldNotBeNull();
		concrete.ShouldBeOfType<SqlServerLeaderElection>();
	}

	[Fact]
	public void AddSqlServerLeaderElectionFactory_RegistersFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddSqlServerLeaderElectionFactory(TestConnectionString);

		// Assert - Check factory is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddSqlServerLeaderElectionFactory_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerLeaderElectionFactory(TestConnectionString));
	}

	[Fact]
	public void AddSqlServerLeaderElectionFactory_ThrowsOnNullConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerLeaderElectionFactory(null!));
	}

	[Fact]
	public void AddSqlServerLeaderElectionFactory_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSqlServerLeaderElectionFactory(TestConnectionString);

		// Assert
		result.ShouldBeSameAs(services);
	}
}
