// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Integration.Tests.Data.Saga;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Db;

/// <summary>
/// Runs the shared <see cref="DbConformanceTestKit"/> against a REAL Oracle connection.
/// </summary>
/// <remarks>
/// <para>
/// The framework ships no Oracle-specific <c>IDb</c> subclass (Oracle providers -- outbox, saga,
/// event-store -- issue Dapper requests directly over their own <c>OracleConnection</c>, bypassing
/// <c>IDb</c>). The generic self-healing <see cref="Db"/> base is provider-agnostic by construction, so
/// this class wraps <see cref="DomainDb"/> -- a shipped, non-abstract <see cref="IDb"/> implementation --
/// with a real <c>Oracle.ManagedDataAccess.Client.OracleConnection</c> from a TestContainers-backed
/// <c>gvenzl/oracle-free</c> instance, so the self-heal, open, and close branches are exercised against
/// ODP.NET's actual connection-state semantics -- not a hand-wired double.
/// </para>
/// <para>
/// Reuses <see cref="OracleSagaStoreContainerFixture"/> purely for its container/connection-string --
/// no saga schema is provisioned or required, since the <see cref="IDb"/> contract never issues SQL. The
/// class shares the fixture's <see cref="IClassFixture{TFixture}"/> and
/// <c>"Oracle SagaStore Integration Tests"</c> collection so it runs sequentially with the other Oracle
/// containers this repository already spins up, rather than adding a fourth Oracle container startup
/// (~6 min) purely for connection-lifecycle coverage.
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
[Trait("Database", "Oracle")]
[Collection("Oracle SagaStore Integration Tests")]
public sealed class OracleDbConformanceTests : DbConformanceTestKit, IClassFixture<OracleSagaStoreContainerFixture>
{
	private readonly OracleSagaStoreContainerFixture _fixture;

	public OracleDbConformanceTests(OracleSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override (IDb Db, IDbConnection Underlying) CreateDb()
	{
		var connection = _fixture.CreateConnection();
		return (new DomainDb(connection), connection);
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
