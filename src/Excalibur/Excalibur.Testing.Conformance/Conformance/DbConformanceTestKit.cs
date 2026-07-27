// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)

using System.Data;

using Excalibur.Data;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for <see cref="IDb"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateDb"/> to verify that your <see cref="IDb"/>
/// implementation conforms to the contract. The kit exposes plain <c>public virtual</c> methods with no
/// test-framework attributes; add the attributes your test framework requires (for example <c>[Fact]</c>)
/// on thin overrides in your derived class.
/// </para>
/// <para>
/// <b>Contract note:</b> <see cref="IDb.Connection"/> is a <em>self-healing</em> accessor — by design it
/// always returns a ready/open connection (accessing it re-opens a closed/broken connection). Therefore
/// the effect of <see cref="IDb.Close"/> is NOT observable through <see cref="IDb.Connection"/>. Tests that
/// verify closure observe the <b>underlying</b> <see cref="IDbConnection"/> returned alongside the
/// <see cref="IDb"/> from <see cref="CreateDb"/>; a dedicated method locks the self-heal behaviour as an
/// explicit, intended invariant.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyDbConformanceTests : DbConformanceTestKit
/// {
///     protected override (IDb Db, IDbConnection Underlying) CreateDb()
///     {
///         var connection = new MyDbConnection(connectionString);
///         return (new MyDb(connection), connection);
///     }
///
///     [Fact] public void Open() => Open_ShouldOpenConnection();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class DbConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the <see cref="IDb"/> implementation under test, together with the
	/// underlying <see cref="IDbConnection"/> it wraps.
	/// </summary>
	/// <returns>
	/// The <see cref="IDb"/> instance under test and the underlying connection. The underlying connection is
	/// exposed so that closure can be observed directly (the self-healing <see cref="IDb.Connection"/>
	/// accessor re-opens a closed connection on access, so it cannot observe a closed state).
	/// </returns>
	protected abstract (IDb Db, IDbConnection Underlying) CreateDb();

	/// <summary>
	/// Cleans up the <see cref="IDb"/> instance after each test.
	/// </summary>
	/// <param name="db">The <see cref="IDb"/> instance to dispose.</param>
	protected virtual void DisposeDb(IDb db)
	{
		if (db is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Verifies <see cref="CreateDb"/> returns a non-null <see cref="IDb"/> instance.
	/// </summary>
	public virtual void Db_ShouldImplementIDb()
	{
		var (db, _) = CreateDb();

		try
		{
			if (db is null)
			{
				throw new TestFixtureAssertionException("Expected CreateDb to return a non-null IDb instance.");
			}
		}
		finally
		{
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies <see cref="IDb.Connection"/> returns a non-null connection.
	/// </summary>
	public virtual void Connection_ShouldReturnNonNullConnection()
	{
		var (db, _) = CreateDb();

		try
		{
			if (db.Connection is null)
			{
				throw new TestFixtureAssertionException("Expected IDb.Connection to be non-null.");
			}
		}
		finally
		{
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies <see cref="IDb.Open"/> opens the connection.
	/// </summary>
	public virtual void Open_ShouldOpenConnection()
	{
		var (db, _) = CreateDb();

		try
		{
			db.Open();

			if (db.Connection.State != ConnectionState.Open)
			{
				throw new TestFixtureAssertionException(
					$"Expected connection state Open after Open() but was {db.Connection.State}.");
			}
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies calling <see cref="IDb.Open"/> on an already-open connection does not throw.
	/// </summary>
	public virtual void Open_WhenAlreadyOpen_ShouldNotThrow()
	{
		var (db, _) = CreateDb();
		db.Open();

		try
		{
			// Should not throw — a failure surfaces as an unhandled exception failing the test.
			db.Open();
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies <see cref="IDb.Close"/> closes the underlying connection.
	/// </summary>
	public virtual void Close_ShouldCloseConnection()
	{
		var (db, underlying) = CreateDb();
		db.Open();

		db.Close();

		try
		{
			// Observe the UNDERLYING connection, not db.Connection (which self-heals/re-opens on access).
			if (underlying.State != ConnectionState.Closed)
			{
				throw new TestFixtureAssertionException(
					$"Expected underlying connection state Closed after Close() but was {underlying.State}.");
			}
		}
		finally
		{
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies calling <see cref="IDb.Close"/> on an already-closed connection does not throw.
	/// </summary>
	public virtual void Close_WhenAlreadyClosed_ShouldNotThrow()
	{
		var (db, underlying) = CreateDb();
		if (underlying.State == ConnectionState.Open)
		{
			db.Close();
		}

		try
		{
			// Should not throw — a failure surfaces as an unhandled exception failing the test.
			db.Close();
		}
		finally
		{
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies the connection reports Open after <see cref="IDb.Open"/>.
	/// </summary>
	public virtual void Connection_AfterOpen_IsOpen()
	{
		var (db, _) = CreateDb();

		try
		{
			db.Open();

			if (db.Connection.State != ConnectionState.Open)
			{
				throw new TestFixtureAssertionException(
					$"Expected connection state Open after Open() but was {db.Connection.State}.");
			}
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}

	/// <summary>
	/// Verifies the self-heal contract: <see cref="IDb.Connection"/> always returns a ready/open connection,
	/// re-opening it on access even after a <see cref="IDb.Close"/>.
	/// </summary>
	public virtual void Connection_AfterClose_ReopensReady()
	{
		var (db, _) = CreateDb();
		db.Open();
		db.Close();

		try
		{
			if (db.Connection.State != ConnectionState.Open)
			{
				throw new TestFixtureAssertionException(
					$"Expected IDb.Connection to self-heal to Open after Close() but was {db.Connection.State}.");
			}
		}
		finally
		{
			db.Close();
			DisposeDb(db);
		}
	}
}
