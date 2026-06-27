// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;

namespace Tests.Shared.Conformance;

/// <summary>
/// Base class for IDb conformance tests.
/// Implementations must provide a concrete IDb instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that IDb implementations
/// correctly implement the IDb interface contract.
/// </para>
/// <para>
/// <b>Contract note (bd-grwc7r):</b> <see cref="IDb.Connection"/> is a <em>self-healing</em> accessor — by
/// design it always returns a ready/open connection (the concrete <c>Db.Connection</c> calls
/// <c>DbConnectionExtensions.Ready()</c>, which re-opens a Closed/Broken connection). Therefore the result
/// of <see cref="IDb.Close"/> is NOT observable through <see cref="IDb.Connection"/> (accessing it re-opens
/// the connection). Facts that verify closure observe the <b>underlying</b> <see cref="IDbConnection"/>
/// returned alongside the IDb from <see cref="CreateDb"/>; a dedicated fact locks the self-heal behaviour
/// as an explicit, intended invariant.
/// </para>
/// <para>
/// To create conformance tests for your own IDb implementation:
/// <list type="number">
///   <item>Inherit from DbConformanceTestBase</item>
///   <item>Override CreateDb() to return your IDb implementation together with the underlying connection it wraps</item>
///   <item>Override DisposeDb() to properly clean up the instance</item>
/// </list>
/// </para>
/// </remarks>
public abstract class DbConformanceTestBase : IDisposable
{
	private bool _disposed;

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates a new instance of the IDb implementation under test, together with the underlying
	/// <see cref="IDbConnection"/> it wraps.
	/// </summary>
	/// <returns>
	/// The IDb instance under test and the underlying connection. The underlying connection is exposed so
	/// that closure can be observed directly (the self-healing <see cref="IDb.Connection"/> accessor re-opens
	/// a closed connection on access, so it cannot observe a closed state — see bd-grwc7r).
	/// </returns>
	protected abstract (IDb Db, IDbConnection Underlying) CreateDb();

	/// <summary>
	/// Cleans up the IDb instance after each test.
	/// </summary>
	/// <param name="db">The IDb instance to dispose.</param>
	protected virtual void DisposeDb(IDb db)
	{
		if (db is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	#region Interface Implementation Tests

	[Fact]
	public void Db_ShouldImplementIDb()
	{
		// Arrange
		var (db, _) = CreateDb();

		try
		{
			// Assert
			_ = db.ShouldBeAssignableTo<IDb>();
		}
		finally
		{
			DisposeDb(db);
		}
	}

	#endregion Interface Implementation Tests

	#region Connection Property Tests

	[Fact]
	public void Connection_ShouldReturnNonNullConnection()
	{
		// Arrange
		var (db, _) = CreateDb();

		try
		{
			// Assert
			_ = db.Connection.ShouldNotBeNull();
			_ = db.Connection.ShouldBeAssignableTo<IDbConnection>();
		}
		finally
		{
			DisposeDb(db);
		}
	}

	#endregion Connection Property Tests

	#region Open Tests

	[Fact]
	public void Open_ShouldOpenConnection()
	{
		// Arrange
		var (db, _) = CreateDb();

		try
		{
			// Act
			db.Open();

			// Assert
			db.Connection.State.ShouldBe(ConnectionState.Open);
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	[Fact]
	public void Open_WhenAlreadyOpen_ShouldNotThrow()
	{
		// Arrange
		var (db, _) = CreateDb();
		db.Open();

		try
		{
			// Act & Assert - Opening an already open connection should not throw
			Should.NotThrow(() => db.Open());
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	#endregion Open Tests

	#region Close Tests

	[Fact]
	public void Close_ShouldCloseConnection()
	{
		// Arrange
		var (db, underlying) = CreateDb();
		db.Open();

		// Act
		db.Close();

		try
		{
			// Assert — observe the UNDERLYING connection, not db.Connection (which self-heals/re-opens
			// on access, so it can never report Closed; see bd-grwc7r).
			underlying.State.ShouldBe(ConnectionState.Closed);
		}
		finally
		{
			DisposeDb(db);
		}
	}

	[Fact]
	public void Close_WhenAlreadyClosed_ShouldNotThrow()
	{
		// Arrange
		var (db, underlying) = CreateDb();
		// Ensure the underlying connection is closed.
		if (underlying.State == ConnectionState.Open)
		{
			db.Close();
		}

		try
		{
			// Act & Assert - Closing an already closed connection should not throw
			Should.NotThrow(() => db.Close());
		}
		finally
		{
			DisposeDb(db);
		}
	}

	#endregion Close Tests

	#region Connection State Transitions

	[Fact]
	public void Connection_AfterOpen_IsOpen()
	{
		// Arrange
		var (db, _) = CreateDb();

		try
		{
			// Act
			db.Open();

			// Assert
			db.Connection.State.ShouldBe(ConnectionState.Open);
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	[Fact]
	public void Connection_AfterClose_ReopensReady()
	{
		// Arrange
		var (db, _) = CreateDb();
		db.Open();
		db.Close();

		try
		{
			// Assert — the self-heal contract: IDb.Connection ALWAYS returns a ready/open connection,
			// re-opening it on access even after a Close(). This is intended behaviour every consumer
			// relies on; locking it makes the invariant explicit (bd-grwc7r).
			db.Connection.State.ShouldBe(ConnectionState.Open);
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	#endregion Connection State Transitions

	/// <summary>
	/// Releases unmanaged resources.
	/// </summary>
	/// <param name="disposing">True if disposing managed resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;
		_disposed = true;
	}
}
