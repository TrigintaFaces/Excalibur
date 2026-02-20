// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Reflection;

using Dapper;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

using TransactionStatus = Excalibur.Data.Abstractions.Persistence.TransactionStatus;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerTransactionScope"/> using TestContainers.
/// Tests real SQL Server transaction operations including commit, rollback, and savepoints.
/// </summary>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "Data")]
[Trait("Provider", "SqlServer")]
public sealed class SqlServerTransactionScopeIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _sqlFixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerTransactionScopeIntegrationShould"/> class.
	/// </summary>
	/// <param name="sqlFixture">The SQL Server container fixture.</param>
	public SqlServerTransactionScopeIntegrationShould(SqlServerFixture sqlFixture)
	{
		_sqlFixture = sqlFixture;
	}

	/// <summary>
	/// Tests that committed data persists after transaction commit.
	/// </summary>
	[Fact]
	public async Task CommitTransactionWithRealData()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await ClearTestTableAsync().ConfigureAwait(true);

		TransactionStatus statusAfterCommit;

		// Scope block: ambient TransactionScope must be fully disposed before
		// opening new connections to verify data, otherwise the ambient scope
		// poisons the connection with "TransactionScope already complete".
		{
			await using var scope = CreateTransactionScope();
			var connection = new SqlConnection(_sqlFixture.ConnectionString);
			await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

			var transaction = GetEnlistedTransaction(scope);
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "committed-row", Value = 42 },
				transaction).ConfigureAwait(true);
			await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);
			statusAfterCommit = scope.Status;
		}

		// Assert - Data should be visible after commit (scope fully disposed)
		var count = await CountRowsAsync("committed-row").ConfigureAwait(true);
		count.ShouldBe(1);
		statusAfterCommit.ShouldBe(TransactionStatus.Committed);
	}

	/// <summary>
	/// Tests that rolled back data does not persist.
	/// </summary>
	[Fact]
	public async Task RollbackTransactionDiscardsData()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await ClearTestTableAsync().ConfigureAwait(true);

		await using var scope = CreateTransactionScope();
		var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

		// Act - Insert within transaction and rollback
		var transaction = GetEnlistedTransaction(scope);
		await connection.ExecuteAsync(
			"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
			new { Name = "rolled-back-row", Value = 99 },
			transaction).ConfigureAwait(true);
		await scope.RollbackAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert - Data should not be visible after rollback
		var count = await CountRowsAsync("rolled-back-row").ConfigureAwait(true);
		count.ShouldBe(0);
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	/// <summary>
	/// Tests that disposing an active transaction scope triggers automatic rollback.
	/// </summary>
	[Fact]
	public async Task DisposeActiveTransactionTriggersRollback()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await ClearTestTableAsync().ConfigureAwait(true);

		// Act - Insert within transaction scope that is disposed without commit
		{
			await using var scope = CreateTransactionScope();
			var connection = new SqlConnection(_sqlFixture.ConnectionString);
			await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

			var transaction = GetEnlistedTransaction(scope);
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "auto-rollback-row", Value = 1 },
				transaction).ConfigureAwait(true);
			// scope disposed here without commit
		}

		// Assert - Data should not persist
		var count = await CountRowsAsync("auto-rollback-row").ConfigureAwait(true);
		count.ShouldBe(0);
	}

	/// <summary>
	/// Tests that transaction scope properties are initialized correctly.
	/// </summary>
	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Act
		using var scope = CreateTransactionScope();

		// Assert
		scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
		scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		scope.Status.ShouldBe(TransactionStatus.Active);
		scope.StartTime.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	/// <summary>
	/// Tests that savepoints allow partial rollback within a transaction.
	/// </summary>
	[Fact]
	public async Task RollbackToSavepointPreservesEarlierData()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await ClearTestTableAsync().ConfigureAwait(true);

		{
			await using var scope = CreateTransactionScope();
			var connection = new SqlConnection(_sqlFixture.ConnectionString);
			await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

			var transaction = GetEnlistedTransaction(scope);

			// Insert first row
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "before-savepoint", Value = 1 },
				transaction).ConfigureAwait(true);

			// Create savepoint
			await scope.CreateSavepointAsync("sp1", TestCancellationToken).ConfigureAwait(true);

			// Insert second row after savepoint
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "after-savepoint", Value = 2 },
				transaction).ConfigureAwait(true);

			// Act - Rollback to savepoint
			await scope.RollbackToSavepointAsync("sp1", TestCancellationToken).ConfigureAwait(true);

			// Commit the transaction
			await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);
		}

		// Assert - First row should persist, second should not (scope fully disposed)
		var countBefore = await CountRowsAsync("before-savepoint").ConfigureAwait(true);
		var countAfter = await CountRowsAsync("after-savepoint").ConfigureAwait(true);

		countBefore.ShouldBe(1, "Data before savepoint should persist");
		countAfter.ShouldBe(0, "Data after savepoint should be rolled back");
	}

	/// <summary>
	/// Tests that commit callbacks are invoked on successful commit.
	/// </summary>
	[Fact]
	public async Task InvokeCommitCallbacksOnCommit()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await using var scope = CreateTransactionScope();
		var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

		var callbackInvoked = false;
		scope.OnCommit(() =>
		{
			callbackInvoked = true;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that rollback callbacks are invoked on rollback.
	/// </summary>
	[Fact]
	public async Task InvokeRollbackCallbacksOnRollback()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await using var scope = CreateTransactionScope();
		var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

		var callbackInvoked = false;
		scope.OnRollback(() =>
		{
			callbackInvoked = true;
			return Task.CompletedTask;
		});

		// Act
		await scope.RollbackAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that multiple rows committed in a single transaction are all visible.
	/// </summary>
	[Fact]
	public async Task CommitMultipleOperationsAtomically()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await ClearTestTableAsync().ConfigureAwait(true);

		{
			await using var scope = CreateTransactionScope();
			var connection = new SqlConnection(_sqlFixture.ConnectionString);
			await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

			var transaction = GetEnlistedTransaction(scope);

			// Act - Insert multiple rows
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "atomic-1", Value = 10 },
				transaction).ConfigureAwait(true);
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "atomic-2", Value = 20 },
				transaction).ConfigureAwait(true);
			await connection.ExecuteAsync(
				"INSERT INTO [dbo].[TransactionTestTable] (Name, Value) VALUES (@Name, @Value)",
				new { Name = "atomic-3", Value = 30 },
				transaction).ConfigureAwait(true);

			await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);
		}

		// Assert - All rows should be visible (scope fully disposed)
		var totalCount = await CountAllRowsAsync().ConfigureAwait(true);
		totalCount.ShouldBe(3);
	}

	/// <summary>
	/// Tests that a nested transaction scope can be created from an active scope.
	/// </summary>
	[Fact]
	public async Task CreateNestedTransactionScope()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await using var parentScope = CreateTransactionScope();

		// Act
		var nestedScope = parentScope.CreateNestedScope();

		// Assert
		_ = nestedScope.ShouldNotBeNull();
		nestedScope.TransactionId.ShouldNotBe(parentScope.TransactionId);
		nestedScope.Status.ShouldBe(TransactionStatus.Active);

		// Cleanup
		nestedScope.Dispose();
	}

	/// <summary>
	/// Tests that the OnComplete callback receives the correct status on commit.
	/// </summary>
	[Fact]
	public async Task InvokeCompleteCallbackWithCorrectStatusOnCommit()
	{
		// Arrange
		await InitializeTestTableAsync().ConfigureAwait(true);
		await using var scope = CreateTransactionScope();
		var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await scope.EnlistConnectionAsync(connection, TestCancellationToken).ConfigureAwait(true);

		TransactionStatus? reportedStatus = null;
		scope.OnComplete(status =>
		{
			reportedStatus = status;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		reportedStatus.ShouldBe(TransactionStatus.Committed);
	}

	/// <summary>
	/// Gets the first enlisted <see cref="SqlTransaction"/> from the scope via reflection.
	/// The scope stores transactions internally; this helper extracts them for test verification.
	/// </summary>
	private static SqlTransaction GetEnlistedTransaction(SqlServerTransactionScope scope)
	{
		var field = typeof(SqlServerTransactionScope)
			.GetField("_transactions", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Could not find _transactions field");

		var transactions = (List<SqlTransaction>)(field.GetValue(scope)
			?? throw new InvalidOperationException("_transactions was null"));

		return transactions.Count > 0
			? transactions[0]
			: throw new InvalidOperationException("No transactions enlisted in scope");
	}

	private SqlServerTransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
	{
		var logger = NullLogger<SqlServerTransactionScope>.Instance;
		return new SqlServerTransactionScope(isolationLevel, TimeSpan.FromSeconds(30), logger);
	}

	private async Task<int> CountRowsAsync(string name)
	{
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		return await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM [dbo].[TransactionTestTable] WHERE Name = @Name",
			new { Name = name }).ConfigureAwait(true);
	}

	private async Task<int> CountAllRowsAsync()
	{
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		return await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM [dbo].[TransactionTestTable]").ConfigureAwait(true);
	}

	private async Task ClearTestTableAsync()
	{
		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync("DELETE FROM [dbo].[TransactionTestTable]").ConfigureAwait(true);
	}

	private async Task InitializeTestTableAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransactionTestTable]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [dbo].[TransactionTestTable] (
			        Id INT IDENTITY(1,1) PRIMARY KEY,
			        Name NVARCHAR(255) NOT NULL,
			        Value INT NOT NULL
			    );
			END
			""";

		await using var connection = new SqlConnection(_sqlFixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(true);
		_ = await connection.ExecuteAsync(createTableSql).ConfigureAwait(true);
	}
}
