// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Integration.Tests.Data;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Db;

/// <summary>
/// Runs the shared <see cref="DbConformanceTestKit"/> against a REAL SQL Server connection.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. Until this class, the only <see cref="DbConformanceTestKit"/> deriver wrapped a
/// <c>FakeItEasy</c> double of <see cref="IDbConnection"/> whose <c>Open</c>/<c>Close</c> were hand-wired
/// to flip a tracked enum. A double answers exactly what it was told to answer -- it cannot reproduce
/// what a real ADO.NET provider actually does to connection state, pooling, or a double-<c>Open</c>/
/// double-<c>Close</c> call. This class makes the kit's arms load-bearing for the shipped
/// <see cref="SqlDb"/> implementation, wrapping a real <see cref="SqlConnection"/> against a real
/// SQL Server container.
/// </para>
/// <para>
/// The kit's arms are inherited wholesale and each is surfaced as a <c>[Fact]</c> below, so adding an arm
/// to the kit does not silently skip this provider -- an un-wrapped arm is a visible omission in this
/// file rather than an absence nobody can see. <see cref="ConformanceSuite_ShouldWireEveryArm_Test"/>
/// enforces that mechanically.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerTestCollection.CollectionName)]
public sealed class SqlServerDbConformanceTests : DbConformanceTestKit
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerDbConformanceTests(SqlServerContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override (IDb Db, IDbConnection Underlying) CreateDb()
	{
		var connection = new SqlConnection(_fixture.ConnectionString);
		return (new SqlDb(connection), connection);
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
}
