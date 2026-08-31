// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using IsolationLevel = System.Data.IsolationLevel;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure locks on the property that a transaction scope leaves the caller's async flow clean.
/// </summary>
/// <remarks>
/// <para>
/// The scope creates an ambient <see cref="TransactionScope"/> so a distributed transaction can enlist
/// connections automatically. Completing that scope is not the same as disposing it: a completed but
/// undisposed scope stays on <see cref="Transaction.Current"/> for the calling async flow, and the next
/// connection opened on that flow enlists in it and throws "The current TransactionScope is already
/// complete". The commit path completed the scope and left it there; only the rollback path disposed it.
/// </para>
/// <para>
/// Three things make the resulting failure unusually hard to diagnose, which is why it is locked here rather
/// than reasoned about. It surfaces away from the scope, in whatever code next opens a connection. It names
/// a type the consumer never used. And it appears only AFTER a SUCCESSFUL commit, which is the last place
/// anyone looks for the consequences of a leak.
/// </para>
/// <para>
/// Both arms drive a live SQL Server container, never skipped, because the property is about what the
/// ambient transaction manager does to a real connection open — something no mock reproduces.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerTransactionScopeAmbientDisposalShould
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerTransactionScopeAmbientDisposalShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY: after a committed scope, the caller's flow carries no ambient transaction and a further
	/// connection opens normally.
	/// LIVENESS: the commit genuinely happened, so disposal did not turn the commit into a rollback.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task LeaveNoAmbientTransactionOnTheCallersFlow_AfterAScopeCommits()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var scope = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		await scope.CommitAsync(ct).ConfigureAwait(false);

		// SAFETY -- nothing is left current on this flow.
		Transaction.Current.ShouldBeNull(
			"a committed scope must not remain on the caller's async flow. Completing an ambient " +
			"TransactionScope does not remove it; only disposing it does, and the commit path completed it " +
			"without disposing it while the rollback path disposed it correctly.");

		// SAFETY, the consequence a consumer actually meets: the next connection on this flow still opens.
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(ct).ConfigureAwait(false);

		await using var command = new SqlCommand("SELECT 1", connection);
		var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
		Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
			1,
			"a connection opened after a committed scope must work. A completed-but-undisposed scope makes " +
			"this open throw about a TransactionScope the consumer never wrote.");

		// LIVENESS -- the scope really did commit, so the fix did not quietly convert commits to rollbacks.
		scope.Status.ShouldBe(
			Excalibur.Data.Persistence.TransactionStatus.Committed,
			"disposing the ambient scope after completing it must still leave the transaction COMMITTED. " +
			"Disposing without completing rolls back, so a fix that disposed too early would silently turn " +
			"every commit into a rollback and satisfy the assertions above.");
	}

	/// <summary>
	/// SAFETY: the same must hold when the scope rolls back, so neither outcome strands an ambient scope.
	/// LIVENESS: the rollback is still recorded as a rollback.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task LeaveNoAmbientTransactionOnTheCallersFlow_AfterAScopeRollsBack()
	{
		var ct = TestContext.Current.CancellationToken;
		await using var scope = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(1),
			NullLogger<SqlServerTransactionScope>.Instance);

		await scope.RollbackAsync(ct).ConfigureAwait(false);

		Transaction.Current.ShouldBeNull("a rolled-back scope must not remain on the caller's async flow.");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(ct).ConfigureAwait(false);

		await using var command = new SqlCommand("SELECT 1", connection);
		var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
		Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
			1, "a connection opened after a rolled-back scope must work.");

		scope.Status.ShouldBe(
			Excalibur.Data.Persistence.TransactionStatus.RolledBack,
			"the rollback must still be recorded as a rollback.");
	}
}
