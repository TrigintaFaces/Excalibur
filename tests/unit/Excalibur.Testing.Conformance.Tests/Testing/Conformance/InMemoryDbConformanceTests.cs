// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Testing.Conformance;

using FakeItEasy;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="DbConformanceTestKit"/> runs end-to-end against a sample custom
/// <see cref="IDb"/> implementation and reports pass/fail (wired-and-tested).
/// </summary>
/// <remarks>
/// Uses a faked <see cref="IDbConnection"/> whose Open/Close mutate a tracked state, wrapped by the real
/// self-healing <see cref="Db"/> base, so the self-heal, open, and close branches of the kit are all
/// exercised.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class InMemoryDbConformanceTests : DbConformanceTestKit
{
	/// <inheritdoc />
	protected override (IDb Db, IDbConnection Underlying) CreateDb()
	{
		var state = ConnectionState.Closed;
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).ReturnsLazily(() => state);
		A.CallTo(() => connection.Open()).Invokes(() => state = ConnectionState.Open);
		A.CallTo(() => connection.Close()).Invokes(() => state = ConnectionState.Closed);

		return (new TestDb(connection), connection);
	}

	[Fact]
	public void Db_ShouldImplementIDb_Test() => Db_ShouldImplementIDb();

	[Fact]
	public void Connection_ShouldReturnNonNullConnection_Test() => Connection_ShouldReturnNonNullConnection();

	[Fact]
	public void Open_ShouldOpenConnection_Test() => Open_ShouldOpenConnection();

	[Fact]
	public Task OpenAsync_ShouldOpenConnection_Test() => OpenAsync_ShouldOpenConnection();

	[Fact]
	public Task CloseAsync_ShouldCloseConnection_Test() => CloseAsync_ShouldCloseConnection();

	[Fact]
	public void Open_WhenAlreadyOpen_ShouldNotThrow_Test() => Open_WhenAlreadyOpen_ShouldNotThrow();

	[Fact]
	public void Close_ShouldCloseConnection_Test() => Close_ShouldCloseConnection();

	[Fact]
	public void Close_WhenAlreadyClosed_ShouldNotThrow_Test() => Close_WhenAlreadyClosed_ShouldNotThrow();

	[Fact]
	public void Connection_AfterOpen_IsOpen_Test() => Connection_AfterOpen_IsOpen();

	[Fact]
	public void Connection_AfterClose_ReopensReady_Test() => Connection_AfterClose_ReopensReady();

	/// <summary>Every arm this kit declares is surfaced above; an omission fails by name.</summary>
	[Fact]
	public void ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	private sealed class TestDb(IDbConnection connection) : Db(connection);
}
