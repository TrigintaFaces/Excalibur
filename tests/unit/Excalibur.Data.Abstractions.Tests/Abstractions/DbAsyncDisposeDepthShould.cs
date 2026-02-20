// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data.Common;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="Db"/> DisposeAsync behavior.
/// Covers IAsyncDisposable vs IDisposable paths and idempotency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DbAsyncDisposeDepthShould
{
	[Fact]
	public async Task DisposeAsyncUseAsyncPathWhenConnectionIsAsyncDisposable()
	{
		// Arrange
		var connection = A.Fake<DbConnection>();
		_ = A.CallTo(() => ((IDbConnection)connection).State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act
		await db.DisposeAsync();

		// Assert - Should have used DisposeAsync path
		A.CallTo(() => connection.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsyncUseSyncPathWhenConnectionIsNotAsyncDisposable()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act
		await db.DisposeAsync();

		// Assert - Should have used synchronous Dispose
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsyncBeIdempotent()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act
		await db.DisposeAsync();
		await db.DisposeAsync();
		await db.DisposeAsync();

		// Assert - Only disposed once
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsyncPreventSubsequentSyncDispose()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act
		await db.DisposeAsync();
		db.Dispose();

		// Assert - Connection disposed only once via async path
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SyncDisposePreventSubsequentAsyncDispose()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).Returns(ConnectionState.Open);
		var db = new TestDb(connection);

		// Act
		db.Dispose();

		// Assert - disposed once
		A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	private sealed class TestDb : Db
	{
		public TestDb(IDbConnection connection) : base(connection) { }
	}
}
