// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerProjectionRegistrar"/> and <c>AddSqlServerProjections</c> batch DI extensions.
/// Validates both string-based and Action-based overloads: DI registration, shared config, per-projection overrides.
/// </summary>
/// <remarks>
/// Sprint 716 T.17: Batch registrar DI wiring tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerProjectionRegistrarShould : UnitTestBase
{
	private const string SharedConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;";

	#region String-based overload (S715)

	[Fact]
	public void RegisterMultipleProjections_WithStringOverload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjections(SharedConnectionString, projections =>
		{
			projections
				.Add<ProjectionA>()
				.Add<ProjectionB>();
		});

		// Assert -- both projection stores registered
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionA>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionB>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void ApplySharedConnectionString_WithStringOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddSqlServerProjections(SharedConnectionString, projections =>
		{
			projections.Add<ProjectionA>();
		});

		// Assert -- options should have shared connection string
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerProjectionStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(SharedConnectionString);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddSqlServerProjections(SharedConnectionString, projections =>
		{
			projections.Add<ProjectionA>(opts => opts.TableName = "CustomTableA");
		});

		// Assert -- per-projection override should win
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerProjectionStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(SharedConnectionString);
		options.Value.TableName.ShouldBe("CustomTableA");
	}

	[Fact]
	public void ThrowOnNullConnectionString_WithStringOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjections((string)null!, _ => { }));
	}

	[Fact]
	public void ThrowOnNullConfigure_WithStringOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjections(SharedConnectionString, null!));
	}

	#endregion

	#region Action<Options> overload (S716)

	[Fact]
	public void RegisterMultipleProjections_WithActionOverload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSqlServerProjections(
			opts => opts.ConnectionString = SharedConnectionString,
			projections =>
			{
				projections
					.Add<ProjectionA>()
					.Add<ProjectionB>();
			});

		// Assert
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionA>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionB>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void ApplySharedOptions_WithActionOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		services.AddSqlServerProjections(
			opts =>
			{
				opts.ConnectionString = SharedConnectionString;
				opts.TableName = "SharedTable";
			},
			projections => projections.Add<ProjectionA>());

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerProjectionStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(SharedConnectionString);
		options.Value.TableName.ShouldBe("SharedTable");
	}

	[Fact]
	public void PerProjectionOverrideWinsOverShared_WithActionOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act -- shared sets TableName="SharedTable", per-projection overrides to "OverrideTable"
		services.AddSqlServerProjections(
			opts =>
			{
				opts.ConnectionString = SharedConnectionString;
				opts.TableName = "SharedTable";
			},
			projections => projections.Add<ProjectionA>(opts => opts.TableName = "OverrideTable"));

		// Assert -- per-projection override wins
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerProjectionStoreOptions>>();
		options.Value.TableName.ShouldBe("OverrideTable");
	}

	[Fact]
	public void ThrowOnNullConfigureShared_WithActionOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjections((Action<SqlServerProjectionStoreOptions>)null!, _ => { }));
	}

	[Fact]
	public void ThrowOnNullConfigure_WithActionOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerProjections(
				opts => opts.ConnectionString = SharedConnectionString,
				null!));
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- fluent chaining should return IServiceCollection
		var result = services.AddSqlServerProjections(SharedConnectionString, projections =>
		{
			var registrar = projections.Add<ProjectionA>();
			registrar.ShouldNotBeNull();
			registrar.Add<ProjectionB>();
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Test projection types

	private sealed class ProjectionA
	{
		public string? Name { get; set; }
	}

	private sealed class ProjectionB
	{
		public int Count { get; set; }
	}

	#endregion
}
