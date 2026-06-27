// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Conformance;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Conformance tests for the <see cref="IDb"/> contract, wired with a concrete <see cref="Db"/> subclass
/// over a stateful fake <see cref="System.Data.IDbConnection"/> that models real Open/Close state.
/// </summary>
/// <remarks>
/// Sprint 851 / <c>qxatfw</c>: FIRST concrete deriver of <see cref="DbConformanceTestBase"/> (was 0
/// derivers / 7 dead facts). The fake connection transitions <see cref="System.Data.ConnectionState"/> on
/// Open/Close so the contract exercises real state behaviour rather than recorded calls.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryDbConformanceShould : DbConformanceTestBase
{
	/// <inheritdoc/>
	protected override (IDb Db, IDbConnection Underlying) CreateDb()
	{
		var state = ConnectionState.Closed;
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => connection.State).ReturnsLazily(() => state);
		A.CallTo(() => connection.Open()).Invokes(() => state = ConnectionState.Open);
		A.CallTo(() => connection.Close()).Invokes(() => state = ConnectionState.Closed);

		return (new TestDb(connection), connection);
	}

	private sealed class TestDb(IDbConnection connection) : Db(connection);
}
