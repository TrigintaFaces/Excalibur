// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.SqlServer.Cdc;

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcStateStore"/>.
/// Tests constructor validation and disposal patterns.
/// </summary>
/// <remarks>
/// Sprint 201 - Unit Test Coverage Epic.
/// Excalibur.Dispatch-7dm: CDC Unit Tests.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CdcStateStore")]
public sealed class CdcStateStoreShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenConnectionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new CdcStateStore((IDbConnection)null!));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenDbIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new CdcStateStore((IDb)null!));
	}

	[Fact]
	public void Constructor_AcceptValidConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();

		// Act
		using var store = new CdcStateStore(connection);

		// Assert - No exception thrown
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptValidDb()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(connection);

		// Act
		using var store = new CdcStateStore(db);

		// Assert - No exception thrown
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Dispose_DisposeConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var store = new CdcStateStore(connection);

		// Act
		store.Dispose();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_DisposeAsyncDisposableConnection()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>(options =>
			options.Implements<IAsyncDisposable>());
		var store = new CdcStateStore(connection);

		// Act
		await store.DisposeAsync();

		// Assert
		_ = A.CallTo(() => ((IAsyncDisposable)connection).DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DisposeAsync_DisposeRegularConnection_WhenNotAsyncDisposable()
	{
		// Arrange
		var connection = A.Fake<IDbConnection>();
		var store = new CdcStateStore(connection);

		// Act
		await store.DisposeAsync();

		// Assert
		_ = A.CallTo(() => connection.Dispose()).MustHaveHappenedOnceExactly();
	}
}
