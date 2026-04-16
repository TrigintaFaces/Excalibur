// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Inbox.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerInboxBuilder"/> argument validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "SqlServer")]
public sealed class SqlServerInboxBuilderValidationShould : UnitTestBase
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestInboxBuilder : IInboxBuilder
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
		var builder = new TestInboxBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionString(invalidValue!)));
	}

	// --- ConnectionFactory guards ---

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		var builder = new TestInboxBuilder();

		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionFactory(null!)));
	}

	// --- ConnectionStringName guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestInboxBuilder();

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
		var builder = new TestInboxBuilder();

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
		var builder = new TestInboxBuilder();

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
		var builder = new TestInboxBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString).TableName(invalidValue!)));
	}
}
