// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

namespace Excalibur.Data.IdentityMap.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerIdentityMapBuilder" />.
/// </summary>
[Trait("Component", "IdentityMap")]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class SqlServerIdentityMapBuilderShould
{
	private readonly SqlServerIdentityMapOptions _options = new();
	private readonly SqlServerIdentityMapBuilder _sut;

	public SqlServerIdentityMapBuilderShould()
	{
		_sut = new SqlServerIdentityMapBuilder(_options);
	}

	#region Constructor

	[Fact]
	public void ThrowOnNullOptions()
	{
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerIdentityMapBuilder(null!));
	}

	#endregion

	#region ConnectionString

	[Fact]
	public void SetConnectionString()
	{
		const string expected = "Server=.;Database=MyDb;Trusted_Connection=True;";

		_ = _sut.ConnectionString(expected);

		_options.ConnectionString.ShouldBe(expected);
	}

	[Fact]
	public void ConnectionString_ReturnsBuilderForChaining()
	{
		var result = _sut.ConnectionString("Server=.;Database=X;");

		result.ShouldBeSameAs(_sut);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowsOnNullOrWhitespace(string? value)
	{
		_ = Should.Throw<ArgumentException>(() => _sut.ConnectionString(value!));
	}

	#endregion

	#region SchemaName

	[Fact]
	public void SetSchemaName()
	{
		_ = _sut.SchemaName("custom_schema");

		_options.SchemaName.ShouldBe("custom_schema");
	}

	[Fact]
	public void SchemaName_ReturnsBuilderForChaining()
	{
		var result = _sut.SchemaName("dbo");

		result.ShouldBeSameAs(_sut);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SchemaName_ThrowsOnNullOrWhitespace(string? value)
	{
		_ = Should.Throw<ArgumentException>(() => _sut.SchemaName(value!));
	}

	#endregion

	#region TableName

	[Fact]
	public void SetTableName()
	{
		_ = _sut.TableName("MyTable");

		_options.TableName.ShouldBe("MyTable");
	}

	[Fact]
	public void TableName_ReturnsBuilderForChaining()
	{
		var result = _sut.TableName("T");

		result.ShouldBeSameAs(_sut);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TableName_ThrowsOnNullOrWhitespace(string? value)
	{
		_ = Should.Throw<ArgumentException>(() => _sut.TableName(value!));
	}

	#endregion

	#region CommandTimeout

	[Fact]
	public void SetCommandTimeout()
	{
		_ = _sut.CommandTimeout(TimeSpan.FromSeconds(60));

		_options.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void CommandTimeout_ReturnsBuilderForChaining()
	{
		var result = _sut.CommandTimeout(TimeSpan.FromSeconds(30));

		result.ShouldBeSameAs(_sut);
	}

	[Fact]
	public void CommandTimeout_ThrowsOnZero()
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _sut.CommandTimeout(TimeSpan.Zero));
	}

	[Fact]
	public void CommandTimeout_ThrowsOnNegative()
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _sut.CommandTimeout(TimeSpan.FromSeconds(-1)));
	}

	#endregion

	#region MaxBatchSize

	[Fact]
	public void SetMaxBatchSize()
	{
		_ = _sut.MaxBatchSize(200);

		_options.MaxBatchSize.ShouldBe(200);
	}

	[Fact]
	public void MaxBatchSize_ReturnsBuilderForChaining()
	{
		var result = _sut.MaxBatchSize(50);

		result.ShouldBeSameAs(_sut);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void MaxBatchSize_ThrowsOnNonPositive(int value)
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _sut.MaxBatchSize(value));
	}

	#endregion

	#region Fluent Chaining

	[Fact]
	public void SupportFluentChaining()
	{
		var result = _sut
			.ConnectionString("Server=.;Database=Test;")
			.SchemaName("custom")
			.TableName("MyMap")
			.CommandTimeout(TimeSpan.FromMinutes(2))
			.MaxBatchSize(500);

		result.ShouldBeSameAs(_sut);
		_options.ConnectionString.ShouldBe("Server=.;Database=Test;");
		_options.SchemaName.ShouldBe("custom");
		_options.TableName.ShouldBe("MyMap");
		_options.CommandTimeoutSeconds.ShouldBe(120);
		_options.MaxBatchSize.ShouldBe(500);
	}

	#endregion
}
