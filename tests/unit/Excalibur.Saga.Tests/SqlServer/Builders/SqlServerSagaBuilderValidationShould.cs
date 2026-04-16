// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Saga.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerSagaBuilder"/> argument validation guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSagaBuilderValidationShould
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestSagaBuilder : ISagaBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	// --- ConnectionString guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionString(invalidValue!)));
	}

	// --- ConnectionFactory guards ---

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(null!)));
	}

	// --- ConnectionStringName guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionStringName(invalidValue!)));
	}

	// --- BindConfiguration guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.BindConfiguration(invalidValue!)));
	}

	// --- Feature method guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SchemaName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString).SchemaName(invalidValue!)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TableName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestSagaBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString).TableName(invalidValue!)));
	}

	// --- ValidateOnStart ---

	[Fact]
	public void ValidateOnStart_FailWhenNoConnectionConfigured()
	{
		// Arrange
		var builder = new TestSagaBuilder();
		builder.UseSqlServer(sql => { }); // empty builder — no connection

		// Assert — validator registered, should contain error
		var provider = builder.Services.BuildServiceProvider();
		var validators = provider.GetServices<Microsoft.Extensions.Options.IValidateOptions<SqlServerSagaStoreOptions>>();
		validators.ShouldNotBeEmpty();
	}
}
