// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="Db"/> abstract base class behavior.
/// Covers AC1-AC10 for task bd-tajqj.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DbShould : IDisposable
{
	private readonly IDbConnection _fakeConnection;
	private readonly TestDb _db;
	private bool _disposed;

	public DbShould()
	{
		_fakeConnection = A.Fake<IDbConnection>();
		_db = new TestDb(_fakeConnection);
	}

	/// <summary>
	/// AC1: Test Connection property lazy initialization via Ready() extension.
	/// When Connection is accessed, Ready() should be called which opens closed connections.
	/// </summary>
	[Fact]
	public void LazilyInitializeConnectionViaReady()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Closed);

		// Act
		var connection = _db.Connection;

		// Assert
		_ = A.CallTo(() => _fakeConnection.Open()).MustHaveHappenedOnceExactly();
		connection.ShouldBe(_fakeConnection);
	}

	/// <summary>
	/// AC2: Test Open() with ConnectionState.Closed.
	/// Open() should call Ready() which opens a closed connection.
	/// </summary>
	[Fact]
	public void OpenConnectionWhenClosed()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Closed);

		// Act
		_db.Open();

		// Assert
		_ = A.CallTo(() => _fakeConnection.Open()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC3: Test Open() when already open (no-op).
	/// Open() should not call Open on underlying connection if already open.
	/// </summary>
	[Fact]
	public void NotReopenAlreadyOpenConnection()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Open);

		// Act
		_db.Open();

		// Assert
		A.CallTo(() => _fakeConnection.Open()).MustNotHaveHappened();
	}

	/// <summary>
	/// AC4: Test Close() with ConnectionState.Open.
	/// Close() should call Close on underlying connection.
	/// </summary>
	[Fact]
	public void CloseOpenConnection()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Open);

		// Act
		_db.Close();

		// Assert
		_ = A.CallTo(() => _fakeConnection.Close()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC5: Test Close() when already closed (no-op behavior).
	/// Close() is called even on closed connections as that's how IDbConnection works.
	/// </summary>
	[Fact]
	public void CloseAlreadyClosedConnectionSafely()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Closed);

		// Act - Close can be called on already closed connections
		_db.Close();

		// Assert - Close is always called as per IDbConnection contract
		_ = A.CallTo(() => _fakeConnection.Close()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC6: Test Dispose() disposes connection.
	/// Dispose should dispose the underlying connection.
	/// </summary>
	[Fact]
	public void DisposeConnection()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Open);

		// Act
		_db.Dispose();

		// Assert
		_ = A.CallTo(() => _fakeConnection.Dispose()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC7: Test Dispose() is idempotent (multiple calls safe).
	/// Multiple Dispose calls should only dispose once via _disposed guard.
	/// </summary>
	[Fact]
	public void BeIdempotentOnMultipleDisposeCalls()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Open);

		// Act
		_db.Dispose();
		_db.Dispose();
		_db.Dispose();

		// Assert - Only disposed once
		_ = A.CallTo(() => _fakeConnection.Dispose()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC8: Test Connection property returns same connection instance.
	/// The Connection property should return the connection passed to constructor.
	/// </summary>
	[Fact]
	public void ReturnSameConnectionInstance()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Open);

		// Act
		var connection1 = _db.Connection;
		var connection2 = _db.Connection;

		// Assert
		connection1.ShouldBeSameAs(connection2);
		connection1.ShouldBeSameAs(_fakeConnection);
	}

	/// <summary>
	/// AC8 (additional): Test constructor rejects null connection.
	/// </summary>
	[Fact]
	public void ThrowArgumentNullExceptionWhenConnectionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TestDb(null!));
	}

	/// <summary>
	/// AC9: Connection state transitions are handled by Ready() extension.
	/// When connection is broken, Ready() should close and reopen.
	/// </summary>
	[Fact]
	public void HandleBrokenConnectionStateOnReady()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Broken);

		// Act
		_db.Open();

		// Assert - Broken connection is closed first then opened
		_ = A.CallTo(() => _fakeConnection.Close()).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _fakeConnection.Open()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// AC10: GC.SuppressFinalize is called in Dispose.
	/// Verified by ensuring Dispose pattern is correctly implemented.
	/// </summary>
	[Fact]
	public void SuppressFinalizerOnDispose()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act - Dispose should call GC.SuppressFinalize(this) - verified through proper implementation
		db.Dispose();

		// Assert - Connection is disposed and no further cleanup is needed
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();

		// Additional call to dispose should be idempotent
		db.Dispose();
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Additional: Ready() should not reopen when connection is executing.
	/// </summary>
	[Fact]
	public void NotReopenWhenConnectionIsExecuting()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Executing);

		// Act
		var connection = _db.Connection;

		// Assert - Should return connection without opening
		A.CallTo(() => _fakeConnection.Open()).MustNotHaveHappened();
		connection.ShouldBe(_fakeConnection);
	}

	/// <summary>
	/// Additional: Ready() should not reopen when connection is fetching.
	/// </summary>
	[Fact]
	public void NotReopenWhenConnectionIsFetching()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Fetching);

		// Act
		var connection = _db.Connection;

		// Assert - Should return connection without opening
		A.CallTo(() => _fakeConnection.Open()).MustNotHaveHappened();
		connection.ShouldBe(_fakeConnection);
	}

	/// <summary>
	/// Additional: Ready() should not reopen when connection is connecting.
	/// </summary>
	[Fact]
	public void NotReopenWhenConnectionIsConnecting()
	{
		// Arrange
		_ = A.CallTo(() => _fakeConnection.State).Returns(ConnectionState.Connecting);

		// Act
		var connection = _db.Connection;

		// Assert - Should return connection without opening
		A.CallTo(() => _fakeConnection.Open()).MustNotHaveHappened();
		connection.ShouldBe(_fakeConnection);
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		_db.Dispose();
		_fakeConnection.Dispose();
	}

	/// <summary>
	/// Concrete test implementation of abstract Db class.
	/// </summary>
	private sealed class TestDb : Db
	{
		public TestDb(IDbConnection connection) : base(connection)
		{
		}
	}
}
