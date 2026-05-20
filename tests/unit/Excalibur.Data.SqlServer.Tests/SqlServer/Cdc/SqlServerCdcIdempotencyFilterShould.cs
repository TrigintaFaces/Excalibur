// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcIdempotencyFilter"/>.
/// Covers constructor validation, argument guards, and edge cases.
/// </summary>
/// <remarks>
/// <para>
/// The SQL execution paths (IsProcessedAsync, MarkProcessedAsync, CleanupAsync)
/// require a real SQL Server connection since Dapper extension methods cannot be
/// faked. Those paths are covered by integration tests with TestContainers.
/// </para>
/// <para>
/// These unit tests verify the defensive coding layer: null guards, options
/// validation, and constructor contracts — ensuring the filter fails fast
/// with clear exceptions before hitting the database.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcIdempotencyFilterShould : UnitTestBase
{
	private static readonly byte[] SampleLsn = [0x00, 0x00, 0x00, 0x01];
	private static readonly byte[] SampleSeqVal = [0x00, 0x01];
	private const string SampleTable = "dbo_Orders";

	#region Constructor Validation

	[Fact]
	public void ThrowWhenConnectionIsNull()
	{
		// Arrange
		var options = CreateValidOptions();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SqlServerCdcIdempotencyFilter(null!, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SqlServerCdcIdempotencyFilter(connection, null!, logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var options = CreateValidOptions();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, null!));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_InvalidSchemaName()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "invalid;schema",
			TableName = "ValidTable"
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_InvalidTableName()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "Cdc",
			TableName = "drop table--"
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_EmptySchemaName()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "",
			TableName = "ValidTable"
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_ZeroRetentionPeriod()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			RetentionPeriod = TimeSpan.Zero
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_NegativeRetentionPeriod()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			RetentionPeriod = TimeSpan.FromHours(-1)
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_ZeroCleanupBatchSize()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			CleanupBatchSize = 0
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ThrowWhenOptionsValidationFails_NegativeCleanupBatchSize()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		var options = Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			CleanupBatchSize = -5
		});

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new SqlServerCdcIdempotencyFilter(connection, options, logger));
	}

	[Fact]
	public void ConstructSuccessfully_WithValidDefaults()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var options = CreateValidOptions();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;

		// Act
		var filter = new SqlServerCdcIdempotencyFilter(connection, options, logger);

		// Assert
		filter.ShouldNotBeNull();
	}

	#endregion

	#region IsProcessedAsync — Argument Validation

	[Fact]
	public async Task IsProcessedAsync_ThrowWhenTableNameIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(null!, SampleLsn, SampleSeqVal, CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedAsync_ThrowWhenLsnIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(SampleTable, null!, SampleSeqVal, CancellationToken.None));
	}

	[Fact]
	public async Task IsProcessedAsync_ThrowWhenSeqValIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(SampleTable, SampleLsn, null!, CancellationToken.None));
	}

	#endregion

	#region MarkProcessedAsync — Argument Validation

	[Fact]
	public async Task MarkProcessedAsync_ThrowWhenTableNameIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(null!, SampleLsn, SampleSeqVal, CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedAsync_ThrowWhenLsnIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(SampleTable, null!, SampleSeqVal, CancellationToken.None));
	}

	[Fact]
	public async Task MarkProcessedAsync_ThrowWhenSeqValIsNull()
	{
		// Arrange
		var filter = CreateFilter();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(SampleTable, SampleLsn, null!, CancellationToken.None));
	}

	#endregion

	#region Options QualifiedTableName

	[Fact]
	public void QualifiedTableName_UsesBracketEscaping()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "Cdc",
			TableName = "CdcProcessedEvents"
		};

		// Act & Assert
		options.QualifiedTableName.ShouldBe("[Cdc].[CdcProcessedEvents]");
	}

	[Fact]
	public void QualifiedTableName_ReflectsCustomSchemaAndTable()
	{
		// Arrange
		var options = new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "custom_schema",
			TableName = "MyEvents"
		};

		// Act & Assert
		options.QualifiedTableName.ShouldBe("[custom_schema].[MyEvents]");
	}

	#endregion

	#region Helpers

	private static SqlServerCdcIdempotencyFilter CreateFilter()
	{
		var connection = A.Fake<IDbConnection>();
		var options = CreateValidOptions();
		var logger = NullLogger<SqlServerCdcIdempotencyFilter>.Instance;
		return new SqlServerCdcIdempotencyFilter(connection, options, logger);
	}

	private static IOptions<SqlServerCdcIdempotencyFilterOptions> CreateValidOptions()
	{
		return Options.Create(new SqlServerCdcIdempotencyFilterOptions
		{
			SchemaName = "Cdc",
			TableName = "CdcProcessedEvents",
			RetentionPeriod = TimeSpan.FromHours(24),
			CleanupBatchSize = 1000
		});
	}

	#endregion
}
