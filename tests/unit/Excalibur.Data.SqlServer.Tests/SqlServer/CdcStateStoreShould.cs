// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcStateStore"/> construction and ownership.
/// </summary>
/// <remarks>
/// The store takes a connection factory rather than a connection, and opens one connection per
/// operation. These arms cover what can be established without a server: the factory is required, and
/// the store does not take ownership of any connection. Whether overlapping operations actually succeed
/// is a property of a real server and is covered by the SQL Server concurrency arm.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CdcStateStore")]
public sealed class CdcStateStoreShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenConnectionFactoryIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new CdcStateStore((Func<IDbConnection>)null!));
	}

	[Fact]
	public void Constructor_AcceptAConnectionFactory()
	{
		using var store = new CdcStateStore(A.Fake<IDbConnection>);

		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_NotCallTheFactory_UntilAnOperationRunsIt()
	{
		var created = 0;

		using var store = new CdcStateStore(() =>
		{
			created++;
			return A.Fake<IDbConnection>();
		});

		created.ShouldBe(
			0,
			"the store opens a connection per operation, so constructing it must not open one -- a store "
			+ "that connected eagerly would hold a connection for its whole singleton lifetime, which is "
			+ "the contract this factory replaced");
	}

	[Fact]
	public void Dispose_NotDisposeAConnectionItDoesNotOwn()
	{
		var connection = A.Fake<IDbConnection>();
		var store = new CdcStateStore(() => connection);

		store.Dispose();

		// Each operation disposes the connection it opened, so there is nothing left for the store to
		// release. Disposing a caller-supplied connection here would close one the caller still owns.
		A.CallTo(() => connection.Dispose()).MustNotHaveHappened();
	}

	[Fact]
	public async Task DisposeAsync_NotDisposeAConnectionItDoesNotOwn()
	{
		var connection = A.Fake<IDbConnection>(options => options.Implements<IAsyncDisposable>());
		var store = new CdcStateStore(() => connection);

		await store.DisposeAsync();

		A.CallTo(() => ((IAsyncDisposable)connection).DisposeAsync()).MustNotHaveHappened();
		A.CallTo(() => connection.Dispose()).MustNotHaveHappened();
	}
}
