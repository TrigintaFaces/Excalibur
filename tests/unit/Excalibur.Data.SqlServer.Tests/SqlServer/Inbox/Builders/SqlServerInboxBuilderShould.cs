// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Inbox.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerInboxBuilder"/> — connection overloads,
/// feature methods, last-wins semantics, DeduplicationWindow, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "SqlServer")]
public sealed class SqlServerInboxBuilderShould : UnitTestBase
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestInboxBuilder : IInboxBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	// --- Connection overloads ---

	[Fact]
	public void ConnectionString_SetConnectionStringOnOptions()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void ConnectionFactory_RegisterFactory()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString)));

		// Assert — connection string should be empty (factory takes precedence)
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConnectionStringName_RegisterName()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql => sql.ConnectionStringName("InboxDb"));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void BindConfiguration_RegisterPath()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act — should not throw during builder configuration
		builder.UseSqlServer(sql => sql.BindConfiguration("Inbox:SqlServer"));

		// Assert — BindConfiguration registers an options binder (requires IConfiguration at resolve time)
		// Just verify the call succeeded and services were registered
		builder.Services.ShouldNotBeEmpty();
	}

	// --- Last-wins semantics ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString_WhenCalledAfterConnectionString()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionString(TestConnectionString);
			sql.ConnectionFactory(_ => () => new SqlConnection());
		});

		// Assert — ConnectionString cleared by last-wins
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConnectionString_ClearFactory_WhenCalledAfterConnectionFactory()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionFactory(_ => () => new SqlConnection());
			sql.ConnectionString(TestConnectionString);
		});

		// Assert — ConnectionString should be set (last-wins)
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	// --- Feature methods ---

	[Fact]
	public void SchemaName_SetSchemaOnOptions()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).SchemaName("inbox"));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.SchemaName.ShouldBe("inbox");
	}

	[Fact]
	public void TableName_SetTableOnOptions()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).TableName("InboxMessages"));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.TableName.ShouldBe("InboxMessages");
	}

	// --- DeduplicationWindow ---

	[Fact]
	public void DeduplicationWindow_AcceptPositiveValue()
	{
		// Arrange
		var builder = new TestInboxBuilder();
		var expected = TimeSpan.FromMinutes(30);

		// Act — should not throw
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString)
			   .DeduplicationWindow(expected));

		// Assert — verify it was accepted (no exception)
		var provider = builder.Services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
	}

	[Fact]
	public void DeduplicationWindow_ThrowOnZero()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DeduplicationWindow(TimeSpan.Zero)));
	}

	[Fact]
	public void DeduplicationWindow_ThrowOnNegative()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DeduplicationWindow(TimeSpan.FromMinutes(-5))));
	}

	// --- Fluent chaining ---

	[Fact]
	public void AllMethods_SupportFluentChaining()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString)
			   .SchemaName("inbox")
			   .TableName("InboxEntries")
			   .DeduplicationWindow(TimeSpan.FromHours(1)));

		// Assert
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerInboxOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
		options.Value.SchemaName.ShouldBe("inbox");
		options.Value.TableName.ShouldBe("InboxEntries");
	}

	// --- Entry point ---

	[Fact]
	public void UseSqlServer_ReturnBuilderForChaining()
	{
		// Arrange
		var builder = new TestInboxBuilder();

		// Act
		var result = builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullBuilder()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullConfigure()
	{
		var builder = new TestInboxBuilder();

		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<ISqlServerInboxBuilder>)null!));
	}
}
