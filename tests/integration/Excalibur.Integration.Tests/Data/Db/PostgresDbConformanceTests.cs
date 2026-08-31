// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data;
using Excalibur.Integration.Tests.Data;
using Excalibur.Testing.Conformance;

using Npgsql;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Db;

/// <summary>
/// Runs the shared <see cref="DbConformanceTestKit"/> against a REAL Postgres connection.
/// </summary>
/// <remarks>
/// <para>
/// Postgres ships no <c>IDb</c>-specific subclass of its own -- the framework's generic
/// self-healing <see cref="Db"/> base is provider-agnostic by design, and any consumer wires it to
/// Postgres by constructing one of the shipped marker types (<see cref="DomainDb"/>,
/// <see cref="IOutboxDb"/>'s <c>OutboxDb</c>, etc.) with a real <see cref="NpgsqlConnection"/>. This
/// class wraps <see cref="DomainDb"/> -- a shipped, non-abstract <see cref="IDb"/> implementation -- with
/// a real Postgres connection from a TestContainers-backed instance, so the self-heal, open, and close
/// branches the in-memory deriver exercises with a double are exercised here against Npgsql's actual
/// connection-state semantics.
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
[Trait("Database", "Postgres")]
[Collection(PostgresTestCollection.CollectionName)]
public sealed class PostgresDbConformanceTests : DbConformanceTestKit
{
	private readonly PostgresContainerFixture _fixture;

	public PostgresDbConformanceTests(PostgresContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override (IDb Db, IDbConnection Underlying) CreateDb()
	{
		var connection = new NpgsqlConnection(_fixture.ConnectionString);
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
