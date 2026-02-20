// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;

using FakeItEasy;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="UnitOfWork"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "UnitOfWork")]
public sealed class UnitOfWorkShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithValidConnection_Succeeds()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var uow = new UnitOfWork(connection);

		// Assert
		uow.ShouldNotBeNull();
	}

	[Fact]
	public void Create_WithNullConnection_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new UnitOfWork(null!));
	}

	#endregion

	#region Connection Property Tests

	[Fact]
	public void Connection_ReturnsProvidedConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		var result = uow.Connection;

		// Assert
		result.ShouldBe(connection);
	}

	[Fact]
	public void Connection_OpensClosed_Connection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Closed);
		var uow = new UnitOfWork(connection);

		// Act
		_ = uow.Connection;

		// Assert
		A.CallTo(() => connection.Open()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Transaction Property Tests

	[Fact]
	public void Transaction_InitiallyNull()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Assert
		uow.Transaction.ShouldBeNull();
	}

	#endregion

	#region BeginTransactionAsync Tests

	[Fact]
	public async Task BeginTransactionAsync_SetsTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var transaction = A.Fake<IDbTransaction>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Assert
		uow.Transaction.ShouldBe(transaction);
	}

	[Fact]
	public async Task BeginTransactionAsync_AcceptsCancellationToken()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var transaction = A.Fake<IDbTransaction>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);
		using var cts = new CancellationTokenSource();

		// Act - verify method accepts cancellation token
		await uow.BeginTransactionAsync(cts.Token);

		// Assert
		uow.Transaction.ShouldBe(transaction);
	}

	#endregion

	#region CommitAsync Tests

	[Fact]
	public async Task CommitAsync_CommitsAndDisposesTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var transaction = A.Fake<IDbTransaction>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Act
		await uow.CommitAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => transaction.Commit()).MustHaveHappenedOnceExactly();
		A.CallTo(() => transaction.Dispose()).MustHaveHappenedOnceExactly();
		uow.Transaction.ShouldBeNull();
	}

	[Fact]
	public async Task CommitAsync_WithNoTransaction_DoesNotThrow()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act & Assert - should not throw
		await Should.NotThrowAsync(async () => await uow.CommitAsync(CancellationToken.None));
	}

	#endregion

	#region RollbackAsync Tests

	[Fact]
	public async Task RollbackAsync_RollsBackAndDisposesTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var transaction = A.Fake<IDbTransaction>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Act
		await uow.RollbackAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => transaction.Rollback()).MustHaveHappenedOnceExactly();
		A.CallTo(() => transaction.Dispose()).MustHaveHappenedOnceExactly();
		uow.Transaction.ShouldBeNull();
	}

	[Fact]
	public async Task RollbackAsync_WithNoTransaction_DoesNotThrow()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act & Assert - should not throw
		await Should.NotThrowAsync(async () => await uow.RollbackAsync(CancellationToken.None));
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_DisposesConnectionAndTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var transaction = A.Fake<IDbTransaction>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Act
		await uow.DisposeAsync();

		// Assert
		A.CallTo(() => transaction.Dispose()).MustHaveHappened();
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_CalledMultipleTimes_DisposesOnce()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.DisposeAsync();
		await uow.DisposeAsync();
		await uow.DisposeAsync();

		// Assert
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_WithNoTransaction_DisposesConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.DisposeAsync();

		// Assert
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region IUnitOfWork Interface Tests

	[Fact]
	public void ImplementsIUnitOfWork()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var uow = new UnitOfWork(connection);

		// Assert
		uow.ShouldBeAssignableTo<IUnitOfWork>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var uow = new UnitOfWork(connection);

		// Assert
		uow.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion
}
