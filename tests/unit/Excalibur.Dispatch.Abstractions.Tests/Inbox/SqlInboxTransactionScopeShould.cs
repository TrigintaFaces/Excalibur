// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using System.Data;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="SqlInboxTransactionScope"/> and its <c>AsSqlTransaction()</c> accessor — the
/// relational bridge that lets a scoped inbox handler enlist its own writes in the store's BCL
/// <see cref="IDbTransaction"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SqlInboxTransactionScopeShould
{
	[Fact]
	public void AsSqlTransaction_RoundTripsTheWrappedTransaction()
	{
		// Arrange
		var transaction = A.Fake<IDbTransaction>();
		IInboxTransactionScope scope = new SqlInboxTransactionScope(transaction);

		// Act
		var recovered = scope.AsSqlTransaction();

		// Assert — the handler recovers the exact BCL transaction the store enlisted, so its writes commit
		// atomically with the processed-mark.
		recovered.ShouldBeSameAs(transaction);
	}

	[Fact]
	public void Constructor_RejectsNullTransaction()
	{
		_ = Should.Throw<ArgumentNullException>(() => new SqlInboxTransactionScope(null!));
	}

	[Fact]
	public void AsSqlTransaction_ThrowsOnNullScope()
	{
		IInboxTransactionScope scope = null!;

		_ = Should.Throw<ArgumentNullException>(() => scope.AsSqlTransaction());
	}

	[Fact]
	public void AsSqlTransaction_FailsLoudOnWrongProviderScope()
	{
		// SAFETY: a non-relational (document-store) scope must NOT silently cast to a SQL transaction — a
		// provider mismatch fails closed with a diagnosable error rather than returning null / an obscure
		// InvalidCastException. This is the AsMongoSession/AsCosmosBatch fail-loud contract for the SQL bridge.
		IInboxTransactionScope foreign = new ForeignScope();

		var ex = Should.Throw<InvalidOperationException>(() => foreign.AsSqlTransaction());
		ex.Message.ShouldContain("not a relational");
	}

	/// <summary>A wrong-provider scope (e.g. a document-store scope) used to prove the fail-loud guard.</summary>
	private sealed class ForeignScope : IInboxTransactionScope
	{
	}
}
