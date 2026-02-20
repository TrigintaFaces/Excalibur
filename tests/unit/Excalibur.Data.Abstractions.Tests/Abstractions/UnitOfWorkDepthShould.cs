// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data.Common;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="UnitOfWork"/>.
/// Covers transaction lifecycle, commit, rollback, async disposal, and DbConnection/DbTransaction paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UnitOfWorkDepthShould
{
	[Fact]
	public void ThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new UnitOfWork(null!));
	}

	[Fact]
	public void HaveNullTransactionByDefault()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);

		// Act
		var uow = new UnitOfWork(connection);

		// Assert
		uow.Transaction.ShouldBeNull();
	}

	[Fact]
	public void ExposeReadyConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		var result = uow.Connection;

		// Assert
		result.ShouldBeSameAs(connection);
	}

	[Fact]
	public async Task BeginTransactionUsingSynchronousFallback()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var transaction = A.Fake<IDbTransaction>();
		_ = A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Assert
		uow.Transaction.ShouldBeSameAs(transaction);
	}

	[Fact]
	public async Task CommitTransactionUsingSynchronousFallback()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var transaction = A.Fake<IDbTransaction>();
		_ = A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
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
	public async Task RollbackTransactionUsingSynchronousFallback()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var transaction = A.Fake<IDbTransaction>();
		_ = A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
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
	public async Task CommitSafelyWhenNoTransactionActive()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act - should not throw
		await uow.CommitAsync(CancellationToken.None);

		// Assert
		uow.Transaction.ShouldBeNull();
	}

	[Fact]
	public async Task RollbackSafelyWhenNoTransactionActive()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act - should not throw
		await uow.RollbackAsync(CancellationToken.None);

		// Assert
		uow.Transaction.ShouldBeNull();
	}

	[Fact]
	public async Task DisposeAsyncWithSynchronousTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var transaction = A.Fake<IDbTransaction>();
		_ = A.CallTo(() => connection.BeginTransaction()).Returns(transaction);
		var uow = new UnitOfWork(connection);
		await uow.BeginTransactionAsync(CancellationToken.None);

		// Act
		await uow.DisposeAsync();

		// Assert
		A.CallTo(() => transaction.Dispose()).MustHaveHappened();
		A.CallTo(() => connection.Dispose()).MustHaveHappened();
	}

	[Fact]
	public async Task DisposeAsyncWithoutTransaction()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.DisposeAsync();

		// Assert
		A.CallTo(() => connection.Dispose()).MustHaveHappened();
	}

	[Fact]
	public async Task DisposeAsyncIdempotently()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var uow = new UnitOfWork(connection);

		// Act
		await uow.DisposeAsync();
		await uow.DisposeAsync();

		// Assert - only disposed once
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}
}
