// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Saga.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerSagaBuilder"/> — connection overloads,
/// feature methods, last-wins semantics, and fluent chaining via public API.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSagaBuilderShould
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestSagaBuilder : ISagaBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	// --- Connection overloads (happy path via public entry point) ---

	[Fact]
	public void ConnectionString_SetConnectionStringOnOptions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void ConnectionFactory_RegisterFactory()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act — should not throw, factory is stored internally
		builder.UseSqlServer(sql =>
			sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString)));

		// Assert — options connection string should be null (factory takes precedence)
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionStringName_RegisterName()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql => sql.ConnectionStringName("SagaDb"));

		// Assert — connection string should be null (resolved at DI time)
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void BindConfiguration_RegisterPath()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act — should not throw during builder configuration
		builder.UseSqlServer(sql => sql.BindConfiguration("Saga:SqlServer"));

		// Assert — BindConfiguration registers an options binder (requires IConfiguration at resolve time)
		// Just verify the call succeeded and services were registered
		builder.Services.ShouldNotBeEmpty();
	}

	// --- Last-wins semantics ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString_WhenCalledAfterConnectionString()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act — ConnectionString then ConnectionFactory (last-wins)
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionString(TestConnectionString);
			sql.ConnectionFactory(_ => () => new SqlConnection());
		});

		// Assert — ConnectionString should be cleared
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_ClearFactory_WhenCalledAfterConnectionFactory()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act — ConnectionFactory then ConnectionString (last-wins)
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionFactory(_ => () => new SqlConnection());
			sql.ConnectionString(TestConnectionString);
		});

		// Assert — ConnectionString should be set (last-wins)
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	// --- Feature methods ---

	[Fact]
	public void SchemaName_SetSchemaOnOptions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).SchemaName("saga"));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.SchemaName.ShouldBe("saga");
	}

	[Fact]
	public void TableName_SetTableOnOptions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).TableName("SagaInstances"));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.TableName.ShouldBe("SagaInstances");
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_SupportFluentChaining()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act — all methods should chain without error
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString)
			   .SchemaName("saga")
			   .TableName("SagaInstances"));

		// Assert — options reflect all settings
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
		options.Value.SchemaName.ShouldBe("saga");
		options.Value.TableName.ShouldBe("SagaInstances");
	}

	// --- Entry point ---

	[Fact]
	public void UseSqlServer_ReturnBuilderForChaining()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullConfigure()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<ISqlServerSagaBuilder>)null!));
	}
}
