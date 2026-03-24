// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Excalibur.Data.SqlServer;
namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerCdcBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcBuilderValidationShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SchemaName_ThrowsOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .SchemaName(invalidValue))));
	}

	[Theory]
	[InlineData("cdc")]
	[InlineData("dbo")]
	[InlineData("audit")]
	public void SchemaName_AcceptsValidValues(string validValue)
	{
		var services = new ServiceCollection();

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .SchemaName(validValue)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.SchemaName.ShouldBe(validValue);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void StateTableName_ThrowsOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .StateTableName(invalidValue))));
	}

	[Theory]
	[InlineData("CdcState")]
	[InlineData("ProcessingState")]
	[InlineData("CdcPositions")]
	public void StateTableName_AcceptsValidValues(string validValue)
	{
		var services = new ServiceCollection();

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .StateTableName(validValue)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.StateTableName.ShouldBe(validValue);
	}

	[Fact]
	public void PollingInterval_ThrowsOnZero()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .PollingInterval(TimeSpan.Zero))));
	}

	[Fact]
	public void PollingInterval_ThrowsOnNegative()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .PollingInterval(TimeSpan.FromSeconds(-1)))));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(30)]
	[InlineData(60)]
	public void PollingInterval_AcceptsValidSeconds(int seconds)
	{
		var services = new ServiceCollection();
		var expectedInterval = TimeSpan.FromSeconds(seconds);

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .PollingInterval(expectedInterval)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.PollingInterval.ShouldBe(expectedInterval);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void BatchSize_ThrowsOnInvalidValue(int invalidValue)
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .BatchSize(invalidValue))));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(10000)]
	public void BatchSize_AcceptsValidValues(int validValue)
	{
		var services = new ServiceCollection();

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .BatchSize(validValue)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.BatchSize.ShouldBe(validValue);
	}

	[Fact]
	public void CommandTimeout_ThrowsOnZero()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .CommandTimeout(TimeSpan.Zero))));
	}

	[Fact]
	public void CommandTimeout_ThrowsOnNegative()
	{
		var services = new ServiceCollection();

		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .CommandTimeout(TimeSpan.FromSeconds(-1)))));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(300)]
	public void CommandTimeout_AcceptsValidSeconds(int seconds)
	{
		var services = new ServiceCollection();
		var expectedTimeout = TimeSpan.FromSeconds(seconds);

		_ = services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .CommandTimeout(expectedTimeout)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.CommandTimeout.ShouldBe(expectedTimeout);
	}
}
