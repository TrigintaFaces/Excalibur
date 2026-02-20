// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerProjectionStoreExtensions"/>.
/// Validates DI registration via all 3 overloads: Action, connection string, and connection factory.
/// </summary>
/// <remarks>
/// Sprint 535 (S535.7): Tests for S535.5 — AddSqlServerProjectionStore DI extension.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerProjectionStoreExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;";

	// --- Overload 1: Action<Options> ---

	[Fact]
	public void Register_ProjectionStore_With_Action_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
		});

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<TestProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_Services_Null_On_Action_Overload()
	{
		IServiceCollection services = null!;

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>(options =>
			{
				options.ConnectionString = TestConnectionString;
			}));
	}

	[Fact]
	public void Throw_When_ConfigureOptions_Null_On_Action_Overload()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>((Action<SqlServerProjectionStoreOptions>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_Action_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
		});

		result.ShouldBeSameAs(services);
	}

	// --- Overload 2: Connection string ---

	[Fact]
	public void Register_ProjectionStore_With_ConnectionString_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(TestConnectionString);

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<TestProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_ConnectionString_Null()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>((string)null!));
	}

	[Fact]
	public void Throw_When_ConnectionString_Empty()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>(""));
	}

	[Fact]
	public void Throw_When_ConnectionString_Whitespace()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>("   "));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_ConnectionString_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<TestProjection>(TestConnectionString);

		result.ShouldBeSameAs(services);
	}

	// --- Overload 3: Connection factory ---

	[Fact]
	public void Register_ProjectionStore_With_ConnectionFactory_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(
			() => new SqlConnection(TestConnectionString));

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<TestProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_ConnectionFactory_Null()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjectionStore<TestProjection>((Func<SqlConnection>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_ConnectionFactory_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddSqlServerProjectionStore<TestProjection>(
			() => new SqlConnection(TestConnectionString));

		result.ShouldBeSameAs(services);
	}

	// --- TryAdd semantics ---

	[Fact]
	public void Not_Override_Existing_Registration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSqlServerProjectionStore<TestProjection>(TestConnectionString);

		// Act — register again with different connection string
		services.AddSqlServerProjectionStore<TestProjection>("Server=other;Database=OtherDb;Integrated Security=true;");

		// Assert — should still have exactly one registration (TryAddScoped)
		services.Count(sd => sd.ServiceType == typeof(IProjectionStore<TestProjection>)).ShouldBe(1);
	}

	// --- Options validation ---

	[Fact]
	public void Register_Options_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjectionStore<TestProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
			options.TableName = "CustomProjections";
		});

		// Assert — Options configuration should be registered
		services.Any(sd =>
			sd.ServiceType == typeof(IConfigureOptions<SqlServerProjectionStoreOptions>)).ShouldBeTrue();
	}

	// --- Projection types ---

	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
