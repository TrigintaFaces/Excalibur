// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a SQL Server append whose acknowledgement is lost.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <c>PostgresEventStoreCommitOutcomeShould</c>, which has held this line for the
/// Postgres store while every other provider went unbound. The arm matters because the state it
/// describes is reachable in production and was, until the commit boundary was threaded through this
/// store, unreachable in a test: the store called <c>transaction.CommitAsync</c> directly, so no test
/// could place a fault between a real commit and its acknowledgement.
/// </para>
/// <para>
/// What is asserted is that the store does not claim <see cref="AppendOutcome.Failed"/> — which states
/// that nothing was written — for an append it cannot prove did not commit. The committed case is the
/// one that would do real damage: the rows ARE durable, and a caller told they are absent does not
/// reconcile.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class SqlServerEventStoreCommitOutcomeShould(SqlServerEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost commit acknowledgement is reported as Unknown on both sides of the commit.
	/// </summary>
	/// <param name="committed">Whether the injected fault is raised after the commit really landed.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReportUnknownWhenTheCommitAcknowledgementIsLost(bool committed)
	{
		await InitializeAsync().ConfigureAwait(true);
		var id = Guid.NewGuid().ToString("N");
		var store = new SqlServerEventStore(
			fixture.ConnectionString,
			NullLogger<SqlServerEventStore>.Instance,
			SingleTenantTestContext.Instance)
		{
			CommitTransactionAsync = async (transaction, token) =>
			{
				if (committed)
				{
					await transaction.CommitAsync(token).ConfigureAwait(false);
				}

				// A connection reset, a command timeout, or a pause past the client timeout all arrive
				// here. The store cannot tell this apart from a fault that preceded the commit, which is
				// the whole reason the outcome may not be Failed.
				throw CreateInjectedSqlFault();
			},
		};

		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await store.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		// THE INVARIANT, and it holds on both sides: never Failed. Failed asserts nothing was written,
		// and a terminal fault handler that cannot separate a pre-commit fault from a lost acknowledgement
		// has no grounds for it.
		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once the commit was sent");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");

		// The two sides differ, and the difference is the probe earning its round trip rather than a
		// weaker answer being tolerated. When the commit landed, the store reads its OWN events back by
		// identity and reports AlreadyCommitted -- a measurement, not an inference, and strictly more
		// useful to a caller than Unknown. When it did not land, the probe finds nothing, and finding
		// nothing is not proof of rollback, so the honest answer is Unknown.
		if (committed)
		{
			result.Outcome.ShouldBe(AppendOutcome.AlreadyCommitted, "the probe measured our own events as durable");
			result.NextExpectedVersion.ShouldBe(0L, "a recognised commit states the version it reached");
		}
		else
		{
			result.Outcome.ShouldBe(AppendOutcome.Unknown, "an empty recovery read does not establish rollback");
			result.NextExpectedVersion.ShouldBeNull("an indeterminate append reached no known version");
		}

		// LIVENESS, and the half that makes the committed case worth asserting: the rows really are
		// durable when the fault followed a real commit, so Failed would have been a falsehood about
		// state the caller can go and read.
		var loaded = await store.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(committed ? 1 : 0);
	}

	// SqlException cannot be constructed directly, and the store's catch filter is an allow-list of
	// SqlException or OperationFailedException -- so a plain Exception would propagate instead of
	// exercising the path under test. Forcing a real SqlException from the server keeps the fault the
	// same SHAPE the store actually filters on.
	private static Exception CreateInjectedSqlFault()
	{
		try
		{
			using var connection = new SqlConnection("Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;Connect Timeout=1;TrustServerCertificate=true");
			connection.Open();
		}
		catch (SqlException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Could not manufacture a SqlException for the injected fault.");
	}

	private async Task InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real SQL Server; never skip.");
		await fixture.EnsureInitializedAsync().ConfigureAwait(true);
	}
}
