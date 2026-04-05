// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcStateStoreOptionsValidator"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcStateStoreOptionsValidatorShould : UnitTestBase
{
	private readonly SqlServerCdcStateStoreOptionsValidator _sut = new();

	[Fact]
	public void ReturnSuccess_WhenOptionsAreValid()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "cdc",
			TableName = "CdcState"
		};

		var result = _sut.Validate(name: null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailure_WhenOptionsIsNull()
	{
		var result = _sut.Validate(name: null, options: null!);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be null");
	}

	[Fact]
	public void ReturnFailure_WhenSchemaNameIsNull()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = null!,
			TableName = "CdcState"
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Fact]
	public void ReturnFailure_WhenSchemaNameIsEmpty()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "",
			TableName = "CdcState"
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Fact]
	public void ReturnFailure_WhenSchemaNameIsWhitespace()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "   ",
			TableName = "CdcState"
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SchemaName");
	}

	[Fact]
	public void ReturnFailure_WhenTableNameIsNull()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "cdc",
			TableName = null!
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void ReturnFailure_WhenTableNameIsEmpty()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "cdc",
			TableName = ""
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}

	[Fact]
	public void ReturnFailure_WhenTableNameIsWhitespace()
	{
		var options = new SqlServerCdcStateStoreOptions
		{
			SchemaName = "cdc",
			TableName = "   "
		};

		var result = _sut.Validate(name: null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TableName");
	}
}
