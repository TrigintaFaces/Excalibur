// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;

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
/// To create conformance tests for your own IDb implementation:
/// <list type="number">
///   <item>Inherit from DbConformanceTestBase</item>
///   <item>Override CreateDb() to create an instance of your IDb implementation</item>
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
	/// Creates a new instance of the IDb implementation under test.
	/// </summary>
	/// <returns>A configured IDb instance.</returns>
	protected abstract IDb CreateDb();

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
		var db = CreateDb();

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
		var db = CreateDb();

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
		var db = CreateDb();

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
		var db = CreateDb();
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
		var db = CreateDb();
		db.Open();

		// Act
		db.Close();

		try
		{
			// Assert
			db.Connection.State.ShouldBe(ConnectionState.Closed);
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
		var db = CreateDb();
		// Connection starts closed, ensure it's closed
		if (db.Connection.State == ConnectionState.Open)
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
	public void Connection_CanBeOpenedAndClosed()
	{
		// Arrange
		var db = CreateDb();

		try
		{
			// Act & Assert - Open
			db.Open();
			db.Connection.State.ShouldBe(ConnectionState.Open);

			// Act & Assert - Close
			db.Close();
			db.Connection.State.ShouldBe(ConnectionState.Closed);

			// Act & Assert - Reopen
			db.Open();
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
