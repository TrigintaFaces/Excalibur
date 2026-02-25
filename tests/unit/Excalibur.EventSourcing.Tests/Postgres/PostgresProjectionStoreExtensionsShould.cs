// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>
/// Unit tests for <see cref="PostgresProjectionStoreExtensions"/>.
/// Validates DI registration via all 3 overloads: Action, connection string, and NpgsqlDataSource factory.
/// </summary>
/// <remarks>
/// Sprint 535 (S535.7): Tests for S535.4 — AddPostgresProjectionStore DI extension.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class PostgresProjectionStoreExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb";

	// --- Overload 1: Action<Options> ---

	[Fact]
	public void Register_ProjectionStore_With_Action_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresProjectionStore<SampleProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
		});

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<SampleProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_Services_Null_On_Action_Overload()
	{
		IServiceCollection services = null!;

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>(options =>
			{
				options.ConnectionString = TestConnectionString;
			}));
	}

	[Fact]
	public void Throw_When_ConfigureOptions_Null_On_Action_Overload()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>((Action<PostgresProjectionStoreOptions>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_Action_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddPostgresProjectionStore<SampleProjection>(options =>
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
		services.AddPostgresProjectionStore<SampleProjection>(TestConnectionString);

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<SampleProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_ConnectionString_Null()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>((string)null!));
	}

	[Fact]
	public void Throw_When_ConnectionString_Empty()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>(""));
	}

	[Fact]
	public void Throw_When_ConnectionString_Whitespace()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>("   "));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_ConnectionString_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddPostgresProjectionStore<SampleProjection>(TestConnectionString);

		result.ShouldBeSameAs(services);
	}

	// --- Overload 3: NpgsqlDataSource factory ---

	[Fact]
	public void Register_ProjectionStore_With_DataSourceFactory_Overload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresProjectionStore<SampleProjection>(
			_ => NpgsqlDataSource.Create(TestConnectionString));

		// Assert
		services.Any(sd =>
			sd.ServiceType == typeof(IProjectionStore<SampleProjection>) &&
			sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void Throw_When_DataSourceFactory_Null()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddPostgresProjectionStore<SampleProjection>((Func<IServiceProvider, NpgsqlDataSource>)null!));
	}

	[Fact]
	public void Return_Services_For_Chaining_On_DataSourceFactory_Overload()
	{
		var services = new ServiceCollection();

		var result = services.AddPostgresProjectionStore<SampleProjection>(
			_ => NpgsqlDataSource.Create(TestConnectionString));

		result.ShouldBeSameAs(services);
	}

	// --- TryAdd semantics ---

	[Fact]
	public void Not_Override_Existing_Registration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddPostgresProjectionStore<SampleProjection>(TestConnectionString);

		// Act — register again with different connection string
		services.AddPostgresProjectionStore<SampleProjection>("Host=other;Database=OtherDb");

		// Assert — should still have exactly one registration (TryAddScoped)
		services.Count(sd => sd.ServiceType == typeof(IProjectionStore<SampleProjection>)).ShouldBe(1);
	}

	// --- Options validation ---

	[Fact]
	public void Register_Options_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddPostgresProjectionStore<SampleProjection>(options =>
		{
			options.ConnectionString = TestConnectionString;
			options.TableName = "custom_projections";
		});

		// Assert — Options configuration should be registered
		services.Any(sd =>
			sd.ServiceType == typeof(IConfigureOptions<PostgresProjectionStoreOptions>)).ShouldBeTrue();
	}
}
